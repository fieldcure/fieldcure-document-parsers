using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class DocxParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly DocxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsDocx()
    {
        CollectionAssert.Contains(_parser.SupportedExtensions.ToList(), ".docx");
    }

    #endregion

    #region simple_text.docx — basic paragraphs

    [TestMethod]
    public void SimpleText_ExtractsThreeLines()
    {
        var text = ReadText("simple_text.docx");
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.IsTrue(lines.Length >= 3, $"Expected at least 3 lines, got {lines.Length}.");
    }

    #endregion

    #region multiple_runs.docx — bold/italic runs merged into one line

    [TestMethod]
    public void MultipleRuns_MergesRunsIntoSingleLine()
    {
        var text = ReadText("multiple_runs.docx");

        Assert.IsTrue(
            text.Contains("This paragraph contains bold text and italic text mixed together."),
            $"Runs should be merged. Actual:\n{text}");
    }

    #endregion

    #region with_table.docx — text + markdown table + text

    [TestMethod]
    public void WithTable_ContainsMarkdownTable()
    {
        var text = ReadText("with_table.docx");

        Assert.IsTrue(text.Contains('|'), "Table should be converted to markdown with pipe characters.");
        Assert.IsTrue(text.Contains("---"), "Table should have markdown separator row.");
    }

    [TestMethod]
    public void WithTable_Has3x4Table()
    {
        var text = ReadText("with_table.docx");

        // Count separator row dashes to verify column count (3 columns → 2 inner separators)
        var lines = text.Split('\n');
        var separatorLine = lines.FirstOrDefault(l => l.Contains("---") && l.Contains('|'));

        Assert.IsNotNull(separatorLine, "Should have a markdown separator row.");

        // 3 columns means | --- | --- | --- | → 4 pipe chars
        var pipeCount = separatorLine.Count(c => c == '|');
        Assert.IsTrue(pipeCount >= 4, $"Expected 3+ columns (4+ pipes), got {pipeCount} pipes.");
    }

    #endregion

    #region multiple_tables.docx — two tables

    [TestMethod]
    public void MultipleTables_ExtractsBothTables()
    {
        var text = ReadText("multiple_tables.docx");

        var lines = text.Split('\n');
        var separatorRows = lines.Where(l => l.Contains("---") && l.Contains('|')).ToList();

        Assert.IsTrue(separatorRows.Count >= 2,
            $"Expected at least 2 separator rows for 2 tables, got {separatorRows.Count}.");
    }

    #endregion

    #region empty.docx — empty document

    [TestMethod]
    public void EmptyDocument_ReturnsEmptyOrWhitespace()
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, "empty.docx"));
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeMetadata = false });
        Assert.IsTrue(string.IsNullOrWhiteSpace(text), $"Expected empty, got: '{text}'");
    }

    #endregion

    #region pipe_in_table.docx — pipe character inside cell

    [TestMethod]
    public void PipeInTable_EscapesPipeInCellContent()
    {
        var text = ReadText("pipe_in_table.docx");

        // The pipe character inside a cell should be escaped as \|
        Assert.IsTrue(text.Contains("\\|"),
            $"Pipe in cell content should be escaped. Actual:\n{text}");
    }

    #endregion

    #region Heading detection

    private static byte[] CreateDocxWithHeadings()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("Chapter 1"))),
                new Paragraph(new Run(new Text("Body paragraph."))),
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading2" }),
                    new Run(new Text("Section 1.1"))),
                new Paragraph(new Run(new Text("More body text."))),
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading3" }),
                    new Run(new Text("Subsection 1.1.1")))
            ));
        }
        return ms.ToArray();
    }

    [TestMethod]
    public void WithHeadings_ContainsMarkdownHeadings()
    {
        var text = _parser.ExtractText(CreateDocxWithHeadings());

        Assert.IsTrue(text.Contains("# Chapter 1"), $"Expected '# Chapter 1'. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Section 1.1"), $"Expected '## Section 1.1'. Actual:\n{text}");
        Assert.IsTrue(text.Contains("### Subsection 1.1.1"), $"Expected '### Subsection 1.1.1'. Actual:\n{text}");
    }

    [TestMethod]
    public void WithHeadings_BodyTextHasNoHashPrefix()
    {
        var text = _parser.ExtractText(CreateDocxWithHeadings());
        var lines = text.Split('\n');

        var bodyLine = lines.FirstOrDefault(l => l.Contains("Body paragraph."));
        Assert.IsNotNull(bodyLine, "Body paragraph should be present.");
        Assert.IsFalse(bodyLine.TrimStart().StartsWith('#'), "Body text should not have # prefix.");
    }

    [TestMethod]
    public void WithHeadings_OutlineLevelFallback()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new ParagraphProperties(new OutlineLevel { Val = 0 }),
                    new Run(new Text("Outline Heading 1"))),
                new Paragraph(new Run(new Text("Body.")))
            ));
        }
        var data = ms.ToArray();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("# Outline Heading 1"),
            $"OutlineLevel 0 should produce '# '. Actual:\n{text}");
    }

    #endregion
}
