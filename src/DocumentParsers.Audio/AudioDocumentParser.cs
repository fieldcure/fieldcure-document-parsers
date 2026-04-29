using FieldCure.DocumentParsers.Audio.Conversion;
using FieldCure.DocumentParsers.Audio.Formatting;
using FieldCure.DocumentParsers.Audio.Runtime;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Parses audio files into timestamped Markdown transcripts.
/// </summary>
public sealed class AudioDocumentParser : IDocumentParser, IAsyncDisposable
{
    private static readonly Lazy<GitHubReleasesWhisperRuntimeProvisioner> s_defaultProvisioner =
        new(() => new GitHubReleasesWhisperRuntimeProvisioner(),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IAudioTranscriber _transcriber;
    private readonly bool _ownsTranscriber;
    private readonly IWhisperRuntimeProvisioner _provisioner;

    /// <summary>
    /// Creates an audio parser using the default Whisper.net transcriber and the
    /// default <see cref="GitHubReleasesWhisperRuntimeProvisioner"/>.
    /// </summary>
    public AudioDocumentParser()
        : this(new WhisperTranscriber(), ownsTranscriber: true, provisioner: null)
    {
    }

    /// <summary>
    /// Creates an audio parser with a custom transcriber.
    /// </summary>
    /// <param name="transcriber">Transcriber used to produce transcript segments.</param>
    public AudioDocumentParser(IAudioTranscriber transcriber)
        : this(transcriber, ownsTranscriber: false, provisioner: null)
    {
    }

    /// <summary>
    /// Creates an audio parser with a custom transcriber and provisioner. Useful for
    /// tests that need to inject a fake provisioner pointing at a hand-rolled local
    /// manifest.
    /// </summary>
    /// <param name="transcriber">Transcriber used to produce transcript segments.</param>
    /// <param name="provisioner">Provisioner whose cache directory is wired into
    /// Whisper.net's loader on first transcription.</param>
    public AudioDocumentParser(IAudioTranscriber transcriber, IWhisperRuntimeProvisioner provisioner)
        : this(transcriber, ownsTranscriber: false, provisioner: provisioner)
    {
    }

    /// <summary>
    /// Creates an audio parser and records whether it owns the transcriber's lifetime.
    /// </summary>
    private AudioDocumentParser(
        IAudioTranscriber transcriber,
        bool ownsTranscriber,
        IWhisperRuntimeProvisioner? provisioner)
    {
        ArgumentNullException.ThrowIfNull(transcriber);
        _transcriber = transcriber;
        _ownsTranscriber = ownsTranscriber;
        _provisioner = provisioner ?? s_defaultProvisioner.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".mp3",
        ".wav",
        ".m4a",
        ".ogg",
        ".flac",
        ".webm"
    ];

    /// <inheritdoc />
    public string ExtractText(byte[] data)
        => ExtractText(data, AudioExtractionOptions.Default);

    /// <summary>
    /// Extracts a Markdown transcript with audio-specific options.
    /// </summary>
    /// <param name="data">Raw audio bytes.</param>
    /// <param name="options">Audio extraction options.</param>
    /// <returns>Timestamped Markdown transcript.</returns>
    public string ExtractText(byte[] data, AudioExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        return ParseAsync(data, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Parses audio bytes into a Markdown transcript.
    /// </summary>
    /// <param name="data">Raw audio bytes.</param>
    /// <param name="options">Audio extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Timestamped Markdown transcript.</returns>
    public Task<string> ParseAsync(
        byte[] data,
        AudioExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return ParseAsync(new MemoryStream(data, writable: false), options, cancellationToken);
    }

    /// <summary>
    /// Parses an audio stream into a Markdown transcript.
    /// </summary>
    /// <param name="stream">Readable audio stream.</param>
    /// <param name="options">Audio extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Timestamped Markdown transcript.</returns>
    public async Task<string> ParseAsync(
        Stream stream,
        AudioExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        options ??= AudioExtractionOptions.Default;

        await EnsureRuntimeProvisionedAsync(options, cancellationToken).ConfigureAwait(false);

        using var pcmStream = AudioConverter.ToPcm16kMono(stream, options.SourceExtension);

        var segments = new List<TranscriptSegment>();
        await foreach (var segment in _transcriber.TranscribeAsync(pcmStream, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            segments.Add(segment);
        }

        var formattingOptions =
            _transcriber is IModelSizeReporting reporter
            && reporter.EffectiveModelSize is { } effectiveModelSize
                ? options.WithModelSize(effectiveModelSize)
                : options;

        return MarkdownFormatter.Format(segments, formattingOptions);
    }

    /// <summary>
    /// Phase 2 + 3 of the v0.3 capability lifecycle. Selects the best variant for the
    /// host (Cuda &gt; Vulkan &gt; Cpu, gated by driver presence and manifest policy),
    /// downloads it on first call (idempotent thereafter), then activates Whisper.net's
    /// loader against the provisioner's cache directory.
    /// </summary>
    /// <remarks>
    /// <para>Activation is a one-time per-process side effect — Whisper.net caches its
    /// first successful probe. Subsequent calls only re-check provisioning, which is a
    /// cheap file-existence test once the cache is warm.</para>
    /// <para>Skipped entirely when the consumer injected a custom <see cref="IAudioTranscriber"/>
    /// (<c>_ownsTranscriber == false</c>): injection signals "I'm bringing my own
    /// transcription stack, do not touch Whisper.net's loader on my behalf." This keeps
    /// fakes / mocks / out-of-process transcribers usable without forcing a manifest fetch.</para>
    /// </remarks>
    private async Task EnsureRuntimeProvisionedAsync(
        AudioExtractionOptions options,
        CancellationToken cancellationToken)
    {
        if (!_ownsTranscriber) return;

        var variant = WhisperRuntime.SelectPreferredVariant(_provisioner);

        if (!_provisioner.IsProvisioned(variant))
        {
            await _provisioner.ProvisionAsync(variant, cancellationToken, options.ProgressCallback)
                .ConfigureAwait(false);
        }

        WhisperRuntime.Activate(_provisioner);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsTranscriber)
        {
            await _transcriber.DisposeAsync().ConfigureAwait(false);
        }
    }
}
