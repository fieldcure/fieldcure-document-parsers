namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class PdfParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly PdfParser _parser = new();

    private byte[] ReadBytes(string filename)
        => File.ReadAllBytes(Path.Combine(TestDataDir, filename));

    private string ReadText(string filename)
        => _parser.ExtractText(ReadBytes(filename));

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsPdf()
    {
        CollectionAssert.Contains(_parser.SupportedExtensions.ToList(), ".pdf");
    }

    #endregion

    #region sample_math_equations.pdf — text extraction

    [TestMethod]
    [Description("ExtractText must return non-empty text from a non-trivial PDF.")]
    public void ExtractText_ReturnsNonEmptyText()
    {
        var text = ReadText("sample_math_equations.pdf");

        Assert.IsFalse(string.IsNullOrWhiteSpace(text),
            "ExtractText should return non-empty text.");
    }

    [TestMethod]
    [Description("Each page must be preceded by a '## Page N' header.")]
    public void ExtractText_InsertsPageHeaders()
    {
        var text = ReadText("sample_math_equations.pdf");

        Assert.IsTrue(text.Contains("## Page 1"),
            "Expected '## Page 1' header.");
        Assert.IsTrue(text.Contains("## Page 2"),
            "Expected '## Page 2' header for second page.");
    }

    [TestMethod]
    [Description("Page headers must appear in ascending order.")]
    public void ExtractText_PageHeadersAreOrdered()
    {
        var text = ReadText("sample_math_equations.pdf");

        var pos1 = text.IndexOf("## Page 1", StringComparison.Ordinal);
        var pos2 = text.IndexOf("## Page 2", StringComparison.Ordinal);

        Assert.IsTrue(pos1 >= 0, "'## Page 1' not found.");
        Assert.IsTrue(pos2 >= 0, "'## Page 2' not found.");
        Assert.IsTrue(pos1 < pos2, "'## Page 1' should precede '## Page 2'.");
    }

    [TestMethod]
    [Description("Plain-text content from the document body must be present.")]
    public void ExtractText_ContainsBodyText()
    {
        var text = ReadText("sample_math_equations.pdf");

        Assert.IsTrue(text.Contains("The Gradient"),
            "Expected section title 'The Gradient'.");
        Assert.IsTrue(text.Contains("The impedance function"),
            "Expected prose 'The impedance function'.");
        Assert.IsTrue(text.Contains("The DRT representation"),
            "Expected prose 'The DRT representation'.");
        Assert.IsTrue(text.Contains("The Loewner matrix"),
            "Expected prose 'The Loewner matrix'.");
    }

    [TestMethod]
    [Description("Page 1 content must appear before Page 2 content.")]
    public void ExtractText_ContentOrderMatchesPages()
    {
        var text = ReadText("sample_math_equations.pdf");

        // "The Gradient" is on page 1; "Loewner" equation continuation is on page 2.
        var gradPos   = text.IndexOf("The Gradient",   StringComparison.Ordinal);
        var page2Pos  = text.IndexOf("## Page 2",      StringComparison.Ordinal);

        Assert.IsTrue(gradPos  >= 0, "'The Gradient' not found.");
        Assert.IsTrue(page2Pos >= 0, "'## Page 2' not found.");
        Assert.IsTrue(gradPos < page2Pos,
            "'The Gradient' should appear before '## Page 2' marker.");
    }

    #endregion
}
