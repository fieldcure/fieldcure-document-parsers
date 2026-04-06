# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test src/DocumentParsers.Tests
dotnet test src/DocumentParsers.Pdf.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~DocxParserTests.ExtractText_SimpleText"

# Pack NuGet packages
dotnet pack -c Release -o artifacts
```

Requires .NET 8.0 SDK (global.json pins SDK 9.0.308; both 8.0 and 9.0 are used in CI).

## Architecture

**Purpose:** Lightweight document-to-text extraction library for .NET, designed for LLM/RAG pipelines. Outputs markdown tables and LaTeX math notation for LLM comprehension.

**Two NuGet packages:**
- `FieldCure.DocumentParsers` — Core library for DOCX, HWPX, XLSX, PPTX (depends on OpenXML SDK)
- `FieldCure.DocumentParsers.Pdf` — PDF extension (depends on PdfPig + PDFtoImage); separated to avoid native binary dependencies

**Key abstractions:**
- `IDocumentParser` — Base interface: `SupportedExtensions` + `ExtractText(byte[] data)`
- `IMediaDocumentParser : IDocumentParser` — Adds `ExtractImages(byte[] data, int dpi)` for formats with embedded images
- `DocumentParserFactory` — Thread-safe static factory with `ConcurrentDictionary`. Auto-registers built-in parsers; supports dynamic registration via `Register()`. Lookup is case-insensitive by extension.
- `DocumentParserFactoryExtensions.AddPdfSupport()` — Registers PdfParser into the factory

**Parser implementations:**
- `Ooxml/DocxParser` — DOCX via OpenXML SDK (paragraphs, nested tables, math → LaTeX)
- `Ooxml/XlsxParser` — XLSX via OpenXML SDK (sheets as markdown tables, SharedString resolution)
- `Ooxml/PptxParser` — PPTX via OpenXML SDK (slides, text frames, tables, speaker notes with `[Notes]` prefix)
- `Hwpx/HwpxParser` — HWPX (Korean OWPML) via ZipArchive + XLinq (paragraphs, tables, equations)
- `PdfParser` — PDF via PdfPig (text) + PDFtoImage (page rendering as PNG)

**Math equation conversion (internal):**
- `OoxmlMathConverter` — Converts OOXML `m:oMath` elements to LaTeX, output wrapped in `[math: ...]`
- `HancomMathNormalizer` — Converts Hancom equation script to LaTeX (200+ token mappings, ported from hml-equation-parser)

## Output Conventions

All parsers produce plain text with these conventions:
- Tables → Markdown pipe tables (pipe characters in cells escaped as `\|`)
- Math equations → `[math: LaTeX]` inline format
- Multi-page/slide docs → `## Page {n}` or `## Slide {n}` headers
- PPTX speaker notes → prefixed with `[Notes]`

## Testing

Tests use MSTest. Test data files (`.docx`, `.hwpx`, `.xlsx`, `.pptx`, `.pdf`) live in each test project's `TestData/` directory and are copied to the output directory via `PreserveNewest`.
