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

`WhisperModelSize.Large` resolves to **ggml-large-v2** (≈ 3 GB) — large-v3
hallucinates repetition loops on long-form audio, so v2 is the safer default.
See `RELEASENOTES.DocumentParsers.Audio.md` for the v0.2.1 entry behind this
choice.

## Environment-aware model selection

Instead of hard-coding a `ModelSize`, let the library pick one based on the
detected GPU / RAM / cores of the host:

```csharp
using FieldCure.DocumentParsers.Audio;

var recommended = WhisperEnvironment.RecommendModelSize(); // QualityBias.Accuracy default
var options = AudioExtractionOptions.Default.WithModelSize(recommended);

// Diagnostic snapshot (CUDA/Vulkan flags, RAM, cores)
var probe = WhisperEnvironment.Probe();
Console.Error.WriteLine(
    $"[Audio] CUDA={probe.CudaAvailable} Vulkan={probe.VulkanAvailable} " +
    $"RAM={probe.SystemRamBytes / (1024L * 1024 * 1024)}GB → {recommended}");
```

The balanced matrix (used directly by `QualityBias.Balanced`):

| Environment | Recommended model |
|---|---|
| GPU available, RAM ≥ 16 GB | `Large` |
| GPU available, RAM ≥ 8 GB | `Medium` |
| CPU only, RAM ≥ 16 GB, cores ≥ 8 | `Small` |
| CPU only, RAM ≥ 8 GB | `Base` |
| Otherwise | `Tiny` |

`QualityBias.Accuracy` (default) shifts the recommendation one tier up — fits
batch indexing where transcription latency is acceptable. `QualityBias.Speed`
shifts one tier down for interactive flows.

`AudioExtractionOptions.WithModelSize(WhisperModelSize)` is a class-friendly
substitute for the `with` expression syntax (the type is a class, not a
record), useful when downstream layers want to override only the model size
while preserving the rest of the options.

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

- The package is **Windows-only** because audio decoding uses NAudio's
  Media Foundation path for several containers. The assembly carries
  `[SupportedOSPlatform("windows")]`.
- The ggml model is downloaded at runtime; it is not included in the NuGet
  package.
- For tests or cloud transcription, inject a custom `IAudioTranscriber`.
