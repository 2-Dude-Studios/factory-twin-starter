#!/usr/bin/env python3
"""Render every phrase of script_v2 with Kokoro TTS, trimmed of edge silence, plus manifest.json of durations.

Usage (from the repo root, inside the venv that has kokoro-onnx):
  python tools/video/tts2.py                                   # am_michael, models in tools/video/models
  python tools/video/tts2.py --voice af_heart --models D:/kokoro --out tools/video/build/vo/af_heart

Model files (not in git): kokoro-v1.0.onnx and voices-v1.0.bin from
https://github.com/thewh1teagle/kokoro-onnx/releases (model-files-v1.1). Kokoro-82M weights: Apache-2.0.
"""
import argparse, json, pathlib, sys, wave

import numpy as np
from kokoro_onnx import Kokoro

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from script_v2 import PHRASES  # noqa: E402


def trim(a, sr, thresh=0.012, pad_s=0.03):
    idx = np.flatnonzero(np.abs(a) > thresh)
    if not len(idx):
        return a
    pad = int(pad_s * sr)
    return a[max(0, idx[0] - pad): min(len(a), idx[-1] + pad)]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--voice", default="am_michael", help="Kokoro voice id (default am_michael)")
    ap.add_argument("--models", type=pathlib.Path, default=HERE / "models",
                    help="folder with kokoro-v1.0.onnx and voices-v1.0.bin (default tools/video/models)")
    ap.add_argument("--out", type=pathlib.Path, help="output folder (default tools/video/build/vo/<voice>)")
    args = ap.parse_args()
    out = args.out or HERE / "build" / "vo" / args.voice
    out.mkdir(parents=True, exist_ok=True)
    lang = "en-gb" if args.voice.startswith("b") else "en-us"

    k = Kokoro(str(args.models / "kokoro-v1.0.onnx"), str(args.models / "voices-v1.0.bin"))
    manifest = {}
    for pid, text, speed, emph, gap, at in PHRASES:
        samples, sr = k.create(text, voice=args.voice, speed=speed, lang=lang)
        a = trim(np.asarray(samples, dtype=np.float32), sr)
        pcm = (np.clip(a, -1, 1) * 32767).astype("<i2")
        with wave.open(str(out / f"{pid}.wav"), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(pcm.tobytes())
        manifest[pid] = len(a) / sr
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    print(f"{len(manifest)} phrases rendered to {out}")


if __name__ == "__main__":
    main()
