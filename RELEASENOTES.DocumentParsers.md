# Release Notes — FieldCure.DocumentParsers

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
