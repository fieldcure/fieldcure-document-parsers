namespace FieldCure.DocumentParsers.Pdf;

/// <summary>
/// Abstraction for OCR engines used as fallback when PDF text extraction yields no content.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Extracts text from a PDF page rendered as an image.
    /// </summary>
    /// <param name="imageBytes">PNG image bytes of the rendered page.</param>
    /// <returns>Extracted text.</returns>
    Task<string> RecognizeAsync(byte[] imageBytes);
}
