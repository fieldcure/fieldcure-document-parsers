namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class PptxParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly PptxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsPptx()
    {
        CollectionAssert.Contains(_parser.SupportedExtensions.ToList(), ".pptx");
    }

    #endregion

    #region simple_text.pptx — three slides

    [TestMethod]
    public void SimpleText_ReturnsNonEmptyText()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsFalse(string.IsNullOrWhiteSpace(text), "Expected non-empty text.");
    }

    [TestMethod]
    public void SimpleText_ContainsTitle()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("DocumentParsers Test"), $"Expected 'DocumentParsers Test' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsFieldCure()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("FieldCure"), $"Expected 'FieldCure' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsSupportedFormats()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("Supported Formats"), $"Expected 'Supported Formats' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsHwpx()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("HWPX"), $"Expected 'HWPX' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsPdf()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("PDF"), $"Expected 'PDF' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsDocxParser()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("DocxParser"), $"Expected 'DocxParser' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsOpenXmlSdk()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("OpenXml SDK"), $"Expected 'OpenXml SDK' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsBclOnly()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("BCL only"), $"Expected 'BCL only' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsSlideHeaders()
    {
        var text = ReadText("simple_text.pptx");
        Assert.IsTrue(text.Contains("## Slide 1"), $"Expected '## Slide 1' in output.\n{text}");
        Assert.IsTrue(text.Contains("## Slide 2"), $"Expected '## Slide 2' in output.\n{text}");
        Assert.IsTrue(text.Contains("## Slide 3"), $"Expected '## Slide 3' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_Slide3ContainsMarkdownTable()
    {
        var text = ReadText("simple_text.pptx");
        // Slide 3 has a table — output should contain pipe characters
        var slide3Start = text.IndexOf("## Slide 3");
        Assert.IsTrue(slide3Start >= 0, "Expected '## Slide 3' header.");
        var slide3Text = text.Substring(slide3Start);
        Assert.IsTrue(slide3Text.Contains("|"), $"Expected markdown table in slide 3.\n{slide3Text}");
    }

    #endregion
}
