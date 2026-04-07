namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class MetadataFormatterTests
{
    [TestMethod]
    public void AllNull_ReturnsEmpty()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter();
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void TitleOnly_ReturnsYamlWithTitle()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter(title: "Test Document");
        Assert.IsTrue(result.StartsWith("---\n"));
        Assert.IsTrue(result.Contains("title: Test Document"));
        Assert.IsTrue(result.EndsWith("---\n\n"));
    }

    [TestMethod]
    public void AllFields_ReturnsCompleteYaml()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter(
            title: "Report",
            author: "Alice",
            created: new DateTime(2026, 4, 1),
            modified: new DateTime(2026, 4, 7),
            subject: "Sales",
            keywords: "Q1, revenue",
            description: "Quarterly report");

        Assert.IsTrue(result.Contains("title: Report"));
        Assert.IsTrue(result.Contains("author: Alice"));
        Assert.IsTrue(result.Contains("created: 2026-04-01"));
        Assert.IsTrue(result.Contains("modified: 2026-04-07"));
        Assert.IsTrue(result.Contains("subject: Sales"));
        Assert.IsTrue(result.Contains("keywords: \"Q1, revenue\""));
        Assert.IsTrue(result.Contains("description: Quarterly report"));
    }

    [TestMethod]
    public void YamlSpecialChars_AreQuoted()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter(title: "Title: with colon");
        Assert.IsTrue(result.Contains("title: \"Title: with colon\""),
            $"Colons should be quoted. Actual:\n{result}");
    }

    [TestMethod]
    public void EmptyStrings_AreOmitted()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter(title: "", author: "  ");
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void DateOnly_NoOtherFields()
    {
        var result = MetadataFormatter.FormatYamlFrontMatter(created: new DateTime(2026, 1, 15));
        Assert.IsTrue(result.Contains("created: 2026-01-15"));
        Assert.IsFalse(result.Contains("title:"));
    }
}
