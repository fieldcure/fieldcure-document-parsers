# Release Notes — FieldCure.DocumentParsers.Pdf.Ocr

## [1.0.0] - 2026-04-08

### Added
- `TesseractOcrEngine` — Tesseract OCR fallback for scanned PDFs with no text layer
- Embedded traineddata (tessdata_fast): English + Korean
- Automatic language discovery from tessdata directory
- Korean post-processing: removes spurious inter-character spaces from Tesseract output
- Engine pool via `ConcurrentBag` + `SemaphoreSlim` for concurrent OCR (default: `min(ProcessorCount, 4)`)
- `DocumentParserFactoryOcrExtensions.AddPdfOcrSupport()` — one-line factory registration with OCR
