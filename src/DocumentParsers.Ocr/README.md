# FieldCure.DocumentParsers.Ocr

Tesseract OCR fallback for scanned PDFs.

This package plugs into [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers)
by registering an `OcrPdfParser` for `.pdf` — when PdfPig yields no text layer
for a page, the page is rendered at 300 DPI (via PDFium) and recognized with Tesseract.

> **⚠ Platform: Windows only.**
> The bundled Tesseract 5.2.0 package ships native Windows binaries (`leptonica-1.82.0.dll`, `tesseract50.dll`). The assembly is marked `[SupportedOSPlatform("windows")]` — cross-platform consumers will see a CA1416 warning at compile time. Linux / macOS support is planned for a future release.
>
> If you only need text from PDFs with an embedded text layer, use the core [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers) package (pure managed, fully cross-platform).

## Install

```bash
dotnet add package FieldCure.DocumentParsers.Ocr
```

## Quick Start

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Ocr;

// Registers OcrPdfParser with a Tesseract engine. Dispose the engine at shutdown.
using var ocr = DocumentParserFactoryOcrExtensions.AddOcrSupport();

// Use the factory as usual — scanned pages are OCR'd automatically.
var parser = DocumentParserFactory.GetParser(".pdf")!;
var text = parser.ExtractText(File.ReadAllBytes("scanned.pdf"));
```

### Custom engine

```csharp
using var myEngine = new TesseractOcrEngine(maxPoolSize: 8);
DocumentParserFactoryOcrExtensions.AddOcrSupport(myEngine);
```

Implement `IOcrEngine` to use a different OCR backend.

## How It Works

1. PdfPig extracts text for each page (same pipeline as the core package).
2. If a page yields less than 5% non-whitespace or fewer than 10 meaningful chars, the page is rendered at 300 DPI via PDFium.
3. The rendered image is fed to the `IOcrEngine`.
4. Korean output is post-processed to remove spurious inter-character spaces.

## Included Languages

- English (`eng.traineddata`)
- Korean (`kor.traineddata`)

Languages are auto-discovered from embedded traineddata files.

## Thread Safety

`TesseractOcrEngine` uses an engine pool (default size: `min(ProcessorCount, 4)`).

## Related Packages

- [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers) — Core text extraction
- [FieldCure.DocumentParsers.Imaging](https://www.nuget.org/packages/FieldCure.DocumentParsers.Imaging) — Page image rendering

## License

[MIT](https://github.com/fieldcure/fieldcure-document-parsers/blob/main/LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
