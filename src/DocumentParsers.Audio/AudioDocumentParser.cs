using FieldCure.DocumentParsers.Audio.Conversion;
using FieldCure.DocumentParsers.Audio.Formatting;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Parses audio files into timestamped Markdown transcripts.
/// </summary>
public sealed class AudioDocumentParser : IDocumentParser, IAsyncDisposable
{
    private readonly IAudioTranscriber _transcriber;
    private readonly bool _ownsTranscriber;

    /// <summary>
    /// Creates an audio parser using the default Whisper.net transcriber.
    /// </summary>
    public AudioDocumentParser()
        : this(new WhisperTranscriber(), ownsTranscriber: true)
    {
    }

    /// <summary>
    /// Creates an audio parser with a custom transcriber.
    /// </summary>
    /// <param name="transcriber">Transcriber used to produce transcript segments.</param>
    public AudioDocumentParser(IAudioTranscriber transcriber)
        : this(transcriber, ownsTranscriber: false)
    {
    }

    /// <summary>
    /// Creates an audio parser and records whether it owns the transcriber's lifetime.
    /// </summary>
    /// <param name="transcriber">Transcriber used to produce transcript segments.</param>
    /// <param name="ownsTranscriber">Whether this parser should dispose the transcriber.</param>
    private AudioDocumentParser(IAudioTranscriber transcriber, bool ownsTranscriber)
    {
        ArgumentNullException.ThrowIfNull(transcriber);
        _transcriber = transcriber;
        _ownsTranscriber = ownsTranscriber;
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
        using var pcmStream = AudioConverter.ToPcm16kMono(stream, options.SourceExtension);

        var segments = new List<TranscriptSegment>();
        await foreach (var segment in _transcriber.TranscribeAsync(pcmStream, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            segments.Add(segment);
        }

        return MarkdownFormatter.Format(segments, options);
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
