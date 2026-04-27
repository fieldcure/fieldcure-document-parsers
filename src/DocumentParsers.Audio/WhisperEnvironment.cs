using System.Runtime.InteropServices;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Inspects the local Whisper runtime environment and recommends an appropriate
/// model size based on detected CPU, GPU, and system memory characteristics.
/// </summary>
/// <remarks>
/// <para>This type only <em>recommends</em> — it does not enforce a model choice.
/// Consumers may override by setting <see cref="AudioExtractionOptions.ModelSize"/>
/// directly. Detection is performed once and cached for the process lifetime.</para>
/// <para>This API is Windows-only, matching the platform constraints of
/// <c>FieldCure.DocumentParsers.Audio</c> v0.1.</para>
/// </remarks>
public static class WhisperEnvironment
{
    private static readonly Lazy<WhisperEnvironmentInfo> _cached =
        new(ProbeInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Returns a snapshot of the detected Whisper runtime environment.
    /// Intended for diagnostics and startup logging.
    /// </summary>
    public static WhisperEnvironmentInfo Probe() => _cached.Value;

    /// <summary>
    /// Recommends a Whisper model size for the current environment.
    /// </summary>
    /// <param name="bias">
    /// Tradeoff hint between transcription quality and runtime cost.
    /// Defaults to <see cref="QualityBias.Accuracy"/> because the primary
    /// consumer (Mcp.Rag) prioritizes indexing fidelity over latency.
    /// </param>
    /// <remarks>
    /// The bias adjusts the recommendation by one tier. It does not validate
    /// whether the upgraded tier fits within the detected RAM/VRAM — callers
    /// who request <see cref="QualityBias.Accuracy"/> on a constrained host
    /// accept the risk of OOM fallback to CPU at first transcription.
    /// </remarks>
    public static WhisperModelSize RecommendModelSize(
        QualityBias bias = QualityBias.Accuracy)
    {
        var baseline = SelectBalanced(Probe());
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
        var hasGpu = info.CudaAvailable || info.VulkanAvailable;

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
        _ => size,  // Large stays at top
    };

    private static WhisperModelSize StepDown(WhisperModelSize size) => size switch
    {
        WhisperModelSize.Large => WhisperModelSize.Medium,
        WhisperModelSize.Medium => WhisperModelSize.Small,
        WhisperModelSize.Small => WhisperModelSize.Base,
        WhisperModelSize.Base => WhisperModelSize.Tiny,
        _ => size,  // Tiny stays at bottom
    };

    private static WhisperEnvironmentInfo ProbeInternal() =>
        new(CudaAvailable: DetectCuda(),
            VulkanAvailable: DetectVulkan(),
            SystemRamBytes: GetSystemRamBytes(),
            LogicalCores: Environment.ProcessorCount);

    private static bool DetectCuda()
    {
        // nvcuda.dll ships with the NVIDIA driver itself, so its presence
        // indicates a usable GPU runtime. We deliberately avoid checking
        // cudart64_*.dll because that requires the user to install the
        // CUDA Toolkit separately, which is not a realistic prerequisite.
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }

    private static bool DetectVulkan()
    {
        // NOTE: vulkan-1.dll may be present on Windows 10/11 without an
        // actual GPU-backed Vulkan device (Microsoft ships a runtime stub).
        // We accept this false-positive risk in v0.1 — if no real device
        // exists, Whisper.net's runtime fallback chain will land on CPU
        // automatically. Tighter detection would require P/Invoking
        // vkEnumeratePhysicalDevices, which is deferred to v0.2.
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "vulkan-1.dll"));
    }

    private static long GetSystemRamBytes()
    {
        // GC.GetGCMemoryInfo().TotalAvailableMemoryBytes reports the .NET
        // process budget, not physical RAM. We need the latter to choose
        // a model size that the host can actually accommodate.
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? (long)status.ullTotalPhys : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

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
    /// <summary>
    /// Follow the default matrix optimized for interactive responsiveness.
    /// </summary>
    Balanced,

    /// <summary>
    /// Shift the recommendation one tier upward when feasible. Suitable for
    /// batch indexing scenarios where transcription latency is acceptable.
    /// </summary>
    Accuracy,

    /// <summary>
    /// Shift the recommendation one tier downward. Suitable for low-latency
    /// UI flows where the user is actively waiting on the transcript.
    /// </summary>
    Speed,
}

/// <summary>
/// Snapshot of the detected Whisper runtime environment at probe time.
/// </summary>
public sealed record WhisperEnvironmentInfo(
    bool CudaAvailable,
    bool VulkanAvailable,
    long SystemRamBytes,
    int LogicalCores);
