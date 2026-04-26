# Release Notes — FieldCure.DocumentParsers

## [Unreleased]

### Changed
- `ExtractionOptions` is no longer `sealed`. Downstream parser packages (e.g. `FieldCure.DocumentParsers.Audio`) subclass it to add format-specific options. Source- and binary-compatible for existing callers.

### Added
- `ExtractionOptions.SourceExtension` — optional file extension hint (e.g. `.pdf`, `.mp3`) callers can supply when they already know the source format, letting parsers skip format probing.

## [2.0.0] - 2026-04-20

### Breaking Changes
- **PDF text extraction promoted to the core package.** `PdfParser` is now auto-registered in `DocumentParserFactory` for `.pdf` — no extension call required.
- **Package topology reorganized.** `FieldCure.DocumentParsers.Pdf` (v1.1.0) is deprecated in favour of:
  - Core (this package) — text extraction for DOCX/HWPX/XLSX/PPTX/HTML/**PDF** via OpenXml + **PdfPig** (pure managed, no native binaries).
  - `FieldCure.DocumentParsers.Imaging` (new) — PDF page image rendering via PDFtoImage (PDFium).
  - `FieldCure.DocumentParsers.Ocr` (replaces `.Pdf.Ocr`) — Tesseract OCR fallback for scanned PDFs.
- **Namespace change.** `FieldCure.DocumentParsers.Pdf.PdfParser` → `FieldCure.DocumentParsers.PdfParser`.
- **`AddPdfSupport()` removed.** Core auto-registers PDF; the extension method is no longer needed.
- **`PdfParser` no longer implements `IMediaDocumentParser`.** For page image rendering, register `PdfImageRenderer` from the Imaging package.
- **OCR fallback moved out of `PdfParser`.** The `PdfParser(IOcrEngine)` overload is removed. Use `OcrPdfParser` from the Ocr package instead.

### Migration (from 1.1.0)
```csharp
// Before
using FieldCure.DocumentParsers.Pdf;
DocumentParserFactoryExtensions.AddPdfSupport();

// After (text only) — nothing to register
using FieldCure.DocumentParsers;
var parser = DocumentParserFactory.GetParser(".pdf");

// After (with images)
using FieldCure.DocumentParsers.Imaging;
DocumentParserFactoryImagingExtensions.AddImagingSupport();

// After (with OCR)
using FieldCure.DocumentParsers.Ocr;
using var ocr = DocumentParserFactoryOcrExtensions.AddOcrSupport();
```

### Added
- Core now depends on `PdfPig` 0.1.* for PDF text extraction.

## [1.1.0] - 2026-04-07

### Added
- `HtmlParser` — HTML/HTM text extraction via SmartReader (content extraction) + ReverseMarkdown (HTML → Markdown conversion)
- **Metadata** — YAML front matter output (`title`, `author`, `created`, `modified`, `subject`, `keywords`, `description`) for DOCX, HWPX, PPTX, XLSX
- **Footnotes / Endnotes** — inline `[^N]` / `[^enN]` references with `## Footnotes` / `## Endnotes` definition sections (DOCX, HWPX)
- **Comments / Memos** — inline blockquote `> **[Comment — author]:**` format (DOCX comments, HWPX memos)
- **Headers / Footers** — blockquote `> **[Header]:**` / `> **[Footer]:**` with first-page/even-page variants (DOCX, HWPX)
- `ExtractionOptions` class — opt-out control for metadata, headers, footers, footnotes, endnotes, comments
- `MetadataFormatter` internal utility — `PackageProperties` / Dublin Core → YAML front matter
- `FootnoteCollector` internal utility — footnote/endnote collection and markdown rendering

### Changed
- HWPX element names corrected to camelCase (`footNote`, `endNote`) matching actual Hancom Office output
- HWPX memo detection changed from `hp:memo` to `hp:fieldBegin[type="MEMO"]` with Author/CreateDateTime parameter extraction
- HWPX parser now processes `hp:ctrl` wrappers recursively
- All HWPX element name comparisons are now case-insensitive (`StringComparison.OrdinalIgnoreCase`)
- Footnote/endnote/memo inner text excluded from body output via triple-layer exclusion filter
- Test count increased from 70 to 149 (79 new tests including 21 real-file integration tests)

## [1.0.0] - 2026-04-06

### Added
- `XlsxParser` unit tests — 9 tests covering two-sheet extraction, markdown table output
- `PptxParser` unit tests — 11 tests covering three-slide extraction, slide headers, table output

### Changed
- `OoxmlMathConverter` and `HancomMathNormalizer` are now `internal` (previously `public`) — these are implementation details not intended for direct consumption
- Test count increased from 48 to 70

### Note
This is the first stable release. Public API surface is now committed:
`IDocumentParser`, `IMediaDocumentParser`, `DocumentImage`, `DocumentParserFactory`, and all five parser classes (`DocxParser`, `HwpxParser`, `XlsxParser`, `PptxParser`, `PdfParser`).

## [0.4.0] - 2026-04-02

### Added
- `HwpxParser` heading detection — parses `header.xml` outline levels via paraPr/style chains, outputs `#`–`#######` Markdown headings
- `DocxParser` heading detection — resolves `ParagraphStyleId` (Heading1–9) with `OutlineLevel` fallback
- `DocumentParsers.Cli` console project for manual output inspection (`dotnet run --project src/DocumentParsers.Cli -- <file>`)

### Changed
- `ExtractText()` output upgraded from plain text to structured Markdown (headings + tables)
- `IDocumentParser` XML documentation updated to reflect Markdown output contract

## [0.3.0] - 2026-03-27

### Added
- `HancomMathNormalizer` — Hancom equation script → LaTeX structural conversion (ported from hml-equation-parser architecture)
- `OoxmlMathConverter` — OOXML Math (`m:oMath`) → LaTeX structural conversion for DOCX/PPTX
- Math equation output in `[math: LaTeX]` format for LLM / RAG consumption
- `HwpxParser` now extracts `<hp:equation>` blocks as `[math: ...]` lines
- `DocxParser` now extracts `m:oMath` elements as inline or block `[math: ...]`
- Support for `\frac`, `\sum`, `\int`, `\left`/`\right`, `\widehat` and other LaTeX structures
- Greek and special characters preserved as Unicode (γ, λ, τ, ∞) for direct RAG search hits

### Changed
- `src/DocumentParsers/` reorganized into `Ooxml/` and `Hwpx/` subfolders
- Migrated to independent repository (fieldcure/fieldcure-document-parsers)

## [0.2.0] - 2026-03-25

### Added
- `XlsxParser` — XLSX spreadsheet extraction as markdown tables with SharedString resolution
- `PptxParser` — PPTX slide text, tables, and speaker notes extraction
- `IMediaDocumentParser` interface for parsers with image extraction capability
- `DocumentImage` record for extracted images with label and index
- `DocumentParserFactory.Register()` method for external parser registration (e.g., PDF)

### Changed
- `DocumentParserFactory` now uses `ConcurrentDictionary` for thread-safe dynamic registration
- `SupportedExtensions` now includes `.xlsx` and `.pptx`

## [0.1.0] - 2026-03-22

### Added
- `IDocumentParser` interface for extensible document text extraction
- `DocxParser` — DOCX text extraction with markdown table conversion (via OpenXML SDK)
- `HwpxParser` — HWPX (Korean OWPML) text extraction with markdown table conversion
- `DocumentParserFactory` — extension-based parser resolution with `SupportedExtensions` discovery
- Markdown table output with pipe escaping for LLM / RAG consumption
- NuGet package README with quick start guide
