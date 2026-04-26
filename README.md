# FieldCure.DocumentParsers

[![Core](https://img.shields.io/nuget/v/FieldCure.DocumentParsers?label=Core)](https://www.nuget.org/packages/FieldCure.DocumentParsers)
[![Imaging](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Imaging?label=Imaging)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Imaging)
[![Ocr](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Ocr?label=Ocr)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Ocr)
[![Audio](https://img.shields.io/nuget/v/FieldCure.DocumentParsers.Audio?label=Audio)](https://www.nuget.org/packages/FieldCure.DocumentParsers.Audio)
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
- **PDF** — text extraction (PdfPig, pure managed). Page image rendering and OCR are separate opt-in packages.
- **Audio** — MP3/WAV/M4A/OGG/FLAC/WebM → timestamped Markdown transcripts via Whisper.net (separate opt-in package).

## Packages

| Package | Description | Native deps |
|---------|-------------|:-----------:|
| `FieldCure.DocumentParsers` | DOCX, HWPX, XLSX, PPTX, HTML, **PDF** (text) | — |
| `FieldCure.DocumentParsers.Imaging` | PDF → page images (adds `IMediaDocumentParser`) | PDFium |
| `FieldCure.DocumentParsers.Ocr` | Tesseract OCR fallback for scanned PDFs — **Windows only** | PDFium + Tesseract |
| `FieldCure.DocumentParsers.Audio` | Audio → timestamped transcripts via Whisper.net — **Windows only** | Whisper.net + NAudio |

The core package is pure managed — no native binaries are pulled in unless you opt into Imaging, Ocr, or Audio.

> The Ocr package is currently **Windows only** — the bundled Tesseract 5.2.0 ships native Windows binaries only. The assembly carries `[SupportedOSPlatform("windows")]`, so non-Windows consumers will see CA1416 warnings at compile time. Cross-platform OCR is on the roadmap; in the meantime use the core package directly for PDFs that have an embedded text layer (works everywhere).

> **Deprecated (v2.0):** `FieldCure.DocumentParsers.Pdf` (replaced by core + Imaging) and `FieldCure.DocumentParsers.Pdf.Ocr` (renamed to `.Ocr`).

## Installation

```bash
# Core (DOCX, HWPX, XLSX, PPTX, HTML, PDF text)
dotnet add package FieldCure.DocumentParsers

# PDF page rendering (optional, pulls PDFium)
dotnet add package FieldCure.DocumentParsers.Imaging

# OCR fallback for scanned PDFs (optional, pulls Tesseract + PDFium)
dotnet add package FieldCure.DocumentParsers.Ocr

# Audio transcription (optional, pulls Whisper.net runtimes + NAudio)
dotnet add package FieldCure.DocumentParsers.Audio
```

## Quick Start

```csharp
using FieldCure.DocumentParsers;

// PDF is now registered automatically — no AddPdfSupport() call needed.
var parser = DocumentParserFactory.GetParser(".pdf");
var text = parser!.ExtractText(File.ReadAllBytes("document.pdf"));

// Same API for all formats
foreach (var ext in DocumentParserFactory.SupportedExtensions)
    Console.WriteLine(ext);
// .docx, .hwpx, .xlsx, .pptx, .html, .htm, .pdf
```

```csharp
// Opt-out control for metadata, footnotes, etc.
var parser = new DocxParser();
var options = new ExtractionOptions
{
    IncludeMetadata = false,
    IncludeFootnotes = false
};
var text = parser.ExtractText(File.ReadAllBytes("report.docx"), options);
```

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Imaging;

// Upgrade the factory's .pdf entry to IMediaDocumentParser (text + images).
DocumentParserFactoryImagingExtensions.AddImagingSupport();
var pdf = (IMediaDocumentParser)DocumentParserFactory.GetParser(".pdf")!;
var images = pdf.ExtractImages(File.ReadAllBytes("document.pdf"), dpi: 150);
```

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Ocr;

// Register an OCR-augmented PDF parser. Dispose the engine at shutdown.
using var ocr = DocumentParserFactoryOcrExtensions.AddOcrSupport();

// Scanned pages are OCR'd; pages with an embedded text layer go through PdfPig.
var parser = DocumentParserFactory.GetParser(".pdf")!;
var text = parser.ExtractText(File.ReadAllBytes("scanned.pdf"));
```

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Audio;

// Register audio support. Dispose the transcriber at shutdown.
await using var transcriber = DocumentParserFactoryAudioExtensions.AddAudioSupport();

var parser = DocumentParserFactory.GetParser(".mp3")!;
var transcript = parser.ExtractText(File.ReadAllBytes("meeting.mp3"));
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

Pipe characters inside cells are escaped as `\|` to preserve table structure.

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

### PDF

| Supported | Not Yet Supported |
|-----------|-------------------|
| Text extraction (text-based PDF) — core package | Form field extraction |
| Page image rendering — `Imaging` package | Digital signature info |
| OCR fallback for scanned PDFs — `Ocr` package | PDF/A validation |
| Multi-page documents, Unicode text | |
| English + Korean OCR (tessdata_fast) — `Ocr` | |

### Audio

| Supported | Not Yet Supported |
|-----------|-------------------|
| MP3, WAV, M4A, OGG, FLAC, WebM — `Audio` package | Real-time microphone input |
| Timestamped Markdown transcript | Speaker diarization |
| Whisper ggml model cache | Video audio track extraction |
| Custom `IAudioTranscriber` injection | Word-level timestamps |

## Repository Structure

All library projects multi-target `net8.0;net10.0`.

```
src/
├── DocumentParsers/                     FieldCure.DocumentParsers 2.0 (net8.0 + net10.0)
│   ├── Ooxml/                           DocxParser, PptxParser, XlsxParser
│   ├── Hwpx/                            HwpxParser
│   ├── Html/                            HtmlParser
│   └── Pdf/                             PdfParser (text via PdfPig)
├── DocumentParsers.Imaging/             FieldCure.DocumentParsers.Imaging 1.0 (net8.0 + net10.0)
├── DocumentParsers.Ocr/                 FieldCure.DocumentParsers.Ocr 1.0 (net8.0 + net10.0)
├── DocumentParsers.Audio/               FieldCure.DocumentParsers.Audio 0.1 (net8.0 + net10.0)
├── DocumentParsers.Cli/                 Console tool for manual output inspection
├── DocumentParsers.Tests/               MSTest — core + PdfParser tests
├── DocumentParsers.Imaging.Tests/       MSTest — PdfImageRenderer tests
├── DocumentParsers.Ocr.Tests/           MSTest — OcrPdfParser + TesseractOcrEngine tests
└── DocumentParsers.Audio.Tests/         MSTest — Audio parser tests
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
- [FieldCure.DocumentParsers.Imaging](RELEASENOTES.DocumentParsers.Imaging.md)
- [FieldCure.DocumentParsers.Ocr](RELEASENOTES.DocumentParsers.Ocr.md)
- [FieldCure.DocumentParsers.Audio](RELEASENOTES.DocumentParsers.Audio.md)

## License

[MIT](LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
