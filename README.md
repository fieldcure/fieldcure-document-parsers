# FieldCure.DocumentParsers

[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers)](https://www.nuget.org/packages/FieldCure.DocumentParsers)
[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Pdf)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf)
[![NuGet](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Pdf.Ocr)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf.Ocr)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Lightweight document-to-text extraction library for .NET.
Converts DOCX, HWPX, XLSX, PPTX, HTML, and PDF files into structured Markdown
with heading detection and table support — designed for LLM/RAG pipelines.

## Features

- **DOCX** — headings, paragraphs, tables → markdown, math equations → LaTeX, metadata → YAML front matter, footnotes, endnotes, comments, headers/footers
- **HWPX** — Korean standard format (KS X 6101/OWPML), headings, tables → markdown, equations → LaTeX, metadata → YAML front matter, footnotes, endnotes, memos, headers/footers
- **XLSX** — sheets → markdown tables (multi-sheet support), metadata → YAML front matter
- **PPTX** — slide text, speaker notes, slide tables → markdown, grouped shapes, metadata → YAML front matter
- **HTML** — readable content extraction (SmartReader) → GitHub-flavored Markdown (ReverseMarkdown)
- **PDF** — text extraction (PdfPig) + page image rendering (PDFtoImage) — separate package

## Packages

| Package | Description | Dependencies |
|---------|-------------|:------------:|
| `FieldCure.DocumentParsers` | DOCX, HWPX, XLSX, PPTX, HTML | DocumentFormat.OpenXml, SmartReader, ReverseMarkdown |
| `FieldCure.DocumentParsers.Pdf` | PDF text + images | PdfPig, PDFtoImage |
| `FieldCure.DocumentParsers.Pdf.Ocr` | Tesseract OCR for scanned PDFs | Tesseract (eng + kor) |

PDF and OCR are separate packages to keep the core lightweight (no native binaries).

## Installation

```bash
# Core (DOCX, HWPX, XLSX, PPTX)
dotnet add package FieldCure.DocumentParsers

# PDF support (optional)
dotnet add package FieldCure.DocumentParsers.Pdf

# PDF + OCR for scanned PDFs (optional)
dotnet add package FieldCure.DocumentParsers.Pdf.Ocr
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
// .docx, .hwpx, .xlsx, .pptx, .html, .htm
```

```csharp
// Opt-out control for metadata, footnotes, etc.
var parser = new DocxParser();
var options = new ExtractionOptions
{
    IncludeMetadata = false,
    IncludeFootnotes = false
};
var text = parser.ExtractText(bytes, options);
```

```csharp
using FieldCure.DocumentParsers.Pdf;

// Register PDF support (call once at startup)
DocumentParserFactoryExtensions.AddPdfSupport();

// Extract PDF pages as images
var pdfParser = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;
var images = pdfParser.ExtractImages(File.ReadAllBytes("document.pdf"), dpi: 150);
```

```csharp
using FieldCure.DocumentParsers.Pdf.Ocr;

// Register PDF with OCR fallback for scanned PDFs (call once at startup)
using var ocrEngine = DocumentParserFactoryOcrExtensions.AddPdfOcrSupport();

// Scanned pages are automatically OCR'd when text extraction yields no content
var parser = DocumentParserFactory.GetParser(".pdf")!;
var text = parser.ExtractText(File.ReadAllBytes("scanned.pdf"));
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
| Paragraph text | Charts / SmartArt |
| Headings (Heading1–9 style + OutlineLevel) | Images (embedded) — no OCR |
| Tables → markdown (including nested) | Tracked changes |
| Hyperlink text | Text boxes / shapes |
| Math equations (OMML → LaTeX) | Legacy .doc format (use LibreOffice to convert) |
| Numbered / bulleted lists (as text) | |
| Multi-section documents | |
| Metadata → YAML front matter | |
| Footnotes / Endnotes | |
| Comments → inline blockquote | |
| Headers / Footers | |

### HWPX

| Supported | Not Yet Supported |
|-----------|-------------------|
| Paragraph text (hp:p) | Form fields |
| Headings (header.xml outline levels) | Legacy .hwp format (binary, not XML) |
| Standalone tables (hp:tbl) | |
| Embedded tables (hp:p > hp:run > hp:tbl) | |
| Math equations (hp:equation → LaTeX) | |
| Drawing text (hp:drawText) | |
| Multi-section documents | |
| Table cell merging | |
| Metadata → YAML front matter | |
| Footnotes / Endnotes | |
| Memos → inline blockquote | |
| Headers / Footers | |

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

### HTML

| Supported | Not Yet Supported |
|-----------|-------------------|
| Readable article extraction (SmartReader) | JavaScript-rendered content (SPA) |
| GitHub-flavored Markdown output | Login-required pages |
| Tables, headings, links preserved | Embedded media extraction |
| Nav / ads / footer auto-removal | Non-UTF-8 encodings |

### PDF (separate package)

| Supported | Not Yet Supported |
|-----------|-------------------|
| Text extraction (text-based PDF) | Form field extraction |
| Page image rendering | Digital signature info |
| Multi-page documents | PDF/A validation |
| Unicode text | |
| OCR fallback for scanned PDFs (Pdf.Ocr) | |
| English + Korean OCR (tessdata_fast) | |

## Repository Structure

```
src/
├── DocumentParsers/                FieldCure.DocumentParsers (net8.0)
│   ├── Ooxml/                      DocxParser, PptxParser, XlsxParser
│   ├── Hwpx/                       HwpxParser
│   └── Html/                       HtmlParser
├── DocumentParsers.Pdf/            FieldCure.DocumentParsers.Pdf (net8.0)
├── DocumentParsers.Pdf.Ocr/        FieldCure.DocumentParsers.Pdf.Ocr (net8.0)
├── DocumentParsers.Cli/            Console tool for manual output inspection
├── DocumentParsers.Tests/          MSTest — 138 tests
├── DocumentParsers.Pdf.Tests/      MSTest — 11 tests
└── DocumentParsers.Pdf.Ocr.Tests/  MSTest — 19 tests
                                    Total: 168 tests
```

## Build & Test

```bash
dotnet build
dotnet test
```

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

## Release Notes

- [FieldCure.DocumentParsers](RELEASENOTES.DocumentParsers.md)
- [FieldCure.DocumentParsers.Pdf](RELEASENOTES.DocumentParsers.Pdf.md)
- [FieldCure.DocumentParsers.Pdf.Ocr](RELEASENOTES.DocumentParsers.Pdf.Ocr.md)

## License

[MIT](LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
