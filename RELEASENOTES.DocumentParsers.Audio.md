# FieldCure.DocumentParsers.Audio Release Notes

## v0.2.2 - 2026-04-27

### Changed

- **Windows cache directory** moves from `%USERPROFILE%\.fieldcure\whisper-models\` to `%LOCALAPPDATA%\FieldCure\WhisperModels\` to match where the rest of the FieldCure ecosystem stores per-user data (Mcp.Rag KB stores under `%LOCALAPPDATA%\FieldCure\Mcp.Rag\`, AssistStudio runner data under `%LOCALAPPDATA%\FieldCure\AssistStudio\`). The Unix dot-prefixed convention (`~/.fieldcure/whisper-models/`) is unchanged.

### Why

The Unix-style `~/.fieldcure/` path was historically inherited and looked out-of-place on Windows next to the rest of the suite's `%LOCALAPPDATA%\FieldCure\…` data. Moving to `LocalApplicationData` puts all FieldCure per-user data in one place and follows Windows conventions.

### Migration

- **No automatic migration.** First transcription after upgrade re-downloads the model (~150 MB to ~3 GB depending on `WhisperModelSize`).
- **To preserve the existing cache without re-download**, copy or move the contents manually before running:
  ```powershell
  $old = "$env:USERPROFILE\.fieldcure\whisper-models"
  $new = "$env:LOCALAPPDATA\FieldCure\WhisperModels"
  if (Test-Path $old) {
      New-Item -ItemType Directory -Force -Path $new | Out-Null
      Move-Item "$old\*" $new
      Remove-Item $old   # optional cleanup
  }
  ```
- Unix users: nothing to do.
- The `%USERPROFILE%\.fieldcure\` directory is no longer used by this package on Windows after the move; safe to delete once empty.

---

## v0.2.1 - 2026-04-27

### Changed

- `WhisperModelSize.Large` now resolves to `GgmlType.LargeV2` instead of `GgmlType.LargeV3`. The cache filename moves from `ggml-large-v3.bin` to `ggml-large-v2.bin` accordingly.

### Why

Empirical measurement on three audio classes (Korean studio TTS clips, 16-min English broadcast, 40-min English documentary with music) reproduced the well-documented large-v3 long-form repetition-loop failure: the V3 weights enter an infinite-repetition state on long-form audio regardless of decoder configuration. `WithNoContext()` and the standard temperature-fallback chain (`WithTemperatureInc(0.2)` + `WithLogProbThreshold(-1.0)` + `WithEntropyThreshold(2.4)`) both had no effect. Switching to large-v2 weights resolves the loop while leaving Korean clean-clip quality essentially unchanged at the semantic level (~1.1 % CER difference, mostly Korean spacing and Arabic-numeral / Hangul-numeral surface conventions). See `tools/AudioBenchmark/baseline-2026-04-27.md` and `tools/AudioBenchmark/results/2026-04-27-v2-validation.csv` for raw numbers.

### Migration

- **Cache cleanup**: existing installations carry `~/.fieldcure/whisper-models/ggml-large-v3.bin` (≈ 3 GB) which is no longer referenced. Delete manually if disk space matters; the package itself never reads the file again. The replacement `ggml-large-v2.bin` (≈ 3 GB) is downloaded on first transcription after upgrade.
- **Quality**: long-form WER on this v0.2.1 baseline drops from 100 %+ (looped) to ~2.5 % on the test corpus. Korean short-clip CER may differ ~1.1 % vs prior V3 output; review existing transcripts if exact-match comparisons matter.
- **API**: no public surface changes. `WhisperModelSize` enum, `AudioExtractionOptions`, `WhisperEnvironment.RecommendModelSize` all unchanged.

## v0.2.0 - 2026-04-27

- Added `WhisperEnvironment` — Windows-only probe that detects CUDA / Vulkan availability (`nvcuda.dll` / `vulkan-1.dll`), physical RAM (`GlobalMemoryStatusEx`), and logical cores, then recommends a Whisper model size (`Tiny`→`Large`) via a tiered matrix. VRAM probing is intentionally deferred to a future release.
- Added `QualityBias` enum (`Balanced` / `Accuracy` / `Speed`) and `WhisperEnvironmentInfo` record exposed by `WhisperEnvironment.Probe()` for diagnostics.
- Added `AudioExtractionOptions.WithModelSize(WhisperModelSize)` — class-friendly partial-override helper that returns a copy of the options with the specified model size. Provided because `AudioExtractionOptions` is a class (not a record) and core `ExtractionOptions` uses `init`-only properties; the helper is paired with a reflection-based regression test that fails if a new init property is added without being appended to the copy.

## v0.1.0 - 2026-04-26

- Added `AudioDocumentParser` for `.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`, and `.webm`.
- Added `IAudioTranscriber` plus a default Whisper.net-backed `WhisperTranscriber`.
- Added `WhisperModelProvider` for runtime ggml model download and caching under `{UserProfile}/.fieldcure/whisper-models/`.
- Added NAudio conversion to 16 kHz mono PCM WAV before transcription.
- Added `DocumentParserFactoryAudioExtensions.AddAudioSupport()` for opt-in factory registration.
- Added unit tests using an injected fake transcriber so the package can be tested without model downloads.
