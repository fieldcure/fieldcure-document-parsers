using System.IO.Compression;
using System.Text;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class HwpxParserTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly HwpxParser _parser = new();

    private string ReadText(string filename)
    {
        var data = File.ReadAllBytes(Path.Combine(TestDataDir, filename));
        return _parser.ExtractText(data);
    }

    #region Extension support

    [TestMethod]
    public void SupportedExtensions_ContainsHwpx()
    {
        CollectionAssert.Contains(_parser.SupportedExtensions.ToList(), ".hwpx");
    }

    #endregion

    #region simple_text.hwpx — basic paragraphs

    [TestMethod]
    public void SimpleText_ExtractsThreeLines()
    {
        var text = ReadText("simple_text.hwpx");
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.IsTrue(lines.Length >= 3, $"Expected at least 3 lines, got {lines.Length}.");
    }

    #endregion

    #region multiple_runs.hwpx — bold/italic runs merged into one line

    [TestMethod]
    public void MultipleRuns_MergesRunsIntoSingleLine()
    {
        var text = ReadText("multiple_runs.hwpx");

        Assert.IsTrue(
            text.Contains("This paragraph contains bold text and italic text mixed together."),
            $"Runs should be merged. Actual:\n{text}");
    }

    #endregion

    #region with_table.hwpx — text + markdown table + text

    [TestMethod]
    public void WithTable_ContainsMarkdownTable()
    {
        var text = ReadText("with_table.hwpx");

        Assert.IsTrue(text.Contains('|'), "Table should be converted to markdown with pipe characters.");
        Assert.IsTrue(text.Contains("---"), "Table should have markdown separator row.");
    }

    [TestMethod]
    public void WithTable_Has3x4Table()
    {
        var text = ReadText("with_table.hwpx");

        var lines = text.Split('\n');
        var separatorLine = lines.FirstOrDefault(l => l.Contains("---") && l.Contains('|'));

        Assert.IsNotNull(separatorLine, "Should have a markdown separator row.");

        var pipeCount = separatorLine.Count(c => c == '|');
        Assert.IsTrue(pipeCount >= 4, $"Expected 3+ columns (4+ pipes), got {pipeCount} pipes.");
    }

    #endregion

    #region multiple_tables.hwpx — two tables

    [TestMethod]
    public void MultipleTables_ExtractsBothTables()
    {
        var text = ReadText("multiple_tables.hwpx");

        var lines = text.Split('\n');
        var separatorRows = lines.Where(l => l.Contains("---") && l.Contains('|')).ToList();

        Assert.IsTrue(separatorRows.Count >= 2,
            $"Expected at least 2 separator rows for 2 tables, got {separatorRows.Count}.");
    }

    #endregion

    #region empty.hwpx — empty document

    [TestMethod]
    public void EmptyDocument_ReturnsEmptyOrWhitespace()
    {
        var text = ReadText("empty.hwpx");
        Assert.IsTrue(string.IsNullOrWhiteSpace(text), $"Expected empty, got: '{text}'");
    }

    #endregion

    #region pipe_in_table.hwpx — pipe character inside cell

    [TestMethod]
    public void PipeInTable_EscapesPipeInCellContent()
    {
        var text = ReadText("pipe_in_table.hwpx");

        Assert.IsTrue(text.Contains("\\|"),
            $"Pipe in cell content should be escaped. Actual:\n{text}");
    }

    #endregion

    #region sample_math_equations.hwpx — math equation extraction

    // Helper: returns all lines of the extracted text.
    private string[] MathLines() =>
        ReadText("sample_math_equations.hwpx").Split('\n');

    [TestMethod]
    [Description("hp:equation/hp:script elements must be emitted as [math: ...] blocks.")]
    public void MathEquations_ContainsMathBlockFormat()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains("[math:"),
            $"Expected [math: ...] blocks in output.\nActual:\n{text}");
    }

    [TestMethod]
    [Description("All 7 hp:equation elements must each produce exactly one [math: ...] line.")]
    public void MathEquations_ExtractsSevenEquations()
    {
        var count = MathLines().Count(l => l.Contains("[math:"));

        Assert.AreEqual(7, count, $"Expected 7 [math:] lines, got {count}.");
    }

    [TestMethod]
    [Description("OVER keyword (Hancom fraction) must convert to \\frac{}{}.")]
    public void MathEquations_FracFromOVERKeyword()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains(@"\frac"),
            $@"Expected \frac (from OVER conversion).\nActual:\n{text}");
    }

    [TestMethod]
    [Description("hat keyword must convert to \\widehat{}.")]
    public void MathEquations_WidehatFromHatKeyword()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains(@"\widehat"),
            $@"Expected \widehat (from hat conversion).\nActual:\n{text}");
    }

    [TestMethod]
    [Description("sum with _ and ^ limits must be present in the H(s) equation.")]
    public void MathEquations_SumWithSubscriptSuperscript()
    {
        var text = ReadText("sample_math_equations.hwpx");

        // H(s) = sum_{k=1}^{N} frac{...}
        Assert.IsTrue(text.Contains(@"\sum _"),
            $@"Expected \sum _ (sum with subscript limit).\nActual:\n{text}");
        Assert.IsTrue(text.Contains(@"\frac{ { { γ } }"),
            $@"Expected \frac with γ numerator in H(s) equation.\nActual:\n{text}");
    }

    [TestMethod]
    [Description("LEFT/RIGHT keywords must convert to \\left and \\right delimiters.")]
    public void MathEquations_LeftRightDelimiters()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains(@"\left ("),
            $@"Expected \left ( delimiter.\nActual:\n{text}");
        Assert.IsTrue(text.Contains(@"\right )"),
            $@"Expected \right ) delimiter.\nActual:\n{text}");
    }

    [TestMethod]
    [Description("UNDEROVER ∫ pattern must convert to \\int with _ and ^ limits.")]
    public void MathEquations_IntegralWithLimits()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains(@"\int _"),
            $@"Expected \int _ (integral with lower limit).\nActual:\n{text}");
        Assert.IsTrue(text.Contains("∞"),
            $@"Expected ∞ (upper limit of integral).\nActual:\n{text}");
    }

    [TestMethod]
    [Description("Absolute-value bars LEFT | … RIGHT | must produce \\left | … \\right |.")]
    public void MathEquations_AbsoluteValueBars()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains(@"\left |"),
            $@"Expected \left | (absolute value open bar).\nActual:\n{text}");
        Assert.IsTrue(text.Contains(@"\right |"),
            $@"Expected \right | (absolute value close bar).\nActual:\n{text}");
    }

    [TestMethod]
    [Description("Plain text paragraphs surrounding equations must be preserved.")]
    public void MathEquations_SurroundingTextPreserved()
    {
        var text = ReadText("sample_math_equations.hwpx");

        Assert.IsTrue(text.Contains("The impedance function"),
            "Expected surrounding text 'The impedance function' to be preserved.");
        Assert.IsTrue(text.Contains("The DRT representation"),
            "Expected surrounding text 'The DRT representation' to be preserved.");
        Assert.IsTrue(text.Contains("The Loewner matrix"),
            "Expected surrounding text 'The Loewner matrix' to be preserved.");
    }

    [TestMethod]
    [Description("Text immediately before an equation block must appear before it in the output " +
                 "(e.g., 'The impedance function … is defined as:' precedes the K_RL equation).")]
    public void MathEquations_TextBeforeEquationPreservesOrder()
    {
        var text = ReadText("sample_math_equations.hwpx");

        // "The impedance function" (line 14) must appear before the K_RL equation (line 15).
        var textPos = text.IndexOf("The impedance function", StringComparison.Ordinal);
        var mathPos = text.IndexOf(@"[math: { { K } } _ { { RL } }", StringComparison.Ordinal);

        Assert.IsTrue(textPos >= 0, "'The impedance function' not found in output.");
        Assert.IsTrue(mathPos >= 0,  "K_RL equation not found in output.");
        Assert.IsTrue(textPos < mathPos,
            $"'The impedance function' (pos {textPos}) should appear before the K_RL equation (pos {mathPos}).");
    }

    [TestMethod]
    [Description("'The DRT representation … integral form:' line must appear before the Z(ω) integral equation.")]
    public void MathEquations_IntroTextBeforeIntegralEquation()
    {
        var text = ReadText("sample_math_equations.hwpx");

        var textPos = text.IndexOf("The DRT representation", StringComparison.Ordinal);
        var mathPos = text.IndexOf(@"\int _", StringComparison.Ordinal);

        Assert.IsTrue(textPos >= 0, "'The DRT representation' not found.");
        Assert.IsTrue(mathPos >= 0,  @"\int _ not found.");
        Assert.IsTrue(textPos < mathPos,
            $"Intro text (pos {textPos}) should precede the integral equation (pos {mathPos}).");
    }

    #endregion

    #region Heading detection (programmatically generated HWPX)

    private static byte[] CreateHwpxWithHeadings()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // header.xml with paraProperties containing outline headings
            var headerXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <hh:head xmlns:hh="http://www.hancom.co.kr/hwpml/2011/head">
                  <hh:refList>
                    <hh:paraProperties itemCnt="4">
                      <hh:paraPr id="0"/>
                      <hh:paraPr id="1">
                        <hh:heading type="OUTLINE" idRef="0" level="0"/>
                      </hh:paraPr>
                      <hh:paraPr id="2">
                        <hh:heading type="OUTLINE" idRef="0" level="1"/>
                      </hh:paraPr>
                      <hh:paraPr id="3">
                        <hh:heading type="NONE" idRef="0" level="0"/>
                      </hh:paraPr>
                    </hh:paraProperties>
                  </hh:refList>
                </hh:head>
                """;

            var headerEntry = archive.CreateEntry("Contents/header.xml");
            using (var writer = new StreamWriter(headerEntry.Open(), Encoding.UTF8))
                writer.Write(headerXml);

            // section0.xml with paragraphs referencing paraPr ids
            var sectionXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <hp:sec xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                  <hp:p paraPrIDRef="1"><hp:run><hp:t>Chapter Title</hp:t></hp:run></hp:p>
                  <hp:p paraPrIDRef="0"><hp:run><hp:t>Body paragraph one.</hp:t></hp:run></hp:p>
                  <hp:p paraPrIDRef="2"><hp:run><hp:t>Section Title</hp:t></hp:run></hp:p>
                  <hp:p paraPrIDRef="0"><hp:run><hp:t>Body paragraph two.</hp:t></hp:run></hp:p>
                  <hp:p paraPrIDRef="3"><hp:run><hp:t>Not a heading despite heading element.</hp:t></hp:run></hp:p>
                </hp:sec>
                """;

            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
                writer.Write(sectionXml);
        }

        return ms.ToArray();
    }

    [TestMethod]
    public void WithHeadings_ContainsMarkdownHeadings()
    {
        var text = _parser.ExtractText(CreateHwpxWithHeadings());

        Assert.IsTrue(text.Contains("# Chapter Title"),
            $"Expected '# Chapter Title'. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Section Title"),
            $"Expected '## Section Title'. Actual:\n{text}");
    }

    [TestMethod]
    public void WithHeadings_BodyTextHasNoHashPrefix()
    {
        var text = _parser.ExtractText(CreateHwpxWithHeadings());
        var lines = text.Split('\n');

        var bodyLine = lines.FirstOrDefault(l => l.Contains("Body paragraph one."));
        Assert.IsNotNull(bodyLine, "Body paragraph should be present.");
        Assert.IsFalse(bodyLine.TrimStart().StartsWith('#'), "Body text should not have # prefix.");
    }

    [TestMethod]
    public void WithHeadings_HeadingTypeNone_NotTreatedAsHeading()
    {
        var text = _parser.ExtractText(CreateHwpxWithHeadings());
        var lines = text.Split('\n');

        var nonHeadingLine = lines.FirstOrDefault(l => l.Contains("Not a heading"));
        Assert.IsNotNull(nonHeadingLine, "Non-heading paragraph should be present.");
        Assert.IsFalse(nonHeadingLine.TrimStart().StartsWith('#'),
            $"type=NONE should not produce heading prefix. Actual:\n{nonHeadingLine}");
    }

    [TestMethod]
    public void WithoutHeaderXml_StillExtractsText()
    {
        // HWPX with no header.xml — should not crash, just extract text without headings
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var sectionXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <hp:sec xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                  <hp:p paraPrIDRef="1"><hp:run><hp:t>Hello world</hp:t></hp:run></hp:p>
                </hp:sec>
                """;

            var entry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                writer.Write(sectionXml);
        }

        var text = _parser.ExtractText(ms.ToArray());
        Assert.IsTrue(text.Contains("Hello world"), $"Should extract text. Actual:\n{text}");
        Assert.IsFalse(text.TrimStart().StartsWith('#'), "Without header.xml, no heading prefix.");
    }

    #endregion
}
