using System.Text;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Extracts text from PDF files via PdfPig (pure managed, no native dependencies).
/// Pages are separated by <c>## Page {n}</c> headers.
/// For page image rendering, see <c>PdfImageRenderer</c> in
/// <c>FieldCure.DocumentParsers.Imaging</c>. For OCR fallback on scanned PDFs,
/// see <c>OcrPdfParser</c> in <c>FieldCure.DocumentParsers.Ocr</c>.
/// </summary>
public sealed class PdfParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    /// <summary>
    /// Extracts text from all pages using PdfPig.
    /// Pages are separated by page headers (<c>## Page {n}</c>).
    /// </summary>
    public string ExtractText(byte[] data)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(data);
        var sb = new StringBuilder();
        var pageNumber = 0;

        foreach (var page in document.GetPages())
        {
            pageNumber++;
            if (sb.Length > 0) { sb.AppendLine(); sb.AppendLine(); }
            sb.AppendLine($"## Page {pageNumber}");
            sb.AppendLine();

            var text = ContentOrderTextExtractor.GetText(page);
            sb.Append(text);
        }

        return sb.ToString().TrimEnd();
    }
}
