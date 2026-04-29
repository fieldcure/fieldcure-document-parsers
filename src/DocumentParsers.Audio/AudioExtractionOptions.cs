using FieldCure.DocumentParsers.Audio.Runtime;
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
    /// Optional progress reporter for v0.3 runtime provisioning. Fires during the
    /// download / verify / activate phases of <see cref="Runtime.IWhisperRuntimeProvisioner.ProvisionAsync"/>
    /// when a transcription call triggers a first-time runtime fetch. Whisper inference
    /// progress (segment-level) is NOT exposed here — that flows through Whisper.net's
    /// own segment callback API.
    /// </summary>
    public IProgress<WhisperRuntimeProgress>? ProgressCallback { get; init; }

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
            ProgressCallback = ProgressCallback,
        };

    /// <summary>
    /// Returns a copy of these options with <see cref="ModelSize"/> overridden and
    /// <see cref="ModelPath"/> cleared, so transcript metadata reflects the effective
    /// model reported by an <see cref="Transcription.IModelSizeReporting"/> transcriber
    /// rather than the caller-supplied path.
    /// </summary>
    /// <remarks>
    /// MAINTENANCE: like <see cref="WithModelSize"/>, this method explicitly copies every
    /// <c>init</c>-only property declared on this type and its base. When a new <c>init</c>
    /// property is added, it MUST be appended here. The
    /// <c>WithEffectiveModel_PreservesAllInitPropertiesExceptModelPath</c> regression test
    /// enforces this via reflection.
    /// </remarks>
    /// <param name="modelSize">Effective model size to record.</param>
    /// <returns>Copy with <c>ModelSize</c> overridden and <c>ModelPath</c> cleared.</returns>
    public AudioExtractionOptions WithEffectiveModel(WhisperModelSize modelSize) =>
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
            ModelPath = null,      // cleared so the effective size wins in formatting
            TranslateToEnglish = TranslateToEnglish,
            RuntimeLibraryOrder = RuntimeLibraryOrder,
            IncludeConfidence = IncludeConfidence,
            IncludeTimestamps = IncludeTimestamps,
            ProgressCallback = ProgressCallback,
        };
}
