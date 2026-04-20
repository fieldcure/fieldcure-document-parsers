namespace FieldCure.DocumentParsers.Ocr.Tests;

[TestClass]
public class OcrPdfParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private static byte[] ReadBytes(string filename)
        => File.ReadAllBytes(Path.Combine(TestDataDir, filename));

    #region Scanned PDF — OCR fallback triggers

    [TestMethod]
    [Description("Scanned English PDF should produce text via OCR fallback.")]
    public void ExtractText_ScannedEnglishPdf_ReturnsOcrText()
    {
        using var engine = new TesseractOcrEngine();
        var parser = new OcrPdfParser(engine);

        var text = parser.ExtractText(ReadBytes("scanned_english.pdf"));

        Assert.IsTrue(text.Contains("## Page 1"),
            "Should have page header.");
        Assert.IsTrue(text.Contains("Impedance Spectroscopy", StringComparison.OrdinalIgnoreCase),
            "Should contain OCR'd title.");
        Assert.IsTrue(text.Contains("SOH", StringComparison.Ordinal),
            "Should contain 'SOH'.");
    }

    [TestMethod]
    [Description("Scanned Korean PDF should produce text via OCR fallback with Korean post-processing.")]
    public void ExtractText_ScannedKoreanPdf_ReturnsOcrText()
    {
        using var engine = new TesseractOcrEngine();
        var parser = new OcrPdfParser(engine);

        var text = parser.ExtractText(ReadBytes("scanned_korean.pdf"));

        Assert.IsTrue(text.Contains("## Page 1"),
            "Should have page header.");
        Assert.IsTrue(text.Contains("배터리", StringComparison.Ordinal),
            "Should contain '배터리'.");
        Assert.IsTrue(text.Contains("임피던스", StringComparison.Ordinal),
            "Should contain '임피던스'.");
    }

    [TestMethod]
    [Description("Scanned mixed PDF should produce text with both languages.")]
    public void ExtractText_ScannedMixedPdf_ReturnsOcrText()
    {
        using var engine = new TesseractOcrEngine();
        var parser = new OcrPdfParser(engine);

        var text = parser.ExtractText(ReadBytes("scanned_mixed.pdf"));

        Assert.IsTrue(text.Contains("EIS", StringComparison.Ordinal),
            "Should contain English 'EIS'.");
        Assert.IsTrue(text.Contains("배터리", StringComparison.Ordinal),
            "Should contain Korean '배터리'.");
    }

    #endregion

    #region Text PDF — no OCR needed

    [TestMethod]
    [Description("Text PDF must not trigger OCR — output should match core PdfParser.")]
    public void ExtractText_TextPdf_MatchesCoreParserOutput()
    {
        var pdfPath = Path.Combine(TestDataDir, "sample_math_equations.pdf");
        if (!File.Exists(pdfPath))
        {
            Assert.Inconclusive("sample_math_equations.pdf not found.");
            return;
        }

        var data = File.ReadAllBytes(pdfPath);

        using var engine = new TesseractOcrEngine();
        var ocrParser = new OcrPdfParser(engine);
        var coreParser = new PdfParser();

        var coreText = coreParser.ExtractText(data);
        var ocrText = ocrParser.ExtractText(data);

        Assert.AreEqual(coreText, ocrText,
            "Text PDF should produce identical output via core and OCR-augmented parser.");
    }

    [TestMethod]
    [Description("OCR engine must not be called when PdfPig extracts meaningful text.")]
    public void ExtractText_TextPdf_DoesNotCallOcr()
    {
        var pdfPath = Path.Combine(TestDataDir, "sample_math_equations.pdf");
        if (!File.Exists(pdfPath))
        {
            Assert.Inconclusive("sample_math_equations.pdf not found.");
            return;
        }

        var data = File.ReadAllBytes(pdfPath);
        var mockEngine = new MockOcrEngine();
        var parser = new OcrPdfParser(mockEngine);

        parser.ExtractText(data);

        Assert.AreEqual(0, mockEngine.CallCount,
            "OCR should not be called for PDFs with extractable text.");
    }

    #endregion

    #region Scanned PDF via core PdfParser (no OCR) — headers only

    [TestMethod]
    [Description("Scanned PDF via core PdfParser (no OCR) should return only page headers.")]
    public void CorePdfParser_ScannedPdf_ReturnsOnlyHeaders()
    {
        var parser = new PdfParser();
        var text = parser.ExtractText(ReadBytes("scanned_english.pdf"));

        Assert.IsTrue(text.Contains("## Page 1"),
            "Should have page header.");

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nonHeaderLines = lines.Where(l => !l.StartsWith("## Page")).ToList();
        Assert.AreEqual(0, nonHeaderLines.Count,
            "Scanned PDF via core parser should have no text content besides page headers.");
    }

    #endregion

    #region Factory registration

    [TestMethod]
    [Description("AddOcrSupport must register an OcrPdfParser with the factory.")]
    public void AddOcrSupport_RegistersParser()
    {
        using var engine = DocumentParserFactoryOcrExtensions.AddOcrSupport();

        var parser = DocumentParserFactory.GetParser(".pdf");
        Assert.IsInstanceOfType<OcrPdfParser>(parser,
            "Factory should resolve .pdf to OcrPdfParser after AddOcrSupport().");

        // Restore core default for other tests.
        DocumentParserFactory.Register(new PdfParser());
    }

    [TestMethod]
    [Description("AddOcrSupport(IOcrEngine) allows caller to inject a custom engine.")]
    public void AddOcrSupport_WithCustomEngine_Registers()
    {
        var mock = new MockOcrEngine();
        DocumentParserFactoryOcrExtensions.AddOcrSupport(mock);
        try
        {
            var parser = DocumentParserFactory.GetParser(".pdf");
            Assert.IsInstanceOfType<OcrPdfParser>(parser);
        }
        finally
        {
            DocumentParserFactory.Register(new PdfParser());
        }
    }

    #endregion

    #region Helper

    private sealed class MockOcrEngine : IOcrEngine
    {
        public int CallCount { get; private set; }

        public Task<string> RecognizeAsync(byte[] imageBytes)
        {
            CallCount++;
            return Task.FromResult("mock-ocr-text");
        }
    }

    #endregion
}
