# PoC: DocumentParsers.Audio v0.3 — Whisper runtime download

**Branch:** `poc/audio-runtime-download`
**Status:** **Signed off (2026-04-28).** Q1 / Q4 / Q5 accepted as written. Q2 refined (manifest-vs-actual version mismatch is a warning log, not a hard fail). Q3 refined (minDriverVersion source of truth lives in the manifest, not Audio code). Cosmetic: orphan `.download.*` cleanup added to provisioner constructor; size estimate phrasing tightened. Implementation lands on `release/v0.3.0`.

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
    int? CudaDriverVersion,      // null when driver not installed; otherwise nvcuda!cuDriverGetVersion result
                                 // (e.g. 12030 = CUDA 12.3). Compared against manifest's minDriverVersion
                                 // in Phase 3 to gate CudaUsable. Audio code does NOT hardcode the threshold.
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
      "minDriverVersion": 12000,
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

**Manifest field semantics:**

- `minDriverVersion` (variant-level, integer): minimum NVIDIA driver version, in `cuDriverGetVersion` integer form (e.g. `12000` = CUDA 12.0 = driver R525+, `12030` = CUDA 12.3 = driver R545+). Audio's Phase 3 reads this and gates `CudaUsable` on `WhisperEnvironmentInfo.CudaDriverVersion >= minDriverVersion`. **Source of truth lives in the manifest, not in Audio code.** Future multi-CUDA-major scenario (e.g., shipping both `cuda-12` and `cuda-13` variants) extends naturally — each variant carries its own `minDriverVersion`, Audio picks the highest-compatible at activation time.
- `nvidiaRedist: true` (file-level, boolean): marks files governed by NVIDIA's CUDA Toolkit EULA Attachment A redistributable terms. **Primary use:** at first download of a file with this flag set, the provisioner emits a one-line NVIDIA EULA attribution to stderr (e.g., `[whisper-runtime] cudart64_12.dll: NVIDIA CUDA Toolkit components (https://docs.nvidia.com/cuda/eula/)`). Surfaces the license trail without requiring users to read our docs to discover it. **Secondary use:** audit-style listing (`whisper-runtime --list-redist`) is a trivial derivative for any future operator query, no extra plumbing needed. Files without `nvidiaRedist` (whisper.dll, ggml-*-whisper.dll) are MIT-licensed via Whisper.net.Runtime upstream and need no notice.

**Concurrency model:** identical to `WhisperModelProvider`. Per-file `SemaphoreSlim` keyed by absolute target path. Two parallel `ProvisionAsync(Cuda)` calls: one downloads, the other waits at the gate, both observe the file present on completion. Cross-process: if two Mcp.Rag instances on the same machine race, both write to `<file>.download` (each gets its own random GUID-suffixed temp), one wins the `File.Move(overwrite: true)`, the other's move overwrites with byte-identical content. Acceptable.

**Orphan cleanup at construction:** `WhisperRuntimeProvisioner` constructor sweeps `<CacheDirectory>/runtimes/**/*.download.*` and deletes any matches. Catches the case where a previous process was killed mid-download (Ctrl+C, host shutdown, host OOM). Cheap (file enumeration only, no I/O on success path) and prevents unbounded `.download.<guid>` accumulation across restarts. Fail-soft: enumeration failures (permission denied on a corrupted dir) are swallowed with a debug log.

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
    bool CudaUsable,    // Driver present AND CudaDriverVersion >= manifest's cuda.minDriverVersion AND cuda variant provisioned
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

2. **Manifest hosting and version pinning.** ✅ Resolved. Manifest URL is pinned to the *exact Whisper.net version Audio was tested against* (e.g., `v1.9.0`). Manifest tag bumps follow native ABI changes: major (1.x → 2.x) always cuts a new tag; minor (1.9 → 1.10) almost always; patch (1.9.0 → 1.9.1) usually same natives, occasionally new tag. To handle drift between the Audio package's compiled-against `Whisper.net` version and the manifest's `whisperNetRuntimeVersion` field, Audio compares the two at startup and emits a **warning log** (not a hard fail) on mismatch. Override-mode (`FIELDCURE_WHISPER_RUNTIME_DIR`) bypasses the network manifest fetch but still loads the local manifest copy for hash + version-warning logic.

3. **Cuda variant selection: minimum driver version source of truth.** ✅ Resolved. Lives in the **manifest** (`cuda.minDriverVersion`), not hardcoded in Audio. Audio's contract: read driver version via `nvcuda!cuDriverGetVersion`, expose as `WhisperEnvironmentInfo.CudaDriverVersion`. Phase 3 (`GetActivationStatus`) compares to manifest field and gates `CudaUsable`. This makes the manifest self-contained — `whisper-runtimes` repo owns the policy, Audio just reports environment facts. Future multi-CUDA-major (12 + 13 in one release) extends naturally: each variant carries its own `minDriverVersion`, Audio picks the highest-compatible at activation. Why not the alternative (registry lookup, hardcoded threshold): registry path is fragile across driver versions, and hardcoding forces Audio releases on every CUDA-major bump even when only the manifest needs updating.

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
7. Mcp.Rag v2.4.0 follow-up: depend on Audio v0.3, drop direct `Whisper.net.Runtime` PackageReference, restore `.Cuda` / `.Vulkan` removal from csproj. Tool nupkg lands in the ~140-150 MB range (multi-RID Sqlite + CPU Whisper transitive multi-RID + everything else; exact size confirmed at v2.4.0 pack time), well under the 250 MB cap, with GPU acceleration available on first GPU-using invocation post-install via runtime download.

## Decision log

- **2026-04-28:** Path 1 (DocumentParsers.Audio owns runtime download) over Path 2 (Mcp.Rag owns). Cohesion: Audio-related concerns stay in Audio package. Path 2's only pro (single-consumer simplicity) was valid but cohesion ranked higher. Future Audio consumers benefit without re-solving.
- **2026-04-28:** Synchronous wait inside `ExtractText` over async API change. Keeps `IDocumentParser` signature stable. Mcp.Rag's batch indexing context tolerates the first-call latency. AssistStudio doesn't hit this path (NativeAudio decision).
- **2026-04-28:** `IProgress<T>` over `Action<T>` for progress. Standard .NET pattern; cross-thread safe with captured `SynchronizationContext`.
- **2026-04-28:** GitHub Releases over NuGet-fetch fallback for v0.3. License-not-blocked makes GH Releases the primary; nuget.org fallback deferred to v0.4 if needed.
- **2026-04-28:** Manifest version tracks Whisper.net version (`v1.9.0`). Independent versioning deferred until multi-version support is real.
- **2026-04-28:** Manifest-vs-actual `Whisper.net` version mismatch is a *warning log*, not a fail. Hard fail would block legitimate patch-level upgrades where ABI is unchanged; warning preserves user agency and surfaces telemetry for future investigation.
- **2026-04-28:** Minimum CUDA driver version lives in the manifest (`cuda.minDriverVersion`), not in Audio code. `whisper-runtimes` repo is single source of truth for runtime-selection policy; Audio reports environment facts only. Enables multi-CUDA-major variants without Audio releases.
- **2026-04-28:** `nvidiaRedist: true` flag's primary use is per-file EULA attribution at first download (stderr one-liner). Audit-listing is a derivative use. Files without the flag are MIT-licensed and need no notice.
- **2026-04-28:** `WhisperRuntimeProvisioner` constructor sweeps `<cache>/runtimes/**/*.download.*` orphans on init. Catches mid-download cancellations across process lifetimes; cheap and fail-soft.
