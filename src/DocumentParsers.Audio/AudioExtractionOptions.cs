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

    /// <summary>
    /// Returns a copy of these options with the specified model size, leaving
    /// all other properties unchanged. Provided as a class-friendly substitute
    /// for the <c>with</c> expression syntax that records support natively.
    /// </summary>
    /// <remarks>
    /// MAINTENANCE: this method explicitly copies every <c>init</c>-only property
    /// declared on <see cref="AudioExtractionOptions"/> and its base
    /// <see cref="ExtractionOptions"/>. When a new <c>init</c> property is added
    /// to either type, it MUST be appended here, otherwise the copy silently
    /// drops it. The <c>WithModelSize_PreservesAllInitProperties</c> regression
    /// test enforces this via reflection.
    /// </remarks>
    public AudioExtractionOptions WithModelSize(WhisperModelSize modelSize) =>
        new()
        {
            // Base ExtractionOptions properties
            IncludeMetadata = IncludeMetadata,
            IncludeHeaders = IncludeHeaders,
            IncludeFooters = IncludeFooters,
            IncludeFootnotes = IncludeFootnotes,
            IncludeEndnotes = IncludeEndnotes,
            IncludeComments = IncludeComments,
            SourceExtension = SourceExtension,

            // AudioExtractionOptions own properties
            Language = Language,
            ModelSize = modelSize, // overridden
            ModelPath = ModelPath,
            TranslateToEnglish = TranslateToEnglish,
            RuntimeLibraryOrder = RuntimeLibraryOrder,
            IncludeConfidence = IncludeConfidence,
            IncludeTimestamps = IncludeTimestamps,
        };
}
