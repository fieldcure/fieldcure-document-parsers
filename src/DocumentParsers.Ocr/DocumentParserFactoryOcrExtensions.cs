namespace FieldCure.DocumentParsers.Ocr;

/// <summary>
/// Registers an <see cref="OcrPdfParser"/> with <see cref="DocumentParserFactory"/>,
/// replacing the factory's default text-only PDF handling with one that OCRs pages
/// lacking a text layer.
/// </summary>
public static class DocumentParserFactoryOcrExtensions
{
    /// <summary>
    /// Registers <see cref="OcrPdfParser"/> with the factory using the provided engine.
    /// The caller owns the engine's lifetime (typically a long-lived singleton).
    /// </summary>
    /// <param name="ocrEngine">OCR engine for scanned-page fallback.</param>
    public static void AddOcrSupport(IOcrEngine ocrEngine)
        => DocumentParserFactory.Register(new OcrPdfParser(ocrEngine));

    /// <summary>
    /// Convenience: creates a new <see cref="TesseractOcrEngine"/>,
    /// registers <see cref="OcrPdfParser"/> with it, and returns the engine so the
    /// caller can dispose it at shutdown.
    /// </summary>
    /// <returns>The engine instance for lifetime management.</returns>
    public static TesseractOcrEngine AddOcrSupport()
    {
        var engine = new TesseractOcrEngine();
        AddOcrSupport(engine);
        return engine;
    }
}
