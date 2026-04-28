namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Snapshot of which Whisper runtime variants are usable right now on this host.
/// Returned by <see cref="WhisperRuntime.GetActivationStatus"/>.
/// </summary>
/// <param name="CpuUsable">CPU runtime is provisioned. Always true after a successful CPU
/// provision call (and on a fresh machine after the package's bundled CPU runtime is found).</param>
/// <param name="CudaUsable">CUDA driver is present, the host's <c>cuDriverGetVersion</c>
/// is at least the manifest's <c>cuda.minDriverVersion</c>, AND the cuda variant is
/// provisioned in the cache directory.</param>
/// <param name="VulkanUsable">Vulkan loader is present AND the vulkan variant is provisioned.</param>
/// <param name="LibraryPath">The directory passed to <c>RuntimeOptions.LibraryPath</c>.
/// Diagnostics use only — Whisper.net's loader walks the search-path order from there.</param>
public sealed record WhisperActivationStatus(
    bool CpuUsable,
    bool CudaUsable,
    bool VulkanUsable,
    string LibraryPath);
