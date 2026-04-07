namespace FieldCure.DocumentParsers.Tests;

/// <summary>
/// Integration tests using real DOCX files created by python-docx / Word.
/// </summary>
[TestClass]
public class DocxRealFileTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly DocxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region docx-with-footnotes.docx

    [TestMethod]
    public void WithFootnotes_ContainsFootnoteMarkers()
    {
        var text = ReadText("docx-with-footnotes.docx");
        Assert.IsTrue(text.Contains("[^"), $"Should contain footnote markers. Actual:\n{text}");
    }

    [TestMethod]
    public void WithFootnotes_ContainsFootnotesSection()
    {
        var text = ReadText("docx-with-footnotes.docx");
        Assert.IsTrue(text.Contains("## Footnotes"), $"Should contain Footnotes section. Actual:\n{text}");
    }

    [TestMethod]
    public void WithFootnotes_ContainsEndnotesSection()
    {
        var text = ReadText("docx-with-footnotes.docx");
        Assert.IsTrue(text.Contains("## Endnotes"), $"Should contain Endnotes section. Actual:\n{text}");
    }

    [TestMethod]
    public void WithFootnotes_ContainsMetadata()
    {
        var text = ReadText("docx-with-footnotes.docx");
        Assert.IsTrue(text.Contains("title: Footnote Test Document"), $"Should contain title. Actual:\n{text}");
    }

    #endregion

    #region docx-with-comments.docx

    [TestMethod]
    public void WithComments_ContainsCommentBlockquote()
    {
        var text = ReadText("docx-with-comments.docx");
        Assert.IsTrue(text.Contains("> **[Comment"), $"Should contain comment blockquote. Actual:\n{text}");
    }

    [TestMethod]
    public void WithComments_ContainsAuthorNames()
    {
        var text = ReadText("docx-with-comments.docx");
        Assert.IsTrue(text.Contains("Alice Kim"), $"Should contain first author. Actual:\n{text}");
        Assert.IsTrue(text.Contains("Bob Park"), $"Should contain second author. Actual:\n{text}");
    }

    #endregion

    #region docx-with-headers.docx

    [TestMethod]
    public void WithHeaders_ContainsHeaderBlockquote()
    {
        var text = ReadText("docx-with-headers.docx");
        Assert.IsTrue(text.Contains("> **[Header]"), $"Should contain header. Actual:\n{text}");
    }

    [TestMethod]
    public void WithHeaders_ContainsFooterBlockquote()
    {
        var text = ReadText("docx-with-headers.docx");
        Assert.IsTrue(text.Contains("> **[Footer]"), $"Should contain footer. Actual:\n{text}");
    }

    [TestMethod]
    public void WithHeaders_ContainsFirstPageHeader()
    {
        var text = ReadText("docx-with-headers.docx");
        Assert.IsTrue(text.Contains("[Header — First Page]"), $"Should contain first page header. Actual:\n{text}");
    }

    #endregion

    #region docx-no-extras.docx

    [TestMethod]
    public void NoExtras_NoFootnotesSection()
    {
        var text = ReadText("docx-no-extras.docx");
        Assert.IsFalse(text.Contains("## Footnotes"), $"Should not contain Footnotes section. Actual:\n{text}");
        Assert.IsFalse(text.Contains("## Endnotes"), $"Should not contain Endnotes section. Actual:\n{text}");
    }

    [TestMethod]
    public void NoExtras_NoComments()
    {
        var text = ReadText("docx-no-extras.docx");
        Assert.IsFalse(text.Contains("[Comment"), $"Should not contain comments. Actual:\n{text}");
    }

    #endregion
}
