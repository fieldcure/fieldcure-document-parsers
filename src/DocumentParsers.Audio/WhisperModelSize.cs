namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Available Whisper model sizes.
/// Larger models are more accurate but slower and require more memory.
/// </summary>
public enum WhisperModelSize
{
    /// <summary>Smallest model, fastest, lowest accuracy.</summary>
    Tiny,

    /// <summary>Balanced speed and accuracy.</summary>
    Base,

    /// <summary>Improved accuracy with moderate resource use.</summary>
    Small,

    /// <summary>High accuracy with higher memory and CPU/GPU requirements.</summary>
    Medium,

    /// <summary>Highest accuracy, slowest, largest memory footprint.</summary>
    Large
}
