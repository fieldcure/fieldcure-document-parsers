using System.Runtime.InteropServices;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Inspects the local Whisper runtime environment. Phase 1 of the v0.3 capability
/// lifecycle: pure system inspection, no I/O outside <c>System32</c>, no side effects.
/// </summary>
/// <remarks>
/// <para>v0.3 split: <see cref="Detect"/> reports environment facts (driver presence,
/// driver version, RAM, cores). The decision "is Cuda usable right now?" lives on
/// <see cref="Runtime.WhisperRuntime.GetActivationStatus"/> in Phase 3, which combines
/// these facts with the manifest's <c>cuda.minDriverVersion</c> policy and the
/// provisioner's cache state.</para>
/// <para>This API is Windows-only, matching the package's
/// <c>[SupportedOSPlatform("windows")]</c> attribute.</para>
/// </remarks>
public static class WhisperEnvironment
{
    private static readonly Lazy<WhisperEnvironmentInfo> s_cached =
        new(DetectInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Returns a cached snapshot of the detected Whisper runtime environment.</summary>
    public static WhisperEnvironmentInfo Detect() => s_cached.Value;

    /// <summary>v0.2 alias for <see cref="Detect"/>. Removed in v0.4.</summary>
    [Obsolete("Use Detect(). The v0.2 name conflated detection with implicit activation; v0.3 splits them. This shim will be removed in v0.4.")]
    public static WhisperEnvironmentInfo Probe() => Detect();

    /// <summary>Recommends a Whisper model size for the current environment.</summary>
    /// <param name="bias">Tradeoff hint between transcription quality and runtime cost.
    /// Defaults to <see cref="QualityBias.Accuracy"/> because the primary consumer
    /// (Mcp.Rag) prioritizes indexing fidelity over latency.</param>
    /// <remarks>The bias adjusts the recommendation by one tier. It does not validate
    /// whether the upgraded tier fits within the detected RAM/VRAM — callers who request
    /// <see cref="QualityBias.Accuracy"/> on a constrained host accept the risk of OOM
    /// fallback to CPU at first transcription.</remarks>
    public static WhisperModelSize RecommendModelSize(QualityBias bias = QualityBias.Accuracy)
    {
        var baseline = SelectBalanced(Detect());
        return bias switch
        {
            QualityBias.Accuracy => StepUp(baseline),
            QualityBias.Speed => StepDown(baseline),
            _ => baseline,
        };
    }

    private static WhisperModelSize SelectBalanced(WhisperEnvironmentInfo info)
    {
        const long Gb = 1024L * 1024 * 1024;
        var hasGpu = info.CudaDriverAvailable || info.VulkanDriverAvailable;

        // Matrix authored 2026-04 based on observed RTF on representative hardware.
        // CPU+Medium and below recommendations bias toward responsiveness; consumers
        // doing batch jobs should pass QualityBias.Accuracy.
        if (hasGpu && info.SystemRamBytes >= 16 * Gb) return WhisperModelSize.Large;
        if (hasGpu && info.SystemRamBytes >= 8 * Gb) return WhisperModelSize.Medium;
        if (info.SystemRamBytes >= 16 * Gb && info.LogicalCores >= 8) return WhisperModelSize.Small;
        if (info.SystemRamBytes >= 8 * Gb) return WhisperModelSize.Base;
        return WhisperModelSize.Tiny;
    }

    private static WhisperModelSize StepUp(WhisperModelSize size) => size switch
    {
        WhisperModelSize.Tiny => WhisperModelSize.Base,
        WhisperModelSize.Base => WhisperModelSize.Small,
        WhisperModelSize.Small => WhisperModelSize.Medium,
        WhisperModelSize.Medium => WhisperModelSize.Large,
        _ => size,
    };

    private static WhisperModelSize StepDown(WhisperModelSize size) => size switch
    {
        WhisperModelSize.Large => WhisperModelSize.Medium,
        WhisperModelSize.Medium => WhisperModelSize.Small,
        WhisperModelSize.Small => WhisperModelSize.Base,
        WhisperModelSize.Base => WhisperModelSize.Tiny,
        _ => size,
    };

    private static WhisperEnvironmentInfo DetectInternal()
    {
        var cudaPresent = DetectCudaDriver();
        int? cudaVersion = cudaPresent ? TryGetCudaDriverVersion() : null;

        return new WhisperEnvironmentInfo(
            CudaDriverAvailable: cudaPresent,
            CudaDriverVersion: cudaVersion,
            VulkanDriverAvailable: DetectVulkanDriver(),
            SystemRamBytes: GetSystemRamBytes(),
            LogicalCores: Environment.ProcessorCount);
    }

    private static bool DetectCudaDriver()
    {
        // nvcuda.dll ships with the NVIDIA driver — its presence in System32 indicates
        // a CUDA-capable host. We deliberately avoid checking cudart64_*.dll because
        // those are the redistributables we provision at runtime, not driver-shipped.
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }

    private static int? TryGetCudaDriverVersion()
    {
        // cuDriverGetVersion returns version as int (e.g., 12030 = CUDA 12.3 = driver R545+).
        // We P/Invoke directly into nvcuda.dll. The driver guarantees this entry point exists
        // on every supported version, so any failure is taken to mean "version unknown".
        try
        {
            var rc = cuDriverGetVersion(out var version);
            return rc == 0 ? version : (int?)null;
        }
        catch (DllNotFoundException)
        {
            return null;  // driver absent
        }
        catch (EntryPointNotFoundException)
        {
            return null;  // older driver lacking the entry point (extremely rare)
        }
    }

    private static bool DetectVulkanDriver()
    {
        // NOTE: vulkan-1.dll may be present on Windows 10/11 without an actual GPU-backed
        // Vulkan device (Microsoft ships a runtime stub). We accept this false-positive
        // risk in v0.3 — if no real device exists, Whisper.net's runtime fallback chain
        // lands on CPU automatically. Tighter detection (vkEnumeratePhysicalDevices)
        // remains deferred.
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "vulkan-1.dll"));
    }

    private static long GetSystemRamBytes()
    {
        // GC.GetGCMemoryInfo().TotalAvailableMemoryBytes reports the .NET process budget,
        // not physical RAM. We need the latter to choose a model size that the host can
        // actually accommodate.
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? (long)status.ullTotalPhys : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int cuDriverGetVersion(out int version);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

/// <summary>
/// Hint expressing the consumer's tradeoff between transcription quality and
/// runtime cost. Used by <see cref="WhisperEnvironment.RecommendModelSize"/>.
/// </summary>
public enum QualityBias
{
    /// <summary>Follow the default matrix optimized for interactive responsiveness.</summary>
    Balanced,

    /// <summary>Shift the recommendation one tier upward when feasible. Suitable for
    /// batch indexing scenarios where transcription latency is acceptable.</summary>
    Accuracy,

    /// <summary>Shift the recommendation one tier downward. Suitable for low-latency
    /// UI flows where the user is actively waiting on the transcript.</summary>
    Speed,
}

/// <summary>
/// Snapshot of the detected Whisper runtime environment at probe time.
/// Returned by <see cref="WhisperEnvironment.Detect"/>.
/// </summary>
/// <param name="CudaDriverAvailable"><c>nvcuda.dll</c> exists in System32 (the user's
/// NVIDIA driver is installed). Does NOT mean Cuda inference is currently usable —
/// the cuda runtime variant must also be provisioned and the driver version must
/// satisfy the manifest's <c>cuda.minDriverVersion</c>. See
/// <see cref="Runtime.WhisperRuntime.GetActivationStatus"/>.</param>
/// <param name="CudaDriverVersion">Driver version reported by
/// <c>cuDriverGetVersion</c>, or <see langword="null"/> if the driver is absent or the
/// entry point unavailable. Encoded as an integer where, e.g., <c>12030</c> = CUDA 12.3.
/// Compared against the manifest's <c>cuda.minDriverVersion</c> policy at activation time.</param>
/// <param name="VulkanDriverAvailable"><c>vulkan-1.dll</c> exists in System32. Same caveat
/// as <see cref="CudaDriverAvailable"/> — driver presence ≠ runtime usable.</param>
/// <param name="SystemRamBytes">Physical RAM in bytes (via
/// <c>GlobalMemoryStatusEx</c>).</param>
/// <param name="LogicalCores"><see cref="Environment.ProcessorCount"/>.</param>
public sealed record WhisperEnvironmentInfo(
    bool CudaDriverAvailable,
    int? CudaDriverVersion,
    bool VulkanDriverAvailable,
    long SystemRamBytes,
    int LogicalCores)
{
    /// <summary>v0.2 alias for <see cref="CudaDriverAvailable"/>. Removed in v0.4.</summary>
    [Obsolete("Use CudaDriverAvailable. The v0.2 name conflated 'driver present' with 'runtime usable'; v0.3 separates them. Removed in v0.4.")]
    public bool CudaAvailable => CudaDriverAvailable;

    /// <summary>v0.2 alias for <see cref="VulkanDriverAvailable"/>. Removed in v0.4.</summary>
    [Obsolete("Use VulkanDriverAvailable. Removed in v0.4.")]
    public bool VulkanAvailable => VulkanDriverAvailable;
}
