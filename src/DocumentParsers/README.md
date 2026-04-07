# FieldCure.DocumentParsers

**Lightweight document text extraction for .NET** — DOCX, HWPX, XLSX, PPTX, HTML, and more. Structured Markdown output for LLM / RAG consumption.

## Features

- **DOCX** — Headings, paragraphs, tables (nested), math → LaTeX, metadata → YAML, footnotes, endnotes, comments, headers/footers
- **HWPX** — Korean standard (KS X 6101 / OWPML). Headings, paragraphs, tables, math → LaTeX, metadata → YAML, footnotes, endnotes, memos, headers/footers
- **XLSX** — Sheets as markdown tables, metadata → YAML
- **PPTX** — Slide text, tables, speaker notes, metadata → YAML
- **HTML** — Readable content extraction via SmartReader → GitHub-flavored Markdown via ReverseMarkdown
- **Math equations** — DOCX (`m:oMath`) and HWPX (`hp:equation`) converted to `[math: LaTeX]`
- **Metadata** — YAML front matter (`title`, `author`, `created`, `modified`, `subject`, `keywords`, `description`)
- **Footnotes / Endnotes** — `[^N]` / `[^enN]` inline references with definition sections
- **Comments** — Inline blockquote `> **[Comment — author]:**` format
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
    Console.WriteLine(ext);  // .docx, .hwpx, .xlsx, .pptx, .html, .htm

// Opt-out control for metadata, footnotes, etc.
var docxParser = new DocxParser();
var options = new ExtractionOptions
{
    IncludeMetadata = false,
    IncludeFootnotes = false
};
var text = docxParser.ExtractText(bytes, options);
```

## Output Format

Headings are prefixed with `#` markers. Tables are rendered as markdown.
Documents with metadata include YAML front matter; footnotes/endnotes are rendered as reference-style links:

```
---
title: 2026 Business Plan
author: Alice
created: 2026-04-01
---

> **[Header]:** Company Confidential

# 2026 Business Plan

Please refer to the table below[^1] for details.

| Category | Q1 | Q2 |
| --- | --- | --- |
| Revenue | 100 | 150 |
| Cost | 80 | 90 |

> **[Footer]:** Page 1

## Footnotes

[^1]: Source: internal finance report.
```

Pipe characters inside cells are escaped as `\|` to preserve table structure.
Use `ExtractionOptions` to selectively disable metadata, footnotes, comments, or headers/footers.

## Supported Formats

| Format | Extension | Parser | Description |
|--------|-----------|--------|-------------|
| Word | `.docx` | `DocxParser` | OpenXML (Office 2007+) |
| Hangul | `.hwpx` | `HwpxParser` | OWPML (Hancom Office) |
| Excel | `.xlsx` | `XlsxParser` | OpenXML spreadsheets |
| PowerPoint | `.pptx` | `PptxParser` | OpenXML presentations |
| HTML | `.html`, `.htm` | `HtmlParser` | SmartReader + ReverseMarkdown |

## PDF Support

PDF requires native libraries (PDFium). Install the separate package:

```bash
dotnet add package FieldCure.DocumentParsers.Pdf
```

```csharp
using FieldCure.DocumentParsers.Pdf;

// Register PDF support (call once at startup)
DocumentParserFactoryExtensions.AddPdfSupport();
```

See [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) for details.

## Related Packages

- [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) — PDF text extraction and page rendering
- [FieldCure.AssistStudio.Core](https://www.nuget.org/packages/FieldCure.AssistStudio.Core) — AI provider client library that uses this package for document attachments

## License

[MIT](https://github.com/fieldcure/fieldcure-document-parsers/blob/main/LICENSE) — Copyright (c) 2026 FieldCure Co., Ltd.
