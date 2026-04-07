using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace FieldCure.DocumentParsers.Tests;

[TestClass]
public class PptxEnhancedTests
{
    private readonly PptxParser _parser = new();

    [TestMethod]
    public void Metadata_YamlFrontMatter()
    {
        var data = CreatePptxWithMetadata("Presentation Title", "Presenter");
        var text = _parser.ExtractText(data);

        Assert.IsTrue(text.Contains("title: Presentation Title"), $"Should contain title. Actual:\n{text}");
        Assert.IsTrue(text.Contains("author: Presenter"), $"Should contain author. Actual:\n{text}");
    }

    [TestMethod]
    public void Metadata_Excluded_WhenOptionDisabled()
    {
        var data = CreatePptxWithMetadata("Title", "Author");
        var text = _parser.ExtractText(data, new ExtractionOptions { IncludeMetadata = false });

        Assert.IsFalse(text.Contains("title:"), $"Should not contain metadata. Actual:\n{text}");
    }

    private static byte[] CreatePptxWithMetadata(string title, string author)
    {
        using var ms = new MemoryStream();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            doc.PackageProperties.Title = title;
            doc.PackageProperties.Creator = author;

            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new Presentation(new SlideIdList());
        }
        return ms.ToArray();
    }
}
