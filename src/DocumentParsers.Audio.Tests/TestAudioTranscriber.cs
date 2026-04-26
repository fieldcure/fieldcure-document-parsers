using System.Runtime.CompilerServices;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Test transcriber that records the PCM stream shape and emits configured segments.
/// </summary>
internal sealed class TestAudioTranscriber(params TranscriptSegment[] segments) : IAudioTranscriber
{
    /// <summary>
    /// Number of times transcription was requested.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Whether the parser supplied a seekable PCM stream.
    /// </summary>
    public bool WasSeekable { get; private set; }

    /// <summary>
    /// Sample rate read from the WAV header.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Channel count read from the WAV header.
    /// </summary>
    public short Channels { get; private set; }

    /// <summary>
    /// Bits per sample read from the WAV header.
    /// </summary>
    public short BitsPerSample { get; private set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        Stream pcmStream,
        AudioExtractionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallCount++;
        WasSeekable = pcmStream.CanSeek;
        ReadWavHeader(pcmStream);

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return segment;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads the relevant WAV header fields from the PCM stream.
    /// </summary>
    /// <param name="pcmStream">PCM WAV stream supplied to the transcriber.</param>
    private void ReadWavHeader(Stream pcmStream)
    {
        var originalPosition = pcmStream.CanSeek ? pcmStream.Position : 0;
        Span<byte> header = stackalloc byte[44];
        var read = pcmStream.Read(header);
        if (read < header.Length)
        {
            throw new InvalidDataException("PCM stream did not contain a complete WAV header.");
        }

        SampleRate = BitConverter.ToInt32(header.Slice(24, 4));
        Channels = BitConverter.ToInt16(header.Slice(22, 2));
        BitsPerSample = BitConverter.ToInt16(header.Slice(34, 2));

        if (pcmStream.CanSeek)
        {
            pcmStream.Position = originalPosition;
        }
    }
}
