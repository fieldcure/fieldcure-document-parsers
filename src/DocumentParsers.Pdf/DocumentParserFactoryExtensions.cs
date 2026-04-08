namespace FieldCure.DocumentParsers.Pdf;

/// <summary>
/// Registers PDF parser with <see cref="DocumentParserFactory"/>.
/// </summary>
public static class DocumentParserFactoryExtensions
{
    /// <summary>
    /// Registers PDF parser (PdfPig + PDFtoImage) with the factory.
    /// Call once at startup.
    /// </summary>
    public static void AddPdfSupport()
        => DocumentParserFactory.Register(new PdfParser());

    /// <summary>
    /// Registers PDF parser with OCR fallback support.
    /// When text extraction yields no content, pages are rendered and processed via OCR.
    /// </summary>
    /// <param name="ocrEngine">OCR engine for scanned page fallback.</param>
    public static void AddPdfSupport(IOcrEngine ocrEngine)
        => DocumentParserFactory.Register(new PdfParser(ocrEngine));
}
