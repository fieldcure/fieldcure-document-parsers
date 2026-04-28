namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// End-to-end Whisper.net transcription tests against the upstream whisper.net sample audio
/// set. Opt-in via <c>FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS=1</c> and
/// <c>FIELDCURE_WHISPER_MODEL_PATH</c>; otherwise reported as <see cref="Assert.Inconclusive(string)"/>
/// so default test runs remain fast and offline.
/// </summary>
[TestClass]
public class WhisperNetSampleTests
{
    private const string EnableWhisperFixtureTestsEnv = "FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS";
    private const string WhisperModelPathEnv = "FIELDCURE_WHISPER_MODEL_PATH";

    private static readonly string SampleDir =
        Path.Combine(AppContext.BaseDirectory, "TestData", "WhisperNetSamples");

    /// <summary>
    /// Verifies that bush.wav transcribes to text containing recognizable phrases from the
    /// Columbia disaster address and produces well-formed Markdown.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_BushSample_TranscribesColumbiaAddress()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(
            new MemoryStream(ReadSample("bush.wav")),
            new AudioExtractionOptions
            {
                SourceExtension = ".wav",
                Language = "en",
                ModelPath = modelPath,
                IncludeTimestamps = true
            });

        AssertWellFormedMarkdown(markdown);

        var normalized = NormalizeText(markdown);
        AssertContainsAny(
            normalized,
            "fellow americans",
            "mission control",
            "columbia",
            "space shuttle");
    }

    /// <summary>
    /// Verifies that kennedy.mp3 transcribes to text containing recognizable phrases from
    /// the Apollo program speech.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_KennedySample_TranscribesApolloSpeech()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(
            new MemoryStream(ReadSample("kennedy.mp3")),
            new AudioExtractionOptions
            {
                SourceExtension = ".mp3",
                Language = "en",
                ModelPath = modelPath,
                IncludeTimestamps = false
            });

        AssertWellFormedMarkdown(markdown);

        var normalized = NormalizeText(markdown);
        AssertContainsAny(
            normalized,
            "moon",
            "decade",
            "landing a man",
            "space project");
    }

    /// <summary>
    /// Verifies that multichannel.wav transcribes the two-speaker dialogue, exercising
    /// channel down-mixing in the conversion pipeline.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Whisper_MultichannelSample_TranscribesDialogue()
    {
        var modelPath = GetWhisperModelPathOrInconclusive();
        await using var parser = new AudioDocumentParser();

        var markdown = await parser.ParseAsync(
            new MemoryStream(ReadSample("multichannel.wav")),
            new AudioExtractionOptions
            {
                SourceExtension = ".wav",
                Language = "en",
                ModelPath = modelPath,
                IncludeTimestamps = false
            });

        AssertWellFormedMarkdown(markdown);

        var normalized = NormalizeText(markdown);
        AssertContainsAny(
            normalized,
            "birthday",
            "looking forward",
            "how are you");
    }

    /// <summary>
    /// Confirms that the upstream attribution note is shipped alongside the fixtures.
    /// </summary>
    [TestMethod]
    public void WhisperNetSamples_IncludeAttributionNote()
    {
        var attributionPath = Path.Combine(SampleDir, "ATTRIBUTION.md");
        Assert.IsTrue(File.Exists(attributionPath), $"Missing attribution file: {attributionPath}");

        var text = File.ReadAllText(attributionPath);
        Assert.IsTrue(text.Contains("whisper.net", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(text.Contains("bush.wav", StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("kennedy.mp3", StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("multichannel.wav", StringComparison.Ordinal));
    }

    /// <summary>
    /// Asserts that the produced Markdown carries the standard transcript header and a
    /// duration row from <c>MarkdownFormatter</c>.
    /// </summary>
    /// <param name="markdown">Markdown to inspect.</param>
    private static void AssertWellFormedMarkdown(string markdown)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(markdown), "Transcript markdown was empty.");
        Assert.IsTrue(markdown.Contains("# Audio Transcript", StringComparison.Ordinal),
            "Expected '# Audio Transcript' header in markdown output.");
        Assert.IsTrue(markdown.Contains("| Duration |", StringComparison.Ordinal),
            "Expected a 'Duration' metadata row in markdown output.");
    }

    /// <summary>
    /// Asserts that at least one of the candidate phrases appears in the normalized transcript.
    /// </summary>
    /// <param name="normalized">Normalized transcript text.</param>
    /// <param name="candidates">Candidate phrases (lowercase, alphanumeric only).</param>
    private static void AssertContainsAny(string normalized, params string[] candidates)
    {
        foreach (var phrase in candidates)
        {
            if (normalized.Contains(phrase, StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail(
            $"None of the expected phrases were present in the transcript. " +
            $"Looked for: [{string.Join(", ", candidates)}]. Transcript was: {normalized}");
    }

    /// <summary>
    /// Reads a whisper.net sample fixture from the test output directory.
    /// </summary>
    /// <param name="fileName">Fixture file name.</param>
    /// <returns>Fixture bytes.</returns>
    private static byte[] ReadSample(string fileName)
    {
        var path = Path.Combine(SampleDir, fileName);
        if (!File.Exists(path))
        {
            Assert.Fail($"Missing whisper.net sample fixture: {path}");
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
    /// Normalizes transcript text to lowercase letters/digits separated by single spaces.
    /// </summary>
    /// <param name="text">Text to normalize.</param>
    /// <returns>Normalized text suitable for loose phrase matching.</returns>
    private static string NormalizeText(string text)
    {
        var chars = text
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
