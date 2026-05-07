# Release Notes — FieldCure.DocumentParsers.Ocr

## [1.1.0] - 2026-05-07

### Added
- **win-arm64 native binaries.** Adds `tesseract50.dll` + `leptonica-1.82.0.dll` ARM64 builds checked in at `src/DocumentParsers.Ocr/native/win-arm64/` and packaged at `arm64/` in the nupkg. `build/FieldCure.DocumentParsers.Ocr.targets` selects the correct-arch DLLs at consumer build time by detecting host RID via `$(NETCoreSdkRuntimeIdentifier)` (with `$(Platform)`, `$(RuntimeIdentifier)`, `$(PlatformTarget)` as override paths for explicit-arch builds on cross-arch hosts).
- `scripts/build-tesseract-arm64.ps1` — reproducibility script that builds + stages + PE-patches + signs the ARM64 DLLs via vcpkg.
- `scripts/vcpkg-overlay/` — slim overlay-port for tesseract (libcurl + libarchive disabled) and a custom `arm64-windows-mixed` triplet that statically links image format dependencies into leptonica. Resulting payload: 2 DLLs / ~7MB, matching the upstream x64 footprint.
- CI matrix: `windows-11-arm` runner alongside `windows-latest`, both running the full test suite.

### Why
AssistStudio v1.x is moving to ARM64. The upstream `Tesseract` 5.2.0 NuGet ships only x64 native binaries — there is no published win-arm64 path. Rather than wait on upstream or fork the .NET wrapper, we build the natives ourselves from source on a pinned vcpkg baseline and embed them in this package.

### Compatibility notes
- ARM64 ships **Tesseract 5.5.2 native + Leptonica 1.87.0 (filename leptonica-1.82.0.dll)**; x64 is unchanged at **Tesseract 5.0 native + Leptonica 1.82.0** (redistributed from the upstream Tesseract NuGet 5.2.0). The Tesseract C ABI is additive across 5.0 -> 5.5 (no symbol removals or signature changes), so the wrapper's `[DllImport]` surface is fully compatible. The split exists because we want to keep the proven x64 path under upstream maintenance rather than self-build both.
- The Tesseract.NET wrapper (`charlesw/tesseract`) hardcodes the native filenames `leptonica-1.82.0` and `tesseract50` in `Tesseract.Interop.Constants`. To match: the ARM64 leptonica is renamed from `leptonica-1.87.0.dll` to `leptonica-1.82.0.dll` (filename only — internal Leptonica version stays 1.87.0), and `tesseract50.dll`'s PE import directory is patched in-place from `leptonica-1.87.0.dll` to `leptonica-1.82.0.dll` (same byte length, surgical edit) before signing.
- The wrapper also hard-codes its native lookup at `<baseDir>\x64\` for any 64-bit process — no arch detection. We honor that by landing ARM64 binaries at the consumer's output `x64\` folder regardless of architecture; the folder name is wrapper-driven, not arch-driven.
- ARM64 DLLs are Authenticode-signed by Fieldcure Co., Ltd. (GlobalSign EV).

### Migration
None. Existing x64 consumers see no change. ARM64 consumers get native support automatically once they build on an ARM64 host or pass `Platform=ARM64` / `RuntimeIdentifier=win-arm64`.

## [1.0.0] - 2026-04-20

### Added
Initial release. This package is the successor to the deprecated
`FieldCure.DocumentParsers.Pdf.Ocr` (final version 1.0.1). From a NuGet
perspective this is a brand-new package ID, so versioning restarts at 1.0.0.

- `OcrPdfParser : IDocumentParser` — PdfPig text extraction with OCR fallback for pages lacking a text layer. Replaces the old `PdfParser(IOcrEngine)` constructor overload.
- `IOcrEngine` interface (moved here from the deprecated `.Pdf` package).
- `TesseractOcrEngine` with embedded English + Korean tessdata (`tessdata_fast`), automatic language discovery, and an engine pool (`ConcurrentBag` + `SemaphoreSlim`, default size `min(ProcessorCount, 4)`).
- Korean post-processing: removes spurious inter-character spaces from Tesseract output.
- `DocumentParserFactoryOcrExtensions.AddOcrSupport()` (creates and returns a `TesseractOcrEngine`) and `AddOcrSupport(IOcrEngine)` (caller-supplied engine).
- Tesseract native DLLs (`leptonica-1.82.0.dll`, `tesseract50.dll`) bundled with `build/` + `buildTransitive/` targets for `PackAsTool` consumers.

### Dependencies
- `FieldCure.DocumentParsers.Imaging` — page rendering via PDFium.
- `Tesseract` 5.2.0 — OCR engine + native binaries.

### Platform
Windows only. The bundled Tesseract 5.2.0 ships native Windows binaries
(`leptonica-1.82.0.dll`, `tesseract50.dll`). The assembly is marked
`[SupportedOSPlatform("windows")]`, so cross-platform consumers will see
CA1416 warnings at compile time — better a compile-time nudge than a runtime
`DllNotFoundException`. Linux / macOS support is planned for a future release
(will coexist with the current `x64/` + `build/*.targets` layout via additional
`runtimes/<rid>/native/` entries).

### Migration from `FieldCure.DocumentParsers.Pdf.Ocr` 1.0.1
```csharp
// Before
using FieldCure.DocumentParsers.Pdf.Ocr;
using var engine = DocumentParserFactoryOcrExtensions.AddPdfOcrSupport();

// After
using FieldCure.DocumentParsers.Ocr;
using var engine = DocumentParserFactoryOcrExtensions.AddOcrSupport();
```
