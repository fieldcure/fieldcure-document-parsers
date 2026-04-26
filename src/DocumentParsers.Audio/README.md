# FieldCure.DocumentParsers.Audio

Audio transcription support for `FieldCure.DocumentParsers`.

This package adds an `IDocumentParser` for `.mp3`, `.wav`, `.m4a`, `.ogg`,
`.flac`, and `.webm` files. Audio is converted to 16 kHz mono PCM WAV with
NAudio, then transcribed with Whisper.net into timestamped Markdown for RAG
pipelines.

## Install

```bash
dotnet add package FieldCure.DocumentParsers.Audio
```

## Usage

```csharp
using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Audio;

await using var transcriber = DocumentParserFactoryAudioExtensions.AddAudioSupport();

var parser = DocumentParserFactory.GetParser(".mp3")!;
var markdown = parser.ExtractText(File.ReadAllBytes("meeting.mp3"));
```

For explicit options, instantiate `AudioDocumentParser` directly:

```csharp
var parser = new AudioDocumentParser();
var markdown = parser.ExtractText(
    File.ReadAllBytes("meeting.m4a"),
    new AudioExtractionOptions
    {
        SourceExtension = ".m4a",
        Language = "ko",
        ModelSize = WhisperModelSize.Base,
        IncludeTimestamps = true
    });
```

Set `ModelPath` to use a pre-downloaded ggml model. Otherwise the default model
provider downloads the selected model from Hugging Face on first use and caches
it under `{UserProfile}/.fieldcure/whisper-models/`.

## Lifecycle

`WhisperTranscriber` caches the underlying `WhisperFactory` for the model path
in use, so it holds native resources for as long as it lives. Dispose it once at
application shutdown:

```csharp
// Startup
var transcriber = DocumentParserFactoryAudioExtensions.AddAudioSupport();

// Shutdown
await transcriber.DisposeAsync();
```

When you construct `AudioDocumentParser()` with no arguments it owns its
transcriber, and `await using` on the parser disposes it for you.

## Runtime selection (CUDA / Vulkan / CPU)

Whisper.net resolves the native runtime through process-global state
(`Whisper.net.LibraryLoader.RuntimeOptions.RuntimeLibraryOrder`). To override
the default `CUDA → Vulkan → CPU` order, set it **once at startup** rather than
per call:

```csharp
using Whisper.net.LibraryLoader;

// App startup
RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu];
```

`AudioExtractionOptions.RuntimeLibraryOrder` writes to the same global, which
is convenient for one-off configuration but is last-writer-wins under
concurrent calls.

## Notes

- The package is Windows-only in v0.1.0 because audio decoding uses NAudio's
  Media Foundation path for several containers.
- The ggml model is downloaded at runtime; it is not included in the NuGet
  package.
- For tests or cloud transcription, inject a custom `IAudioTranscriber`.
