namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Identifies a Whisper native runtime flavor distributed via
/// <see href="https://github.com/fieldcure/fieldcure-whisper-runtimes">fieldcure-whisper-runtimes</see>.
/// </summary>
public enum WhisperRuntimeVariant
{
    /// <summary>CPU-only runtime. Always provisionable; no driver requirement.</summary>
    Cpu,

    /// <summary>NVIDIA GPU runtime. Requires <c>nvcuda.dll</c> from the user's NVIDIA driver
    /// (not redistributed by us) plus CUDA Toolkit redistributables we fetch.</summary>
    Cuda,

    /// <summary>Cross-vendor GPU runtime via Vulkan. Requires <c>vulkan-1.dll</c> from the
    /// user's GPU driver / Vulkan Loader (not redistributed by us).</summary>
    Vulkan,
}
