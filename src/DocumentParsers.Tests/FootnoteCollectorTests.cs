namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class FootnoteCollectorTests
{
    [TestMethod]
    public void NoNotes_RenderAll_ReturnsEmpty()
    {
        var collector = new FootnoteCollector();
        Assert.AreEqual("", collector.RenderAll());
    }

    [TestMethod]
    public void AddFootnote_ReturnsMarker()
    {
        var collector = new FootnoteCollector();
        var marker = collector.AddFootnote(1, "First footnote.");
        Assert.AreEqual("[^1]", marker);
    }

    [TestMethod]
    public void AddEndnote_ReturnsMarkerWithPrefix()
    {
        var collector = new FootnoteCollector();
        var marker = collector.AddEndnote(1, "First endnote.");
        Assert.AreEqual("[^en1]", marker);
    }

    [TestMethod]
    public void RenderAll_FootnotesBeforeEndnotes()
    {
        var collector = new FootnoteCollector();
        collector.AddFootnote(1, "Footnote 1");
        collector.AddEndnote(1, "Endnote 1");

        var result = collector.RenderAll();

        Assert.IsTrue(result.Contains("## Footnotes"));
        Assert.IsTrue(result.Contains("## Endnotes"));
        Assert.IsTrue(result.IndexOf("## Footnotes") < result.IndexOf("## Endnotes"),
            "Footnotes section should come before Endnotes section.");
    }

    [TestMethod]
    public void RenderAll_OnlyFootnotes_NoEndnotesSection()
    {
        var collector = new FootnoteCollector();
        collector.AddFootnote(2, "Some note.");

        var result = collector.RenderAll();

        Assert.IsTrue(result.Contains("## Footnotes"));
        Assert.IsFalse(result.Contains("## Endnotes"));
        Assert.IsTrue(result.Contains("[^2]: Some note."));
    }

    [TestMethod]
    public void RenderAll_OnlyEndnotes_NoFootnotesSection()
    {
        var collector = new FootnoteCollector();
        collector.AddEndnote(3, "End note text.");

        var result = collector.RenderAll();

        Assert.IsFalse(result.Contains("## Footnotes"));
        Assert.IsTrue(result.Contains("## Endnotes"));
        Assert.IsTrue(result.Contains("[^en3]: End note text."));
    }

    [TestMethod]
    public void MixedFootnotesAndEndnotes_AllRendered()
    {
        var collector = new FootnoteCollector();
        collector.AddFootnote(1, "FN1");
        collector.AddFootnote(2, "FN2");
        collector.AddEndnote(1, "EN1");

        var result = collector.RenderAll();

        Assert.IsTrue(result.Contains("[^1]: FN1"));
        Assert.IsTrue(result.Contains("[^2]: FN2"));
        Assert.IsTrue(result.Contains("[^en1]: EN1"));
    }
}
