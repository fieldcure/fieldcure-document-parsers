# FieldCure Document Parsers

Document text and image extraction for .NET — RAG-ready output with markdown tables and math equation support.

[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers?label=FieldCure.DocumentParsers)](https://www.nuget.org/packages/FieldCure.DocumentParsers)
[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Pdf?label=FieldCure.DocumentParsers.Pdf)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `FieldCure.DocumentParsers` | DOCX, HWPX, XLSX, PPTX text extraction | [nuget.org](https://www.nuget.org/packages/FieldCure.DocumentParsers) |
| `FieldCure.DocumentParsers.Pdf` | PDF text extraction and page rendering | [nuget.org](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) |

## Features

- **DOCX / HWPX / XLSX / PPTX** — full text extraction via OpenXML SDK and OWPML
- **Math equations** — `m:oMath` (DOCX) and `hp:equation` (HWPX) converted to `[math: LaTeX]` blocks
- **Markdown tables** — all document tables converted with pipe escaping for LLM consumption
- **PDF text** — page-by-page extraction with `## Page N` headers via PdfPig
- **PDF images** — each page rendered to PNG via PDFtoImage (PDFium)
- **Factory pattern** — `DocumentParserFactory.GetParser(".docx")` resolves the right parser
- **Extensible** — implement `IDocumentParser` and call `DocumentParserFactory.Register()`

## Quick Start

```bash
dotnet add package FieldCure.DocumentParsers
dotnet add package FieldCure.DocumentParsers.Pdf  # optional, for PDF support
```

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Pdf;

// Register PDF support (call once at startup)
DocumentParserFactoryExtensions.AddPdfSupport();

// Extract text from any supported format
var parser = DocumentParserFactory.GetParser(".hwpx");
var text = parser!.ExtractText(File.ReadAllBytes("report.hwpx"));

// Extract PDF pages as images
var pdfParser = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;
var images = pdfParser.ExtractImages(File.ReadAllBytes("document.pdf"), dpi: 150);
```

## Repository Structure

```
src/
├── DocumentParsers/            FieldCure.DocumentParsers (net8.0)
│   ├── Ooxml/                  DocxParser, PptxParser, XlsxParser, OoxmlMathConverter
│   └── Hwpx/                   HwpxParser, HancomMathNormalizer
├── DocumentParsers.Pdf/        FieldCure.DocumentParsers.Pdf (net8.0)
├── DocumentParsers.Tests/      MSTest — 41 tests
└── DocumentParsers.Pdf.Tests/  MSTest — 11 tests
```

## Build & Test

```bash
dotnet build
dotnet test
```

## Release Notes

- [FieldCure.DocumentParsers](RELEASENOTES.DocumentParsers.md)
- [FieldCure.DocumentParsers.Pdf](RELEASENOTES.DocumentParsers.Pdf.md)

## License

[MIT](LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
