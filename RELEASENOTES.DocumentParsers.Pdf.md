# Release Notes — FieldCure.DocumentParsers.Pdf

## [1.1.0] - 2026-04-08

### Added
- `IOcrEngine` interface for pluggable OCR engines
- `PdfParser(IOcrEngine)` constructor overload with OCR fallback for scanned pages
- `AddPdfSupport(IOcrEngine)` factory registration overload
- Pages with < 5% meaningful text are automatically rendered at 300 DPI and sent to OCR

### Note
Backward compatible — existing parameterless constructor and `AddPdfSupport()` unchanged.

## [1.0.0] - 2026-04-06

### Note
First stable release, aligned with FieldCure.DocumentParsers 1.0.0.
No API changes from 0.2.0 — public surface (`PdfParser`, `AddPdfSupport()`) is now committed as stable.

## [0.2.0] - 2026-03-27

### Added
- Unit test project (`DocumentParsers.Pdf.Tests`) with 11 tests covering text extraction, page headers, content ordering, and image rendering
- Higher DPI rendering test (`ExtractImages_HigherDpiProducesLargerImage`)

### Changed
- Migrated to independent repository (fieldcure/fieldcure-document-parsers)
- `RepositoryUrl` updated to `https://github.com/fieldcure/fieldcure-document-parsers`

## [0.1.0] - 2026-03-25

### Added
- `PdfParser` — PDF text extraction via PdfPig with `## Page {n}` page headers
- `PdfParser.ExtractImages` — PDF page rendering to PNG via PDFtoImage (PDFium)
- `IMediaDocumentParser` support for combined text + image extraction
- `DocumentParserFactoryExtensions.AddPdfSupport()` — one-line factory registration
- Extracted from AssistStudio.Core to enable independent package consumption
