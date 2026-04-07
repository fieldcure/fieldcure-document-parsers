using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class DocxEnhancedTests
{
    private readonly DocxParser _parser = new();

    #region Metadata

    [TestMethod]
    public void Metadata_YamlFrontMatter()
    {
        var data = CreateDocxWithMetadata("Test Title", "Test Author");
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.StartsWith("---\n"), $"Should start with YAML front matter. Actual:\n{text}");
        Assert.IsTrue(text.Contains("title: Test Title"), $"Should contain title. Actual:\n{text}");
        Assert.IsTrue(text.Contains("author: Test Author"), $"Should contain author. Actual:\n{text}");
    }

    [TestMethod]
    public void Metadata_Excluded_WhenOptionDisabled()
    {
        var data = CreateDocxWithMetadata("Test Title", "Test Author");
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeMetadata = false });

        Assert.IsFalse(text.Contains("---"), $"Should not contain YAML front matter. Actual:\n{text}");
        Assert.IsFalse(text.Contains("title:"), $"Should not contain metadata. Actual:\n{text}");
    }

    private static byte[] CreateDocxWithMetadata(string title, string author)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            doc.PackageProperties.Title = title;
            doc.PackageProperties.Creator = author;
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Body text.")))));
        }
        return ms.ToArray();
    }

    #endregion

    #region Footnotes

    [TestMethod]
    public void Footnotes_InlineAndDefinition()
    {
        var data = CreateDocxWithFootnotes();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("[^2]"), $"Should contain inline footnote reference. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Footnotes"), $"Should contain Footnotes section. Actual:\n{text}");
        Assert.IsTrue(text.Contains("[^2]: This is a footnote."), $"Should contain footnote definition. Actual:\n{text}");
    }

    [TestMethod]
    public void Footnotes_Excluded_WhenOptionDisabled()
    {
        var data = CreateDocxWithFootnotes();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeFootnotes = false });

        Assert.IsFalse(text.Contains("[^"), $"Should not contain footnote references. Actual:\n{text}");
        Assert.IsFalse(text.Contains("## Footnotes"), $"Should not contain Footnotes section. Actual:\n{text}");
    }

    [TestMethod]
    public void NoFootnotes_NoSection()
    {
        var data = CreateDocxWithMetadata("Title", "Author");
        var text = _parser.ExtractText(data);

        Assert.IsFalse(text.Contains("## Footnotes"), $"Should not contain Footnotes section. Actual:\n{text}");
    }

    private static byte[] CreateDocxWithFootnotes()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();

            // Add footnotes part
            var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
            footnotesPart.Footnotes = new Footnotes(
                // System separator (id=0) — must be skipped
                new Footnote(
                    new Paragraph(new Run(new Text("")))
                ) { Id = 0 },
                // System continuation separator (id=1) — must be skipped
                new Footnote(
                    new Paragraph(new Run(new Text("")))
                ) { Id = 1 },
                // Real footnote (id=2)
                new Footnote(
                    new Paragraph(new Run(new Text("This is a footnote.")))
                ) { Id = 2 }
            );

            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new Run(new Text("Text with footnote")),
                    new Run(new FootnoteReference { Id = 2 })
                ),
                new Paragraph(new Run(new Text("More text.")))
            ));
        }
        return ms.ToArray();
    }

    #endregion

    #region Endnotes

    [TestMethod]
    public void Endnotes_InlineAndDefinition()
    {
        var data = CreateDocxWithEndnotes();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("[^en2]"), $"Should contain inline endnote reference. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Endnotes"), $"Should contain Endnotes section. Actual:\n{text}");
        Assert.IsTrue(text.Contains("[^en2]: This is an endnote."), $"Should contain endnote definition. Actual:\n{text}");
    }

    private static byte[] CreateDocxWithEndnotes()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();

            var endnotesPart = mainPart.AddNewPart<EndnotesPart>();
            endnotesPart.Endnotes = new Endnotes(
                new Endnote(new Paragraph(new Run(new Text("")))) { Id = 0 },
                new Endnote(new Paragraph(new Run(new Text("")))) { Id = 1 },
                new Endnote(new Paragraph(new Run(new Text("This is an endnote.")))) { Id = 2 }
            );

            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new Run(new Text("Text with endnote")),
                    new Run(new EndnoteReference { Id = 2 })
                )
            ));
        }
        return ms.ToArray();
    }

    #endregion

    #region Comments

    [TestMethod]
    public void Comments_InlineBlockquote()
    {
        var data = CreateDocxWithComments();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("> **[Comment"), $"Should contain comment blockquote. Actual:\n{text}");
        Assert.IsTrue(text.Contains("Review this section."), $"Should contain comment text. Actual:\n{text}");
    }

    [TestMethod]
    public void Comments_WithAuthor()
    {
        var data = CreateDocxWithComments();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("Alice"), $"Should contain comment author. Actual:\n{text}");
    }

    [TestMethod]
    public void Comments_Excluded_WhenOptionDisabled()
    {
        var data = CreateDocxWithComments();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeComments = false });

        Assert.IsFalse(text.Contains("[Comment"), $"Should not contain comments. Actual:\n{text}");
    }

    private static byte[] CreateDocxWithComments()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();

            var commentsPart = mainPart.AddNewPart<WordprocessingCommentsPart>();
            commentsPart.Comments = new Comments(
                new Comment(
                    new Paragraph(new Run(new Text("Review this section.")))
                ) { Id = "0", Author = "Alice" }
            );

            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new Run(new Text("Some text to review.")),
                    new Run(new CommentReference { Id = "0" })
                )
            ));
        }
        return ms.ToArray();
    }

    #endregion

    #region Headers / Footers

    [TestMethod]
    public void HeadersFooters_Blockquote()
    {
        var data = CreateDocxWithHeaderFooter();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("> **[Header]:"), $"Should contain header blockquote. Actual:\n{text}");
        Assert.IsTrue(text.Contains("Company Report"), $"Should contain header text. Actual:\n{text}");
        Assert.IsTrue(text.Contains("> **[Footer]:"), $"Should contain footer blockquote. Actual:\n{text}");
        Assert.IsTrue(text.Contains("Confidential"), $"Should contain footer text. Actual:\n{text}");
    }

    [TestMethod]
    public void Headers_Excluded_WhenOptionDisabled()
    {
        var data = CreateDocxWithHeaderFooter();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeHeaders = false });

        Assert.IsFalse(text.Contains("[Header]"), $"Should not contain header. Actual:\n{text}");
    }

    [TestMethod]
    public void Footers_Excluded_WhenOptionDisabled()
    {
        var data = CreateDocxWithHeaderFooter();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeFooters = false });

        Assert.IsFalse(text.Contains("[Footer]"), $"Should not contain footer. Actual:\n{text}");
    }

    private static byte[] CreateDocxWithHeaderFooter()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();

            // Add header
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new Header(
                new Paragraph(new Run(new Text("Company Report"))));
            var headerPartId = mainPart.GetIdOfPart(headerPart);

            // Add footer
            var footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(
                new Paragraph(new Run(new Text("Confidential"))));
            var footerPartId = mainPart.GetIdOfPart(footerPart);

            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Body content."))),
                new SectionProperties(
                    new HeaderReference { Id = headerPartId, Type = HeaderFooterValues.Default },
                    new FooterReference { Id = footerPartId, Type = HeaderFooterValues.Default }
                )
            ));
        }
        return ms.ToArray();
    }

    #endregion

    #region ExtractionOptions combinations

    [TestMethod]
    public void AllOptionsDisabled_ReturnsBodyOnly()
    {
        var data = CreateDocxWithMetadata("Title", "Author");
        var text = _parser.ExtractText(data, new ExtractionOptions
        {
            IncludeMetadata = false,
            IncludeHeaders = false,
            IncludeFooters = false,
            IncludeFootnotes = false,
            IncludeEndnotes = false,
            IncludeComments = false,
        });

        Assert.IsTrue(text.Contains("Body text."), $"Should contain body text. Actual:\n{text}");
        Assert.IsFalse(text.Contains("---"), $"Should not contain YAML. Actual:\n{text}");
    }

    #endregion
}
