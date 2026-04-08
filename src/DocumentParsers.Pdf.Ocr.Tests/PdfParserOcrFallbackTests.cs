namespace FieldCure.DocumentParsers.Pdf.Ocr.Tests;

[TestClass]
public class PdfParserOcrFallbackTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private static readonly string SiblingPdfTestDataDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "DocumentParsers.Pdf.Tests", "TestData");

    private static byte[] ReadBytes(string filename)
        => File.ReadAllBytes(Path.Combine(TestDataDir, filename));

    #region Scanned PDF — OCR fallback triggers

    [TestMethod]
    [Description("Scanned English PDF should produce text via OCR fallback.")]
    public void ExtractText_ScannedEnglishPdf_ReturnsOcrText()
    {
        using var engine = new TesseractOcrEngine();
        var parser = new PdfParser(engine);

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
        var parser = new PdfParser(engine);

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
        var parser = new PdfParser(engine);

        var text = parser.ExtractText(ReadBytes("scanned_mixed.pdf"));

        Assert.IsTrue(text.Contains("EIS", StringComparison.Ordinal),
            "Should contain English 'EIS'.");
        Assert.IsTrue(text.Contains("배터리", StringComparison.Ordinal),
            "Should contain Korean '배터리'.");
    }

    #endregion

    #region Text PDF — no OCR needed

    [TestMethod]
    [Description("Text PDF must not trigger OCR — output should match non-OCR parser.")]
    public void ExtractText_TextPdf_MatchesNonOcrOutput()
    {
        var pdfPath = Path.Combine(SiblingPdfTestDataDir, "sample_math_equations.pdf");
        if (!File.Exists(pdfPath))
        {
            Assert.Inconclusive("sample_math_equations.pdf not found.");
            return;
        }

        var data = File.ReadAllBytes(pdfPath);

        var baseParser = new PdfParser();
        using var engine = new TesseractOcrEngine();
        var ocrParser = new PdfParser(engine);

        var baseText = baseParser.ExtractText(data);
        var ocrText = ocrParser.ExtractText(data);

        Assert.AreEqual(baseText, ocrText,
            "Text PDF should produce identical output with or without OCR engine.");
    }

    [TestMethod]
    [Description("OCR engine must not be called when PdfPig extracts meaningful text.")]
    public void ExtractText_TextPdf_DoesNotCallOcr()
    {
        var pdfPath = Path.Combine(SiblingPdfTestDataDir, "sample_math_equations.pdf");
        if (!File.Exists(pdfPath))
        {
            Assert.Inconclusive("sample_math_equations.pdf not found.");
            return;
        }

        var data = File.ReadAllBytes(pdfPath);
        var mockEngine = new MockOcrEngine();
        var parser = new PdfParser(mockEngine);

        parser.ExtractText(data);

        Assert.AreEqual(0, mockEngine.CallCount,
            "OCR should not be called for PDFs with extractable text.");
    }

    #endregion

    #region Scanned PDF without OCR engine — empty text

    [TestMethod]
    [Description("Scanned PDF without OCR engine should return only page headers.")]
    public void ExtractText_ScannedPdf_WithoutOcrEngine_ReturnsOnlyHeaders()
    {
        var parser = new PdfParser();
        var text = parser.ExtractText(ReadBytes("scanned_english.pdf"));

        Assert.IsTrue(text.Contains("## Page 1"),
            "Should have page header.");

        // Without OCR, scanned PDF should yield very little text
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nonHeaderLines = lines.Where(l => !l.StartsWith("## Page")).ToList();
        Assert.AreEqual(0, nonHeaderLines.Count,
            "Scanned PDF without OCR should have no text content besides page headers.");
    }

    #endregion

    #region Factory registration

    [TestMethod]
    [Description("AddPdfOcrSupport must register a PDF parser with the factory.")]
    public void AddPdfOcrSupport_RegistersParser()
    {
        using var engine = DocumentParserFactoryOcrExtensions.AddPdfOcrSupport();

        var parser = DocumentParserFactory.GetParser(".pdf");
        Assert.IsNotNull(parser, "Factory should resolve .pdf after AddPdfOcrSupport().");

        // Clean up: re-register without OCR to avoid side effects on other tests
        DocumentParserFactoryExtensions.AddPdfSupport();
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
