using System.Text;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FieldCure.DocumentParsers.Ocr;

/// <summary>
/// PDF parser that falls back to OCR for pages with no extractable text layer.
/// Text extraction uses PdfPig; when a page yields no meaningful content,
/// the page is rendered at <see cref="OcrRenderDpi"/> via PDFium and sent to the
/// injected <see cref="IOcrEngine"/>.
/// </summary>
public sealed class OcrPdfParser : IDocumentParser
{
    #region Fields

    private readonly IOcrEngine _ocrEngine;

    /// <summary>
    /// Minimum non-whitespace character count for a page to be considered as
    /// having meaningful text content.
    /// </summary>
    private const int MinMeaningfulCharCount = 10;

    /// <summary>
    /// Minimum ratio of non-whitespace characters to total characters.
    /// Below this threshold, the page is considered mostly empty.
    /// </summary>
    private const double MinMeaningfulRatio = 0.05;

    /// <summary>
    /// DPI used to rasterize pages for OCR. Higher than display DPI for better recognition.
    /// </summary>
    private const int OcrRenderDpi = 300;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates an OCR-augmented PDF parser.
    /// </summary>
    /// <param name="ocrEngine">OCR engine to use when PdfPig yields no content for a page.</param>
    public OcrPdfParser(IOcrEngine ocrEngine)
    {
        ArgumentNullException.ThrowIfNull(ocrEngine);
        _ocrEngine = ocrEngine;
    }

    #endregion

    #region IDocumentParser

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    /// <inheritdoc />
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

            if (IsMostlyEmpty(text))
            {
                text = OcrPage(data, pageNumber - 1);
            }

            sb.Append(text);
        }

        return sb.ToString().TrimEnd();
    }

    #endregion

    #region OCR fallback

    /// <summary>
    /// Determines whether extracted text is mostly empty or meaningless.
    /// </summary>
    private static bool IsMostlyEmpty(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var nonWhitespace = 0;
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c)) nonWhitespace++;
        }

        return nonWhitespace < MinMeaningfulCharCount
            || (double)nonWhitespace / text.Length < MinMeaningfulRatio;
    }

    /// <summary>
    /// Renders a single page at OCR DPI and runs OCR.
    /// </summary>
    private string OcrPage(byte[] data, int pageIndex)
    {
        using var ms = new MemoryStream();
        var options = new PDFtoImage.RenderOptions(Dpi: OcrRenderDpi);
        PDFtoImage.Conversion.SavePng(ms, data, pageIndex, options: options);
        return _ocrEngine.RecognizeAsync(ms.ToArray()).GetAwaiter().GetResult();
    }

    #endregion
}
