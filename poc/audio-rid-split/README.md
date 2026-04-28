# PoC: DocumentParsers.Audio RID-split — **Negative result**

**Branch:** `poc/audio-rid-split` (do not merge)
**Status:** Closed. Hypothesis was wrong; preserving as a reference so future investigators don't re-spend time on the same dead end.

## TL;DR

> We hypothesized that `FieldCure.DocumentParsers.Audio` nupkg carries dead Linux/macOS Whisper native bytes that should be trimmed via win-x64 RID-split. **It doesn't.** A 4-variant pack-inspection (run locally, before pushing the matrix to CI) showed every variant produces the same ~150 KB managed-only nupkg with zero `runtimes/` entries. The Whisper native bytes live in the separately-published `Whisper.net.Runtime.*` nupkgs and are pulled into a *consumer's* output (publish or PackAsTool bundle) at restore time via NuGet's transitive resolution — not into our package. Real PoC moved to `poc/mcprag-rid-split` in the `fieldcure-mcp-rag` repo.

## Hypothesis (going in)

The Mcp.Rag v2.3.2 hotfix ran into nuget.org's 250 MB limit when bundling `Whisper.net.Runtime` + `.Cuda` + `.Vulkan` PackageReferences directly into its dotnet-tool nupkg. The user's mental model (and ours) had two distinct contributing problems:

1. **DocumentParsers.Audio nupkg dead weight** — Audio is `[SupportedOSPlatform("windows")]` but the Whisper runtime packages it depends on are multi-platform, so we assumed Audio's own nupkg was carrying Linux/macOS native bytes that would never run anywhere a consumer could use them.
2. **Mcp.Rag PackAsTool 250 MB blow-up** — PackAsTool strips transitive native runtimes, which is why Mcp.Rag had to re-declare `Whisper.net.Runtime.*` directly. That direct ref then bundles all-RID natives into the tool nupkg.

This PoC was meant to address (1). Hypothesis: a win-x64 RID lock on `DocumentParsers.Audio.csproj` would shrink its nupkg and would also be the technique we'd carry to the (2) PoC.

## Method

Four pack variants of `src/DocumentParsers.Audio/DocumentParsers.Audio.csproj`, gated on the MSBuild property `$(PoCVariant)` so production behavior (property unset) is unaffected:

| Variant | Modification |
|---------|--------------|
| **A** | baseline — no extra mods, just version-stamped `0.2.2-poc-A` |
| **B1** | `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` (plural form) |
| **B2** | `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` (singular form) |
| **C**  | `ExcludeAssets="native"` on all three `Whisper.net.Runtime.*` PackageReferences + a `BeforeTargets="GenerateNuspec"` MSBuild target that pulls win-x64 native files via `GeneratePathProperty` and packs them under `runtimes/win-x64/native/` |

Plan was: pack matrix on `windows-latest` GitHub Actions runner, inspect each nupkg's `runtimes/` tree, then add consumer scenarios (Win lib, Linux lib, dotnet tool) in a second iteration.

## Result (local smoke-test, before CI)

A single `dotnet pack` of variant A locally surfaced the disconfirming evidence in seconds:

| Variant | nupkg size | `runtimes/` entries |
|---------|-----------:|--------------------:|
| A | 151,093 bytes (0.14 MB) | **0** |
| B2 | 150,925 bytes (0.14 MB) | **0** |
| C | 151,098 bytes (0.14 MB) | **0** |

(B1 not run — A/B2/C agreement was already enough to invalidate the hypothesis. C surprisingly produced 0 entries too; suspect `ExcludeAssets="native"` empties `$(PkgWhisper_net_Runtime)\runtimes\win-x64\native\` or the `BeforeTargets="GenerateNuspec"` target misfires. Not pursued — see "Why this is moot" below.)

## Why the hypothesis was wrong

`DocumentParsers.Audio` nupkg is **managed-only by design.** Its `<PackageReference>` entries to `Whisper.net.Runtime` / `.Cuda` / `.Vulkan` create *NuGet dependency edges*, not pack-time inlining. NuGet's transitive resolution then pulls those runtime packages into a *consumer's* graph at restore time, and RID resolution decides which `runtimes/<rid>/native/` subset the consumer materializes:

- **Library consumer** (`PackageReference` to Audio in a `net8.0` lib): NuGet's standard build/publish RID resolution picks runtimes based on the consumer's `RuntimeIdentifier` (or all RIDs if unset, populating `bin/.../runtimes/`). Audio's csproj has no influence over this — the consumer governs.
- **PackAsTool consumer** (Mcp.Rag): bypasses the per-RID resolution and bundles whatever `runtimes/` the publish output contains, which is governed by the *consumer's* publish flags, not Audio's pack flags.

So the bytes we wanted to trim live downstream of Audio's nupkg, in places Audio.csproj cannot reach.

### Why C also produced an empty `runtimes/` tree

Variant C tried to actively *add* win-x64 natives to Audio's nupkg via `<None Pack="true">`. Even setting aside whether the target fired correctly, this is the wrong direction: it would *add* bytes to Audio's nupkg without removing anything from consumers (the natives still flow transitively). Diagnosing C's misfire was abandoned.

## What this means for the original problem

The Mcp.Rag 250 MB cap was always a **consumer-side** problem (PackAsTool publish output bloat from all-RID native runtimes), not an Audio-side problem. The two issues we framed at the start collapse into one — and it lives entirely in `Mcp.Rag.csproj`, not `DocumentParsers.Audio.csproj`.

**Real PoC moved to:** `poc/mcprag-rid-split` branch in `fieldcure-mcp-rag` repo.
That PoC tests `<RuntimeIdentifier>` / `<RuntimeIdentifiers>` / `ExcludeAssets="runtime"` / `dotnet publish -r win-x64 → PackAsTool` variants on the consumer side, where the runtime bytes actually accumulate.

## Artifacts in this branch

- `src/DocumentParsers.Audio/DocumentParsers.Audio.csproj` — diff adds inert `$(PoCVariant)` conditional blocks. Property unset = original v0.2.2 behavior, so even if this csproj diff somehow reaches main, production packs are unaffected. **Still: do not merge.**
- `.github/workflows/poc-audio-rid-split.yml` — workflow_dispatch-only (auto-trigger removed since the conclusion is already proven locally; left in place as reproducibility scaffold for anyone who wants to verify on a clean runner).
- `poc/audio-rid-split/README.md` — this document.

## Lessons for future similar investigations

1. **`dotnet pack` is library-pack, not application-bundle.** Native runtimes from transitive `runtimes/<rid>/native/` packages do not end up in a library nupkg unless the csproj explicitly inlines them.
2. **Smoke-test the cheapest variant locally before authoring a CI matrix.** A 30-second `dotnet pack` would have caught the wrong premise before we wrote workflow YAML, README, and four variant blocks.
3. **"`Whisper runtime` 처리"** is shorthand for at least three distinct mechanisms — package authoring, transitive restore RID resolution, and PackAsTool bundling. Conflating them was the root cause of our wrong scoping. The Mcp.Rag follow-up PoC must stay scoped to PackAsTool-side behavior only.
