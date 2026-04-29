using System.Runtime.CompilerServices;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Unit tests for <see cref="AudioDocumentParser"/>.
/// </summary>
[TestClass]
public class AudioDocumentParserTests
{
    /// <summary>
    /// Verifies that all supported audio file extensions are registered by the parser.
    /// </summary>
    [TestMethod]
    public void SupportedExtensions_ContainsAudioFormats()
    {
        var parser = new AudioDocumentParser(new TestAudioTranscriber());
        var extensions = parser.SupportedExtensions.ToList();

        CollectionAssert.AreEquivalent(
            new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".webm" },
            extensions);
    }

    /// <summary>
    /// Verifies that injected transcriber output is rendered as timestamped Markdown.
    /// </summary>
    [TestMethod]
    public void ExtractText_WithInjectedTranscriber_ReturnsTimestampedMarkdown()
    {
        var transcriber = new TestAudioTranscriber(
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "hello world", "en", 0.95f),
            new TranscriptSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), "second line", "en", 0.9f));
        var parser = new AudioDocumentParser(transcriber);

        var markdown = parser.ExtractText(CreateSilentWav(), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            IncludeConfidence = true
        });

        Assert.AreEqual(1, transcriber.CallCount);
        Assert.IsTrue(markdown.Contains("# Audio Transcript"));
        Assert.IsTrue(markdown.Contains("| Duration | 00:00:05 |"));
        Assert.IsTrue(markdown.Contains("[00:00:00] hello world (confidence: 0.95)"));
        Assert.IsTrue(markdown.Contains("[00:00:03] second line (confidence: 0.90)"));
    }

    /// <summary>
    /// Verifies that timestamp markers can be omitted from the transcript body.
    /// </summary>
    [TestMethod]
    public void ExtractText_WithTimestampsDisabled_OmitsTimestampMarkers()
    {
        var parser = new AudioDocumentParser(new TestAudioTranscriber(
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "plain text")));

        var markdown = parser.ExtractText(CreateSilentWav(), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            IncludeTimestamps = false
        });

        Assert.IsFalse(markdown.Contains("[00:00:00]"));
        Assert.IsTrue(markdown.EndsWith("plain text", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that metadata reflects the model size reported by the transcriber.
    /// </summary>
    [TestMethod]
    public void ExtractText_WhenTranscriberReportsModelSize_UsesReportedModelInMetadata()
    {
        var parser = new AudioDocumentParser(new ModelSizeReportingTranscriber(
            WhisperModelSize.Large,
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "large model text")));

        var markdown = parser.ExtractText(CreateSilentWav(), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            ModelSize = WhisperModelSize.Base
        });

        StringAssert.Contains(markdown, "| Model | large |");
        Assert.IsFalse(markdown.Contains("| Model | base |", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that metadata falls back to caller options when no effective size is reported.
    /// </summary>
    [TestMethod]
    public void ExtractText_WhenTranscriberDoesNotReportModelSize_UsesOptionsModelInMetadata()
    {
        var parser = new AudioDocumentParser(new TestAudioTranscriber(
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "small model text")));

        var markdown = parser.ExtractText(CreateSilentWav(), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            ModelSize = WhisperModelSize.Small
        });

        StringAssert.Contains(markdown, "| Model | small |");
        Assert.IsFalse(markdown.Contains("| Model | base |", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the factory extension registers the audio parser.
    /// </summary>
    [TestMethod]
    public void AddAudioSupport_RegistersAudioParser()
    {
        var transcriber = new TestAudioTranscriber();
        DocumentParserFactoryAudioExtensions.AddAudioSupport(transcriber);

        var parser = DocumentParserFactory.GetParser(".mp3");

        Assert.IsInstanceOfType<AudioDocumentParser>(parser);
    }

    /// <summary>
    /// Test transcriber that exposes the effective model size used internally.
    /// </summary>
    private sealed class ModelSizeReportingTranscriber : IAudioTranscriber, IModelSizeReporting
    {
        /// <summary>
        /// Transcript segments emitted by the test transcriber.
        /// </summary>
        private readonly TranscriptSegment[] _segments;

        /// <summary>
        /// Creates a test transcriber with a reported model size.
        /// </summary>
        /// <param name="modelSize">Model size reported as the effective model.</param>
        /// <param name="segments">Transcript segments to emit.</param>
        public ModelSizeReportingTranscriber(
            WhisperModelSize modelSize,
            params TranscriptSegment[] segments)
        {
            EffectiveModelSize = modelSize;
            _segments = segments;
        }

        /// <summary>
        /// Gets the model size reported as the effective transcription model.
        /// </summary>
        public WhisperModelSize? EffectiveModelSize { get; }

        /// <inheritdoc />
        public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
            Stream pcmStream,
            AudioExtractionOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var segment in _segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return segment;
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a one-second silent WAV fixture in memory.
    /// </summary>
    /// <returns>Silent WAV bytes.</returns>
    private static byte[] CreateSilentWav()
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int seconds = 1;
        var dataLength = sampleRate * channels * (bitsPerSample / 8) * seconds;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);

        return stream.ToArray();
    }
}
