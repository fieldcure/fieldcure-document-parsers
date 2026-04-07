using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OoxmlMath = DocumentFormat.OpenXml.Math;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Extracts structured Markdown from DOCX files using Open XML SDK.
/// Headings are detected via paragraph style IDs and outline levels.
/// Math equations are converted to LaTeX notation. Tables are converted to markdown pipe format.
/// </summary>
public sealed class DocxParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".docx"];

    /// <inheritdoc />
    public string ExtractText(byte[] data)
        => ExtractText(data, ExtractionOptions.Default);

    /// <summary>
    /// Extracts structured Markdown from DOCX bytes with configurable extraction options.
    /// </summary>
    public string ExtractText(byte[] data, ExtractionOptions options)
    {
        using var stream = new MemoryStream(data);
        using var doc = WordprocessingDocument.Open(stream, false);
        var mainPart = doc.MainDocumentPart;
        var body = mainPart?.Document?.Body;
        if (body is null || mainPart is null) return "";

        var sb = new StringBuilder();

        // Metadata — YAML front matter
        if (options.IncludeMetadata)
        {
            var props = doc.PackageProperties;
            var yaml = MetadataFormatter.FormatYamlFrontMatter(
                title: props.Title,
                author: props.Creator,
                created: props.Created,
                modified: props.Modified,
                subject: props.Subject,
                keywords: props.Keywords,
                description: props.Description);
            sb.Append(yaml);
        }

        // Headers — blockquote before body
        if (options.IncludeHeaders)
        {
            var headerText = ExtractHeaderFooterText(mainPart, isHeader: true);
            if (!string.IsNullOrEmpty(headerText))
            {
                sb.Append(headerText);
                sb.AppendLine();
            }
        }

        // Pre-load footnotes and endnotes for inline reference
        var footnoteMap = new Dictionary<int, string>();
        var endnoteMap = new Dictionary<int, string>();
        var footnoteCollector = new FootnoteCollector();

        if (options.IncludeFootnotes)
            footnoteMap = LoadFootnotes(mainPart);
        if (options.IncludeEndnotes)
            endnoteMap = LoadEndnotes(mainPart);

        // Pre-load comments for inline insertion
        var commentMap = new Dictionary<int, (string? Author, string? Date, string Text)>();
        if (options.IncludeComments)
            commentMap = LoadComments(mainPart);

        // Track comment ranges — which paragraph a CommentReference appears in
        var bodyStartLen = sb.Length;

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph paragraph)
            {
                var text = ExtractParagraphText(paragraph, options, footnoteMap, endnoteMap, footnoteCollector);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb.Length > bodyStartLen) sb.AppendLine();
                    var headingLevel = GetHeadingLevel(paragraph);
                    if (headingLevel > 0)
                        sb.Append(new string('#', headingLevel) + " ");
                    sb.Append(text);
                }

                // Inline comments — output after the paragraph containing CommentReference
                if (options.IncludeComments)
                {
                    foreach (var commentRef in paragraph.Descendants<CommentReference>())
                    {
                        var id = commentRef.Id?.Value;
                        if (id is not null && int.TryParse(id, out var commentId) && commentMap.TryGetValue(commentId, out var comment))
                        {
                            sb.AppendLine();
                            sb.AppendLine();
                            var label = FormatCommentLabel(comment.Author, comment.Date);
                            sb.Append($"> **{label}** {comment.Text}");
                        }
                    }
                }
            }
            else if (element is Table table)
            {
                var tableText = ConvertTableToMarkdown(table);
                if (!string.IsNullOrEmpty(tableText))
                {
                    if (sb.Length > bodyStartLen) { sb.AppendLine(); sb.AppendLine(); }
                    sb.Append(tableText);
                }
            }
        }

        // Footers — blockquote after body
        if (options.IncludeFooters)
        {
            var footerText = ExtractHeaderFooterText(mainPart, isHeader: false);
            if (!string.IsNullOrEmpty(footerText))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(footerText);
            }
        }

        // Footnotes and endnotes sections at end
        if (options.IncludeFootnotes || options.IncludeEndnotes)
        {
            var notesSections = footnoteCollector.RenderAll();
            if (!string.IsNullOrEmpty(notesSections))
                sb.Append(notesSections);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts header or footer text from document sections as blockquote lines.
    /// </summary>
    private static string ExtractHeaderFooterText(MainDocumentPart mainPart, bool isHeader)
    {
        var sb = new StringBuilder();
        var sectionProps = mainPart.Document?.Body?.Descendants<SectionProperties>() ?? [];

        foreach (var secProps in sectionProps)
        {
            if (isHeader)
            {
                foreach (var headerRef in secProps.Elements<HeaderReference>())
                {
                    var part = mainPart.GetPartById(headerRef.Id!) as HeaderPart;
                    if (part is null) continue;

                    var text = ExtractPartText(part.Header);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var type = headerRef.Type?.Value;
                    var typeLabel = type == HeaderFooterValues.First ? "[Header — First Page]:"
                        : type == HeaderFooterValues.Even ? "[Header — Even]:"
                        : "[Header]:";
                    sb.AppendLine($"> **{typeLabel}** {text}");
                }
            }
            else
            {
                foreach (var footerRef in secProps.Elements<FooterReference>())
                {
                    var part = mainPart.GetPartById(footerRef.Id!) as FooterPart;
                    if (part is null) continue;

                    var text = ExtractPartText(part.Footer);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var type = footerRef.Type?.Value;
                    var typeLabel = type == HeaderFooterValues.First ? "[Footer — First Page]:"
                        : type == HeaderFooterValues.Even ? "[Footer — Even]:"
                        : "[Footer]:";
                    sb.AppendLine($"> **{typeLabel}** {text}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts plain text from a header or footer element.
    /// </summary>
    private static string ExtractPartText(OpenXmlCompositeElement? element)
    {
        if (element is null) return "";
        var paragraphs = element.Elements<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(" ", paragraphs);
    }

    /// <summary>
    /// Loads footnotes from the document, skipping system-reserved ids (0 and 1).
    /// </summary>
    private static Dictionary<int, string> LoadFootnotes(MainDocumentPart mainPart)
    {
        var map = new Dictionary<int, string>();
        var footnotesPart = mainPart.FootnotesPart;
        if (footnotesPart?.Footnotes is null) return map;

        foreach (var footnote in footnotesPart.Footnotes.Elements<Footnote>())
        {
            if (footnote.Id?.Value is not { } idLong) continue;
            var id = (int)idLong;
            if (id <= 1) continue; // Skip separator (0) and continuation separator (1)

            var text = string.Join(" ", footnote.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrWhiteSpace(text))
                map[id] = text;
        }

        return map;
    }

    /// <summary>
    /// Loads endnotes from the document, skipping system-reserved ids (0 and 1).
    /// </summary>
    private static Dictionary<int, string> LoadEndnotes(MainDocumentPart mainPart)
    {
        var map = new Dictionary<int, string>();
        var endnotesPart = mainPart.EndnotesPart;
        if (endnotesPart?.Endnotes is null) return map;

        foreach (var endnote in endnotesPart.Endnotes.Elements<Endnote>())
        {
            if (endnote.Id?.Value is not { } idLong) continue;
            var id = (int)idLong;
            if (id <= 1) continue;

            var text = string.Join(" ", endnote.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrWhiteSpace(text))
                map[id] = text;
        }

        return map;
    }

    /// <summary>
    /// Loads comments from the document.
    /// </summary>
    private static Dictionary<int, (string? Author, string? Date, string Text)> LoadComments(MainDocumentPart mainPart)
    {
        var map = new Dictionary<int, (string? Author, string? Date, string Text)>();
        var commentsPart = mainPart.WordprocessingCommentsPart;
        if (commentsPart?.Comments is null) return map;

        foreach (var comment in commentsPart.Comments.Elements<Comment>())
        {
            if (comment.Id?.Value is not { } idStr) continue;
            if (!int.TryParse(idStr, out var id)) continue;

            var author = comment.Author?.Value;
            var date = comment.Date?.HasValue == true ? comment.Date.Value.ToString("yyyy-MM-dd") : null;
            var text = string.Join(" ", comment.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrWhiteSpace(text))
                map[id] = (author, date, text);
        }

        return map;
    }

    /// <summary>
    /// Formats the label for a comment blockquote, including author and date if available.
    /// </summary>
    private static string FormatCommentLabel(string? author, string? date)
    {
        if (author is not null && date is not null)
            return $"[Comment — {author}, {date}]:";
        if (author is not null)
            return $"[Comment — {author}]:";
        return "[Comment]:";
    }

    /// <summary>
    /// Determines the heading level (1-9) for a paragraph. Returns 0 for body text.
    /// </summary>
    private static int GetHeadingLevel(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is not null)
        {
            // "Heading1" ~ "Heading9" pattern
            if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(styleId.AsSpan("Heading".Length), out var level)
                && level is >= 1 and <= 9)
                return level;
        }

        // Fallback: OutlineLevel (0 = Heading 1, 8 = Heading 9)
        var outlineLevel = paragraph.ParagraphProperties?
            .GetFirstChild<OutlineLevel>()?.Val?.Value;
        if (outlineLevel.HasValue && outlineLevel.Value <= 8)
            return outlineLevel.Value + 1;

        return 0;
    }

    /// <summary>
    /// Extracts text from a paragraph, converting inline math elements to LaTeX notation
    /// and inserting footnote/endnote reference markers.
    /// </summary>
    private static string ExtractParagraphText(
        Paragraph paragraph,
        ExtractionOptions options,
        Dictionary<int, string> footnoteMap,
        Dictionary<int, string> endnoteMap,
        FootnoteCollector footnoteCollector)
    {
        // Check if this paragraph contains any math elements
        var hasMath = paragraph.Descendants<OoxmlMath.OfficeMath>().Any()
                   || paragraph.Descendants<OoxmlMath.Paragraph>().Any();

        var hasNotes = (options.IncludeFootnotes && paragraph.Descendants<FootnoteReference>().Any())
                    || (options.IncludeEndnotes && paragraph.Descendants<EndnoteReference>().Any());

        if (!hasMath && !hasNotes)
            return paragraph.InnerText;

        // Process children sequentially to preserve math structure and note references
        var sb = new StringBuilder();
        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                // Block math (standalone equation): m:oMathPara
                case OoxmlMath.Paragraph mathPara:
                    foreach (var oMath in mathPara.Elements<OoxmlMath.OfficeMath>())
                    {
                        var latex = OoxmlMathConverter.ToLaTeX(oMath);
                        if (!string.IsNullOrWhiteSpace(latex))
                            sb.Append($"[math: {latex}]");
                    }
                    break;

                // Inline math: m:oMath
                case OoxmlMath.OfficeMath oMath:
                    var inlineLatex = OoxmlMathConverter.ToLaTeX(oMath);
                    if (!string.IsNullOrWhiteSpace(inlineLatex))
                        sb.Append($"[math: {inlineLatex}]");
                    break;

                // Regular text run — may contain footnote/endnote references
                case Run run:
                    foreach (var runChild in run.ChildElements)
                    {
                        if (runChild is FootnoteReference fnRef && options.IncludeFootnotes)
                        {
                            if (fnRef.Id?.Value is { } fnIdLong)
                            {
                                var fnId = (int)fnIdLong;
                                if (footnoteMap.TryGetValue(fnId, out var fnText))
                                    sb.Append(footnoteCollector.AddFootnote(fnId, fnText));
                            }
                        }
                        else if (runChild is EndnoteReference enRef && options.IncludeEndnotes)
                        {
                            if (enRef.Id?.Value is { } enIdLong)
                            {
                                var enId = (int)enIdLong;
                                if (endnoteMap.TryGetValue(enId, out var enText))
                                    sb.Append(footnoteCollector.AddEndnote(enId, enText));
                            }
                        }
                        else if (runChild is Text text)
                        {
                            sb.Append(text.Text);
                        }
                    }
                    break;

                // Other elements (bookmarks, etc.) — extract inner text
                default:
                    var txt = child.InnerText;
                    if (!string.IsNullOrEmpty(txt))
                        sb.Append(txt);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Legacy overload for internal use (table cells, etc.) without note processing.
    /// </summary>
    private static string ExtractParagraphText(Paragraph paragraph)
    {
        // Check if this paragraph contains any math elements
        var hasMath = paragraph.Descendants<OoxmlMath.OfficeMath>().Any()
                   || paragraph.Descendants<OoxmlMath.Paragraph>().Any();

        if (!hasMath)
            return paragraph.InnerText;

        var sb = new StringBuilder();
        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                case OoxmlMath.Paragraph mathPara:
                    foreach (var oMath in mathPara.Elements<OoxmlMath.OfficeMath>())
                    {
                        var latex = OoxmlMathConverter.ToLaTeX(oMath);
                        if (!string.IsNullOrWhiteSpace(latex))
                            sb.Append($"[math: {latex}]");
                    }
                    break;

                case OoxmlMath.OfficeMath oMath:
                    var inlineLatex = OoxmlMathConverter.ToLaTeX(oMath);
                    if (!string.IsNullOrWhiteSpace(inlineLatex))
                        sb.Append($"[math: {inlineLatex}]");
                    break;

                case Run run:
                    sb.Append(run.InnerText);
                    break;

                default:
                    var text = child.InnerText;
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts an OpenXml <see cref="Table"/> element to a markdown-formatted table string.
    /// The first row is treated as the header row.
    /// </summary>
    private static string ConvertTableToMarkdown(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return "";

        var tableData = new List<string[]>();
        var maxCols = 0;

        foreach (var row in rows)
        {
            var cells = row.Elements<TableCell>()
                .Select(cell =>
                {
                    // A cell may contain multiple paragraphs — join with space
                    var cellParagraphs = cell.Elements<Paragraph>()
                        .Select(ExtractParagraphText)
                        .Where(t => !string.IsNullOrEmpty(t));
                    return string.Join(" ", cellParagraphs);
                })
                .ToArray();

            if (cells.Length > maxCols) maxCols = cells.Length;
            tableData.Add(cells);
        }

        if (maxCols == 0) return "";

        var sb = new StringBuilder();
        for (var i = 0; i < tableData.Count; i++)
        {
            var row = tableData[i];
            sb.Append('|');
            for (var j = 0; j < maxCols; j++)
            {
                var cellText = j < row.Length ? row[j].Replace("|", "\\|") : "";
                sb.Append($" {cellText} |");
            }
            sb.AppendLine();

            // Separator after first row (treated as header)
            if (i == 0)
            {
                sb.Append('|');
                for (var j = 0; j < maxCols; j++)
                    sb.Append(" --- |");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
