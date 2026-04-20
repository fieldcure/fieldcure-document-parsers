# Release Notes — FieldCure.DocumentParsers.Ocr

## [1.0.0] - 2026-04-20

### Added
Initial release. This package is the successor to the deprecated
`FieldCure.DocumentParsers.Pdf.Ocr` (final version 1.0.1). From a NuGet
perspective this is a brand-new package ID, so versioning restarts at 1.0.0.

- `OcrPdfParser : IDocumentParser` — PdfPig text extraction with OCR fallback for pages lacking a text layer. Replaces the old `PdfParser(IOcrEngine)` constructor overload.
- `IOcrEngine` interface (moved here from the deprecated `.Pdf` package).
- `TesseractOcrEngine` with embedded English + Korean tessdata (`tessdata_fast`), automatic language discovery, and an engine pool (`ConcurrentBag` + `SemaphoreSlim`, default size `min(ProcessorCount, 4)`).
- Korean post-processing: removes spurious inter-character spaces from Tesseract output.
- `DocumentParserFactoryOcrExtensions.AddOcrSupport()` (creates and returns a `TesseractOcrEngine`) and `AddOcrSupport(IOcrEngine)` (caller-supplied engine).
- Tesseract native DLLs (`leptonica-1.82.0.dll`, `tesseract50.dll`) bundled with `build/` + `buildTransitive/` targets for `PackAsTool` consumers.

### Dependencies
- `FieldCure.DocumentParsers.Imaging` — page rendering via PDFium.
- `Tesseract` 5.2.0 — OCR engine + native binaries.

### Platform
Windows only. The bundled Tesseract 5.2.0 ships native Windows binaries
(`leptonica-1.82.0.dll`, `tesseract50.dll`). The assembly is marked
`[SupportedOSPlatform("windows")]`, so cross-platform consumers will see
CA1416 warnings at compile time — better a compile-time nudge than a runtime
`DllNotFoundException`. Linux / macOS support is planned for a future release
(will coexist with the current `x64/` + `build/*.targets` layout via additional
`runtimes/<rid>/native/` entries).

### Migration from `FieldCure.DocumentParsers.Pdf.Ocr` 1.0.1
```csharp
// Before
using FieldCure.DocumentParsers.Pdf.Ocr;
using var engine = DocumentParserFactoryOcrExtensions.AddPdfOcrSupport();

// After
using FieldCure.DocumentParsers.Ocr;
using var engine = DocumentParserFactoryOcrExtensions.AddOcrSupport();
```
