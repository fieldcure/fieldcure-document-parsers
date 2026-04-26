# FieldCure.DocumentParsers.Audio Release Notes

## v0.1.0 - 2026-04-26

- Added `AudioDocumentParser` for `.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`, and `.webm`.
- Added `IAudioTranscriber` plus a default Whisper.net-backed `WhisperTranscriber`.
- Added `WhisperModelProvider` for runtime ggml model download and caching under `{UserProfile}/.fieldcure/whisper-models/`.
- Added NAudio conversion to 16 kHz mono PCM WAV before transcription.
- Added `DocumentParserFactoryAudioExtensions.AddAudioSupport()` for opt-in factory registration.
- Added unit tests using an injected fake transcriber so the package can be tested without model downloads.
