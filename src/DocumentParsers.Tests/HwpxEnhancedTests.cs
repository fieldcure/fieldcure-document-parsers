using System.IO.Compression;
using System.Text;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class HwpxEnhancedTests
{
    private readonly HwpxParser _parser = new();

    #region Metadata

    [TestMethod]
    public void Metadata_YamlFrontMatter()
    {
        var data = CreateHwpxWithMetadata("HWPX 문서 제목", "홍길동");
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("---"), $"Should contain YAML front matter. Actual:\n{text}");
        Assert.IsTrue(text.Contains("홍길동"), $"Should contain author. Actual:\n{text}");
    }

    [TestMethod]
    public void Metadata_Excluded_WhenOptionDisabled()
    {
        var data = CreateHwpxWithMetadata("Title", "Author");
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeMetadata = false });

        Assert.IsFalse(text.Contains("---"), $"Should not contain YAML. Actual:\n{text}");
    }

    private static byte[] CreateHwpxWithMetadata(string title, string author)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // content.hpf with Dublin Core metadata
            var hpfEntry = archive.CreateEntry("Contents/content.hpf");
            using (var writer = new StreamWriter(hpfEntry.Open(), Encoding.UTF8))
            {
                writer.Write($"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <package>
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:title>{title}</dc:title>
                        <dc:creator>{author}</dc:creator>
                        <dc:date>2026-04-01</dc:date>
                      </metadata>
                    </package>
                    """);
            }

            // section0.xml with body text
            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"
                            xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                      <hp:p><hp:run><hp:t>본문 텍스트</hp:t></hp:run></hp:p>
                    </hs:sec>
                    """);
            }
        }
        return ms.ToArray();
    }

    #endregion

    #region Footnotes

    [TestMethod]
    public void Footnotes_InlineAndDefinition()
    {
        var data = CreateHwpxWithFootnote();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("[^1]"), $"Should contain inline footnote reference. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Footnotes"), $"Should contain Footnotes section. Actual:\n{text}");
        Assert.IsTrue(text.Contains("각주 내용"), $"Should contain footnote text. Actual:\n{text}");
    }

    [TestMethod]
    public void Footnotes_Excluded_WhenOptionDisabled()
    {
        var data = CreateHwpxWithFootnote();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeFootnotes = false });

        Assert.IsFalse(text.Contains("[^"), $"Should not contain footnote references. Actual:\n{text}");
        Assert.IsFalse(text.Contains("## Footnotes"), $"Should not contain Footnotes section. Actual:\n{text}");
    }

    private static byte[] CreateHwpxWithFootnote()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"
                            xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                      <hp:p>
                        <hp:run>
                          <hp:t>본문 텍스트</hp:t>
                          <hp:ctrl>
                            <hp:footNote number="1" instId="1">
                              <hp:subList>
                                <hp:p><hp:run><hp:t>각주 내용</hp:t></hp:run></hp:p>
                              </hp:subList>
                            </hp:footNote>
                          </hp:ctrl>
                          <hp:t>이후 텍스트</hp:t>
                        </hp:run>
                      </hp:p>
                    </hs:sec>
                    """);
            }
        }
        return ms.ToArray();
    }

    #endregion

    #region Endnotes

    [TestMethod]
    public void Endnotes_InlineAndDefinition()
    {
        var data = CreateHwpxWithEndnote();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("[^en1]"), $"Should contain inline endnote reference. Actual:\n{text}");
        Assert.IsTrue(text.Contains("## Endnotes"), $"Should contain Endnotes section. Actual:\n{text}");
        Assert.IsTrue(text.Contains("미주 내용"), $"Should contain endnote text. Actual:\n{text}");
    }

    private static byte[] CreateHwpxWithEndnote()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"
                            xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                      <hp:p>
                        <hp:run>
                          <hp:t>본문</hp:t>
                          <hp:ctrl>
                            <hp:endNote number="1" instId="1">
                              <hp:subList>
                                <hp:p><hp:run><hp:t>미주 내용</hp:t></hp:run></hp:p>
                              </hp:subList>
                            </hp:endNote>
                          </hp:ctrl>
                        </hp:run>
                      </hp:p>
                    </hs:sec>
                    """);
            }
        }
        return ms.ToArray();
    }

    #endregion

    #region Memos (Comments)

    [TestMethod]
    public void Memos_AsComments()
    {
        var data = CreateHwpxWithMemo();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("> **[Comment"), $"Should contain memo as comment. Actual:\n{text}");
        Assert.IsTrue(text.Contains("메모 내용"), $"Should contain memo text. Actual:\n{text}");
    }

    [TestMethod]
    public void Memos_Excluded_WhenOptionDisabled()
    {
        var data = CreateHwpxWithMemo();
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeComments = false });

        Assert.IsFalse(text.Contains("[Comment]"), $"Should not contain comments. Actual:\n{text}");
    }

    private static byte[] CreateHwpxWithMemo()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"
                            xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                      <hp:p>
                        <hp:run>
                          <hp:t>검토 대상 텍스트</hp:t>
                          <hp:ctrl>
                            <hp:fieldBegin type="MEMO">
                              <hp:parameters cnt="2" name="">
                                <hp:stringParam name="Author">홍길동</hp:stringParam>
                                <hp:stringParam name="CreateDateTime">2026-04-07T12:00:00Z</hp:stringParam>
                              </hp:parameters>
                              <hp:subList>
                                <hp:p><hp:run><hp:t>메모 내용</hp:t></hp:run></hp:p>
                              </hp:subList>
                            </hp:fieldBegin>
                          </hp:ctrl>
                        </hp:run>
                      </hp:p>
                    </hs:sec>
                    """);
            }
        }
        return ms.ToArray();
    }

    #endregion

    #region Headers / Footers

    [TestMethod]
    public void HeaderFooter_Blockquote()
    {
        var data = CreateHwpxWithHeaderFooter();
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("> **[Header]:**"), $"Should contain header blockquote. Actual:\n{text}");
        Assert.IsTrue(text.Contains("머리글 텍스트"), $"Should contain header text. Actual:\n{text}");
        Assert.IsTrue(text.Contains("> **[Footer]:**"), $"Should contain footer blockquote. Actual:\n{text}");
        Assert.IsTrue(text.Contains("바닥글 텍스트"), $"Should contain footer text. Actual:\n{text}");
    }

    private static byte[] CreateHwpxWithHeaderFooter()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sectionEntry = archive.CreateEntry("Contents/section0.xml");
            using (var writer = new StreamWriter(sectionEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"
                            xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                      <hp:headerFooter>
                        <hp:header>
                          <hp:p><hp:run><hp:t>머리글 텍스트</hp:t></hp:run></hp:p>
                        </hp:header>
                        <hp:footer>
                          <hp:p><hp:run><hp:t>바닥글 텍스트</hp:t></hp:run></hp:p>
                        </hp:footer>
                      </hp:headerFooter>
                      <hp:p><hp:run><hp:t>본문</hp:t></hp:run></hp:p>
                    </hs:sec>
                    """);
            }
        }
        return ms.ToArray();
    }

    #endregion
}
