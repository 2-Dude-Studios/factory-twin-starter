"""Builds the virtual commissioning clip (about 76 s) from takes D and E.

Picture follows docs/voiceover_vc.md:
    0-20 s   take D 0-20     hall dolly, cell, code card with the bug
   20-40 s   take D 17-37    close-up, tests run, row 1 fails, parts queue
   40-44 s   take E 0-4      code card with the fixed line
   44-55 s   take E 4-275    the whole test run, time-lapsed, caption
   55-61 s   I/O list card   over a blurred frame of take E
   61-68 s   take E 14-21    close-up, parts flowing
   68-76 s   end card        logo, line, URL

Without --voice-dir it writes a picture-only cut (game audio only). --guides burns in a timecode and the voice block
that belongs at each moment, for recording against. With --voice-dir, block1.wav ... block6.wav (your recording,
one file per numbered block of docs/voiceover_vc_script.md) start at their block's time, the game audio is ducked
under them and the mix is normalised to -14 LUFS.

Needs ffmpeg on PATH and Pillow (cards). Intermediates go to tools/video/build/vc/ (git-ignored).

    python tools/video/vc/build_vc.py --guides --logo "<logo.png>" Builds/Recordings/<out>.mp4
"""
import argparse
import csv
import glob
import os
import pathlib
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = pathlib.Path(__file__).resolve().parents[3]
WORK = ROOT / "tools" / "video" / "build" / "vc"
W, H, FPS = 1920, 1080, 60

# Panel colours from Assets/_Project/UI/Dashboard.uss, so the cards match the in-engine panels.
INK = (231, 236, 239)
MUTED = (147, 160, 170)
PANEL = (16, 20, 23, 235)
LINE = (255, 255, 255, 26)
ACCENT = (242, 183, 5)

# Voice blocks of docs/voiceover_vc_script.md: number, start, end (video seconds).
BLOCKS = [(1, 0, 8), (2, 8, 20), (3, 20, 40), (4, 40, 55), (5, 55, 68), (6, 68, 76)]
TIMELAPSE = (4.0, 275.0, 11.0)  # take E from, to, shown length


def run(cmd):
    print(">", " ".join(str(c) for c in cmd[:6]), "...")
    subprocess.run([str(c) for c in cmd], check=True)


def latest(pattern):
    files = sorted(glob.glob(str(ROOT / "Builds" / "Recordings" / pattern)), key=os.path.getmtime)
    if not files:
        sys.exit(f"No recording matches {pattern}")
    return files[-1]


def font(fonts, name, size):
    return ImageFont.truetype(str(pathlib.Path(fonts) / name), size)


def clip(src, start, end, out, speed=1.0, audio_from=None):
    """One segment as constant 60 fps H.264 + 48 kHz stereo AAC, so the parts concatenate cleanly."""
    length = (end - start) / speed
    vf = f"setpts=(PTS-STARTPTS)/{speed},fps={FPS},scale={W}:{H},format=yuv420p"
    cmd = ["ffmpeg", "-v", "error", "-y", "-ss", start, "-to", end, "-i", src]
    if audio_from is None:
        audio_from = (src, start)
    cmd += ["-ss", audio_from[1], "-t", length, "-i", audio_from[0]]
    cmd += ["-map", "0:v", "-map", "1:a", "-vf", vf, "-af", "aresample=48000,aformat=channel_layouts=stereo",
            "-t", length, "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-c:a", "aac", "-b:a", "192k", out]
    run(cmd)


def still_with_card(src, at, card_png, length, out, audio_src, audio_at):
    """A blurred, darkened frame of a take with a card on top."""
    frame = WORK / (pathlib.Path(out).stem + "_bg.png")
    run(["ffmpeg", "-v", "error", "-y", "-ss", at, "-i", src, "-frames:v", "1", "-vf", f"scale={W}:{H}", frame])
    bg = Image.open(frame).convert("RGB").filter(ImageFilter.GaussianBlur(18))
    bg = Image.blend(bg, Image.new("RGB", bg.size, (0, 0, 0)), 0.45)
    card = Image.open(card_png).convert("RGBA")
    bg.paste(card, (0, 0), card)
    composed = WORK / (pathlib.Path(out).stem + "_still.png")
    bg.save(composed)
    run(["ffmpeg", "-v", "error", "-y", "-loop", "1", "-framerate", FPS, "-t", length, "-i", composed,
         "-ss", audio_at, "-t", length, "-i", audio_src,
         "-vf", f"fade=t=in:st=0:d=0.4,fade=t=out:st={length - 0.4}:d=0.4,format=yuv420p",
         "-af", "aresample=48000,aformat=channel_layouts=stereo",
         "-map", "0:v", "-map", "1:a", "-t", length, "-c:v", "libx264", "-preset", "medium", "-crf", "17",
         "-c:a", "aac", "-b:a", "192k", out])


def io_list_card(fonts, out):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = 260, 150, 1660, 930
    d.rounded_rectangle((x0, y0, x1, y1), 14, fill=PANEL, outline=LINE, width=2)
    d.text((x0 + 48, y0 + 40), "YOUR I/O LIST", font=font(fonts, "seguisb.ttf", 22), fill=MUTED)
    d.text((x0 + 48, y0 + 72), "The twin is wired to the PLC exactly as the machine will be",
           font=font(fonts, "bahnschrift.ttf", 40), fill=INK)
    rows = list(csv.reader(open(ROOT / "tools" / "plc" / "io_list.csv", encoding="utf-8")))[1:]
    mono = font(fonts, "consola.ttf", 26)
    head = font(fonts, "seguisb.ttf", 20)
    cols = [x0 + 48, x0 + 300, x0 + 450, x0 + 760]
    y = y0 + 160
    for label, cx in zip(["PLC TAG", "ADDRESS", "DIRECTION", "TWIN SIGNAL"], cols):
        d.text((cx, y), label, font=head, fill=MUTED)
    y += 38
    for row in rows:
        tag, iec, _, _, direction, _, topic, _ = row
        d.line((x0 + 48, y - 6, x1 - 48, y - 6), fill=LINE, width=1)
        d.text((cols[0], y), tag, font=mono, fill=INK)
        d.text((cols[1], y), iec, font=mono, fill=ACCENT)
        d.text((cols[2], y), "to PLC" if direction.startswith("input") else "from PLC", font=mono, fill=MUTED)
        d.text((cols[3], y), topic or "(heartbeat)", font=mono, fill=INK)
        y += 40
    img.save(out)


def caption_png(fonts, text, out):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = font(fonts, "seguisb.ttf", 30)
    box = d.textbbox((0, 0), text, font=f)
    w, h = box[2] - box[0], box[3] - box[1]
    x, y = W - w - 96, H - h - 96
    d.rounded_rectangle((x - 22, y - 16, x + w + 22, y + h + 22), 10, fill=(16, 20, 23, 215))
    d.text((x, y), text, font=f, fill=ACCENT)
    img.save(out)


def end_card(fonts, logo, out):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y = 300
    if logo and pathlib.Path(logo).exists():
        mark = Image.open(logo).convert("RGBA")
        mark.thumbnail((360, 220))
        img.paste(mark, ((W - mark.width) // 2, y), mark)
        y += mark.height + 50
    for text, f, fill in [("Find the bug before the machine ships.", font(fonts, "bahnschrift.ttf", 64), INK),
                          ("Virtual commissioning with a digital twin", font(fonts, "segoeui.ttf", 32), MUTED),
                          ("Book a call  ·  2dudestudios.com/manufacturing", font(fonts, "seguisb.ttf", 34), ACCENT)]:
        box = d.textbbox((0, 0), text, font=f)
        d.text(((W - (box[2] - box[0])) // 2, y), text, font=f, fill=fill)
        y += (box[3] - box[1]) + 34
    img.save(out)


def guide_png(fonts, number, start, end, out):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    text = f"VO block {number}   {start // 60}:{start % 60:02d} - {end // 60}:{end % 60:02d}"
    f = font(fonts, "seguisb.ttf", 30)
    box = d.textbbox((0, 0), text, font=f)
    w = box[2] - box[0]
    x, y = (W - w) // 2, H - 92
    d.rounded_rectangle((x - 20, y - 12, x + w + 20, y + 46), 8, fill=(180, 30, 30, 220))
    d.text((x, y), text, font=f, fill=(255, 255, 255))
    img.save(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--take-d", default=None)
    ap.add_argument("--take-e", default=None)
    ap.add_argument("--logo", default=os.environ.get("FACTORY_TWIN_LOGO"))
    ap.add_argument("--fonts", default="C:/Windows/Fonts")
    ap.add_argument("--guides", action="store_true", help="burn in timecode and voice-block labels")
    ap.add_argument("--voice-dir", default=None, help="folder with block1.wav ... block6.wav")
    a = ap.parse_args()

    take_d = a.take_d or latest("2DS_FactoryTwin_TakeD_*.mp4")
    take_e = a.take_e or latest("2DS_FactoryTwin_TakeE_*.mp4")
    WORK.mkdir(parents=True, exist_ok=True)
    print("take D:", take_d)
    print("take E:", take_e)

    io_card = WORK / "io_card.png"
    io_list_card(a.fonts, io_card)
    end_png = WORK / "end_card.png"
    end_card(a.fonts, a.logo, end_png)
    span = TIMELAPSE[1] - TIMELAPSE[0]
    caption = WORK / "timelapse_caption.png"
    caption_png(a.fonts, f"Full test run: {int(span) // 60} min {int(span) % 60} s, sped up", caption)

    parts = []

    def part(name):
        p = WORK / f"{len(parts):02d}_{name}.mp4"
        parts.append(p)
        return p

    clip(take_d, 0, 20, part("d_open"))
    clip(take_d, 17, 37, part("d_fail"))
    clip(take_e, 0, 4, part("e_code"))
    speed = span / TIMELAPSE[2]
    lapse_raw = WORK / "e_lapse_raw.mp4"
    clip(take_e, TIMELAPSE[0], TIMELAPSE[1], lapse_raw, speed=speed, audio_from=(take_e, TIMELAPSE[0]))
    lapse = part("e_lapse")
    run(["ffmpeg", "-v", "error", "-y", "-i", lapse_raw, "-i", caption, "-filter_complex", "[0:v][1:v]overlay=0:0,format=yuv420p",
         "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-c:a", "copy", lapse])
    still_with_card(take_e, 14, io_card, 6, part("io_card"), take_e, 15)
    clip(take_e, 14, 21, part("e_flow"))
    still_with_card(take_e, 320, end_png, 8, part("end_card"), take_e, 320)

    listing = WORK / "parts.txt"
    listing.write_text("".join(f"file '{p.as_posix()}'\n" for p in parts), encoding="utf-8")
    joined = WORK / "joined.mp4"
    run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", joined])

    inputs = ["-i", joined]
    filters = ["[0:v]fade=t=in:st=0:d=0.6[v0]"]
    last = "v0"
    if a.guides:
        for i, (n, s, e) in enumerate(BLOCKS):
            g = WORK / f"guide_{n}.png"
            guide_png(a.fonts, n, s, e, g)
            inputs += ["-i", g]
            filters.append(f"[{last}][{i + 1}:v]overlay=0:0:enable='between(t,{s},{e - 0.05})'[g{n}]")
            last = f"g{n}"
        fontfile = (pathlib.Path(a.fonts) / "consola.ttf").as_posix().replace(":", "\\:")
        filters.append(f"[{last}]drawtext=fontfile='{fontfile}':text='%{{pts\\:hms}}':x=w-tw-40:y=36:fontsize=34:"
                       f"fontcolor=white:box=1:boxcolor=0x000000AA:boxborderw=10[vt]")
        last = "vt"
    filters.append(f"[{last}]format=yuv420p[vout]")

    audio_map = "0:a"
    if a.voice_dir:
        first = len(inputs) // 2
        voice_labels = []
        for k, (n, s, _) in enumerate(BLOCKS):
            wav = pathlib.Path(a.voice_dir) / f"block{n}.wav"
            if not wav.exists():
                sys.exit(f"Missing {wav}")
            inputs += ["-i", wav]
            filters.append(f"[{first + k}:a]aresample=48000,aformat=channel_layouts=stereo,adelay={int(s * 1000)}:all=1[vo{n}]")
            voice_labels.append(f"[vo{n}]")
        filters.append(f"{''.join(voice_labels)}amix=inputs={len(voice_labels)}:normalize=0[voice]")
        filters.append("[voice]asplit[vkey][vmix]")
        filters.append("[0:a]volume=-6dB[bed]")
        filters.append("[bed][vkey]sidechaincompress=threshold=0.03:ratio=6:attack=20:release=400[ducked]")
        filters.append("[ducked][vmix]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=7[aout]")
        audio_map = "[aout]"
    else:
        filters.append("[0:a]loudnorm=I=-20:TP=-2:LRA=7[aout]")
        audio_map = "[aout]"

    script = WORK / "final_filters.txt"
    script.write_text(";\n".join(filters), encoding="utf-8")
    out = pathlib.Path(a.out)
    if not out.is_absolute():
        out = ROOT / out
    run(["ffmpeg", "-v", "error", "-y", *inputs, "-/filter_complex", script, "-map", "[vout]", "-map", audio_map,
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-r", FPS, "-c:a", "aac", "-b:a", "192k",
         "-movflags", "+faststart", out])
    print("wrote", out)


if __name__ == "__main__":
    main()
