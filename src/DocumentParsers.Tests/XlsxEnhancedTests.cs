using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class XlsxEnhancedTests
{
    private readonly XlsxParser _parser = new();

    [TestMethod]
    public void Metadata_YamlFrontMatter()
    {
        var data = CreateXlsxWithMetadata("Spreadsheet Title", "Data Analyst");
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("title: Spreadsheet Title"), $"Should contain title. Actual:\n{text}");
        Assert.IsTrue(text.Contains("author: Data Analyst"), $"Should contain author. Actual:\n{text}");
    }

    [TestMethod]
    public void Metadata_Excluded_WhenOptionDisabled()
    {
        var data = CreateXlsxWithMetadata("Title", "Author");
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeMetadata = false });

        Assert.IsFalse(text.Contains("title:"), $"Should not contain metadata. Actual:\n{text}");
    }

    private static byte[] CreateXlsxWithMetadata(string title, string author)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            doc.PackageProperties.Title = title;
            doc.PackageProperties.Creator = author;

            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook(new Sheets());
        }
        return ms.ToArray();
    }
}
