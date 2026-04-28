using Whisper.net.LibraryLoader;

namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Phase 3 of the v0.3 capability lifecycle: Activate. Wires the
/// <see cref="IWhisperRuntimeProvisioner"/> cache directory into Whisper.net's
/// loader by setting <see cref="RuntimeOptions.LibraryPath"/>, and reports which
/// variants are usable right now.
/// </summary>
public static class WhisperRuntime
{
    private static readonly object s_activateLock = new();
    private static IWhisperRuntimeProvisioner? s_activeProvisioner;

    /// <summary>Idempotently sets <see cref="RuntimeOptions.LibraryPath"/> so Whisper.net
    /// looks under <paramref name="provisioner"/>'s cache directory for native binaries.
    /// Subsequent calls with the same provisioner are no-ops; calls with a different
    /// provisioner replace the path (rare in practice — a process typically owns one
    /// provisioner for its lifetime).</summary>
    /// <remarks>Must be called BEFORE the first <c>WhisperFactory.From*()</c>. Whisper.net's
    /// loader caches the result of its first probe.</remarks>
    public static void Activate(IWhisperRuntimeProvisioner provisioner)
    {
        ArgumentNullException.ThrowIfNull(provisioner);

        lock (s_activateLock)
        {
            if (ReferenceEquals(s_activeProvisioner, provisioner)) return;

            // RuntimeOptions.LibraryPath uses Path.GetDirectoryName(...) on its value, so
            // we pass a sentinel file path inside the cache directory. The file does not
            // need to exist — Whisper.net only consumes the directory portion.
            RuntimeOptions.LibraryPath = Path.Combine(provisioner.CacheDirectory, "_sentinel");
            s_activeProvisioner = provisioner;
        }
    }

    /// <summary>Reports which runtime variants are usable on this host right now.</summary>
    /// <param name="provisioner">Provisioner whose cache state and manifest are inspected.</param>
    /// <remarks>"Usable" means: driver present (Phase 1) AND variant provisioned (Phase 2)
    /// AND, for Cuda, driver version meets the manifest's
    /// <c>cuda.minDriverVersion</c> policy.</remarks>
    public static WhisperActivationStatus GetActivationStatus(IWhisperRuntimeProvisioner provisioner)
    {
        ArgumentNullException.ThrowIfNull(provisioner);

        var env = WhisperEnvironment.Detect();
        var manifest = provisioner.GetManifestAsync().GetAwaiter().GetResult();

        var cpuUsable = provisioner.IsProvisioned(WhisperRuntimeVariant.Cpu);

        var cudaUsable = false;
        if (env.CudaDriverAvailable && provisioner.IsProvisioned(WhisperRuntimeVariant.Cuda))
        {
            var minDriver = manifest.GetVariant(WhisperRuntimeVariant.Cuda)?.MinDriverVersion;
            cudaUsable = minDriver is null
                || (env.CudaDriverVersion is int actual && actual >= minDriver);
        }

        var vulkanUsable = env.VulkanDriverAvailable
            && provisioner.IsProvisioned(WhisperRuntimeVariant.Vulkan);

        return new WhisperActivationStatus(
            CpuUsable: cpuUsable,
            CudaUsable: cudaUsable,
            VulkanUsable: vulkanUsable,
            LibraryPath: provisioner.CacheDirectory);
    }

    /// <summary>Selects the best variant the consumer should provision given current
    /// environment + manifest policy. Order: Cuda (if driver + minDriverVersion satisfied)
    /// → Vulkan (if driver) → Cpu. Used by <see cref="AudioDocumentParser"/>'s lazy
    /// provisioning to decide which variant to acquire on first transcription.</summary>
    public static WhisperRuntimeVariant SelectPreferredVariant(IWhisperRuntimeProvisioner provisioner)
    {
        ArgumentNullException.ThrowIfNull(provisioner);

        var env = WhisperEnvironment.Detect();
        if (env.CudaDriverAvailable)
        {
            var manifest = provisioner.GetManifestAsync().GetAwaiter().GetResult();
            var minDriver = manifest.GetVariant(WhisperRuntimeVariant.Cuda)?.MinDriverVersion;
            if (minDriver is null || (env.CudaDriverVersion is int actual && actual >= minDriver))
            {
                return WhisperRuntimeVariant.Cuda;
            }
        }
        if (env.VulkanDriverAvailable)
        {
            return WhisperRuntimeVariant.Vulkan;
        }
        return WhisperRuntimeVariant.Cpu;
    }
}
