namespace FieldCure.DocumentParsers.Audio.Transcription;

/// <summary>
/// Abstraction for speech-to-text inference engines.
/// </summary>
public interface IAudioTranscriber : IAsyncDisposable
{
    /// <summary>
    /// Transcribes a 16 kHz mono PCM WAV stream into text segments.
    /// </summary>
    /// <param name="pcmStream">Seekable 16 kHz mono 16-bit PCM WAV stream.</param>
    /// <param name="options">Transcription options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async sequence of transcript segments.</returns>
    IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        Stream pcmStream,
        AudioExtractionOptions options,
        CancellationToken cancellationToken = default);
}
