using Whisper.net.LibraryLoader;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Options for controlling audio transcription behavior.
/// </summary>
public class AudioExtractionOptions : ExtractionOptions
{
    /// <summary>
    /// Language hint for transcription (for example, <c>ko</c> or <c>en</c>).
    /// When null, Whisper auto-detects the language.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Whisper model size to use for transcription.
    /// </summary>
    public WhisperModelSize ModelSize { get; init; } = WhisperModelSize.Base;

    /// <summary>
    /// Custom path to a pre-downloaded ggml model file.
    /// When set, <see cref="ModelSize"/> is ignored.
    /// </summary>
    public string? ModelPath { get; init; }

    /// <summary>
    /// Whether to translate non-English speech to English.
    /// </summary>
    public bool TranslateToEnglish { get; init; }

    /// <summary>
    /// Override the default Whisper.net runtime selection priority.
    /// </summary>
    public IReadOnlyList<RuntimeLibrary>? RuntimeLibraryOrder { get; init; }

    /// <summary>
    /// Whether to include segment-level confidence scores in the Markdown output.
    /// </summary>
    public bool IncludeConfidence { get; init; }

    /// <summary>
    /// Whether to include segment timestamps in the Markdown output.
    /// </summary>
    public bool IncludeTimestamps { get; init; } = true;

    /// <summary>
    /// Default audio extraction options.
    /// </summary>
    public static new AudioExtractionOptions Default { get; } = new();
}
