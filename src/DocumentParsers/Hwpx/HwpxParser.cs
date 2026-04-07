using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Extracts structured Markdown from HWPX files (Korean standard document format, KS X 6101 / OWPML).
/// HWPX is a ZIP archive containing XML sections. Headings are detected via header.xml paraShape
/// outline levels. Tables (hp:tbl) are converted to markdown pipe format.
/// </summary>
public sealed class HwpxParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".hwpx"];

    /// <inheritdoc />
    public string ExtractText(byte[] data)
        => ExtractText(data, ExtractionOptions.Default);

    /// <summary>
    /// Extracts structured Markdown from HWPX bytes with configurable extraction options.
    /// </summary>
    public string ExtractText(byte[] data, ExtractionOptions options)
    {
        using var stream = new MemoryStream(data);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Parse header.xml to build heading level maps
        var (paraPrHeadingMap, styleHeadingMap) = BuildHeadingMaps(archive);

        var sb = new StringBuilder();

        // Metadata — YAML front matter from content.hpf Dublin Core
        if (options.IncludeMetadata)
        {
            var yaml = ExtractMetadata(archive);
            sb.Append(yaml);
        }

        // Collect section entries sorted by name (section0.xml, section1.xml, ...)
        var sectionEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("Contents/section", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sectionEntries.Count == 0) return sb.ToString().TrimEnd();

        var footnoteCollector = new FootnoteCollector();
        var footnoteCounter = 0;
        var endnoteCounter = 0;

        foreach (var entry in sectionEntries)
        {
            using var entryStream = entry.Open();
            var xdoc = XDocument.Load(entryStream);

            // Process from section elements, or root as fallback
            var sectionElements = xdoc.Descendants()
                .Where(e => e.Name.LocalName == "sec");

            foreach (var sec in sectionElements)
            {
                // Headers — before section body
                if (options.IncludeHeaders)
                {
                    var headerText = ExtractHeaderFooterFromSection(sec, isHeader: true);
                    if (!string.IsNullOrEmpty(headerText))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(headerText);
                        sb.AppendLine();
                    }
                }

                ProcessBlockElements(sec, sb, paraPrHeadingMap, styleHeadingMap,
                    options, footnoteCollector, ref footnoteCounter, ref endnoteCounter);

                // Footers — after section body
                if (options.IncludeFooters)
                {
                    var footerText = ExtractHeaderFooterFromSection(sec, isHeader: false);
                    if (!string.IsNullOrEmpty(footerText))
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                        sb.Append(footerText);
                    }
                }
            }

            if (!sectionElements.Any() && xdoc.Root is not null)
                ProcessBlockElements(xdoc.Root, sb, paraPrHeadingMap, styleHeadingMap,
                    options, footnoteCollector, ref footnoteCounter, ref endnoteCounter);
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
    /// Extracts metadata from content.hpf Dublin Core elements.
    /// </summary>
    private static string ExtractMetadata(ZipArchive archive)
    {
        var hpfEntry = archive.Entries
            .FirstOrDefault(e => e.FullName.Equals("Contents/content.hpf", StringComparison.OrdinalIgnoreCase));
        if (hpfEntry is null) return "";

        using var entryStream = hpfEntry.Open();
        var xdoc = XDocument.Load(entryStream);

        string? title = null, creator = null, subject = null, description = null, date = null;

        foreach (var el in xdoc.Descendants())
        {
            if (el.Name.LocalName == "title" && string.IsNullOrEmpty(title))
                title = el.Value;
            else if (el.Name.LocalName == "creator" && string.IsNullOrEmpty(creator))
                creator = el.Value;
            else if (el.Name.LocalName == "subject" && string.IsNullOrEmpty(subject))
                subject = el.Value;
            else if (el.Name.LocalName == "description" && string.IsNullOrEmpty(description))
                description = el.Value;
            else if (el.Name.LocalName == "date" && string.IsNullOrEmpty(date))
                date = el.Value;
        }

        // Parse date if present
        DateTime? created = null;
        if (date is not null && DateTime.TryParse(date, out var parsedDate))
            created = parsedDate;

        return MetadataFormatter.FormatYamlFrontMatter(
            title: title,
            author: creator,
            created: created,
            subject: subject,
            description: description);
    }

    /// <summary>
    /// Extracts header or footer text from a section's hp:headerFooter element.
    /// </summary>
    private static string ExtractHeaderFooterFromSection(XElement section, bool isHeader)
    {
        var headerFooterElements = section.Elements()
            .Where(e => e.Name.LocalName == "headerFooter");

        var sb = new StringBuilder();
        foreach (var hf in headerFooterElements)
        {
            var targetLocalName = isHeader ? "header" : "footer";
            var label = isHeader ? "[Header]:" : "[Footer]:";

            foreach (var part in hf.Elements().Where(e => e.Name.LocalName == targetLocalName))
            {
                var paragraphs = part.Descendants()
                    .Where(e => e.Name.LocalName == "p");

                var texts = new List<string>();
                foreach (var p in paragraphs)
                {
                    var text = ExtractParagraphText(p);
                    if (!string.IsNullOrWhiteSpace(text))
                        texts.Add(text);
                }

                if (texts.Count > 0)
                    sb.AppendLine($"> **{label}** {string.Join(" ", texts)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Parses header.xml to build two mappings for heading level resolution:
    /// paraPrHeadingMap (paraPr id -> heading level) and styleHeadingMap (style id -> heading level).
    /// A paragraph's heading level is resolved by styleIDRef first (via style -> paraPrIDRef chain),
    /// then by direct paraPrIDRef fallback.
    /// </summary>
    private static (Dictionary<int, int> paraPrMap, Dictionary<int, int> styleMap) BuildHeadingMaps(ZipArchive archive)
    {
        var paraPrMap = new Dictionary<int, int>();
        var styleMap = new Dictionary<int, int>();

        var headerEntry = archive.Entries
            .FirstOrDefault(e => e.FullName.Equals("Contents/header.xml", StringComparison.OrdinalIgnoreCase));
        if (headerEntry is null) return (paraPrMap, styleMap);

        using var entryStream = headerEntry.Open();
        var xdoc = XDocument.Load(entryStream);

        // Step 1: Build paraPr id -> heading level map
        var paraPrElements = xdoc.Descendants()
            .Where(e => e.Name.LocalName == "paraPr");

        foreach (var paraPr in paraPrElements)
        {
            var idAttr = paraPr.Attribute("id");
            if (idAttr is null || !int.TryParse(idAttr.Value, out var id)) continue;

            var heading = paraPr.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "heading");
            if (heading is null) continue;

            var type = heading.Attribute("type")?.Value;
            if (!string.Equals(type, "OUTLINE", StringComparison.OrdinalIgnoreCase)) continue;

            var levelAttr = heading.Attribute("level");
            var level = 0;
            if (levelAttr is not null) int.TryParse(levelAttr.Value, out level);

            if (level is >= 0 and <= 6)
                paraPrMap[id] = level + 1;
        }

        // Step 2: Build style id -> heading level map (style -> paraPrIDRef -> paraPr heading)
        var styleElements = xdoc.Descendants()
            .Where(e => e.Name.LocalName == "style");

        foreach (var style in styleElements)
        {
            var idAttr = style.Attribute("id");
            var paraPrIdRefAttr = style.Attribute("paraPrIDRef");
            if (idAttr is null || paraPrIdRefAttr is null) continue;
            if (!int.TryParse(idAttr.Value, out var styleId)) continue;
            if (!int.TryParse(paraPrIdRefAttr.Value, out var paraPrIdRef)) continue;

            if (paraPrMap.TryGetValue(paraPrIdRef, out var headingLevel))
                styleMap[styleId] = headingLevel;
        }

        return (paraPrMap, styleMap);
    }

    /// <summary>
    /// Processes block-level elements (paragraphs and tables) from a parent HWPX element.
    /// In HWPX, tables (hp:tbl) are embedded inside paragraphs (hp:p > hp:run > hp:tbl),
    /// so paragraphs are checked for embedded tables as well.
    /// </summary>
    private static void ProcessBlockElements(
        XElement parent, StringBuilder sb,
        Dictionary<int, int> paraPrHeadingMap, Dictionary<int, int> styleHeadingMap,
        ExtractionOptions options, FootnoteCollector footnoteCollector,
        ref int footnoteCounter, ref int endnoteCounter)
    {
        foreach (var child in parent.Elements())
        {
            var localName = child.Name.LocalName;

            if (localName == "p")
            {
                // Determine heading level from styleIDRef (primary) or paraPrIDRef (fallback)
                var headingLevel = GetHeadingLevel(child, paraPrHeadingMap, styleHeadingMap);
                var headingPrefix = headingLevel > 0
                    ? new string('#', headingLevel) + " "
                    : "";

                // Check for embedded tables inside this paragraph (hp:p > hp:run > hp:tbl)
                var embeddedTables = child.Descendants()
                    .Where(e => e.Name.LocalName == "tbl")
                    .ToList();

                if (embeddedTables.Count > 0)
                {
                    // Extract paragraph text (excluding table cell text)
                    var paraText = ExtractParagraphTextExcludingTables(child);
                    if (!string.IsNullOrWhiteSpace(paraText))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(headingPrefix);
                        sb.Append(paraText);
                    }

                    // Then render each embedded table
                    foreach (var tbl in embeddedTables)
                    {
                        var tableText = ConvertTableToMarkdown(tbl);
                        if (!string.IsNullOrEmpty(tableText))
                        {
                            if (sb.Length > 0) { sb.AppendLine(); sb.AppendLine(); }
                            sb.Append(tableText);
                        }
                    }
                }
                else
                {
                    // Process paragraph with footnote/endnote/memo support
                    var paraText = ExtractParagraphTextWithNotes(child, options, footnoteCollector,
                        ref footnoteCounter, ref endnoteCounter);

                    // Check for memos in this paragraph
                    var memoText = "";
                    if (options.IncludeComments)
                        memoText = ExtractMemos(child);

                    if (!string.IsNullOrWhiteSpace(paraText))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(headingPrefix);
                        sb.Append(paraText);
                    }

                    if (!string.IsNullOrEmpty(memoText))
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                        sb.Append(memoText);
                    }
                }
            }
            else if (localName == "tbl")
            {
                // Standalone table (direct child)
                var tableText = ConvertTableToMarkdown(child);
                if (!string.IsNullOrEmpty(tableText))
                {
                    if (sb.Length > 0) { sb.AppendLine(); sb.AppendLine(); }
                    sb.Append(tableText);
                }
            }
            else if (localName is "sec" or "subList" or "cell" or "drawText")
            {
                // Recurse into container elements that may hold paragraphs/tables
                ProcessBlockElements(child, sb, paraPrHeadingMap, styleHeadingMap,
                    options, footnoteCollector, ref footnoteCounter, ref endnoteCounter);
            }
        }
    }

    /// <summary>
    /// Determines the heading level (1-7) for a paragraph element.
    /// Checks styleIDRef first (style may reference a heading paraPr), then falls back to direct paraPrIDRef.
    /// </summary>
    private static int GetHeadingLevel(
        XElement paragraph,
        Dictionary<int, int> paraPrHeadingMap, Dictionary<int, int> styleHeadingMap)
    {
        // Primary: resolve via styleIDRef -> style -> paraPrIDRef chain
        var styleIdRefAttr = paragraph.Attribute("styleIDRef");
        if (styleIdRefAttr is not null
            && int.TryParse(styleIdRefAttr.Value, out var styleIdRef)
            && styleHeadingMap.TryGetValue(styleIdRef, out var styleLevel))
            return styleLevel;

        // Fallback: direct paraPrIDRef lookup
        var paraPrIdRefAttr = paragraph.Attribute("paraPrIDRef");
        if (paraPrIdRefAttr is not null
            && int.TryParse(paraPrIdRefAttr.Value, out var paraPrIdRef))
            return paraPrHeadingMap.GetValueOrDefault(paraPrIdRef);

        return 0;
    }

    /// <summary>
    /// Extracts paragraph text with footnote/endnote inline markers.
    /// </summary>
    private static string ExtractParagraphTextWithNotes(
        XElement paragraph, ExtractionOptions options,
        FootnoteCollector footnoteCollector,
        ref int footnoteCounter, ref int endnoteCounter)
    {
        var hasFootnotes = options.IncludeFootnotes && paragraph.Descendants()
            .Any(e => e.Name.LocalName.Equals("footNote", StringComparison.OrdinalIgnoreCase));
        var hasEndnotes = options.IncludeEndnotes && paragraph.Descendants()
            .Any(e => e.Name.LocalName.Equals("endNote", StringComparison.OrdinalIgnoreCase));

        if (!hasFootnotes && !hasEndnotes)
            return ExtractParagraphText(paragraph);

        // Slow path: process runs to interleave text with footnote/endnote markers
        var sb = new StringBuilder();
        ExtractWithNotes(paragraph, sb, options, footnoteCollector,
            ref footnoteCounter, ref endnoteCounter);
        return sb.ToString();
    }

    /// <summary>
    /// Recursively extracts text, equations, and footnote/endnote markers.
    /// </summary>
    private static void ExtractWithNotes(
        XElement element, StringBuilder sb,
        ExtractionOptions options, FootnoteCollector footnoteCollector,
        ref int footnoteCounter, ref int endnoteCounter)
    {
        foreach (var child in element.Elements())
        {
            var localName = child.Name.LocalName;

            if (localName.Equals("footNote", StringComparison.OrdinalIgnoreCase) && options.IncludeFootnotes)
            {
                footnoteCounter++;
                var content = ExtractNoteContent(child);
                sb.Append(footnoteCollector.AddFootnote(footnoteCounter, content));
            }
            else if (localName.Equals("endNote", StringComparison.OrdinalIgnoreCase) && options.IncludeEndnotes)
            {
                endnoteCounter++;
                var content = ExtractNoteContent(child);
                sb.Append(footnoteCollector.AddEndnote(endnoteCounter, content));
            }
            else if (localName.Equals("fieldBegin", StringComparison.OrdinalIgnoreCase))
            {
                // Memo fields — skip content (handled separately by ExtractMemos)
            }
            else if (localName == "equation")
            {
                var script = child.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "script");
                if (script is not null)
                {
                    var latex = HancomMathNormalizer.ToLaTeX(script.Value);
                    if (!string.IsNullOrWhiteSpace(latex))
                        sb.Append($"[math: {latex}]");
                }
            }
            else if (localName == "t")
            {
                sb.Append(child.Value);
            }
            else if (localName is "tbl")
            {
                // Skip tables — handled separately
            }
            else
            {
                // Recurse into runs, ctrl, etc.
                ExtractWithNotes(child, sb, options, footnoteCollector,
                    ref footnoteCounter, ref endnoteCounter);
            }
        }
    }

    /// <summary>
    /// Extracts memo (comment) fields from a paragraph as blockquotes.
    /// Memos in HWPX use hp:fieldBegin[type="MEMO"] with content in hp:subList.
    /// Author and date are in hp:parameters child elements.
    /// </summary>
    private static string ExtractMemos(XElement paragraph)
    {
        var memoFields = paragraph.Descendants()
            .Where(e => e.Name.LocalName == "fieldBegin"
                     && string.Equals(e.Attribute("type")?.Value, "MEMO", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (memoFields.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (var memo in memoFields)
        {
            // Extract author from parameters
            string? author = null;
            string? dateStr = null;
            var parameters = memo.Elements().FirstOrDefault(e => e.Name.LocalName == "parameters");
            if (parameters is not null)
            {
                foreach (var param in parameters.Elements())
                {
                    var name = param.Attribute("name")?.Value;
                    if (name == "Author") author = param.Value;
                    else if (name == "CreateDateTime") dateStr = param.Value;
                }
            }

            // Extract text from subList
            var subList = memo.Elements().FirstOrDefault(e => e.Name.LocalName == "subList");
            if (subList is null) continue;

            var textParts = subList.Descendants()
                .Where(e => e.Name.LocalName == "t")
                .Select(e => e.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t));
            var text = string.Join(" ", textParts);

            if (string.IsNullOrWhiteSpace(text)) continue;

            // Format date if present
            string? date = null;
            if (dateStr is not null && DateTime.TryParse(dateStr, out var parsedDate))
                date = parsedDate.ToString("yyyy-MM-dd");

            // Format label
            var label = author is not null && date is not null
                ? $"[Comment — {author}, {date}]:"
                : author is not null ? $"[Comment — {author}]:"
                : "[Comment]:";

            if (sb.Length > 0) sb.AppendLine();
            sb.Append($"> **{label}** {text}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts text content from a footnote or endnote element's subList.
    /// Uses direct text extraction without note/memo exclusion filters.
    /// </summary>
    private static string ExtractNoteContent(XElement noteElement)
    {
        var subList = noteElement.Elements().FirstOrDefault(e => e.Name.LocalName == "subList");
        if (subList is null) return "";

        var textNodes = subList.Descendants()
            .Where(e => e.Name.LocalName == "t");
        var sb = new StringBuilder();
        foreach (var t in textNodes)
            sb.Append(t.Value);
        var result = sb.ToString().Trim();
        return result;
    }

    /// <summary>
    /// Checks whether a local name is a note or memo container whose text should be excluded from body output.
    /// </summary>
    private static bool IsNoteOrMemoElement(string localName)
        => localName.Equals("footNote", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("endNote", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("fieldBegin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts and concatenates all text from hp:t elements within a single paragraph (hp:p).
    /// Equation elements (hp:equation) are converted to [math: ...] notation.
    /// Multiple runs in the same paragraph are joined without separator.
    /// </summary>
    private static string ExtractParagraphText(XElement paragraph)
    {
        // Check if paragraph contains equations
        var hasEquations = paragraph.Descendants()
            .Any(e => e.Name.LocalName == "equation");

        if (!hasEquations)
        {
            // Fast path: no equations, just concatenate text nodes
            // Exclude text inside footnote/endnote/memo elements
            var textNodes = paragraph.Descendants()
                .Where(e => e.Name.LocalName == "t"
                         && !e.Ancestors().Any(a => IsNoteOrMemoElement(a.Name.LocalName)));
            var plainSb = new StringBuilder();
            foreach (var t in textNodes)
                plainSb.Append(t.Value);
            return plainSb.ToString();
        }

        // Slow path: process children to interleave text and equations
        var sb = new StringBuilder();
        ExtractWithEquations(paragraph, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Recursively extracts text and equation elements, converting equations to [math: ...].
    /// </summary>
    private static void ExtractWithEquations(XElement element, StringBuilder sb)
    {
        foreach (var child in element.Elements())
        {
            var localName = child.Name.LocalName;

            if (localName == "equation")
            {
                // Extract script text from equation element
                var script = child.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "script");
                if (script is not null)
                {
                    var latex = HancomMathNormalizer.ToLaTeX(script.Value);
                    if (!string.IsNullOrWhiteSpace(latex))
                        sb.Append($"[math: {latex}]");
                }
            }
            else if (localName == "t")
            {
                sb.Append(child.Value);
            }
            else if (localName is "tbl" || IsNoteOrMemoElement(localName))
            {
                // Skip tables, footnotes, endnotes, and memos — handled separately
            }
            else
            {
                // Recurse into runs, ctrl, subList, etc.
                ExtractWithEquations(child, sb);
            }
        }
    }

    /// <summary>
    /// Extracts text from a paragraph while excluding any text that belongs to embedded tables.
    /// This prevents table cell text from being duplicated outside the markdown table.
    /// </summary>
    private static string ExtractParagraphTextExcludingTables(XElement paragraph)
    {
        // Collect all tbl elements to build an exclusion set
        var tableElements = new HashSet<XElement>(
            paragraph.Descendants().Where(e => e.Name.LocalName == "tbl"));

        var textNodes = paragraph.Descendants()
            .Where(e => e.Name.LocalName == "t"
                     && !e.Ancestors().Any(a => tableElements.Contains(a)));

        var sb = new StringBuilder();
        foreach (var t in textNodes)
            sb.Append(t.Value);
        return sb.ToString();
    }

    /// <summary>
    /// Converts an HWPX table element (hp:tbl) to a markdown-formatted table string.
    /// The first row is treated as the header row.
    /// </summary>
    private static string ConvertTableToMarkdown(XElement table)
    {
        var rows = table.Elements()
            .Where(e => e.Name.LocalName == "tr")
            .ToList();

        if (rows.Count == 0) return "";

        var tableData = new List<string[]>();
        var maxCols = 0;

        foreach (var row in rows)
        {
            var cells = row.Elements()
                .Where(e => e.Name.LocalName == "tc")
                .Select(tc =>
                {
                    // A cell may contain paragraphs at any depth (e.g. tc > subList > p in HWPX)
                    var cellParagraphs = tc.Descendants()
                        .Where(e => e.Name.LocalName == "p")
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
