namespace FieldCure.DocumentParsers.Imaging;

/// <summary>
/// Adds PDF page image rendering on top of the core <see cref="PdfParser"/>.
/// Text extraction delegates to an internal <see cref="PdfParser"/> so that
/// registering <see cref="PdfImageRenderer"/> with the factory is strictly additive
/// — existing text-extraction behavior is preserved while enabling
/// <see cref="IMediaDocumentParser.ExtractImages"/>.
/// </summary>
/// <remarks>
/// Native dependency: PDFium via PDFtoImage. Available on Windows, Linux, and macOS,
/// but the native binaries must be present in the runtime. Not suitable for pure
/// managed deployments (use the core <see cref="PdfParser"/> directly for text-only).
/// </remarks>
public sealed class PdfImageRenderer : IMediaDocumentParser
{
    private readonly PdfParser _textParser = new();

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    /// <summary>
    /// Extracts text from all pages. Delegates to the core <see cref="PdfParser"/>
    /// so behavior is identical whether Imaging is registered or not.
    /// </summary>
    public string ExtractText(byte[] data) => _textParser.ExtractText(data);

    /// <summary>
    /// Renders each PDF page as a PNG image.
    /// </summary>
    /// <param name="data">Raw PDF bytes.</param>
    /// <param name="dpi">Render resolution. 150 balances quality vs size.</param>
    /// <returns>One PNG per page with <c>Page {n}</c> labels.</returns>
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
}
