# FieldCure.DocumentParsers

[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers)](https://www.nuget.org/packages/FieldCure.DocumentParsers)
[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Pdf)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Lightweight document-to-text extraction library for .NET.
Converts DOCX, HWPX, XLSX, PPTX, and PDF files into structured Markdown
with heading detection and table support — designed for LLM/RAG pipelines.

## Features

- **DOCX** — headings (style + outline level), paragraphs, tables → markdown, math equations → LaTeX
- **HWPX** — Korean standard format (KS X 6101/OWPML), headings (header.xml outline levels), standalone + embedded tables → markdown, equations → LaTeX
- **XLSX** — sheets → markdown tables (multi-sheet support)
- **PPTX** — slide text, speaker notes, slide tables → markdown, grouped shapes
- **PDF** — text extraction (PdfPig) + page image rendering (PDFtoImage) — separate package

## Packages

| Package | Description | Dependencies |
|---------|-------------|:------------:|
| `FieldCure.DocumentParsers` | DOCX, HWPX, XLSX, PPTX | DocumentFormat.OpenXml |
| `FieldCure.DocumentParsers.Pdf` | PDF text + images | PdfPig, PDFtoImage |

PDF is a separate package to keep the core package lightweight (no native binaries).

## Installation

```bash
# Core (DOCX, HWPX, XLSX, PPTX)
dotnet add package FieldCure.DocumentParsers

# PDF support (optional)
dotnet add package FieldCure.DocumentParsers.Pdf
```

## Quick Start

```csharp
using FieldCure.DocumentParsers;

// Get parser by file extension
var parser = DocumentParserFactory.GetParser(".docx");
if (parser is not null)
{
    var bytes = File.ReadAllBytes("document.docx");
    var text = parser.ExtractText(bytes);
    Console.WriteLine(text);
}

// Check supported extensions
var extensions = DocumentParserFactory.SupportedExtensions;
```

```csharp
using FieldCure.DocumentParsers.Pdf;

// Register PDF support (call once at startup)
DocumentParserFactoryExtensions.AddPdfSupport();

// Extract PDF pages as images
var pdfParser = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;
var images = pdfParser.ExtractImages(File.ReadAllBytes("document.pdf"), dpi: 150);
```

## Custom Parser

Implement `IDocumentParser` to add support for any format:

```csharp
public class MyParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".xyz"];

    public string ExtractText(byte[] data)
    {
        // Your extraction logic
        return "extracted text";
    }
}

// Register
DocumentParserFactory.Register(new MyParser());
```

## Table Output Format

All parsers convert tables to markdown format for LLM comprehension:

```markdown
| Name | Age | City |
| --- | --- | --- |
| Alice | 30 | Seoul |
| Bob | 25 | Busan |
```

## Limitations

### DOCX

| Supported | Not Yet Supported |
|-----------|-------------------|
| Paragraph text | Headers / footers |
| Headings (Heading1–9 style + OutlineLevel) | Charts / SmartArt |
| Tables → markdown (including nested) | Images (embedded) — no OCR |
| Hyperlink text | Comments / tracked changes |
| Math equations (OMML → LaTeX) | Text boxes / shapes |
| Numbered / bulleted lists (as text) | Legacy .doc format (use LibreOffice to convert) |
| Multi-section documents | |

### HWPX

| Supported | Not Yet Supported |
|-----------|-------------------|
| Paragraph text (hp:p) | Footnotes / endnotes |
| Headings (header.xml outline levels) | Form fields |
| Standalone tables (hp:tbl) | Legacy .hwp format (binary, not XML) |
| Embedded tables (hp:p > hp:run > hp:tbl) | |
| Math equations (hp:equation → LaTeX) | |
| Drawing text (hp:drawText) | |
| Multi-section documents | |
| Table cell merging | |

### XLSX

| Supported | Not Yet Supported |
|-----------|-------------------|
| Cell text values | Charts |
| SharedString references | Pivot tables |
| Multi-sheet (separated by headings) | Formula evaluation (values only) |
| Empty row/cell handling | Conditional formatting info |
| Pipe character escaping | Merged cells (partial support) |

### PPTX

| Supported | Not Yet Supported |
|-----------|-------------------|
| Slide text (all shapes) | SmartArt |
| Title / body separation | Charts |
| Speaker notes | Animations / transitions info |
| Slide tables → markdown | Audio / video references |
| Grouped shapes (text extraction) | Math equations |
| Slide ordering | |
| Field elements (slide numbers, dates) | |

### PDF (separate package)

| Supported | Not Yet Supported |
|-----------|-------------------|
| Text extraction (text-based PDF) | Scanned PDF OCR |
| Page image rendering | Form field extraction |
| Multi-page documents | Digital signature info |
| Unicode text | PDF/A validation |

## Repository Structure

```
src/
├── DocumentParsers/            FieldCure.DocumentParsers (net8.0)
│   ├── Ooxml/                  DocxParser, PptxParser, XlsxParser
│   └── Hwpx/                   HwpxParser
├── DocumentParsers.Pdf/        FieldCure.DocumentParsers.Pdf (net8.0)
├── DocumentParsers.Cli/        Console tool for manual output inspection
├── DocumentParsers.Tests/      MSTest — 70 tests
└── DocumentParsers.Pdf.Tests/  MSTest — 11 tests
```

## Build & Test

```bash
dotnet build
dotnet test
```

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#ecosystem).

## Release Notes

- [FieldCure.DocumentParsers](RELEASENOTES.DocumentParsers.md)
- [FieldCure.DocumentParsers.Pdf](RELEASENOTES.DocumentParsers.Pdf.md)

## License

[MIT](LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
