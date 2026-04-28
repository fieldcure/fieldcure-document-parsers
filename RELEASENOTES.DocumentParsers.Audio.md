# FieldCure.DocumentParsers.Audio Release Notes

## v0.3.0 - 2026-04-28

### Added

- **Three-phase capability lifecycle** for Whisper runtime acquisition: `Detect` (host inspection) → `Provision` (binary acquisition) → `Activate` (Whisper.net configuration). Each phase is observable and overridable independently.
- `IWhisperRuntimeProvisioner` — pluggable provisioner abstraction (`GetManifestAsync`, `IsProvisioned`, `ProvisionAsync`, `CacheDirectory`).
- `GitHubReleasesWhisperRuntimeProvisioner` — default implementation that fetches `manifest.json` + per-variant binaries from [`fieldcure-whisper-runtimes`](https://github.com/fieldcure/fieldcure-whisper-runtimes) (GitHub Releases), verifies SHA-256, and caches under `%LOCALAPPDATA%\FieldCure\WhisperRuntimes\`. Supports concurrent processes via per-target `SemaphoreSlim` + atomic `File.Move` overwrite; orphaned `.download.<guid>` temp files are swept on construction.
- `WhisperRuntime.Activate(...)` / `WhisperRuntime.GetActivationStatus()` — explicit configuration of `Whisper.net`'s `RuntimeOptions.LibraryPath` against the cache directory of a chosen variant. Idempotent; the first call wins, subsequent calls report the existing activation.
- `AudioExtractionOptions.ProgressCallback` (`IProgress<WhisperRuntimeProgress>?`) — receives `Resolving` / `Downloading` / `Verifying` phase events with file name and transferred bytes (throttled to ~1 update / 256 KB during download).
- `WhisperEnvironment.Detect()` — successor to `Probe()` with the same return shape plus `CudaDriverVersion`. `Probe()` is retained as an `[Obsolete]` alias and will be removed in v0.4.
- `WhisperEnvironmentInfo.CudaDriverVersion` (int) — driver version reported by `cuDriverGetVersion` (e.g. `12000` = CUDA 12.0). Used to gate the `cuda` variant against the manifest's `minDriverVersion` policy.
- `FIELDCURE_WHISPER_RUNTIME_DIR` environment variable — when set, the provisioner skips all network calls and treats the directory as authoritative (pre-staged manifest + binaries). For air-gapped deployments and CI hermeticity.
- New opt-in `[TestCategory("Integration")]` fixtures `WhisperNetSampleTests` (bush.wav / kennedy.mp3 / multichannel.wav from upstream whisper.net), gated on `FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS=1` + `FIELDCURE_WHISPER_MODEL_PATH`. Default `dotnet test` runs remain offline.

### Changed

- **Cuda and Vulkan native binaries are no longer bundled** in the NuGet package. `Whisper.net.Runtime.Cuda.Windows` and `Whisper.net.Runtime.Vulkan` `PackageReference`s are removed from `DocumentParsers.Audio.csproj`; only the CPU runtime (`Whisper.net.Runtime` 1.9.0) remains in-package. GPU runtimes are fetched on first GPU use from `fieldcure-whisper-runtimes`.
- `WhisperEnvironmentInfo.CudaAvailable` renamed to `CudaDriverAvailable` (the original name now reflects the actual semantics: a driver is detected, not necessarily a usable runtime). The old name is retained as an `[Obsolete]` property aliasing the new one and will be removed in v0.4.

### Why

The package previously bundled all three native runtimes (CPU, CUDA, Vulkan), pushing the unpacked install footprint past 120 MB. Two pressures forced a runtime-download model:

1. **NuGet package-size cap.** Downstream consumers like `FieldCure.Mcp.Rag` (a `dotnet tool` whose own native dependencies — Sqlite cross-RID, ~127 MB — already crowd nuget.org's 250 MB per-package limit) cannot afford to also carry GPU runtimes transitively. Splitting the runtimes off lets each consumer ship without the cap pressure regardless of whether they end up using GPU acceleration.
2. **Pay-for-what-you-use.** Most hosts are CPU-only or use a single GPU vendor. Bundling all variants forced every install to carry ~120 MB of binaries it would never load. Runtime-download means each host fetches at most one GPU variant, and only if the corresponding driver is detected.

### Migration

- **API renames** are source-compatible — the old names still work with deprecation warnings in v0.3 and will be removed in v0.4:
  - `WhisperEnvironment.Probe()` → `WhisperEnvironment.Detect()`
  - `WhisperEnvironmentInfo.CudaAvailable` → `WhisperEnvironmentInfo.CudaDriverAvailable`
- **First GPU transcription per host** triggers a one-time download of the chosen variant to `%LOCALAPPDATA%\FieldCure\WhisperRuntimes\`. Approximate sizes:
  - `cpu` — bundled in the NuGet package (no download)
  - `cuda` (win-x64) — ~75 MB on first CUDA transcription
  - `vulkan` (win-x64) — ~49 MB on first Vulkan transcription

  Subsequent transcriptions reuse the cache. SHA-256 hashes are verified at download time; cached files are not re-hashed on each load.
- **CUDA hosts** must have an NVIDIA driver providing CUDA 12.x runtime (`cudart64_12.dll`, `cublas64_12.dll`, `cublasLt64_12.dll`) on the loader path. The v1.9.0 `fieldcure-whisper-runtimes` release does **not** redistribute these — the upstream `Whisper.net.Runtime.Cuda.Windows` 1.9.0 nupkg doesn't ship them either, and the consumer expects them resolved by the host CUDA runtime. The provisioner gates `cuda` selection on `WhisperEnvironmentInfo.CudaDriverVersion >= 12000` (driver R525+). Hosts below that fall back to `vulkan` if available, else `cpu`.
- **Air-gapped / offline deployments** — pre-stage the cache and set `FIELDCURE_WHISPER_RUNTIME_DIR=<dir>`. The directory must contain `manifest.json` at its root plus `runtimes/<variant>/<rid>/<file>` mirroring the online cache layout. The provisioner performs zero network I/O when this variable is set; missing files raise `WhisperRuntimeMissingException` with the offending names. Layout reference:
  ```
  D:\offline\whisper-runtimes\
  ├── manifest.json
  └── runtimes\
      ├── win-x64\           (cpu)
      ├── cuda\win-x64\
      └── vulkan\win-x64\
  ```
- **Manifest URL** is pinned in `GitHubReleasesWhisperRuntimeProvisioner.DefaultManifestUrl` and resolves to the v1.9.0 release of `fieldcure-whisper-runtimes`. Override via the constructor's `manifestUrl` parameter for forks or staged rollouts.
- **Default cache directory** is `%LOCALAPPDATA%\FieldCure\WhisperRuntimes\` on Windows — sibling to the `WhisperModels\` cache introduced in v0.2.2. No auto-migration from any prior layout (v0.2 never persisted runtime binaries).

---

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
