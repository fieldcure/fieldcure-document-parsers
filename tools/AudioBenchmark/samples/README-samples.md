# Sample Catalog

This file documents the audio samples used to produce the baselines under
[`../results/`](../results/). The audio files themselves are **not committed**
— their copyright status is mixed (audiobook narration, broadcast recordings,
auto-generated transcripts). Anyone reproducing a baseline must re-acquire
the source audio from the documented origin and stage it locally.

## License posture

| Treat as | Examples | Action |
|---|---|---|
| Public domain or permissive | KSS dataset (CC-BY-SA 4.0), VOA Special English (US federal broadcaster, public domain in the US) | Safe to redistribute audio + GT, but still kept out of this repo to avoid binary churn. |
| Copyright unclear / reserved | Broadcaster recordings, commercial audiobooks, auto-generated YouTube transcripts | Audio MUST stay out of any redistribution. Ground-truth text derived from these is also potentially derivative — treat the same. |

When in doubt: keep it out of the repo, document the origin, leave reproduction
to the local user.

## Samples used in the 2026-04-27 baseline

### KSS — Korean Single Speaker dataset

- **Used in**: `2026-04-27-kss-baseline.csv`, `2026-04-27-v2-validation.csv`,
  `2026-04-27-turbo-validation.csv`
- **Origin**: Public dataset by Kyubyong Park.
  Search: "KSS Korean Single Speaker Speech Dataset".
- **License posture**: CC-BY-SA 4.0 per the original release. Safe for
  benchmarking; audio still kept out of the repo.
- **Layout expected**: `<dir>/1_NNNN.wav` files plus `transcript.v.1.4.txt`
  in the pipe-delimited 6-column form
  `path|nfc|nfc|nfd|duration_sec|english`. Use `stage-kss.ps1` to stage a
  subset and write paired `.gt.txt` files from column 2 (NFC).
- **Subset chosen**: every 69th entry of the 1,040-sample subset → 15 files,
  ~50 sec total audio. Even spacing keeps short and longer utterances both
  represented.

### VOA Special English — *Making of a Nation* episode "First Europeans"

- **Used in**: `2026-04-27-voa-nocontext-ab.csv`, `2026-04-27-v2-validation.csv`,
  `2026-04-27-turbo-validation.csv`
- **Origin**: Voice of America Learning English broadcast.
  Search: "VOA Special English Making of a Nation first europeans mp3".
- **License posture**: VOA programming is produced by a US federal
  broadcaster and is public domain inside the US. Re-distribution policy
  varies by jurisdiction — audio kept out of the repo regardless.
- **GT origin**: VOA publishes the episode script. Save as `VOA.txt` in
  the staging directory next to the MP3; newlines collapsed at stage time.
- **Why this clip**: 16-min slow narration with no music or speaker
  changes. Acts as a long-form clean probe.

### BBC Earth — *Five Wonders of our Universe*

- **Used in**: `2026-04-27-bbc-tier-mapping.csv`, `2026-04-27-v2-validation.csv`,
  `2026-04-27-turbo-validation.csv`
- **Origin**: BBC documentary clip available on YouTube.
  Search: "Five Wonders of our Universe BBC Earth Science".
- **License posture**: BBC programming is copyright BBC; YouTube
  extraction is also subject to YouTube ToS. **Audio MUST stay out of any
  redistribution.** Used here only for internal measurement.
- **GT origin**: YouTube auto-transcript copy-pasted to `Five Wonders_script.txt`.
  `stage-bbc-five-wonders.ps1` strips the timestamp lines (`0:01`, `27:54`,
  ...) and collapses to a single line. The auto-transcript itself is
  imperfect — small WER baseline floor (~4 % on the strongest model)
  reflects that, not Whisper.
- **Why this clip**: 40-min documentary with music interludes, multiple
  narrators, and scientific jargon. The harshest long-form probe in the
  set. Reproducibly triggers Large-V3 looping.

## Adding a new sample to the catalog

1. Drop the audio + optional `.gt.txt` somewhere outside the repo (or under
   `tools/AudioBenchmark/samples/` — that directory is gitignored, so
   audio bodies stay local).
2. Append an entry below documenting **origin** (URL or search terms),
   **license posture**, and **why this clip** (what behaviour it stresses).
3. Re-run the relevant benchmark campaign and add the resulting CSV to
   `../results/` with a date prefix. Cross-reference from the catalog entry.

Audio bodies, transcripts, and GT files stay out of git. Only metadata in
this catalog and the quantitative CSVs (`results/*.csv`) are committed.
