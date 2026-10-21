#!/usr/bin/env python3
"""Build the voiced walkthrough video: start card -> takes A/B/C cut -> end card, with the Kokoro voice
(emphasis on key phrases) over ducked game audio, mixed to -14 LUFS / -1 dBTP.

Usage (from the repo root; needs ffmpeg with rubberband, gblur and xfade, e.g. a full Windows build):
  python tools/video/build2.py --logo "C:/.../2 Dude Logo Big.png" Builds/Recordings/2DS_Manufacturing_FactoryTwin_PdM_v02_20261021.mp4

Inputs: the three takes recorded by TakeSequencer (Builds/Recordings/, git-ignored), the phrase WAVs from
tts2.py, and the studio logo (kept outside the repo; pass --logo or set FACTORY_TWIN_LOGO).
Fonts are Windows system fonts (Bahnschrift, Segoe UI); pass --fonts to point elsewhere.
"""
import argparse, json, os, pathlib, re, shutil, subprocess, sys
from dataclasses import dataclass

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path.insert(0, str(HERE))
from script_v2 import PHRASES, TOTAL_S  # noqa: E402

# (input, start, end) after the start card; card 6 s cross-fades 1 s into the dolly. Input 0 = take A, 1 = B, 2 = C.
SEGMENTS = [(0, 0, 8), (0, 8, 20), (0, 20, 34.5), (0, 55, 70.5), (0, 91, 108), (1, 46, 61), (2, 1, 8), (2, 11, 18)]
CARD_S, XFADE_S, END_S = 6.0, 1.0, 10.0
END_BG = 61  # end card background: take B from this second, blurred (the cell running)
SPARKS = (58.5, 72.0)  # failure segment: lift the game audio so the crackle reads


@dataclass
class Config:
    ffmpeg: str
    take_a: pathlib.Path
    take_b: pathlib.Path
    take_c: pathlib.Path
    logo: pathlib.Path
    vo: pathlib.Path
    work: pathlib.Path
    bahn: str
    segoe: str
    segoe_b: str


def font(folder, name):
    # drawtext wants the drive colon escaped: C\:/Windows/Fonts/x.ttf
    return str(pathlib.Path(folder) / name).replace("\\", "/").replace(":", "\\:", 1)


def run(cfg, args):
    r = subprocess.run([cfg.ffmpeg, "-hide_banner", "-v", "error", "-y", *args], capture_output=True, text=True)
    if r.returncode:
        sys.exit(r.stderr[-3000:])
    return r


def alpha_in(t0, d=0.6):
    return f"alpha='min(1,max(0,(t-{t0})/{d}))'"


def picture_and_game(cfg):
    f = []
    # Start card: blurred, darkened still of the hall, logo, title, rule, subtitle.
    f.append(f"[4:v]trim=start_frame=0:end_frame=1,loop=loop={int(CARD_S*60)-1}:size=1:start=0,setpts=N/60/TB,"
             f"gblur=sigma=28,eq=brightness=-0.22:saturation=0.6,format=rgba[sbg]")
    f.append("[3:v]split=2[lg1][lg2]")
    f.append("[lg1]scale=-1:240,format=rgba,fade=t=in:st=0.3:d=0.6:alpha=1[slogo]")
    f.append(f"color=c=0xf2b705:s=140x6:r=60:d={CARD_S},format=rgba,fade=t=in:st=1.2:d=0.5:alpha=1[srule]")
    f.append("[sbg][slogo]overlay=x=(W-w)/2:y=140:shortest=1[s1];[s1][srule]overlay=x=(W-w)/2:y=600[s2]")
    f.append(f"[s2]drawtext=fontfile='{cfg.bahn}':text='Factory Digital Twin':fontsize=116:fontcolor=white:"
             f"x=(w-text_w)/2:y=440:{alpha_in(0.7)},"
             f"drawtext=fontfile='{cfg.segoe}':text='Predictive maintenance on a live production cell':fontsize=44:"
             f"fontcolor=0xd5dbe0:x=(w-text_w)/2:y=635:{alpha_in(1.3)},"
             f"drawtext=fontfile='{cfg.segoe}':text='2 Dude Studios  ·  Manufacturing':fontsize=28:fontcolor=0xaab3ba:"
             f"x=(w-text_w)/2:y=985:{alpha_in(1.8)},"
             f"fade=t=in:st=0:d=0.6,format=yuv420p,setsar=1,settb=AVTB,fps=60[card0]")
    # Takes
    for i, (src, a, b) in enumerate(SEGMENTS, start=1):
        d = b - a
        vf = f"[{src}:v]trim={a}:{b},setpts=PTS-STARTPTS,setsar=1,format=yuv420p,settb=AVTB,fps=60"
        if i == len(SEGMENTS):
            vf += f",fade=t=out:st={d-0.5}:d=0.5"
        f.append(vf + f"[v{i}]")
        fade_in = 1.5 if i == 1 else 0.15
        f.append(f"[{src}:a]atrim={a}:{b},asetpts=PTS-STARTPTS,afade=t=in:st=0:d={fade_in},"
                 f"afade=t=out:st={d-0.15}:d=0.15[a{i}]")
    f.append(f"[card0][v1]xfade=transition=fade:duration={XFADE_S}:offset={CARD_S-XFADE_S}[v01]")
    f.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{CARD_S-XFADE_S},asetpts=PTS-STARTPTS[a0]")
    # End card: blurred moving cell, logo, headline, call-to-action box, footer.
    f.append(f"[5:v]trim={END_BG}:{END_BG+END_S},setpts=PTS-STARTPTS,gblur=sigma=22,"
             f"eq=brightness=-0.26:saturation=0.55,format=rgba[ebg]")
    f.append("[lg2]scale=-1:300,format=rgba,fade=t=in:st=0.2:d=0.6:alpha=1[elogo]")
    f.append(f"color=c=0xf2b705:s=1040x104:r=60:d={END_S},format=rgba,fade=t=in:st=1.3:d=0.5:alpha=1[ebox]")
    f.append("[ebg][elogo]overlay=x=(W-w)/2:y=100:shortest=1[e1];[e1][ebox]overlay=x=(W-w)/2:y=600[e2]")
    f.append(f"[e2]drawtext=fontfile='{cfg.bahn}':text='See your line stop — before it does.':fontsize=84:"
             f"fontcolor=white:x=(w-text_w)/2:y=460:{alpha_in(0.7)},"
             f"drawtext=fontfile='{cfg.segoe_b}':text='Book a call   →   2dudestudios.com/manufacturing':fontsize=42:"
             f"fontcolor=0x1d1600:x=(w-text_w)/2:y=626:{alpha_in(1.5)},"
             f"drawtext=fontfile='{cfg.segoe}':text='Digital twins for manufacturing   ·   Unity   ·   MQTT   ·   OPC UA':"
             f"fontsize=30:fontcolor=0xc4ccd2:x=(w-text_w)/2:y=770:{alpha_in(2.0)},"
             f"fade=t=in:st=0:d=0.6,fade=t=out:st={END_S-1}:d=1,format=yuv420p,setsar=1,settb=AVTB,fps=60[v9]")
    f.append(f"[5:a]atrim={END_BG}:{END_BG+END_S},asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.6,"
             f"afade=t=out:st={END_S-2.5}:d=2.5[a9]")
    vids = "[v01]" + "".join(f"[v{i}]" for i in range(2, len(SEGMENTS) + 1)) + "[v9]"
    auds = "[a0]" + "".join(f"[a{i}]" for i in range(1, len(SEGMENTS) + 1)) + "[a9]"
    # Pin the length: frame timing at the joins can otherwise drop the last frame.
    total = CARD_S - XFADE_S + sum(b - a for _, a, b in SEGMENTS) + END_S
    if abs(total - TOTAL_S) > 1e-6:
        sys.exit(f"cut is {total} s but script_v2.TOTAL_S is {TOTAL_S} s")
    f.append(f"{vids}concat=n={len(SEGMENTS)+1}:v=1:a=0,tpad=stop_mode=clone:stop_duration=1,"
             f"trim=end_frame={round(total*60)}[v]")
    f.append(f"{auds}concat=n={len(SEGMENTS)+2}:v=0:a=1,apad=whole_dur={total},atrim=0:{total}[a]")
    (cfg.work / "picture.txt").write_text(";\n".join(f), encoding="utf-8")
    run(cfg, ["-i", str(cfg.take_a), "-i", str(cfg.take_b), "-i", str(cfg.take_c),
              "-loop", "1", "-framerate", "60", "-t", str(END_S), "-i", str(cfg.logo),
              "-i", str(cfg.take_a), "-i", str(cfg.take_b),
              "-/filter_complex", str(cfg.work / "picture.txt"),
              "-map", "[v]", "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-r", "60", "-pix_fmt", "yuv420p",
              str(cfg.work / "picture.mp4"),
              "-map", "[a]", "-ar", "48000", "-c:a", "pcm_s16le", str(cfg.work / "game.wav")])


def place_phrases(cfg):
    durs = json.loads((cfg.vo / "manifest.json").read_text(encoding="utf-8"))
    t, cues = 0.0, []
    for pid, text, speed, emph, gap, at in PHRASES:
        start = max(t + gap, at if at is not None else 0.0)
        t = start + durs[pid]
        cues.append((pid, start, t, emph, text))
    return cues


def voice(cfg, cues):
    args, f = [], []
    for i, (pid, start, end, emph, _) in enumerate(cues):
        args += ["-i", str(cfg.vo / f"{pid}.wav")]
        chain = f"[{i}:a]aresample=48000"
        if emph:
            chain += ",rubberband=pitch=1.035:formant=preserved,volume=2.5dB"
        f.append(chain + f",adelay={int(round(start*1000))}[d{i}]")
    f.append("".join(f"[d{i}]" for i in range(len(cues))) +
             f"amix=inputs={len(cues)}:normalize=0:duration=longest,apad=whole_dur={TOTAL_S},atrim=0:{TOTAL_S},"
             "highpass=f=90,deesser=i=0.4,acompressor=threshold=-22dB:ratio=2.5:attack=10:release=180:makeup=2,"
             "pan=stereo|c0=c0|c1=c0[vo]")
    (cfg.work / "voice.txt").write_text(";\n".join(f), encoding="utf-8")
    run(cfg, [*args, "-/filter_complex", str(cfg.work / "voice.txt"), "-map", "[vo]", "-c:a", "pcm_s16le",
              str(cfg.work / "voice_raw.wav")])
    i = integrated(cfg, cfg.work / "voice_raw.wav")
    run(cfg, ["-i", str(cfg.work / "voice_raw.wav"), "-af", f"volume={-16 - i:.2f}dB", "-c:a", "pcm_s16le",
              str(cfg.work / "voice.wav")])


def integrated(cfg, path):
    r = subprocess.run([cfg.ffmpeg, "-hide_banner", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
                       capture_output=True, text=True)
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr)[-1])


def mix(cfg):
    game_i = integrated(cfg, cfg.work / "game.wav")
    gain = -30 - game_i  # bed at about -30 LUFS before ducking
    a, b = SPARKS
    run(cfg, ["-i", str(cfg.work / "game.wav"), "-i", str(cfg.work / "voice.wav"), "-filter_complex",
              f"[0:a]volume={gain:.2f}dB,volume=enable='between(t,{a},{b})':volume=5dB[g];[1:a]asplit=2[sc][v];"
              "[g][sc]sidechaincompress=threshold=0.02:ratio=5:attack=15:release=350[gd];"
              "[v][gd]amix=inputs=2:normalize=0[m]", "-map", "[m]", "-c:a", "pcm_s16le", str(cfg.work / "mix.wav")])
    r = subprocess.run([cfg.ffmpeg, "-hide_banner", "-i", str(cfg.work / "mix.wav"), "-af",
                        "loudnorm=I=-14:TP=-1:LRA=11:print_format=json", "-f", "null", "-"],
                       capture_output=True, text=True)
    m = json.loads(r.stderr[r.stderr.rindex("{"):r.stderr.rindex("}") + 1])
    run(cfg, ["-i", str(cfg.work / "mix.wav"), "-af",
              f"loudnorm=I=-14:TP=-1:LRA=11:measured_I={m['input_i']}:measured_TP={m['input_tp']}:"
              f"measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:"
              "linear=true,aresample=48000", "-c:a", "pcm_s16le", str(cfg.work / "final.wav")])
    return game_i


def parse():
    rec = REPO / "Builds" / "Recordings"
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out", type=pathlib.Path, help="output .mp4")
    ap.add_argument("--logo", type=pathlib.Path, default=os.environ.get("FACTORY_TWIN_LOGO"),
                    help="studio logo PNG with transparency (or set FACTORY_TWIN_LOGO)")
    ap.add_argument("--take-a", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeA_20261020_052351.mp4")
    ap.add_argument("--take-b", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeB_20261020_052722.mp4")
    ap.add_argument("--take-c", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeC_20261020_053354.mp4")
    ap.add_argument("--voice", default="am_michael", help="voice whose phrases tts2.py rendered (default am_michael)")
    ap.add_argument("--vo", type=pathlib.Path, help="phrase WAV folder (default tools/video/build/vo/<voice>)")
    ap.add_argument("--work", type=pathlib.Path, default=HERE / "build" / "work", help="intermediate files")
    ap.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg", help="ffmpeg executable")
    ap.add_argument("--fonts", default="C:/Windows/Fonts", help="folder with bahnschrift.ttf, segoeui.ttf, segoeuib.ttf")
    a = ap.parse_args()
    if not a.logo:
        ap.error("--logo is required (or set FACTORY_TWIN_LOGO)")
    for p in (a.take_a, a.take_b, a.take_c, a.logo):
        if not pathlib.Path(p).is_file():
            ap.error(f"not found: {p}")
    return a.out, Config(ffmpeg=a.ffmpeg, take_a=a.take_a, take_b=a.take_b, take_c=a.take_c, logo=a.logo,
                         vo=a.vo or HERE / "build" / "vo" / a.voice, work=a.work,
                         bahn=font(a.fonts, "bahnschrift.ttf"), segoe=font(a.fonts, "segoeui.ttf"),
                         segoe_b=font(a.fonts, "segoeuib.ttf"))


def main():
    out, cfg = parse()
    cfg.work.mkdir(parents=True, exist_ok=True)
    cues = place_phrases(cfg)
    for pid, st, en, emph, text in cues:
        print(f"{pid} {st:6.2f}-{en:6.2f} {'*' if emph else ' '} {text[:64]}")
    picture_and_game(cfg); print("picture done")
    voice(cfg, cues); print("voice done")
    print(f"game bed was {mix(cfg):.1f} LUFS")
    run(cfg, ["-i", str(cfg.work / "picture.mp4"), "-i", str(cfg.work / "final.wav"), "-map", "0:v", "-map", "1:a",
              "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-shortest", "-movflags", "+faststart",
              str(out)])
    print("wrote", out)


if __name__ == "__main__":
    main()
