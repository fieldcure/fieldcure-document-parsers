# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build everything
dotnet build

# Run all non-integration tests
dotnet test

# Run a single test project
dotnet test src/DocumentParsers.Tests
dotnet test src/DocumentParsers.Imaging.Tests
dotnet test src/DocumentParsers.Ocr.Tests
dotnet test src/DocumentParsers.Audio.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~DocxParserTests.ExtractText_SimpleText"

# Skip opt-in integration tests (default behavior — they are gated on env vars,
# but the filter makes intent explicit)
dotnet test --filter "TestCategory!=Integration"

# Pack NuGet packages (artifacts go to ./artifacts/)
dotnet pack -c Release -o artifacts
```

Requires .NET 10.0 SDK (`global.json` pins `10.0.201` with `rollForward: latestFeature`). All library projects multi-target `net8.0;net10.0`; CI installs both.

## Architecture

**Purpose:** Lightweight document-to-text extraction library for .NET, designed for LLM/RAG pipelines. Outputs markdown tables, LaTeX math notation, and timestamped audio transcripts for LLM comprehension.

**Four NuGet packages, versioned independently:**

| Package | Versions | Adds | Native deps |
| --- | --- | --- | --- |
| `FieldCure.DocumentParsers` | v2.x | DOCX, HWPX, XLSX, PPTX, HTML, **PDF (text)** | None (pure managed) |
| `FieldCure.DocumentParsers.Imaging` | v1.x | PDF page → PNG rendering | PDFium |
| `FieldCure.DocumentParsers.Ocr` | v1.x | Tesseract OCR fallback for scanned PDFs | Tesseract + PDFium (via Imaging) |
| `FieldCure.DocumentParsers.Audio` | v0.3.x | MP3/WAV/M4A/OGG/FLAC/WebM transcription via Whisper.net | Whisper.net CPU runtime bundled; CUDA/Vulkan fetched at runtime |

Dependency direction: `Ocr → Imaging → Core`, `Audio → Core`. Audio is **Windows-only** (`SupportedOSPlatform("windows")`); the others are cross-platform. The deprecated v1 packages `FieldCure.DocumentParsers.Pdf` and `FieldCure.DocumentParsers.Pdf.Ocr` are replaced by the Core/Imaging/Ocr split.

**Key abstractions:**
- `IDocumentParser` — Base interface: `SupportedExtensions` + `ExtractText(byte[] data)`
- `IMediaDocumentParser : IDocumentParser` — Adds `ExtractImages(byte[] data, int dpi)` for formats with embedded images
- `DocumentParserFactory` — Thread-safe static factory with `ConcurrentDictionary`. Auto-registers all built-in parsers **including `PdfParser`**; supports dynamic registration via `Register()`. Lookup is case-insensitive by extension.
- `DocumentParserFactoryImagingExtensions.AddImagingSupport()` — Registers `PdfImageRenderer`, upgrading `.pdf` to `IMediaDocumentParser`.
- `DocumentParserFactoryOcrExtensions.AddOcrSupport(IOcrEngine?)` — Registers `OcrPdfParser` for scanned-PDF fallback.
- `DocumentParserFactoryAudioExtensions.AddAudioSupport(IAudioTranscriber?)` — Registers `AudioDocumentParser` for the supported audio extensions.

**Parser implementations:**
- `Ooxml/DocxParser` — DOCX via OpenXML SDK (paragraphs, nested tables, math → LaTeX)
- `Ooxml/XlsxParser` — XLSX via OpenXML SDK (sheets as markdown tables, SharedString resolution)
- `Ooxml/PptxParser` — PPTX via OpenXML SDK (slides, text frames, tables, speaker notes with `[Notes]` prefix)
- `Hwpx/HwpxParser` — HWPX (Korean OWPML) via ZipArchive + XLinq (paragraphs, tables, equations)
- `Html/HtmlParser` — HTML via SmartReader + ReverseMarkdown
- `Pdf/PdfParser` — PDF text via PdfPig (core, pure managed)
- `PdfImageRenderer` (Imaging) — PDF pages → PNG via PDFtoImage; `ExtractText` delegates to core `PdfParser` (additive)
- `OcrPdfParser` (Ocr) — PdfPig + Tesseract fallback when a page has no text layer
- `AudioDocumentParser` (Audio) — NAudio decode → 16 kHz mono PCM → Whisper.net transcription → markdown via `MarkdownFormatter`

**Math equation conversion (internal):**
- `OoxmlMathConverter` — Converts OOXML `m:oMath` elements to LaTeX, output wrapped in `[math: ...]`
- `HancomMathNormalizer` — Converts Hancom equation script to LaTeX (200+ token mappings, ported from hml-equation-parser)

## Audio runtime model (v0.3+)

GPU runtimes (CUDA, Vulkan) are **not** bundled in the NuGet package — only the CPU runtime is. They are downloaded on first GPU use:

- **Three-phase lifecycle:** `Detect` (host probe) → `Provision` (binary acquisition + SHA-256 verify) → `Activate` (Whisper.net `RuntimeOptions.LibraryPath` configuration)
- **Source:** [`fieldcure/fieldcure-whisper-runtimes`](https://github.com/fieldcure/fieldcure-whisper-runtimes) — sister repo, GitHub Releases. Manifest URL pinned in `GitHubReleasesWhisperRuntimeProvisioner.DefaultManifestUrl`.
- **Cache:** `%LOCALAPPDATA%\FieldCure\WhisperRuntimes\` (sibling to `WhisperModels\`). Per-target `SemaphoreSlim` + atomic `File.Move` for concurrency; orphaned `.download.<guid>` files swept on construction.
- **Air-gapped:** set `FIELDCURE_WHISPER_RUNTIME_DIR` to a pre-staged directory (manifest + `runtimes/<variant>/<rid>/` tree). Provisioner skips all network I/O.
- **CUDA host requirement:** NVIDIA driver R525+ (CUDA 12.x runtime). v1.9.0 manifest does NOT redistribute `cudart64_*.dll` / `cublas*.dll`; expected resolved by host CUDA runtime. Selection gated on `WhisperEnvironmentInfo.CudaDriverVersion >= 12000`.

See `RELEASENOTES.DocumentParsers.Audio.md` for full v0.3 surface notes; the consumer code lives under `src/DocumentParsers.Audio/Runtime/`.

## Code conventions

- **XML doc comments are mandatory on every member** — `public`, `internal`, **and `private`** alike. At minimum a `/// <summary>` tag; add `<param>`, `<returns>`, `<exception>`, `<remarks>` where they carry information beyond the signature. The codebase is consistently documented at this level (see `MarkdownFormatter` private helpers, `GitHubReleasesWhisperRuntimeProvisioner` private members, etc.) — agent-authored code that omits docs on private methods will fail review.
- **English only** in code, comments, and committed docs. User-facing strings that need localization are out of scope for this repo.
- **No emojis** in source, comments, or markdown unless a fixture explicitly requires them.
- **C# 12 language features** — projects pin `<LangVersion>12</LangVersion>`. Collection expressions (`[a, b, c]`), primary constructors, and other 12-era features are fine.
- **Nullable reference types enabled.** Argument-null checks via `ArgumentNullException.ThrowIfNull(...)` rather than manual `if (x is null) throw ...`.

## Output Conventions

All parsers produce plain text with these conventions:
- Tables → Markdown pipe tables (pipe characters in cells escaped as `\|`)
- Math equations → `[math: LaTeX]` inline format
- Multi-page / multi-slide docs → `## Page {n}` or `## Slide {n}` headers
- PPTX speaker notes → prefixed with `[Notes]`
- Audio transcripts → `# Audio Transcript` header + metadata table (`Duration`, `Language`, `Model`, `Segments`) + per-segment `[HH:MM:SS]` timestamps (suppressible via `IncludeTimestamps = false`)

## Testing

Tests use MSTest. Test data files live in each test project's `TestData/` directory and are copied to the output directory via `PreserveNewest`.

**Audio integration tests** are opt-in. They run real Whisper transcription against checked-in public-domain fixtures and bear the `[TestCategory("Integration")]` attribute. Default `dotnet test` runs report them as `Inconclusive`. To enable:

```powershell
$env:FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS = '1'
$env:FIELDCURE_WHISPER_MODEL_PATH = 'C:\Users\...\AppData\Local\FieldCure\WhisperModels\ggml-base.bin'
dotnet test src/DocumentParsers.Audio.Tests --filter "TestCategory=Integration"
```

The `ggml-base.bin` model is the cheapest variant suitable for smoke tests; larger models (medium / large-v2) give better Korean accuracy but the integration tests assert presence of distinctive English phrases or segment count, not exact transcripts.

Audio test fixtures:
- `TestData/PublicDomain/` — Gettysburg Address (English MP3) + Korean LibriVox excerpt (WAV). License notes in `LICENSES.md`.
- `TestData/WhisperNetSamples/` — bush.wav, kennedy.mp3, multichannel.wav from upstream whisper.net's sample set. `multichannel.wav` is a sanity fixture for the multi-channel conversion path; assertion follows upstream policy (segment count ≥ 1, no transcript-content match).

## Release process

Per-package release; each package has its own publish script under `scripts/`:

| Script | Package |
| --- | --- |
| `publish-core.ps1` | `FieldCure.DocumentParsers` |
| `publish-imaging.ps1` | `FieldCure.DocumentParsers.Imaging` |
| `publish-ocr.ps1` | `FieldCure.DocumentParsers.Ocr` |
| `publish-audio.ps1` | `FieldCure.DocumentParsers.Audio` |
| `publish-nuget.ps1` | All four (rare; usually a single package moves at a time) |

All scripts: pack → sign (GlobalSign EV USB dongle, `CN=Fieldcure Co., Ltd.`) → push to nuget.org. Accept `-SkipSign`, `-SkipPush`, `-NuGetApiKey` (else read from `$env:NUGET_API_KEY`). See `scripts/README.md` for prerequisites.

**Branch / tag convention:**
- Release work happens on `release/vX.Y.Z` branches off `main`.
- Tag format is **per-package**: `documentparsers.audio-v0.3.0`, `documentparsers.imaging-v1.0.0`, etc. The bare `vX.Y.Z` tag (legacy, used through DocumentParsers v0.3.0) is reserved for the core package only.
- Standard sequence: finish work on `release/vX.Y.Z` → add release-notes section + bump `<Version>` → "Release <Package> vX.Y.Z" anchor commit → tag → publish → merge to main → `git push origin main <tag>`. Merge **after** publish so main only reflects shipped state.

**Release notes** live in per-package files at the repo root:
- `RELEASENOTES.DocumentParsers.md`
- `RELEASENOTES.DocumentParsers.Imaging.md`
- `RELEASENOTES.DocumentParsers.Ocr.md`
- `RELEASENOTES.DocumentParsers.Audio.md`

Newest version first, with sections: `### Added` / `### Changed` / `### Why` / `### Migration`. The csproj `<PackageReleaseNotes>` is a one-paragraph summary intended for nuget.org; the full Markdown lives in these files.

## Related repositories

- [`fieldcure/fieldcure-whisper-runtimes`](https://github.com/fieldcure/fieldcure-whisper-runtimes) — Whisper.net native runtime binaries (CPU/CUDA/Vulkan) repackaged into GitHub Releases. Audio v0.3+ fetches from here.
- [`fieldcure/fieldcure-mcp-rag`](https://github.com/fieldcure/fieldcure-mcp-rag) — MCP RAG server. Primary downstream consumer of DocumentParsers (and, from v2.4 onward, DocumentParsers.Audio).
- [`fieldcure/fieldcure-assiststudio`](https://github.com/fieldcure/fieldcure-assiststudio) — WinUI 3 chat application. For audio, AssistStudio v1.0+ uses provider-native paths (Gemini 1.5+, gpt-4o-audio) rather than transcription.
