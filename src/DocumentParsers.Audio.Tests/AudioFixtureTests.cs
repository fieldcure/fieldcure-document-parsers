using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Tests that exercise real audio fixtures while keeping heavyweight Whisper inference opt-in.
/// </summary>
[TestClass]
public class AudioFixtureTests
{
    private const string EnableWhisperFixtureTestsEnv = "FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS";
    private const string WhisperModelPathEnv = "FIELDCURE_WHISPER_MODEL_PATH";
    private const string EnglishFixtureFileName = "gettysburg_address_64kb.mp3";
    private const string KoreanFixtureFileName = "na_dohyang_short_stories_ko_20s.wav";

    private static readonly string PublicDomainDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData", "PublicDomain");

    /// <summary>
    /// Verifies that the checked-in public-domain MP3 fixture is decoded to Whisper-ready PCM.
    /// </summary>
    [TestMethod]
    public void ExtractText_PublicDomainMp3Fixture_DecodesToPcm16kMono()
    {
        var transcriber = new TestAudioTranscriber(
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "public domain fixture", "en"));
        var parser = new AudioDocumentParser(transcriber);

        var markdown = parser.ExtractText(ReadPublicDomainFixture(EnglishFixtureFileName), new AudioExtractionOptions
        {
            SourceExtension = ".mp3",
            Language = "en"
        });

        Assert.AreEqual(1, transcriber.CallCount);
        Assert.IsTrue(transcriber.WasSeekable);
        Assert.AreEqual(16000, transcriber.SampleRate);
        Assert.AreEqual(1, transcriber.Channels);
        Assert.AreEqual(16, transcriber.BitsPerSample);
        Assert.IsTrue(markdown.Contains("public domain fixture", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the checked-in public-domain Korean WAV fixture is decoded to Whisper-ready PCM.
    /// </summary>
    [TestMethod]
    public void ExtractText_PublicDomainKoreanFixture_DecodesToPcm16kMono()
    {
        var transcriber = new TestAudioTranscriber(
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "public domain Korean fixture", "ko"));
        var parser = new AudioDocumentParser(transcriber);

        var markdown = parser.ExtractText(ReadPublicDomainFixture(KoreanFixtureFileName), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            Language = "ko"
        });

        Assert.AreEqual(1, transcriber.CallCount);
        Assert.IsTrue(transcriber.WasSeekable);
        Assert.AreEqual(16000, transcriber.SampleRate);
        Assert.AreEqual(1, transcriber.Channels);
        Assert.AreEqual(16, transcriber.BitsPerSample);
        Assert.IsTrue(markdown.Contains("public domain Korean fixture", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that public-domain audio fixtures carry an explicit license note.
    /// </summary>
    [TestMethod]
    public void PublicDomainFixtures_IncludeLicenseNote()
    {
        var licensePath = Path.Combine(PublicDomainDataDir, "LICENSES.md");
        var licenseText = File.ReadAllText(licensePath);

        Assert.IsTrue(licenseText.Contains("Public Domain", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(licenseText.Contains(EnglishFixtureFileName, StringComparison.Ordinal));
        Assert.IsTrue(licenseText.Contains(KoreanFixtureFileName, StringComparison.Ordinal));
        Assert.IsTrue(licenseText.Contains("LibriVox", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Runs an opt-in Whisper smoke test against the public-domain English fixture.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_PublicDomainEnglishFixture_TranscribesExpectedTerms()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(new MemoryStream(ReadPublicDomainFixture(EnglishFixtureFileName)), new AudioExtractionOptions
        {
            SourceExtension = ".mp3",
            Language = "en",
            ModelPath = modelPath,
            IncludeTimestamps = false
        });

        var normalized = NormalizeText(markdown);
        Assert.IsTrue(normalized.Contains("four score", StringComparison.Ordinal)
            || normalized.Contains("seven years", StringComparison.Ordinal)
            || normalized.Contains("new nation", StringComparison.Ordinal),
            "Expected at least one known Gettysburg Address phrase.");
    }

    /// <summary>
    /// Runs an opt-in Whisper smoke test against the public-domain Korean fixture.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_PublicDomainKoreanFixture_TranscribesHangul()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(new MemoryStream(ReadPublicDomainFixture(KoreanFixtureFileName)), new AudioExtractionOptions
        {
            SourceExtension = ".wav",
            Language = "ko",
            ModelPath = modelPath,
            IncludeTimestamps = false
        });

        var normalizedTranscript = NormalizeText(markdown);
        Assert.IsTrue(
            normalizedTranscript.Any(IsKoreanSyllable),
            "Expected Hangul syllables from the LibriVox Korean fixture.");
    }

    /// <summary>
    /// Verifies that, when the transcriber does not implement
    /// <see cref="IModelSizeReporting"/>, the metadata header reports the
    /// caller-supplied <c>ModelPath</c> filename (the formatter's path-wins policy).
    /// </summary>
    /// <remarks>
    /// Opt-in: skipped as <c>Inconclusive</c> unless both environment variables are set.
    /// <code>
    /// # PowerShell
    /// $env:FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS = '1'
    /// $env:FIELDCURE_WHISPER_MODEL_PATH = "$env:LOCALAPPDATA\FieldCure\WhisperModels\ggml-base.bin"
    /// dotnet test src/DocumentParsers.Audio.Tests --filter "TestCategory=Integration"
    /// </code>
    /// The <c>ggml-base.bin</c> model can be downloaded from
    /// <c>https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin</c>.
    /// </remarks>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_PublicDomainEnglishFixture_HeaderReportsModelPathFileName()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(
            new MemoryStream(ReadPublicDomainFixture(EnglishFixtureFileName)),
            new AudioExtractionOptions
            {
                SourceExtension = ".mp3",
                Language = "en",
                ModelPath = modelPath,
                IncludeTimestamps = false,
            });

        var expectedFileName = Path.GetFileName(modelPath);
        StringAssert.Contains(markdown, $"| Model | {expectedFileName} |");
    }

    /// <summary>
    /// Reads a public-domain fixture from the test output directory.
    /// </summary>
    /// <param name="fileName">Fixture file name.</param>
    /// <returns>Fixture bytes.</returns>
    private static byte[] ReadPublicDomainFixture(string fileName)
    {
        var path = Path.Combine(PublicDomainDataDir, fileName);
        if (!File.Exists(path))
        {
            Assert.Fail($"Missing public-domain fixture: {path}");
        }

        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Resolves the Whisper model path for opt-in fixture tests.
    /// </summary>
    /// <returns>Whisper ggml model path.</returns>
    private static string GetWhisperModelPathOrInconclusive()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableWhisperFixtureTestsEnv), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {EnableWhisperFixtureTestsEnv}=1 and {WhisperModelPathEnv}=<ggml model path> to run Whisper fixture tests.");
        }

        var modelPath = Environment.GetEnvironmentVariable(WhisperModelPathEnv);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            Assert.Inconclusive($"Set {WhisperModelPathEnv} to an existing ggml model file.");
        }

        return modelPath;
    }

    /// <summary>
    /// Normalizes transcript text for loose phrase matching.
    /// </summary>
    /// <param name="text">Text to normalize.</param>
    /// <returns>Lowercase text with punctuation collapsed to spaces.</returns>
    private static string NormalizeText(string text)
    {
        var chars = text
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || IsKoreanSyllable(c) ? c : ' ')
            .ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Determines whether a character is a precomposed Hangul syllable.
    /// </summary>
    /// <param name="c">Character to inspect.</param>
    /// <returns>True when the character is a Hangul syllable.</returns>
    private static bool IsKoreanSyllable(char c)
        => c is >= '\uAC00' and <= '\uD7A3';
}
