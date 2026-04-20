namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class DocumentParserFactoryTests
{
    [TestMethod]
    [DataRow(".docx")]
    [DataRow(".DOCX")]
    public void GetParser_Docx_ReturnsDocxParser(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsInstanceOfType<DocxParser>(parser);
    }

    [TestMethod]
    [DataRow(".hwpx")]
    [DataRow(".HWPX")]
    public void GetParser_Hwpx_ReturnsHwpxParser(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsInstanceOfType<HwpxParser>(parser);
    }

    [TestMethod]
    [DataRow(".xlsx")]
    [DataRow(".XLSX")]
    public void GetParser_Xlsx_ReturnsXlsxParser(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsInstanceOfType<XlsxParser>(parser);
    }

    [TestMethod]
    [DataRow(".pptx")]
    [DataRow(".PPTX")]
    public void GetParser_Pptx_ReturnsPptxParser(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsInstanceOfType<PptxParser>(parser);
    }

    [TestMethod]
    [DataRow(".pdf")]
    [DataRow(".PDF")]
    public void GetParser_Pdf_ReturnsPdfParser(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsInstanceOfType<PdfParser>(parser);
    }

    [TestMethod]
    [DataRow(".txt")]
    [DataRow(".xyz")]
    [DataRow("")]
    public void GetParser_Unsupported_ReturnsNull(string ext)
    {
        var parser = DocumentParserFactory.GetParser(ext);
        Assert.IsNull(parser);
    }

    [TestMethod]
    public void SupportedExtensions_ContainsExpected()
    {
        var extensions = DocumentParserFactory.SupportedExtensions.ToList();
        CollectionAssert.Contains(extensions, ".docx");
        CollectionAssert.Contains(extensions, ".hwpx");
        CollectionAssert.Contains(extensions, ".xlsx");
        CollectionAssert.Contains(extensions, ".pptx");
        CollectionAssert.Contains(extensions, ".pdf");
    }

    [TestMethod]
    public void Register_OverridesExistingParser()
    {
        // Register replaces any existing parser for that extension.
        var custom = new CustomPdfParser();
        DocumentParserFactory.Register(custom);
        try
        {
            Assert.AreSame(custom, DocumentParserFactory.GetParser(".pdf"));
        }
        finally
        {
            // Restore default so subsequent tests are unaffected.
            DocumentParserFactory.Register(new PdfParser());
        }
    }

    private sealed class CustomPdfParser : IDocumentParser
    {
        public IReadOnlyList<string> SupportedExtensions => [".pdf"];
        public string ExtractText(byte[] data) => string.Empty;
    }
}
