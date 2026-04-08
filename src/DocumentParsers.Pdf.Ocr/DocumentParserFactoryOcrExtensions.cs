namespace FieldCure.DocumentParsers.Pdf.Ocr;

/// <summary>
/// Convenience methods for registering PDF+OCR support with <see cref="DocumentParserFactory"/>.
/// </summary>
public static class DocumentParserFactoryOcrExtensions
{
    /// <summary>
    /// Creates a <see cref="TesseractOcrEngine"/> and registers a PDF parser with OCR fallback.
    /// The caller owns the returned engine and must dispose it at shutdown.
    /// </summary>
    /// <returns>The engine instance for lifetime management.</returns>
    public static TesseractOcrEngine AddPdfOcrSupport()
    {
        var engine = new TesseractOcrEngine();
        DocumentParserFactoryExtensions.AddPdfSupport(engine);
        return engine;
    }
}
