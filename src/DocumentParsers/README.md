# FieldCure.DocumentParsers

**Lightweight document text extraction for .NET** — DOCX, HWPX, XLSX, PPTX, and more. Tables are converted to markdown for LLM / RAG consumption.

## Features

- **DOCX** — Headings (Heading1–9 + OutlineLevel), paragraphs, tables (including nested), multi-run text via OpenXML SDK
- **HWPX** — Korean standard format (KS X 6101 / OWPML). Headings (header.xml outline levels), paragraphs, tables, multi-section support
- **XLSX** — Spreadsheet sheets as markdown tables with SharedString resolution
- **PPTX** — Slide text, tables, and speaker notes extraction
- **Math equations** — DOCX (`m:oMath`) and HWPX (`hp:equation`) equations converted to `[math: LaTeX]` blocks
- **Markdown tables** — All document tables are converted to markdown with pipe escaping
- **Factory pattern** — `DocumentParserFactory.GetParser(".docx")` returns the right parser
- **Zero platform dependency** — Targets `net8.0`, no Windows-specific APIs
- **Extensible** — Implement `IDocumentParser` and call `DocumentParserFactory.Register()`

## Install

```bash
dotnet add package FieldCure.DocumentParsers
```

## Quick Start

```csharp
using FieldCure.DocumentParsers;

// Auto-detect parser by extension
var parser = DocumentParserFactory.GetParser(".docx");
if (parser is not null)
{
    var bytes = File.ReadAllBytes("report.docx");
    var text = parser.ExtractText(bytes);
    Console.WriteLine(text);
}

// Check all supported extensions
foreach (var ext in DocumentParserFactory.SupportedExtensions)
    Console.WriteLine(ext);  // .docx, .hwpx, .xlsx, .pptx
```

## Output Format

Headings are prefixed with `#` markers. Tables are rendered as markdown:

```
# 2025 Business Plan
Please refer to the table below for details.

## Financial Summary

| Category | Q1 | Q2 |
| --- | --- | --- |
| Revenue | 100 | 150 |
| Cost | 80 | 90 |

End of report.
```

Pipe characters inside cells are escaped as `\|` to preserve table structure.

## Supported Formats

| Format | Extension | Parser | Description |
|--------|-----------|--------|-------------|
| Word | `.docx` | `DocxParser` | OpenXML (Office 2007+) |
| Hangul | `.hwpx` | `HwpxParser` | OWPML (Hancom Office) |
| Excel | `.xlsx` | `XlsxParser` | OpenXML spreadsheets |
| PowerPoint | `.pptx` | `PptxParser` | OpenXML presentations |

## PDF Support

PDF requires native libraries (PDFium). Install the separate package:

```bash
dotnet add package FieldCure.DocumentParsers.Pdf
```

See [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) for details.

## Related Packages

- [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) — PDF text extraction and page rendering
- [FieldCure.AssistStudio.Core](https://www.nuget.org/packages/FieldCure.AssistStudio.Core) — AI provider client library that uses this package for document attachments

## License

[MIT](https://github.com/fieldcure/fieldcure-document-parsers/blob/main/LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
