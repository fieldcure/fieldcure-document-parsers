namespace FieldCure.DocumentParsers.Tests;

/// <summary>
/// Integration tests using real HWPX files created by Hancom Office.
/// </summary>
[TestClass]
public class HwpxRealFileTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly HwpxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region hwpx-with-footnotes.hwpx

    [TestMethod]
    public void WithFootnotes_ContainsNoteMarkers()
    {
        var text = ReadText("hwpx-with-footnotes.hwpx");
        Assert.IsTrue(text.Contains("[^"), $"Should contain note markers. Actual:\n{text}");
    }

    [TestMethod]
    public void WithFootnotes_ContainsNoteSections()
    {
        var text = ReadText("hwpx-with-footnotes.hwpx");
        // The file contains both footNote and endNote elements
        var hasFootnotes = text.Contains("## Footnotes");
        var hasEndnotes = text.Contains("## Endnotes");
        Assert.IsTrue(hasFootnotes || hasEndnotes,
            $"Should contain at least one notes section. Actual:\n{text}");
    }

    [TestMethod]
    public void WithFootnotes_NoteContentNotInBody()
    {
        var text = ReadText("hwpx-with-footnotes.hwpx");
        // Note content should NOT appear inline in body text
        var bodyLines = text.Split('\n')
            .TakeWhile(l => !l.StartsWith("## Footnotes") && !l.StartsWith("## Endnotes"))
            .ToList();
        var bodyText = string.Join("\n", bodyLines);
        Assert.IsFalse(bodyText.Contains("first footnote with detailed explanation"),
            $"Footnote content should not appear in body. Body:\n{bodyText}");
    }

    #endregion

    #region hwpx-with-comments.hwpx

    [TestMethod]
    public void WithComments_ContainsCommentBlockquote()
    {
        var text = ReadText("hwpx-with-comments.hwpx");
        Assert.IsTrue(text.Contains("> **[Comment"), $"Should contain comment blockquote. Actual:\n{text}");
    }

    [TestMethod]
    public void WithComments_ContainsAuthor()
    {
        var text = ReadText("hwpx-with-comments.hwpx");
        // Author should appear in comment label
        Assert.IsTrue(text.Contains("Comment —"), $"Should contain author in comment. Actual:\n{text}");
    }

    [TestMethod]
    public void WithComments_MemoContentNotInBody()
    {
        var text = ReadText("hwpx-with-comments.hwpx");
        // Memo content should only appear in blockquote, not as inline text
        var lines = text.Split('\n');
        var bodyLines = lines.Where(l => !l.StartsWith(">")).ToList();
        var bodyText = string.Join("\n", bodyLines);
        Assert.IsFalse(bodyText.Contains("double-check"),
            $"Memo content should not appear in body text. Body:\n{bodyText}");
    }

    #endregion

    #region hwpx-with-metadata.hwpx

    [TestMethod]
    public void WithMetadata_ContainsYamlFrontMatter()
    {
        var text = ReadText("hwpx-with-metadata.hwpx");
        Assert.IsTrue(text.StartsWith("---"), $"Should start with YAML front matter. Actual:\n{text}");
    }

    [TestMethod]
    public void WithMetadata_ContainsTitle()
    {
        var text = ReadText("hwpx-with-metadata.hwpx");
        Assert.IsTrue(text.Contains("title:"), $"Should contain title field. Actual:\n{text}");
    }

    #endregion

    #region hwpx-no-extras.hwpx

    [TestMethod]
    public void NoExtras_NoNoteSections()
    {
        var text = ReadText("hwpx-no-extras.hwpx");
        Assert.IsFalse(text.Contains("## Footnotes"), $"Should not contain Footnotes. Actual:\n{text}");
        Assert.IsFalse(text.Contains("## Endnotes"), $"Should not contain Endnotes. Actual:\n{text}");
    }

    [TestMethod]
    public void NoExtras_NoComments()
    {
        var text = ReadText("hwpx-no-extras.hwpx");
        Assert.IsFalse(text.Contains("[Comment"), $"Should not contain comments. Actual:\n{text}");
    }

    #endregion
}
