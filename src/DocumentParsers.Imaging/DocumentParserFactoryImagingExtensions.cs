namespace FieldCure.DocumentParsers.Imaging;

/// <summary>
/// Registers <see cref="PdfImageRenderer"/> with <see cref="DocumentParserFactory"/>,
/// upgrading the factory's <c>.pdf</c> entry from text-only <see cref="PdfParser"/>
/// to a full <see cref="IMediaDocumentParser"/> that supports page image rendering.
/// </summary>
public static class DocumentParserFactoryImagingExtensions
{
    /// <summary>
    /// Registers <see cref="PdfImageRenderer"/> so that
    /// <see cref="DocumentParserFactory.GetParser"/>("<c>.pdf</c>") returns an
    /// <see cref="IMediaDocumentParser"/>. Text extraction continues to work
    /// unchanged — the renderer delegates text to the same PdfPig pipeline.
    /// Call once at startup.
    /// </summary>
    public static void AddImagingSupport()
        => DocumentParserFactory.Register(new PdfImageRenderer());
}
