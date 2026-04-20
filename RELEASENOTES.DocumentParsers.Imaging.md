# Release Notes — FieldCure.DocumentParsers.Imaging

## [1.0.0] - 2026-04-20

### Added
- Initial release. Splits page image rendering out of the legacy `FieldCure.DocumentParsers.Pdf` package.
- `PdfImageRenderer : IMediaDocumentParser` — renders each PDF page as a PNG via PDFtoImage (PDFium). `ExtractText` delegates to the core `PdfParser` so registering this renderer is strictly additive.
- `DocumentParserFactoryImagingExtensions.AddImagingSupport()` — one-line registration that upgrades the factory's `.pdf` entry from the core text-only parser to a full `IMediaDocumentParser`.

### Note
Consumers of the deprecated `FieldCure.DocumentParsers.Pdf` package should migrate:
```csharp
// Before
using FieldCure.DocumentParsers.Pdf;
DocumentParserFactoryExtensions.AddPdfSupport();
var p = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;

// After
using FieldCure.DocumentParsers.Imaging;
DocumentParserFactoryImagingExtensions.AddImagingSupport();
var p = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;
```
