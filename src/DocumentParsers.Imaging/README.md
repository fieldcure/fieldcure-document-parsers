# FieldCure.DocumentParsers.Imaging

**PDF page image rendering** — extension for [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers).

Core DocumentParsers v2.0 ships with pure-managed PDF text extraction (PdfPig).
This package adds **page image rendering** via PDFtoImage (PDFium native) —
useful for vision models, thumbnails, or feeding OCR engines.

## Install

```bash
dotnet add package FieldCure.DocumentParsers.Imaging
```

## Quick Start

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Imaging;

// Registers PdfImageRenderer, upgrading the factory's .pdf entry
// from text-only PdfParser to a full IMediaDocumentParser.
DocumentParserFactoryImagingExtensions.AddImagingSupport();

var parser = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;

// Text extraction (same pipeline as the core package — no regression).
var text = parser.ExtractText(File.ReadAllBytes("document.pdf"));

// Page rendering (new capability).
var images = parser.ExtractImages(File.ReadAllBytes("document.pdf"), dpi: 150);
foreach (var img in images)
    File.WriteAllBytes($"{img.Label}.png", img.Data);
```

## Native Dependency

PDFium binaries are bundled via the PDFtoImage package (Windows/Linux/macOS).
For pure managed deployments or environments where native PDFium cannot load,
use the core `FieldCure.DocumentParsers` package directly — text extraction
does not require this package.

## Related Packages

- [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers) — Core text extraction (DOCX/HWPX/XLSX/PPTX/HTML/PDF)
- [FieldCure.DocumentParsers.Ocr](https://www.nuget.org/packages/FieldCure.DocumentParsers.Ocr) — Tesseract OCR fallback for scanned PDFs

## License

[MIT](https://github.com/fieldcure/fieldcure-document-parsers/blob/main/LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
