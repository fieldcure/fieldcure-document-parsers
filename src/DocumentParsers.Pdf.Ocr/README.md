# FieldCure.DocumentParsers.Pdf.Ocr

Tesseract OCR fallback for scanned PDFs in [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf).

## Quick Start

```csharp
using FieldCure.DocumentParsers.Pdf.Ocr;

// Register PDF parser with OCR fallback (call once at startup)
using var ocrEngine = DocumentParserFactoryOcrExtensions.AddPdfOcrSupport();

// Use as usual — scanned pages are automatically OCR'd
var parser = DocumentParserFactory.GetParser(".pdf")!;
var text = parser.ExtractText(File.ReadAllBytes("scanned.pdf"));
```

## How It Works

1. Text extraction is attempted via PdfPig (same as the base PDF package).
2. If a page yields no meaningful text (< 5% non-whitespace or < 10 chars), it is rendered at 300 DPI.
3. The rendered image is processed by Tesseract OCR.
4. Korean text is post-processed to remove spurious inter-character spaces.

## Included Languages

- English (`eng.traineddata`)
- Korean (`kor.traineddata`)

Languages are auto-discovered from embedded traineddata files.

## Thread Safety

Uses an engine pool (default size: `min(ProcessorCount, 4)`) for concurrent OCR processing.
