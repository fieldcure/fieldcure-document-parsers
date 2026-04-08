namespace FieldCure.DocumentParsers.Pdf.Ocr.Tests;

[TestClass]
public class TesseractOcrEngineTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private static byte[] ReadBytes(string filename)
        => File.ReadAllBytes(Path.Combine(TestDataDir, filename));

    private static byte[] RenderFirstPage(string filename, int dpi = 300)
    {
        var images = new PdfParser().ExtractImages(ReadBytes(filename), dpi);
        return images[0].Data;
    }

    #region Construction

    [TestMethod]
    [Description("Engine construction must succeed and extract tessdata to temp.")]
    public void Constructor_ExtractsTessdataAndCreatesEngine()
    {
        using var engine = new TesseractOcrEngine();

        var tessdataPath = Path.Combine(Path.GetTempPath(), "FieldCure.Ocr", "tessdata");
        Assert.IsTrue(File.Exists(Path.Combine(tessdataPath, "eng.traineddata")),
            "eng.traineddata should be extracted to temp.");
        Assert.IsTrue(File.Exists(Path.Combine(tessdataPath, "kor.traineddata")),
            "kor.traineddata should be extracted to temp.");
    }

    [TestMethod]
    [Description("Dispose must not throw even when called multiple times.")]
    public void Dispose_DoesNotThrow()
    {
        var engine = new TesseractOcrEngine();
        engine.Dispose();
        engine.Dispose(); // double-dispose should be safe
    }

    [TestMethod]
    [Description("RecognizeAsync must throw on disposed engine.")]
    public void RecognizeAsync_AfterDispose_Throws()
    {
        var engine = new TesseractOcrEngine();
        engine.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            engine.RecognizeAsync([0x89, 0x50, 0x4E, 0x47]).GetAwaiter().GetResult());
    }

    #endregion

    #region English OCR — scanned_english.pdf

    [TestMethod]
    [Description("OCR of scanned English PDF should extract title and key terms.")]
    public async Task RecognizeAsync_EnglishPdf_ExtractsTitle()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_english.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("Impedance Spectroscopy", StringComparison.OrdinalIgnoreCase),
            "Should contain 'Impedance Spectroscopy'.");
        Assert.IsTrue(text.Contains("SOH", StringComparison.Ordinal),
            "Should contain 'SOH'.");
    }

    [TestMethod]
    [Description("OCR of scanned English PDF should extract section headings.")]
    public async Task RecognizeAsync_EnglishPdf_ExtractsSectionHeadings()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_english.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("Introduction", StringComparison.OrdinalIgnoreCase),
            "Should contain 'Introduction' heading.");
        Assert.IsTrue(text.Contains("Experimental Methods", StringComparison.OrdinalIgnoreCase),
            "Should contain 'Experimental Methods' heading.");
    }

    [TestMethod]
    [Description("OCR of scanned English PDF should extract numeric data.")]
    public async Task RecognizeAsync_EnglishPdf_ExtractsNumericData()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_english.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("96.3", StringComparison.Ordinal),
            "Should contain accuracy value '96.3'.");
        Assert.IsTrue(text.Contains("18650", StringComparison.Ordinal),
            "Should contain cell model '18650'.");
    }

    #endregion

    #region Korean OCR — scanned_korean.pdf

    [TestMethod]
    [Description("OCR of scanned Korean PDF should extract Korean title keywords.")]
    public async Task RecognizeAsync_KoreanPdf_ExtractsKoreanTitle()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_korean.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("배터리", StringComparison.Ordinal),
            "Should contain '배터리'.");
        Assert.IsTrue(text.Contains("임피던스", StringComparison.Ordinal),
            "Should contain '임피던스'.");
    }

    [TestMethod]
    [Description("Korean post-processing should remove spurious spaces between Hangul chars.")]
    public async Task RecognizeAsync_KoreanPdf_NoSpuriousSpaces()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_korean.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        // Tesseract commonly produces "리 튬 이 온" — post-processing should yield "리튬이온"
        Assert.IsFalse(text.Contains("리 튬", StringComparison.Ordinal),
            "Korean post-processing should remove space in '리 튬'.");
    }

    [TestMethod]
    [Description("OCR of scanned Korean PDF should extract section headings.")]
    public async Task RecognizeAsync_KoreanPdf_ExtractsSectionHeadings()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_korean.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("서론", StringComparison.Ordinal),
            "Should contain '서론' heading.");
    }

    #endregion

    #region Mixed Korean-English OCR — scanned_mixed.pdf

    [TestMethod]
    [Description("OCR of mixed PDF should extract both English and Korean terms.")]
    public async Task RecognizeAsync_MixedPdf_ExtractsBothLanguages()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_mixed.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        // English terms
        Assert.IsTrue(text.Contains("EIS", StringComparison.Ordinal),
            "Should contain English term 'EIS'.");
        Assert.IsTrue(text.Contains("DRT", StringComparison.Ordinal),
            "Should contain English term 'DRT'.");
        Assert.IsTrue(text.Contains("Gradient Boosting", StringComparison.OrdinalIgnoreCase),
            "Should contain 'Gradient Boosting'.");

        // Korean terms
        Assert.IsTrue(text.Contains("배터리", StringComparison.Ordinal),
            "Should contain Korean term '배터리'.");
    }

    [TestMethod]
    [Description("OCR of mixed PDF should extract numeric data correctly.")]
    public async Task RecognizeAsync_MixedPdf_ExtractsNumericData()
    {
        using var engine = new TesseractOcrEngine();
        var imageBytes = RenderFirstPage("scanned_mixed.pdf");

        var text = await engine.RecognizeAsync(imageBytes);

        Assert.IsTrue(text.Contains("96.3", StringComparison.Ordinal),
            "Should contain accuracy value '96.3'.");
        Assert.IsTrue(text.Contains("18650", StringComparison.Ordinal),
            "Should contain cell model '18650'.");
    }

    #endregion

    #region Concurrency

    [TestMethod]
    [Description("Multiple concurrent OCR calls should not deadlock or throw.")]
    public async Task RecognizeAsync_ConcurrentCalls_Succeeds()
    {
        using var engine = new TesseractOcrEngine(maxPoolSize: 2);
        var imageBytes = RenderFirstPage("scanned_english.pdf");

        var tasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => engine.RecognizeAsync(imageBytes)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var text in results)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(text),
                "Each concurrent OCR call should return non-empty text.");
        }
    }

    #endregion
}
