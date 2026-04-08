using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Tesseract;

namespace FieldCure.DocumentParsers.Pdf.Ocr;

/// <summary>
/// Tesseract-based OCR engine with automatic language discovery and engine pooling.
/// Extracts embedded traineddata files on first use and discovers
/// all available languages from the tessdata directory.
/// </summary>
public sealed partial class TesseractOcrEngine : IOcrEngine, IDisposable
{
    #region Fields

    private readonly ConcurrentBag<TesseractEngine> _pool;
    private readonly SemaphoreSlim _semaphore;
    private readonly string _tessdataPath;
    private readonly string _languages;
    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new Tesseract OCR engine with automatic language discovery.
    /// Embedded traineddata files are extracted to a temp directory on first use.
    /// </summary>
    /// <param name="maxPoolSize">
    /// Maximum number of concurrent Tesseract engine instances.
    /// Defaults to <c>min(ProcessorCount, 4)</c>.
    /// </param>
    public TesseractOcrEngine(int? maxPoolSize = null)
    {
        var poolSize = maxPoolSize ?? Math.Min(Environment.ProcessorCount, 4);

        _tessdataPath = ExtractEmbeddedTessdata();
        _languages = DiscoverLanguages(_tessdataPath);
        _pool = new ConcurrentBag<TesseractEngine>();
        _semaphore = new SemaphoreSlim(poolSize, poolSize);

        // Pre-create one engine to fail fast on bad tessdata
        _pool.Add(CreateEngine());
    }

    #endregion

    #region IOcrEngine

    /// <inheritdoc />
    public Task<string> RecognizeAsync(byte[] imageBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(imageBytes);

        _semaphore.Wait();
        try
        {
            if (!_pool.TryTake(out var engine))
            {
                engine = CreateEngine();
            }

            try
            {
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                var text = page.GetText();
                return Task.FromResult(PostProcessKorean(text));
            }
            finally
            {
                _pool.Add(engine);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

    #region Korean post-processing

    /// <summary>
    /// Removes spurious spaces between Korean characters inserted by Tesseract.
    /// Applied in two passes to handle cascading adjacency.
    /// </summary>
    private static string PostProcessKorean(string text)
    {
        // Two passes: removing one space may bring previously non-adjacent Korean chars together
        text = KoreanSpaceRegex().Replace(text, "$1$2");
        text = KoreanSpaceRegex().Replace(text, "$1$2");
        return text;
    }

    /// <summary>
    /// Matches a Korean syllable followed by whitespace followed by another Korean syllable.
    /// Unicode range AC00-D7A3 covers all precomposed Hangul syllables.
    /// </summary>
    [GeneratedRegex(@"([\uAC00-\uD7A3])\s+([\uAC00-\uD7A3])")]
    private static partial Regex KoreanSpaceRegex();

    #endregion

    #region Resource extraction

    /// <summary>
    /// Extracts embedded traineddata files to a persistent temp directory.
    /// Skips extraction if files already exist with matching size.
    /// </summary>
    private static string ExtractEmbeddedTessdata()
    {
        var tessdataPath = Path.Combine(Path.GetTempPath(), "FieldCure.Ocr", "tessdata");
        Directory.CreateDirectory(tessdataPath);

        var assembly = typeof(TesseractOcrEngine).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".traineddata", StringComparison.OrdinalIgnoreCase)))
        {
            // Resource name format: FieldCure.DocumentParsers.Pdf.Ocr.tessdata.eng.traineddata
            // Extract language name: second-to-last segment before ".traineddata"
            var segments = name.Split('.');
            var langIndex = segments.Length - 2; // "eng" or "kor" is right before "traineddata"
            var fileName = segments[langIndex] + ".traineddata";
            var destPath = Path.Combine(tessdataPath, fileName);

            using var stream = assembly.GetManifestResourceStream(name)!;
            if (File.Exists(destPath) && new FileInfo(destPath).Length == stream.Length)
                continue;

            using var fs = File.Create(destPath);
            stream.CopyTo(fs);
        }

        return tessdataPath;
    }

    /// <summary>
    /// Discovers available languages from traineddata files in the tessdata directory.
    /// English is always listed first for optimal multi-language recognition.
    /// </summary>
    private static string DiscoverLanguages(string tessdataPath)
    {
        var languages = Directory.GetFiles(tessdataPath, "*.traineddata")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(l => l == "eng")
            .ToList();

        return languages.Count > 0
            ? string.Join("+", languages)
            : "eng";
    }

    #endregion

    #region Engine pool

    private TesseractEngine CreateEngine()
        => new(_tessdataPath, _languages, EngineMode.Default);

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_pool.TryTake(out var engine))
        {
            engine.Dispose();
        }

        _semaphore.Dispose();
    }

    #endregion
}
