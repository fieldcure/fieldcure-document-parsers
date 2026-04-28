# Whisper.net Sample Audio Attribution

These fixtures are copied from the [whisper.net](https://github.com/sandrohanea/whisper.net)
project's sample audio set, used here for opt-in end-to-end transcription tests against the
real Whisper.net runtime.

## Files

| File | Source content | Notes |
| --- | --- | --- |
| `bush.wav` | President George W. Bush — Space Shuttle Columbia disaster address (2003) | U.S. federal government work; public domain in the United States. |
| `kennedy.mp3` | President John F. Kennedy — "We choose to go to the Moon" / Apollo program speech | U.S. federal government work; public domain in the United States. |
| `multichannel.wav` | Two-speaker dialogue ("Hi, how are you?" / "I'm really looking forward to your birthday party!") | Distributed as a sample with whisper.net (MIT). |

## Expected transcripts (per upstream README)

- **bush.wav** — "My fellow Americans, this day has brought terrible news and great sadness to
  our country. At 9:00 this morning, Mission Control in Houston lost contact with our space
  shuttle Columbia. A short time later, debris was seen falling from the skies above Texas.
  The Columbia's lost; there are no survivors."
- **kennedy.mp3** — "I believe that this nation should commit itself to achieving the goal,
  before this decade is out, of landing a man on the moon and returning him safely to the
  earth. No single space project in this period will be more impressive to mankind, or more
  important for the long-range exploration of space."
- **multichannel.wav** — Speaker A: "Hi, how are you?" / Speaker B: "I'm really looking
  forward to your birthday party!"

## Usage

Tests that consume these fixtures live in `WhisperNetSampleTests.cs` and are gated by the same
opt-in environment variables as the other Whisper integration tests:

- `FIELDCURE_AUDIO_ENABLE_WHISPER_FIXTURE_TESTS=1`
- `FIELDCURE_WHISPER_MODEL_PATH=<path to a ggml model>`

Without those variables the tests report `Inconclusive` so default `dotnet test` runs stay
fast and offline.
