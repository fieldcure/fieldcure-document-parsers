using System.Text;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FieldCure.DocumentParsers.Pdf;

/// <summary>
/// Extracts text and page images from PDF files.
/// Text extraction via PdfPig, page rendering via PDFtoImage (PDFium).
/// When an <see cref="IOcrEngine"/> is provided, pages with no extractable text
/// are automatically processed via OCR as a fallback.
/// </summary>
public sealed class PdfParser : IMediaDocumentParser
{
    #region Fields

    private readonly IOcrEngine? _ocrEngine;

    /// <summary>
    /// Minimum number of non-whitespace characters for a page to be considered
    /// as having meaningful text content.
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

    #region Constructors

    /// <summary>
    /// Creates a parser with text extraction only (no OCR fallback).
    /// </summary>
    public PdfParser() { }

    /// <summary>
    /// Creates a parser with OCR fallback for scanned pages.
    /// </summary>
    /// <param name="ocrEngine">OCR engine to use when text extraction yields no content.</param>
    public PdfParser(IOcrEngine ocrEngine)
    {
        ArgumentNullException.ThrowIfNull(ocrEngine);
        _ocrEngine = ocrEngine;
    }

    #endregion

    #region IMediaDocumentParser

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    /// <summary>
    /// Extracts text from all pages using PdfPig.
    /// Pages are separated by page headers ("## Page {n}").
    /// When an OCR engine is configured, pages with no extractable text
    /// are rendered at 300 DPI and processed via OCR.
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

            if (_ocrEngine is not null && IsMostlyEmpty(text))
            {
                text = OcrPage(data, pageNumber - 1);
            }

            sb.Append(text);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Renders each PDF page as a PNG image.
    /// </summary>
    /// <param name="data">Raw PDF bytes.</param>
    /// <param name="dpi">Render resolution. 150 balances quality vs size.</param>
    /// <returns>One PNG per page with "Page {n}" labels.</returns>
    public IReadOnlyList<DocumentImage> ExtractImages(byte[] data, int dpi = 150)
    {
        var images = new List<DocumentImage>();
        var pageCount = PDFtoImage.Conversion.GetPageCount(data);
        var options = new PDFtoImage.RenderOptions(Dpi: dpi);

        for (var i = 0; i < pageCount; i++)
        {
            using var ms = new MemoryStream();
            PDFtoImage.Conversion.SavePng(ms, data, i, options: options);
            images.Add(new DocumentImage(ms.ToArray(), $"Page {i + 1}", i));
        }

        return images;
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
        return _ocrEngine!.RecognizeAsync(ms.ToArray()).GetAwaiter().GetResult();
    }

    #endregion
}
