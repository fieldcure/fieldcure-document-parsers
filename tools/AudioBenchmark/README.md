# AudioBenchmark

Internal measurement tool for validating Whisper model recommendations in
`FieldCure.DocumentParsers.Audio`. Not shipped in release artifacts.

## Why this tool exists

`WhisperEnvironment.RecommendModelSize` ships a tiered matrix that maps the
detected GPU/RAM/cores of a host to a Whisper model size (`Tiny`→`Large`).
The matrix is calibrated against published whisper.cpp performance
characteristics, but those numbers vary by audio length, language, decoder
defaults, and the specific weights backing the size enum. This tool reproduces
those variables under a single CLI so the matrix can be revisited when
something changes — a Whisper.net upgrade, a new ggml release, a different
host class, or unexpected behaviour from production audio.

The first pass produced [`baseline-2026-04-27.md`](baseline-2026-04-27.md) and
the v0.2.1 decision to map `WhisperModelSize.Large` to `GgmlType.LargeV2`.
The CSVs under [`results/`](results/) are the raw evidence behind that
decision and stay committed for future audits.

## Sample preparation

The benchmark accepts a directory of audio files plus optional ground-truth
text. Layout convention:

```
<sample-dir>/
  myclip.mp3
  myclip.gt.txt        # optional — enables WER/CER columns
  another.wav
  another.gt.txt
```

- Supported audio: `.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`, `.webm`
  (anything `AudioConverter.ToPcm16kMono` accepts).
- Ground truth: same basename with `.gt.txt` suffix. UTF-8, NFC-normalized
  text. Punctuation, spacing, and case differences are normalized away by
  `TranscriptionAccuracy` before comparison, so a transcript-style format
  is fine — no need to match Whisper's casing or punctuation policy.
- Files without a paired `.gt.txt` still get measured (RTF, peak memory,
  cold-start) but the WER/CER columns stay empty.

Two helper scripts stage well-known datasets into a temp directory with
generated GT files (without modifying the source tree):

- `stage-kss.ps1` — Korean Single Speaker dataset. Picks an evenly-spaced
  subset (default 15 files) and writes NFC-normalized GT from column 2 of
  `transcript.v.1.4.txt`.
- `stage-bbc-five-wonders.ps1` — Strips YouTube timestamp lines from a
  paired script file and writes the cleaned GT alongside the BBC
  documentary MP3.

Audio samples themselves are deliberately not committed to this repo —
copyright status varies (audiobook narration, broadcast recordings,
YouTube extracts). See [`samples/README-samples.md`](samples/README-samples.md)
for the source metadata catalog.

## Running

```
AudioBenchmark <input-directory> [options]

Required:
  <input-directory>    Path to directory of audio files (and optional .gt.txt)

Common options:
  --models <list>      Comma-separated WhisperModelSize values.
                       Default: Tiny,Base,Small,Medium,Large
  --output <path>      CSV output path. Default: ./results.csv
  --transcripts <dir>  Per-(file,model) transcript directory.
                       Default: ./transcripts
  --no-warmup          Disable cold-start warmup throwaway pass.

Diagnostic options (bypass WhisperTranscriber, call Whisper.net directly):
  --no-context              Apply WithNoContext() (condition_on_previous_text=false).
  --fallback <inc|full>     Enable temperature fallback chain.
                            inc  = WithTemperatureInc(0.2) only.
                            full = TempInc + LogProbThreshold(-1.0) + EntropyThreshold(2.4).
  --ggml <type>             Override the ggml model variant. Accepts any
                            Whisper.net GgmlType name (LargeV1, LargeV2,
                            LargeV3, LargeV3Turbo, ...). Backs the chosen
                            WhisperModelSize with different weights.
  --transcript-suffix <s>   Append "__<s>" to transcript filenames so an
                            A/B run does not overwrite the previous arm.

Other:
  --probe-api          Print Whisper.net's WhisperProcessorBuilder + GgmlType
                       surface to stdout and exit. No transcription performed.
```

### A/B example

```powershell
# default arm
dotnet run -c Release -- C:\samples --models Large --output ./default.csv

# no-context arm
dotnet run -c Release -- C:\samples --models Large --no-context `
    --transcript-suffix nocontext --output ./nocontext.csv
```

## Output interpretation

Each row in the results CSV represents one (audio file, model, configuration)
measurement.

| Column | Meaning |
|---|---|
| `config` | Self-describing experiment arm (`default`, `no_context`, `fallback_full`, `ggml_largev2`, `ggml_largev3turbo`, ...). |
| `timestamp` | UTC ISO-8601 of measurement start. |
| `file_name` | Source audio filename. |
| `audio_duration_seconds` | Decoded PCM duration. Drives RTF. |
| `model_size` | Public `WhisperModelSize` enum value. Combine with `config` when interpreting Large rows — V2/V3/Turbo all surface as `Large` in this column. |
| `cold_start_seconds` | Warmup pass duration (model download + native runtime load). Recorded once per (config, model) tuple; subsequent rows show 0. |
| `transcription_seconds` | Wall clock for the measured (warm) transcription. |
| `rtf` | `transcription_seconds / audio_duration_seconds`. Sub-1.0 means real-time-or-faster. GPU runs typically land in `0.005–0.15` on this host class. |
| `peak_working_set_mb` | `Process.PeakWorkingSet64 / 1 MB` after the file. Includes Whisper native heap. |
| `transcript_char_count` | Length of the produced transcript. Useful as a loop sentinel — a transcription-loop run inflates this dramatically (e.g. 64 KB instead of 21 KB on a 40-min clip). |
| `runtime_used` | Probe flags (`reported_cuda=...;reported_vulkan=...`). Whisper.net does not expose its resolved native runtime, so this is best-effort. |
| `error` | Exception message; empty on success. |
| `gt_available` | `true` when a paired `.gt.txt` file was found. |
| `wer` | Word Error Rate against GT. Levenshtein on whitespace-tokenized normalized text. Uncapped — values >1.0 are valid (hypothesis longer than reference, e.g. hallucinated repetition). |
| `cer` | Character Error Rate against GT. More meaningful than WER for Korean and other non-space-delimited scripts. |
| `gt_word_count` | Token count of the normalized reference. |

A row with very low `rtf` and high `wer`/`cer` paired with an inflated
`transcript_char_count` is the typical loop signature; spot-check the
matching file under `transcripts/` to confirm.

## Reproducing past results

The committed [`results/`](results/) directory contains every measurement
campaign that informed a decision in `RELEASENOTES.DocumentParsers.Audio.md`.
The companion `baseline-YYYY-MM-DD.md` markdown summarizes the campaign:
goal, host environment, methodology, raw numbers, qualitative spot-checks,
and the resulting decision.

Re-running a campaign requires:

1. Re-stage the same samples under matching basenames (the GT pairing
   is by basename, not full path).
2. Replay the documented CLI invocation. Each `baseline-*.md` quotes the
   exact `dotnet run` lines.
3. Compare new CSV against the archived one — RTF should track within
   ±10% on the same host class; WER/CER should be byte-stable when the
   ggml model and Whisper.net version are unchanged.

## Adding new samples

```
sample-dir/
  myclip.mp3                          # the audio
  myclip.gt.txt                       # optional ground truth
```

Recommended habits:

- Keep audio samples out of the repo (they are gitignored). Note the source
  in `samples/README-samples.md` so future maintainers can re-acquire them.
- Pair every clip with a GT file when the source allows. Without GT, a clip
  contributes only RTF/memory/loop-sentinel signal — no quantitative quality
  comparison.
- For long-form audio, prefer recordings with natural pauses, music
  interludes, and speaker transitions. Loop susceptibility manifests at
  segment boundaries.
- Record sample provenance and approximate licence stance in
  `samples/README-samples.md`. Ground-truth text is also a derivative work
  in some jurisdictions; treat it the same as the source audio.

## Known limitations

- **Windows only.** `FieldCure.DocumentParsers.Audio` and therefore this
  tool is gated by `[SupportedOSPlatform("windows")]`. The NAudio
  Media Foundation resampler is the binding constraint.
- **GT-free quantitative comparison is impossible.** Without `.gt.txt`,
  WER/CER stay empty and only relative timing/memory remains.
- **`runtime_used` is heuristic.** Whisper.net does not expose which
  native runtime (CUDA / Vulkan / CPU) actually serviced a given call.
  We record the probe flags and infer from RTF.
- **Single-host measurements.** The committed baselines were captured on
  a single workstation (RTX-class GPU + 28 cores + 63 GB RAM). Lower-end
  hosts will hit different RTF cliffs and different cold-start ratios;
  re-baseline if the matrix is being adjusted for a different target class.
- **No automatic loop detector.** `transcript_char_count` is the practical
  proxy. A future iteration could fingerprint repeated suffix substrings.
