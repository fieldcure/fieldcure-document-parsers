namespace FieldCure.DocumentParsers.Imaging.Tests;

[TestClass]
public class PdfImageRendererTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly PdfImageRenderer _renderer = new();

    private byte[] ReadBytes(string filename)
        => File.ReadAllBytes(Path.Combine(TestDataDir, filename));

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsPdf()
    {
        CollectionAssert.Contains(_renderer.SupportedExtensions.ToList(), ".pdf");
    }

    #endregion

    #region Image extraction

    [TestMethod]
    [Description("ExtractImages must return one image per PDF page.")]
    public void ExtractImages_ReturnsOneImagePerPage()
    {
        var images = _renderer.ExtractImages(ReadBytes("sample_math_equations.pdf"));

        Assert.AreEqual(2, images.Count,
            $"Expected 2 images (one per page), got {images.Count}.");
    }

    [TestMethod]
    [Description("Each rendered image must have non-empty PNG bytes.")]
    public void ExtractImages_ImagesAreNonEmpty()
    {
        var images = _renderer.ExtractImages(ReadBytes("sample_math_equations.pdf"));

        foreach (var img in images)
        {
            Assert.IsTrue(img.Data.Length > 0,
                $"Image for '{img.Label}' must have non-zero byte length.");
        }
    }

    [TestMethod]
    [Description("Image labels must follow the 'Page N' convention.")]
    public void ExtractImages_LabelsFollowPageConvention()
    {
        var images = _renderer.ExtractImages(ReadBytes("sample_math_equations.pdf"));

        Assert.AreEqual("Page 1", images[0].Label, "First image label should be 'Page 1'.");
        Assert.AreEqual("Page 2", images[1].Label, "Second image label should be 'Page 2'.");
    }

    [TestMethod]
    [Description("Page indices must be zero-based and match the image order.")]
    public void ExtractImages_PageIndicesAreZeroBased()
    {
        var images = _renderer.ExtractImages(ReadBytes("sample_math_equations.pdf"));

        Assert.AreEqual(0, images[0].Index, "First image index should be 0.");
        Assert.AreEqual(1, images[1].Index, "Second image index should be 1.");
    }

    [TestMethod]
    [Description("Higher DPI must produce a larger image than lower DPI.")]
    public void ExtractImages_HigherDpiProducesLargerImage()
    {
        var data = ReadBytes("sample_math_equations.pdf");
        var lo = _renderer.ExtractImages(data, dpi: 72);
        var hi = _renderer.ExtractImages(data, dpi: 150);

        Assert.IsTrue(hi[0].Data.Length > lo[0].Data.Length,
            "150 dpi image should be larger (more bytes) than 72 dpi image.");
    }

    #endregion

    #region Text extraction — delegated to core PdfParser

    [TestMethod]
    [Description("ExtractText must delegate to core PdfParser and emit page headers.")]
    public void ExtractText_DelegatesToCoreParser()
    {
        var text = _renderer.ExtractText(ReadBytes("sample_math_equations.pdf"));

        Assert.IsTrue(text.Contains("## Page 1"), "Expected '## Page 1' header.");
        Assert.IsTrue(text.Contains("## Page 2"), "Expected '## Page 2' header.");
        Assert.IsTrue(text.Contains("The Gradient"),
            "Expected section title 'The Gradient'.");
    }

    [TestMethod]
    [Description("Imaging ExtractText output must equal the core PdfParser output (no regression).")]
    public void ExtractText_MatchesCoreParserOutput()
    {
        var data = ReadBytes("sample_math_equations.pdf");

        var coreText = new PdfParser().ExtractText(data);
        var imagingText = _renderer.ExtractText(data);

        Assert.AreEqual(coreText, imagingText,
            "Imaging renderer must not alter text extraction behavior.");
    }

    #endregion

    #region Factory registration

    [TestMethod]
    [Description("AddImagingSupport upgrades factory's .pdf entry to IMediaDocumentParser.")]
    public void AddImagingSupport_UpgradesFactoryEntry()
    {
        DocumentParserFactoryImagingExtensions.AddImagingSupport();
        try
        {
            var parser = DocumentParserFactory.GetParser(".pdf");
            Assert.IsInstanceOfType<IMediaDocumentParser>(parser,
                "After AddImagingSupport, .pdf should resolve to IMediaDocumentParser.");
        }
        finally
        {
            // Restore core default so unrelated tests are unaffected.
            DocumentParserFactory.Register(new PdfParser());
        }
    }

    #endregion
}
