# FieldCure.DocumentParsers.Audio Release Notes

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
