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
dotnet test src/DocumentParsers.Imaging.Tests
dotnet test src/DocumentParsers.Ocr.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~DocxParserTests.ExtractText_SimpleText"

# Pack NuGet packages
dotnet pack -c Release -o artifacts
```

Requires .NET 10.0 SDK (global.json pins `10.0.201` with `rollForward: latestFeature`). All library projects multi-target `net8.0;net10.0`; CI installs 8.0.x and 10.0.x.

## Architecture

**Purpose:** Lightweight document-to-text extraction library for .NET, designed for LLM/RAG pipelines. Outputs markdown tables and LaTeX math notation for LLM comprehension.

**Three NuGet packages (v2.0):**
- `FieldCure.DocumentParsers` — Core library for DOCX, HWPX, XLSX, PPTX, HTML, **PDF (text)** via OpenXML SDK + PdfPig. Pure managed, no native binaries.
- `FieldCure.DocumentParsers.Imaging` — Adds PDF page image rendering via PDFtoImage (PDFium native).
- `FieldCure.DocumentParsers.Ocr` — Tesseract OCR fallback for scanned PDFs (renamed from `.Pdf.Ocr`). Depends on Imaging.

Dependency direction: `Ocr → Imaging → Core`. The deprecated v1 packages `FieldCure.DocumentParsers.Pdf` and `FieldCure.DocumentParsers.Pdf.Ocr` are replaced by this topology.

**Key abstractions:**
- `IDocumentParser` — Base interface: `SupportedExtensions` + `ExtractText(byte[] data)`
- `IMediaDocumentParser : IDocumentParser` — Adds `ExtractImages(byte[] data, int dpi)` for formats with embedded images
- `DocumentParserFactory` — Thread-safe static factory with `ConcurrentDictionary`. Auto-registers all built-in parsers **including `PdfParser`**; supports dynamic registration via `Register()`. Lookup is case-insensitive by extension.
- `DocumentParserFactoryImagingExtensions.AddImagingSupport()` — Registers `PdfImageRenderer`, upgrading `.pdf` to `IMediaDocumentParser`.
- `DocumentParserFactoryOcrExtensions.AddOcrSupport(IOcrEngine?)` — Registers `OcrPdfParser` for scanned-PDF fallback.

**Parser implementations:**
- `Ooxml/DocxParser` — DOCX via OpenXML SDK (paragraphs, nested tables, math → LaTeX)
- `Ooxml/XlsxParser` — XLSX via OpenXML SDK (sheets as markdown tables, SharedString resolution)
- `Ooxml/PptxParser` — PPTX via OpenXML SDK (slides, text frames, tables, speaker notes with `[Notes]` prefix)
- `Hwpx/HwpxParser` — HWPX (Korean OWPML) via ZipArchive + XLinq (paragraphs, tables, equations)
- `Html/HtmlParser` — HTML via SmartReader + ReverseMarkdown
- `Pdf/PdfParser` — PDF text via PdfPig (core, pure managed)
- `PdfImageRenderer` (Imaging) — PDF pages → PNG via PDFtoImage; `ExtractText` delegates to core `PdfParser` (additive)
- `OcrPdfParser` (Ocr) — PdfPig + Tesseract fallback when a page has no text layer

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
