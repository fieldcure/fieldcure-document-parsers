# Release Notes — FieldCure.DocumentParsers.Pdf.Ocr

## v0.1.0

- Initial release
- Tesseract OCR fallback for scanned PDFs with no text layer
- Embedded traineddata: English + Korean (tessdata_fast)
- Automatic language discovery from tessdata directory
- Korean post-processing: removes spurious inter-character spaces
- Engine pool for concurrent OCR (default: min(ProcessorCount, 4))
