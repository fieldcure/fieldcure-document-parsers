namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class XlsxParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly XlsxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsXlsx()
    {
        CollectionAssert.Contains(_parser.SupportedExtensions.ToList(), ".xlsx");
    }

    #endregion

    #region simple_text.xlsx — two sheets with product data

    [TestMethod]
    public void SimpleText_ReturnsNonEmptyText()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsFalse(string.IsNullOrWhiteSpace(text), "Expected non-empty text.");
    }

    [TestMethod]
    public void SimpleText_ContainsProductName()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Widget A"), $"Expected 'Widget A' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsCategory()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Electronics"), $"Expected 'Electronics' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsPrice()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("29.99"), $"Expected '29.99' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsSheetCategory()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Products"), $"Expected 'Products' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsSummarySheet()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Total Products"), $"Expected 'Total Products' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsAveragePrice()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Average Price"), $"Expected 'Average Price' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsSummaryKeyword()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("Summary"), $"Expected 'Summary' in output.\n{text}");
    }

    [TestMethod]
    public void SimpleText_ContainsMarkdownTable()
    {
        var text = ReadText("simple_text.xlsx");
        Assert.IsTrue(text.Contains("|"), $"Expected markdown table pipes in output.\n{text}");
    }

    #endregion
}
