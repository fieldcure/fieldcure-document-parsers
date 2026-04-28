# PoC: DocumentParsers.Audio v0.3 — Whisper runtime download

**Branch:** `poc/audio-runtime-download`
**Status:** Design spec for review. Implementation lands in a separate `release/v0.3.0` branch once this design is signed off.

This PoC follows two earlier negative-result PoCs that ruled out alternate solutions:

- [`poc/audio-rid-split`](https://github.com/fieldcure/fieldcure-document-parsers/tree/poc/audio-rid-split) (this repo) — DocumentParsers.Audio nupkg trimming was a non-problem; Audio is managed-only.
- [`poc/mcprag-rid-split`](https://github.com/fieldcure/fieldcure-mcp-rag/tree/poc/mcprag-rid-split) (Mcp.Rag repo) — single-nupkg GPU recovery is mathematically impossible at nuget.org's 250 MB cap once cross-platform Sqlite (~120 MB) is preserved.

PoC 0 (desk research) validated the prerequisites for this design:
- `Whisper.net` 1.9.0's `RuntimeOptions.LibraryPath` static property routes the loader to a custom directory. Verified against the local `whisper.net` repo at tag `1.9.0`.
- NVIDIA CUDA Toolkit EULA Attachment A permits self-hosting `cudart64_*.dll`, `cublas64_*.dll`, `cublasLt64_*.dll` etc. on GitHub Releases. `nvcuda.dll` is not on the redistributable list (driver-shipped, lives in `System32`).

## TL;DR

> Move GPU runtime ownership from build-time bundling to runtime download. v0.3 ships only the CPU Whisper runtime (or zero runtimes); Cuda and Vulkan binaries are fetched from `fieldcure/whisper-runtimes` GitHub Releases on first GPU use, cached under `%LOCALAPPDATA%\FieldCure\WhisperRuntimes\`, and activated via `RuntimeOptions.LibraryPath`. Enables Mcp.Rag v2.4.0 to ship under nuget.org's 250 MB cap *with* GPU acceleration restored, and any future Audio consumer benefits without re-solving the problem.

## Three-phase capability lifecycle

The single biggest design clarity gain over v0.2.2: separate **Detect** (system inspection) from **Provision** (binary acquisition) from **Activate** (Whisper.net configuration). v0.2.2's `WhisperEnvironment.Probe()` conflates Detect with implicit Activate.

### Phase 1 — Detect (system inspection)

Read-only checks against the host. No side effects, no I/O outside `System32`. Same shape as v0.2.2's `Probe()`.

```csharp
public static class WhisperEnvironment
{
    public static WhisperEnvironmentInfo Detect();   // renamed from Probe()
}

public sealed record WhisperEnvironmentInfo(
    bool CudaDriverAvailable,    // nvcuda.dll exists in System32 (driver-installed)
    bool VulkanDriverAvailable,  // vulkan-1.dll exists in System32 (loader-installed)
    long SystemRamBytes,
    int LogicalCores);
```

**Critical contract change from v0.2.2:** `CudaAvailable` → `CudaDriverAvailable`. The v0.2.2 name conflated "GPU usable" with "driver present" — v0.3 enforces that this property answers *only* "does the host have a CUDA-capable driver?" The "GPU usable now?" question is answered by `WhisperRuntime.GetActivationStatus()` (see Phase 3).

`Probe()` is kept as a `[Obsolete]` alias that calls `Detect()` for one minor version, then removed in v0.4.

### Phase 2 — Provision (binary acquisition)

Async, idempotent, side-effecting. Acquires the redistributable native binaries needed for a chosen runtime variant. Mirrors `WhisperModelProvider`'s structure exactly: per-path `SemaphoreSlim`, `.download` temp file + atomic `File.Move`, idempotent pre-check.

```csharp
public interface IWhisperRuntimeProvisioner
{
    /// <summary>Local cache directory containing provisioned runtime variants.</summary>
    string CacheDirectory { get; }

    /// <summary>True if all native files for <paramref name="variant"/> are already cached.</summary>
    bool IsProvisioned(WhisperRuntimeVariant variant);

    /// <summary>Ensures all native files for <paramref name="variant"/> are present in the cache,
    /// downloading from GitHub Releases if necessary. Idempotent and safe under concurrent calls.</summary>
    Task ProvisionAsync(
        WhisperRuntimeVariant variant,
        CancellationToken cancellationToken = default,
        IProgress<WhisperRuntimeProgress>? progress = null);
}

public enum WhisperRuntimeVariant { Cpu, Cuda, Vulkan }

public sealed record WhisperRuntimeProgress(
    WhisperRuntimePhase Phase,
    string CurrentFile,
    long BytesReceived,
    long? BytesTotal);  // null when chunked-encoding response has no Content-Length

public enum WhisperRuntimePhase { Resolving, Downloading, Verifying, Activating }
```

**Default provisioner** (`GitHubReleasesWhisperRuntimeProvisioner`) fetches from a manifest at a pinned URL:

```
https://github.com/fieldcure/whisper-runtimes/releases/download/v1.9.0/manifest.json
```

Manifest format (sketch — lock down before first release):

```json
{
  "schemaVersion": 1,
  "whisperNetRuntimeVersion": "1.9.0",
  "variants": {
    "cpu": {
      "win-x64": [
        { "name": "whisper.dll",            "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "ggml-base-whisper.dll",  "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "ggml-cpu-whisper.dll",   "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "ggml-whisper.dll",       "url": "...", "sha256": "...", "bytes": 0 }
      ]
    },
    "cuda": {
      "win-x64": [
        { "name": "whisper.dll",                   "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "ggml-cuda-whisper.dll",         "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "cudart64_12.dll",               "url": "...", "sha256": "...", "bytes": 0, "nvidiaRedist": true },
        { "name": "cublas64_12.dll",               "url": "...", "sha256": "...", "bytes": 0, "nvidiaRedist": true },
        { "name": "cublasLt64_12.dll",             "url": "...", "sha256": "...", "bytes": 0, "nvidiaRedist": true }
      ]
    },
    "vulkan": {
      "win-x64": [
        { "name": "whisper.dll",            "url": "...", "sha256": "...", "bytes": 0 },
        { "name": "ggml-vulkan-whisper.dll", "url": "...", "sha256": "...", "bytes": 0 }
      ]
    }
  }
}
```

Layout in the cache directory (matches Whisper.net's expected probe paths):

```
%LOCALAPPDATA%\FieldCure\WhisperRuntimes\
├── manifest.json                    # last-known good copy, used offline
└── runtimes\
    ├── win-x64\                     # CPU
    │   ├── whisper.dll
    │   └── ggml-*.dll
    ├── cuda\
    │   └── win-x64\
    │       ├── whisper.dll
    │       ├── ggml-cuda-whisper.dll
    │       └── cudart64_12.dll, cublas64_12.dll, cublasLt64_12.dll
    └── vulkan\
        └── win-x64\
            ├── whisper.dll
            └── ggml-vulkan-whisper.dll
```

**Concurrency model:** identical to `WhisperModelProvider`. Per-file `SemaphoreSlim` keyed by absolute target path. Two parallel `ProvisionAsync(Cuda)` calls: one downloads, the other waits at the gate, both observe the file present on completion. Cross-process: if two Mcp.Rag instances on the same machine race, both write to `<file>.download` (each gets its own random GUID-suffixed temp), one wins the `File.Move(overwrite: true)`, the other's move overwrites with byte-identical content. Acceptable.

**Hash verification:** every downloaded file checked against `sha256` from the manifest before `File.Move` to final location. Mismatch → delete temp, throw `WhisperRuntimeIntegrityException`. No partial writes ever land in the cache.

**`bytesTotal` reporting:** prefer `Content-Length` from response headers when available. GitHub Releases serves these reliably for static assets. Manifest's `bytes` field is the authoritative size for verification but not displayed to user (post-download).

**Cancellation:** standard `CancellationToken` propagation. Mid-download cancel → `.download` temp orphaned; `ProvisionAsync` retry on next call deletes orphan and restarts. (Same pattern as `WhisperModelProvider:71-75`.)

### Phase 3 — Activate (Whisper.net configuration)

Synchronous, runs once per process. Sets `RuntimeOptions.LibraryPath` and `RuntimeOptions.RuntimeLibraryOrder` to point Whisper.net at the cache directory.

```csharp
public static class WhisperRuntime
{
    /// <summary>Idempotent. Sets RuntimeOptions.LibraryPath if not already set,
    /// and adjusts RuntimeLibraryOrder based on which variants are provisioned.</summary>
    public static void Activate(IWhisperRuntimeProvisioner provisioner);

    /// <summary>Reports which runtime variants are usable right now (provisioned + driver present).</summary>
    public static WhisperActivationStatus GetActivationStatus(IWhisperRuntimeProvisioner provisioner);
}

public sealed record WhisperActivationStatus(
    bool CpuUsable,
    bool CudaUsable,    // Driver present AND cuda variant provisioned
    bool VulkanUsable,  // Driver present AND vulkan variant provisioned
    string LibraryPath);
```

**Ordering nuance:** `RuntimeLibraryOrder` defaults to `[Cuda, Vulkan, CoreML, OpenVino, Cpu, CpuNoAvx]`. Whisper.net iterates this order and picks the first variant whose `runtimes/<flavor>/<rid>/whisper.dll` exists in the search paths. So the Activate phase doesn't have to "pick" a runtime — it just makes the right ones discoverable. Whisper.net's loader handles the cascade automatically.

**Detect-without-Provision implications:** if `CudaDriverAvailable=true` but Cuda variant not yet provisioned (cache empty), Whisper.net's loader will skip Cuda (no `runtimes/cuda/win-x64/whisper.dll` file present) and fall through to Cpu. This is the desired graceful behavior — no exception, just CPU performance until first explicit `ProvisionAsync(Cuda)`.

## Lazy provisioning in `ExtractText`

The user-facing trigger. `AudioDocumentParser.ExtractText` is the one chokepoint that needs to wire the lifecycle:

```csharp
// Pseudocode — actual implementation reuses existing AudioDocumentParser flow.
public string ExtractText(byte[] data)
{
    var info = WhisperEnvironment.Detect();
    var preferred = SelectPreferredVariant(info);  // Cuda > Vulkan > Cpu, gated by *Available

    if (!_provisioner.IsProvisioned(preferred))
    {
        // Synchronous wait; this is a synchronous method and consumers expect it to block.
        // Mcp.Rag's IndexingEngine batches large jobs so the first-time download cost
        // is amortized across the batch.
        _provisioner.ProvisionAsync(preferred,
                                    progress: _options.ProgressCallback)
                    .GetAwaiter().GetResult();
    }

    WhisperRuntime.Activate(_provisioner);
    return TranscribeInternal(data);
}
```

Trade-offs of synchronous-wait inside `ExtractText`:
- **Pro:** keeps `IDocumentParser.ExtractText` signature unchanged. No async pollution into `DocumentParserFactory` or any consumer.
- **Pro:** matches existing `WhisperModelProvider` pattern (model download is also lazy at first transcription).
- **Con:** first call blocks for ~120 MB download. Acceptable for Mcp.Rag's batch indexing context (`exec` runner). Not acceptable for an interactive UI directly calling `ExtractText` — but per the v1.0 scope decision, AssistStudio's ComposeBar audio path goes through `NativeAudio` (provider-side), not transcription. So no UI consumer hits this path.

## Progress callback API

```csharp
public sealed class AudioExtractionOptions
{
    // ... existing fields ...

    /// <summary>Optional reporter for runtime download / activation progress.
    /// Fires from Phase 2 (Provision) only. Phase 1 (Detect) is sub-millisecond,
    /// Phase 3 (Activate) is sub-millisecond. Whisper inference progress
    /// (transcription itself) is not exposed here — that flows through Whisper.net's
    /// own segment callback.</summary>
    public IProgress<WhisperRuntimeProgress>? ProgressCallback { get; set; }
}
```

`IProgress<T>` chosen over a raw `Action<T>` delegate because `IProgress<T>` is the standard .NET pattern for cross-thread progress reporting (UI updates land on the captured `SynchronizationContext`). Mcp.Rag's logging consumer can pass `new Progress<WhisperRuntimeProgress>(p => logger.Log(...))`.

**Frequency contract:** progress fires at most once per file (Resolving), then once per ~256 KB during Downloading, then once per file (Verifying), then once total (Activating). Bounded — won't flood a slow consumer.

## Environment override: `FIELDCURE_WHISPER_RUNTIME_DIR`

For air-gapped / corporate-proxy environments where outbound HTTP to `github.com` is blocked. If set, the provisioner:
1. Skips network entirely. `ProvisionAsync` becomes a pure cache lookup.
2. Treats the override directory as a pre-staged drop. Layout must match the cache layout above.
3. `IsProvisioned(variant)` returns true iff every file from the manifest exists in `<override>/runtimes/<flavor>/<rid>/`.

If `IsProvisioned` returns false in override mode, `ProvisionAsync` throws `WhisperRuntimeMissingException` with the missing file list — no fallback download attempted. Operator's responsibility to populate the directory.

Override semantics:
```
FIELDCURE_WHISPER_RUNTIME_DIR=D:\offline\whisper-runtimes
  → CacheDirectory = "D:\offline\whisper-runtimes"
  → No HTTP. No manifest fetch. Manifest must be present locally too (loaded for hash verification).
```

If env var unset → standard path (`%LOCALAPPDATA%\FieldCure\WhisperRuntimes\`) and online manifest fetch.

## v0.3 csproj changes

```xml
<ItemGroup>
  <ProjectReference Include="..\DocumentParsers\DocumentParsers.csproj" />
  <PackageReference Include="NAudio" Version="2.2.1" />
  <PackageReference Include="NAudio.Vorbis" Version="1.5.0" />
  <PackageReference Include="Whisper.net" Version="1.9.0" />

  <!-- v0.3: Whisper.net.Runtime CPU only (~7 MB win-x64). Cuda/Vulkan removed —
       fetched at runtime from GitHub Releases. -->
  <PackageReference Include="Whisper.net.Runtime" Version="1.9.0" />
</ItemGroup>
```

`Whisper.net.Runtime.Cuda` and `.Vulkan` PackageReferences removed.

**Open question:** include CPU runtime in nupkg, or also runtime-download it? Including keeps the package self-sufficient for the most common case (no GPU host). Keeps nupkg manageable (~7 MB). Recommend **include CPU**.

## Open questions for design review

1. **GitHub Releases vs nuget.org-fetch fallback.** PoC 0 ranked options as B (nuget.org) > C (add-on package) > A (manual install) under license-blocker assumption. License is *not* blocked, so primary path is GitHub Releases. Should we still wire B as a fallback when GH Releases is unreachable (rate-limit, regional block)? Recommend **no for v0.3, yes for v0.4** — keep PoC scope tight; B fallback is a 200-line add when needed.

2. **Manifest hosting and version pinning.** Manifest URL pinned to `v1.9.0` (matching Whisper.net version). When we upgrade to Whisper.net 2.x, do we cut a new manifest tag (`v2.0.0`) or version the manifest schema independently? Recommend **manifest version tracks Whisper.net version** for v0.3; revisit if we ever support multiple Whisper.net versions in one Audio release.

3. **Cuda variant selection: which CUDA major?** `Whisper.net.Runtime.Cuda` 1.9.0 ships against CUDA 12.x runtime DLLs (`cudart64_12.dll`). If a user's NVIDIA driver is older than the minimum compatible version (CUDA 12.0 requires driver R525+), Cuda activation will fail at `whisper.dll` load. Recommend **detect driver version in Phase 1**, expose `CudaDriverVersion` in `WhisperEnvironmentInfo`, gate `CudaUsable` on minimum threshold. Implementation: read driver version from registry `HKLM\SOFTWARE\NVIDIA Corporation\Global\NVTweak\Devices\NvAPI` or call `nvcuda!cuDriverGetVersion`.

4. **Multi-process cache races.** Two Mcp.Rag instances starting simultaneously, both detecting Cuda needed, both calling `ProvisionAsync`. In-process `SemaphoreSlim` doesn't synchronize across processes. Risk: both write `<file>.download.<guid>` then both call `File.Move(overwrite: true)`. Outcome: one move's content is final, the other overwrites with byte-identical content. **Acceptable**, but worth mentioning in design log so future readers don't add a (slower, more fragile) cross-process lock.

5. **Telemetry.** Should provisioning success/failure events be reported anywhere? Recommend **no telemetry in v0.3**; logger output via `IProgress<WhisperRuntimeProgress>` is sufficient. Telemetry is a v0.5+ concern if at all.

## What this PoC does NOT do

- **No code on this branch.** This PoC artifact is the design spec only. Implementation lands on `release/v0.3.0` once the design is signed off.
- **No GitHub Releases setup.** Creating the `fieldcure/whisper-runtimes` repo, populating Releases, signing the manifest, and sourcing the actual binary set from Whisper.net's published Cuda/Vulkan packages is implementation work, not design work.
- **No AssistStudio coupling.** v1.0 scope explicitly removed AssistStudio's transcription dependency. AssistStudio audio attachments go through provider-native paths (Gemini 1.5+, gpt-4o-audio). This PoC is consumed only by Mcp.Rag's KB indexing path.

## Implementation sequencing (post-design-review)

If this design is signed off, the v0.3.0 implementation order:

1. New `WhisperRuntime` static class + `IWhisperRuntimeProvisioner` interface (no implementation yet).
2. `GitHubReleasesWhisperRuntimeProvisioner` skeleton — manifest fetch, hash verification, `IsProvisioned` predicate. Test with a hand-rolled local manifest first (no real GH Releases yet).
3. Refactor `WhisperEnvironment.Probe()` → `Detect()`, with `[Obsolete]` shim.
4. Wire `ExtractText` lazy provisioning + `WhisperRuntime.Activate`.
5. Manifest schema + initial Releases set up at `fieldcure/whisper-runtimes`.
6. Integration test: tear down cache dir, call `ExtractText` on a 10-second mp3, assert end-to-end download → transcribe.
7. Mcp.Rag v2.4.0 follow-up: depend on Audio v0.3, drop direct `Whisper.net.Runtime` PackageReference, restore `.Cuda` / `.Vulkan` removal from csproj. Tool nupkg returns to ~138 MB CPU-only with GPU acceleration available on first invocation post-install.

## Decision log

- **2026-04-28:** Path 1 (DocumentParsers.Audio owns runtime download) over Path 2 (Mcp.Rag owns). Cohesion: Audio-related concerns stay in Audio package. Path 2's only pro (single-consumer simplicity) was valid but cohesion ranked higher. Future Audio consumers benefit without re-solving.
- **2026-04-28:** Synchronous wait inside `ExtractText` over async API change. Keeps `IDocumentParser` signature stable. Mcp.Rag's batch indexing context tolerates the first-call latency. AssistStudio doesn't hit this path (NativeAudio decision).
- **2026-04-28:** `IProgress<T>` over `Action<T>` for progress. Standard .NET pattern; cross-thread safe with captured `SynchronizationContext`.
- **2026-04-28:** GitHub Releases over NuGet-fetch fallback for v0.3. License-not-blocked makes GH Releases the primary; nuget.org fallback deferred to v0.4 if needed.
- **2026-04-28:** Manifest version tracks Whisper.net version (`v1.9.0`). Independent versioning deferred until multi-version support is real.
