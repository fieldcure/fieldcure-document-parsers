namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class HtmlParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly HtmlParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsHtmlAndHtm()
    {
        var extensions = _parser.SupportedExtensions;
        CollectionAssert.Contains(extensions.ToList(), ".html");
        CollectionAssert.Contains(extensions.ToList(), ".htm");
    }

    #endregion

    #region test.html

    [TestMethod]
    public void ExtractText_ContainsHeading()
    {
        var text = ReadText("test.html");
        Assert.IsTrue(text.Contains("DocumentParsers Test"),
            $"Should contain heading text. Actual:\n{text}");
    }

    [TestMethod]
    public void ExtractText_ContainsBoldText()
    {
        var text = ReadText("test.html");
        Assert.IsTrue(text.Contains("bold"),
            $"Should contain bold text. Actual:\n{text}");
    }

    [TestMethod]
    public void ExtractText_ContainsTableContent()
    {
        var text = ReadText("test.html");
        Assert.IsTrue(text.Contains("Alpha"),
            $"Should contain table content. Actual:\n{text}");
    }

    [TestMethod]
    public void ExtractText_NoHtmlTags()
    {
        var text = ReadText("test.html");
        Assert.IsFalse(text.Contains("<div>"), $"Should not contain HTML tags. Actual:\n{text}");
        Assert.IsFalse(text.Contains("<p>"), $"Should not contain <p> tags. Actual:\n{text}");
        Assert.IsFalse(text.Contains("</article>"), $"Should not contain closing tags. Actual:\n{text}");
    }

    #endregion

    #region Factory registration

    [TestMethod]
    public void Factory_GetParser_Html_ReturnsHtmlParser()
    {
        var parser = DocumentParserFactory.GetParser(".html");
        Assert.IsNotNull(parser);
        Assert.IsInstanceOfType(parser, typeof(HtmlParser));
    }

    [TestMethod]
    public void Factory_GetParser_Htm_ReturnsHtmlParser()
    {
        var parser = DocumentParserFactory.GetParser(".htm");
        Assert.IsNotNull(parser);
        Assert.IsInstanceOfType(parser, typeof(HtmlParser));
    }

    [TestMethod]
    public void Factory_SupportedExtensions_ContainsHtml()
    {
        var extensions = DocumentParserFactory.SupportedExtensions.ToList();
        Assert.IsTrue(extensions.Contains(".html"), "Factory should support .html");
        Assert.IsTrue(extensions.Contains(".htm"), "Factory should support .htm");
    }

    #endregion

    #region Edge cases

    [TestMethod]
    public void ExtractText_PlainHtmlWithoutArticle()
    {
        var html = "<html><body><p>Simple content</p></body></html>";
        var data = System.Text.Encoding.UTF8.GetBytes(html);
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("Simple content"),
            $"Should extract text from simple HTML. Actual:\n{text}");
    }

    #endregion
}
