# Video tools

These scripts rebuild the M6 walkthrough video (`2DS_Manufacturing_FactoryTwin_PdM_v02_<date>.mp4`, 111 s). It is built from the three takes `TakeSequencer` records, a Kokoro TTS voiceover and the studio logo. Script, cut map and claims check: `docs/voiceover.md`.

| File | What it does |
|---|---|
| `script_v2.py` | The voiceover as data. Each phrase has its speed, an emphasis flag, a gap and an optional anchor time on the video timeline. Edit wording and timing here. |
| `tts2.py` | Renders every phrase with Kokoro, trims edge silence and writes `manifest.json` with the durations. |
| `build2.py` | Cuts the takes, makes the start and end cards, places and emphasises the phrases, ducks the game audio under the voice, mixes to −14 LUFS (true peak about −1 dB after AAC) and writes the MP4. |

## Setup (once)

1. Python 3.10–3.13 venv: `python -m venv tools/video/.venv`, then `tools/video/.venv/Scripts/pip install -r tools/video/requirements.txt`.
2. Model files, about 354 MB, not in git: put `kokoro-v1.0.onnx` and `voices-v1.0.bin` from the [kokoro-onnx releases](https://github.com/thewh1teagle/kokoro-onnx/releases) (model-files-v1.1) in `tools/video/models/`. The Kokoro-82M weights are Apache-2.0 (see `docs/LICENSES.md`).
3. ffmpeg on `PATH`, a build that includes rubberband, gblur and xfade (the WinGet `Gyan.FFmpeg` build has them).
4. Windows fonts Bahnschrift and Segoe UI are used for the cards (`--fonts` points elsewhere).

## Build

From the repo root:

```bash
tools/video/.venv/Scripts/python tools/video/tts2.py
```

```bash
python tools/video/build2.py --logo "C:/Users/user/Documents/2 Dude Studios/2 Dude Studios Branding/Logo/2 Dude Logo Big.png" Builds/Recordings/2DS_Manufacturing_FactoryTwin_PdM_v02_20261021.mp4
```

- `tts2.py` takes `--voice` (default `am_michael`), `--models` and `--out`. It takes about a minute on CPU.
- `build2.py` takes about a minute and a half. It defaults to the 20 Oct takes in `Builds/Recordings/`. Pass `--take-a`, `--take-b` and `--take-c` after re-recording, and check the picture cues below, because the failure and warning times move with each take.
- The logo stays outside the repo. Pass `--logo` or set `FACTORY_TWIN_LOGO`.
- `build2.py` only needs ffmpeg and the standard library, so any Python 3.10+ runs it.
- `build2.py` prints where each phrase lands. Intermediate files go to `tools/video/build/` (git-ignored).

## Picture cues

The phrase anchors in `script_v2.py` match the cut in `build2.py` (`SEGMENTS`):

| Cue | Video time | Source |
|---|---|---|
| Amber warning | 46.0 s | Take A 61.5 s |
| CNC failure, sparks | 59.3 s | Take A 95.3 s |
| Tool change | 78.0 s | Take B 52 s |
| End card | 101 s | Take B 61–71 s, blurred |

To find a cue in a new take, step through frames of the dashboard header for the health chip changing. When a cue moves, shift the segment in `SEGMENTS` or the phrase's anchor.

## Emphasis

Kokoro has no emphasis control. Phrases flagged `True` are rendered on their own at a slower speed, then lifted 3.5 % in pitch (rubberband, formants preserved) and raised 2.5 dB. Short pauses before them come from the gaps.
