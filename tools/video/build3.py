#!/usr/bin/env python3
"""Build v03 of the walkthrough video: the approved v02 cut plus mid screens (a data-flow screen, three
chapter cards, an "ignored vs acted on" comparison and a "what we build" screen), burned-in subtitles,
kinetic text, highlighters on the stack light and dashboard, and masked transitions.

v02 stays reproducible: build2.py is untouched, and this script reads its outputs
(tools/video/build/work/picture.mp4 and game.wav) and its functions (place_phrases, voice, mix).

Usage (from the repo root, after build2.py has made tools/video/build/work/):
  python tools/video/build3.py --fonts C:/Windows/Fonts Builds/Recordings/2DS_Manufacturing_FactoryTwin_PdM_v03_<date>.mp4

Optional transition sounds (only clips listed in docs/LICENSES.md): --sfx-chapter X.wav --sfx-reveal Y.wav
Stages (--stage): clips, base, overlay, audio, mux, or all (default). Intermediates go to tools/video/build/work3/.
Needs ffmpeg with xfade (custom), ass, gblur and rubberband. No Python packages beyond the standard library.
"""
import argparse, json, math, os, pathlib, shutil, subprocess, sys

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path.insert(0, str(HERE))
import build2  # noqa: E402

FPS = 60
W, H = 1920, 1080
V02_TOTAL = 111.0
# Mid screens inserted into v02's timeline: (v02 time, seconds, id). Voice gaps make room for them.
INSERTS = [(27.3, 4.0, "X1"), (39.5, 2.5, "X2"), (55.0, 2.5, "X3"), (72.0, 2.5, "X4"), (84.5, 4.0, "X5")]
TOTAL = V02_TOTAL + sum(l for _, l, _ in INSERTS)  # 126.5 s


def shift(t):
    """v02 time -> v03 time."""
    return t + sum(l for a, l, _ in INSERTS if t >= a - 1e-9)


# Colours (RGB hex)
YEL, INK, CARD, GRN, OFF, RED = "F2B705", "1D1600", "1A1D21", "5FD38D", "E7ECEF", "E5484D"

# Generated clips: blurred, darkened take footage (keeps moving). src take, take time where the clip's own
# content starts, and its length on the v03 timeline. Lead/tail frames for the transitions are added around it.
CLIPS = {
    "X1": dict(src="A", t=22.3, len=4.0),
    "X2": dict(src="A", t=55.0, len=2.5),
    "X3": dict(src="A", t=91.0, len=2.5),
    "X4": dict(src="B", t=46.0, len=2.5),
    "X5": dict(src="B", t=58.5, len=4.0),
    "S": dict(src="C", t=11.0, len=7.0),   # "What we build" screen, replaces v02 94-101 (no insert)
}
# The v03 sequence. ("pic", a, b) = v02 picture a..b s; ("dolly",) = take A 0-8 s + v02 picture 13-27.3 s;
# ("card",) = v02 start card 0-5 s; a string = a generated clip; ("T", kind, seconds, edge colour) = transition.
SEQ = [("card",), ("T", "iris", 0.5, YEL), ("dolly",), ("T", "diag", 0.45, YEL), "X1", ("T", "diag", 0.45, YEL),
       ("pic", 27.3, 39.5), ("T", "diag", 0.5, YEL), "X2", ("T", "diag", 0.5, YEL),
       ("pic", 39.5, 55.0), ("T", "diag", 0.45, RED), "X3", ("T", "diag", 0.45, RED),
       ("pic", 55.0, 72.0), ("T", "diag", 0.45, GRN), "X4", ("T", "diag", 0.45, GRN),
       ("pic", 72.0, 84.5), ("T", "diag", 0.45, GRN), "X5", ("T", "diag", 0.45, YEL),
       ("pic", 84.5, 94.0), ("T", "diag", 0.45, YEL), "S", ("T", "diag", 0.5, YEL), ("pic", 101.0, 111.0)]
SLANT = 0.35  # diagonal reveal: the edge leans this much (x per y)
XF = {
    "diag": f"if(lt(X+{SLANT}*Y,(1-P)*(W+{SLANT}*H)),B,A)",
    "iris": "if(lt(hypot(X-W/2,Y-0.24*H),(1-P)*0.8*hypot(W,H)),B,A)",
}

# Measured on real frames of v02 (1920x1080). The CNC close-up camera is static in takes A and B.
STACK = dict(red=(821, 63), amber=(821, 108), green=(821, 148))
CHIP = (1790, 111, 1863, 132)        # CNC health chip
RISK_PCT = (1792, 145, 1862, 167)
RISK_BAR = (1494, 174, 1863, 195)
CHANGE_DUE = (1634, 228, 1770, 242)  # tool life left value + "Change due"
LINE_CHIP = (1706, 387, 1863, 406)   # "Stopped - CNC fault"
PARTS_LOST = (1683, 424, 1760, 462)
CNC_ROW = (1475, 100, 1877, 362)     # CNC card, for the lens callout
LINE_CARD = (1475, 373, 1877, 553)   # Line card, for the comparison
LENS_POS, LENS_SCALE = (560, 250), 1.8
PANEL_SCALE, PANEL_Y, PANEL_X = 1.6, 430, (250, 1027)
# Comparison crops: take A after the failure (v02 66-70 s: "Stopped - CNC fault", parts lost 1) and
# take B after the tool change (v02 82-86 s: Running, parts lost 0). Untouched footage, just cropped.
PANEL_SRC = (("A", 102.0), ("B", 56.0))


# --------------------------------------------------------------------------------------------- helpers
_OLD_FFMPEG = None


def old_ffmpeg(exe):
    """ffmpeg before 6.1 has no -/option file syntax; it takes -filter_complex_script instead."""
    global _OLD_FFMPEG
    if _OLD_FFMPEG is None:
        v = subprocess.run([exe, "-version"], capture_output=True, text=True).stdout.split()
        try:
            major, minor = (int(x) for x in v[2].split("-")[0].split(".")[:2])
            _OLD_FFMPEG = (major, minor) < (6, 1)
        except (IndexError, ValueError):
            _OLD_FFMPEG = False
    return _OLD_FFMPEG


def ff(cfg, args, cwd=None):
    if old_ffmpeg(cfg.ffmpeg):
        args = ["-filter_complex_script" if a == "-/filter_complex" else a for a in args]
    r = subprocess.run([cfg.ffmpeg, "-hide_banner", "-v", "error", "-y", *args], capture_output=True, text=True,
                       cwd=cwd)
    if r.returncode:
        sys.exit(r.stderr[-3000:])
    return r


build2.run = ff  # build2.voice/mix call run(); this keeps them working on older ffmpeg builds too


def ts(t):
    t = max(0.0, t)
    cs = int(round(t * 100))
    return f"{cs // 360000}:{cs // 6000 % 60:02d}:{cs // 100 % 60:02d}.{cs % 100:02d}"


def col(hexrgb):
    r, g, b = hexrgb[0:2], hexrgb[2:4], hexrgb[4:6]
    return f"&H{b}{g}{r}&"


def scol(hexrgb, alpha=0):
    r, g, b = hexrgb[0:2], hexrgb[2:4], hexrgb[4:6]
    return f"&H{alpha:02X}{b}{g}{r}"


def rect(w, h):
    return f"m 0 0 l {w:.1f} 0 l {w:.1f} {h:.1f} l 0 {h:.1f}"


def circle(r):
    k = 0.5523 * r
    d = 2 * r
    return (f"m {r:.1f} 0 b {r + k:.1f} 0 {d:.1f} {r - k:.1f} {d:.1f} {r:.1f} b {d:.1f} {r + k:.1f} {r + k:.1f} {d:.1f} "
            f"{r:.1f} {d:.1f} b {r - k:.1f} {d:.1f} 0 {r + k:.1f} 0 {r:.1f} b 0 {r - k:.1f} {r - k:.1f} 0 {r:.1f} 0")


def quad_line(x0, y0, x1, y1, w=2.0):
    """A straight line as a thin filled quad, in absolute coordinates (use with an7, pos 0,0)."""
    dx, dy = x1 - x0, y1 - y0
    n = math.hypot(dx, dy) or 1
    ox, oy = -dy / n * w / 2, dx / n * w / 2
    return (f"m {x0 + ox:.1f} {y0 + oy:.1f} l {x1 + ox:.1f} {y1 + oy:.1f} l {x1 - ox:.1f} {y1 - oy:.1f} "
            f"l {x0 - ox:.1f} {y0 - oy:.1f}")


class Ass:
    def __init__(self, styles):
        self.styles, self.ev = styles, []

    def add(self, t0, t1, text, style="Shape", layer=1):
        if t1 > t0:
            self.ev.append(f"Dialogue: {layer},{ts(t0)},{ts(t1)},{style},,0,0,0,,{text}")

    def shape(self, t0, t1, x, y, path, tags="", layer=1, an=7):
        self.add(t0, t1, f"{{\\an{an}\\pos({x:.1f},{y:.1f})\\bord0\\shad0{tags}\\p1}}{path}{{\\p0}}", layer=layer)

    def write(self, path):
        head = ["[Script Info]", "ScriptType: v4.00+", f"PlayResX: {W}", f"PlayResY: {H}", "WrapStyle: 2",
                "ScaledBorderAndShadow: yes", "YCbCr Matrix: TV.709", "", "[V4+ Styles]",
                "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, "
                "Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, "
                "Alignment, MarginL, MarginR, MarginV, Encoding"]
        head += [f"Style: {s}" for s in self.styles]
        head += ["", "[Events]", "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"]
        pathlib.Path(path).write_text("\n".join(head + self.ev) + "\n", encoding="utf-8")


def style(name, font, size, colour=OFF, outline_col=CARD, outline_alpha=0, border=1, outline=0, shadow=0, an=7,
          bold=0, ml=0, mr=0, mv=0, back=("000000", 0x80)):
    return (f"{name},{font},{size},{scol(colour)},{scol(colour)},{scol(outline_col, outline_alpha)},"
            f"{scol(*back)},{bold},0,0,0,100,100,0,0,{border},{outline},{shadow},{an},{ml},{mr},{mv},1")


# ------------------------------------------------------------------------------------ timeline plan
def plan():
    """Walk SEQ: piece lengths, xfade offsets and each transition's window on the v03 timeline."""
    pieces, trans = [], []
    items = SEQ
    for i, it in enumerate(items):
        if isinstance(it, tuple) and it[0] == "T":
            continue
        lead = items[i - 1][2] if i > 0 else 0.0
        tail = items[i + 1][2] if i + 1 < len(items) else 0.0
        if isinstance(it, str):
            c = CLIPS[it]
            pieces.append(dict(kind="clip", id=it, real=c["len"], lead=lead, tail=tail, length=lead + c["len"] + tail))
        elif it[0] == "card":
            pieces.append(dict(kind="card", real=5.0, lead=0, tail=tail, length=5.0 + tail))
        elif it[0] == "dolly":
            pieces.append(dict(kind="dolly", real=8.0 + 14.3, lead=0, tail=0, length=22.3))
        else:
            pieces.append(dict(kind="pic", a=it[1], b=it[2], real=it[2] - it[1], lead=0, tail=0,
                               length=it[2] - it[1]))
    cur = pieces[0]["length"]
    ti = [it for it in items if isinstance(it, tuple) and it[0] == "T"]
    for k, p in enumerate(pieces[1:]):
        _, kind, d, edge = ti[k]
        o = cur - d
        trans.append(dict(kind=kind, d=d, edge=edge, offset=o))
        cur = o + p["length"]
    if abs(cur - TOTAL) > 1e-6:
        sys.exit(f"v03 sequence is {cur} s, expected {TOTAL} s")
    return pieces, trans


# ------------------------------------------------------------------------------------- picture passes
def render_clips(cfg, pieces):
    takes = dict(A=cfg.take_a, B=cfg.take_b, C=cfg.take_c)
    for p in pieces:
        if p["kind"] != "clip":
            continue
        c = CLIPS[p["id"]]
        start = c["t"] - p["lead"]
        bg = (f"[0:v]trim={start:.4f}:{start + p['length']:.4f},setpts=PTS-STARTPTS,gblur=sigma=22,"
              f"eq=brightness=-0.27:saturation=0.55,fps={FPS},format=yuv420p,setsar=1[bg]")
        args = ["-i", str(takes[c["src"]])]
        f = [bg]
        last = "bg"
        if p["id"] == "X5":
            for j, (src, t) in enumerate(PANEL_SRC):
                args += ["-i", str(takes[src])]
                x0, y0, x1, y1 = LINE_CARD
                pw, ph = round((x1 - x0) * PANEL_SCALE), round((y1 - y0) * PANEL_SCALE)
                f.append(f"[{j + 1}:v]trim={t - p['lead']:.4f}:{t - p['lead'] + p['length']:.4f},setpts=PTS-STARTPTS,"
                         f"crop={x1 - x0}:{y1 - y0}:{x0}:{y0},scale={pw}:{ph}:flags=lanczos,format=rgba,"
                         f"fade=t=in:st={p['lead'] + 0.25:.2f}:d=0.35:alpha=1[p{j}]")
                f.append(f"[{last}][p{j}]overlay=x={PANEL_X[j]}:y={PANEL_Y}:format=auto[o{j}]")
                last = f"o{j}"
        f.append(f"[{last}]format=yuv420p[v]")
        ff(cfg, [*args, "-filter_complex", ";".join(f), "-map", "[v]", "-an", "-c:v", "libx264", "-preset", "veryfast",
                 "-crf", "12", "-r", str(FPS), str(cfg.work / f"clip_{p['id']}.mp4")])
        print("clip", p["id"], f"{p['length']:.2f} s")


def render_base(cfg, pieces, trans):
    """Cut v02's picture at the inserts, add the clips, join with masked xfades."""
    args = ["-i", str(cfg.pic), "-i", str(cfg.take_a)]
    clip_ix = {}
    for p in pieces:
        if p["kind"] == "clip":
            clip_ix[p["id"]] = len(args) // 2
            args += ["-i", str(cfg.work / f"clip_{p['id']}.mp4")]
    norm = f"setsar=1,format=yuv420p,fps={FPS},settb=AVTB"
    pics = [p for p in pieces if p["kind"] in ("pic", "card", "dolly")]
    f = [f"[0:v]split={len(pics)}" + "".join(f"[s{i}]" for i in range(len(pics)))]
    labels, si = [], 0
    for i, p in enumerate(pieces):
        lab = f"q{i}"
        if p["kind"] == "card":
            f.append(f"[s{si}]trim=0:5.0,setpts=PTS-STARTPTS,tpad=stop_mode=clone:stop_duration={p['tail']},{norm}[{lab}]")
            si += 1
        elif p["kind"] == "dolly":
            f.append(f"[1:v]trim=0:8,setpts=PTS-STARTPTS,{norm}[dA]")
            f.append(f"[s{si}]trim=13.0:27.3,setpts=PTS-STARTPTS,{norm}[dB]")
            f.append(f"[dA][dB]concat=n=2:v=1:a=0,{norm}[{lab}]")
            si += 1
        elif p["kind"] == "pic":
            f.append(f"[s{si}]trim={p['a']}:{p['b']},setpts=PTS-STARTPTS,{norm}[{lab}]")
            si += 1
        else:
            f.append(f"[{clip_ix[p['id']]}:v]{norm}[{lab}]")
        labels.append(lab)
    cur = labels[0]
    for k, t in enumerate(trans):
        out = f"x{k}"
        f.append(f"[{cur}][{labels[k + 1]}]xfade=transition=custom:duration={t['d']}:offset={t['offset']:.4f}:"
                 f"expr='{XF[t['kind']]}'[{out}]")
        cur = out
    f.append(f"[{cur}]trim=end_frame={round(TOTAL * FPS)},format=yuv420p[v]")
    (cfg.work / "base.txt").write_text(";\n".join(f), encoding="utf-8")
    ff(cfg, [*args, "-/filter_complex", str(cfg.work / "base.txt"), "-map", "[v]", "-an", "-frames:v", str(round(TOTAL * FPS)), "-c:v", "libx264",
             "-preset", "veryfast", "-crf", "12", "-r", str(FPS), str(cfg.work / "base.mp4")])
    print("base done")


# -------------------------------------------------------------------------------------- overlays (ASS)
def phrase_times(cfg):
    cues = build2.place_phrases(cfg)
    out = []
    for pid, st, en, emph, text in cues:
        s = shift(st)
        out.append((pid, s, s + (en - st), emph, text))
    return out


DISPLAY = [("M Q T T", "MQTT"), ("O P C U A", "OPC UA"),
           ("two dude studios dot com, slash manufacturing", "2dudestudios.com/manufacturing")]


def display_text(text):
    for a, b in DISPLAY:
        text = text.replace(a, b)
    return text


def wrap2(text, n=42):
    """Up to two lines of about n characters, broken at the space nearest the middle (commas preferred)."""
    if len(text) <= n:
        return text
    words = text.split(" ")
    best, bi = None, 1
    for i in range(1, len(words)):
        a, b = " ".join(words[:i]), " ".join(words[i:])
        score = max(len(a), len(b)) - (4 if a.endswith(",") else 0)
        if best is None or score < best:
            best, bi = score, i
    return " ".join(words[:bi]) + "\\N" + " ".join(words[bi:])


def subtitle_events(cues):
    ev = []
    for i, (pid, s, e, _, text) in enumerate(cues):
        t = display_text(text)
        nxt = cues[i + 1][1] if i + 1 < len(cues) else TOTAL
        end = min(e + 0.25, nxt - 0.02)
        if len(t) <= 84:
            ev.append((s, end, wrap2(t)))
            continue
        parts = [p.strip() for p in t.replace(", ", ",|").split("|")]
        groups, cur = [], ""
        for p in parts:
            if cur and len(cur) + 1 + len(p) > 84:
                groups.append(cur)
                cur = p
            else:
                cur = (cur + " " + p).strip()
        groups.append(cur)
        total = sum(len(g) for g in groups)
        t0 = s
        for k, g in enumerate(groups):
            t1 = t0 + (e - s) * len(g) / total
            ev.append((t0, t1 if k < len(groups) - 1 else end, wrap2(g)))
            t0 = t1
    return ev


def build_subs(cfg, cues):
    a = Ass([style("Sub", "Segoe UI Semibold", 40, colour="FFFFFF", outline_col="101418", outline_alpha=0x5A,
                   border=3, outline=12, shadow=0, an=2, ml=40, mr=500, mv=58),
               style("SubStart", "Segoe UI Semibold", 40, colour="FFFFFF", outline_col="101418", outline_alpha=0x5A,
                     border=3, outline=12, shadow=0, an=2, ml=40, mr=40, mv=140),
               style("SubEnd", "Segoe UI Semibold", 40, colour="FFFFFF", outline_col="101418", outline_alpha=0x5A,
                     border=3, outline=12, shadow=0, an=2, ml=40, mr=40, mv=58)])
    card_end, end_card = 5.5, shift(101.0)
    for s, e, t in subtitle_events(cues):
        st = "SubStart" if e <= card_end + 0.3 else ("SubEnd" if s >= end_card - 0.05 else "Sub")
        a.add(s, e, t, style=st, layer=0)
    a.write(cfg.work / "subs.ass")
    return a


def kinetic(a, t0, t1, words, x=90, y=205, size=52, accent=YEL):
    """Words appear one by one in a dark card with a yellow edge; the line drifts in. words: [(text, colour)]."""
    parts = []
    for i, (w, c) in enumerate(words):
        d0 = 120 + i * 110
        parts.append(f"{{\\c{col(c)}\\alpha&HFF&\\t({d0},{d0 + 220},\\alpha&H00&)}}{w} ")
    a.add(t0, t1, f"{{\\an7\\fs{size}\\move({x + 26},{y},{x},{y},0,450)\\fad(0,280)}}" + "".join(parts).rstrip(), style="Kin",
          layer=3)
    a.shape(t0, t1, x - 30, y - 16, rect(7, size * 1.25 + 32),
            f"\\1c{col(accent)}\\fad(150,280)\\org({x - 30},{y - 16})\\fscy0\\t(0,300,\\fscy100)", layer=4)


def ring(a, t0, cx, cy, r, colour=YEL, pulses=2, hold=1.6, width=4):
    """A ring that holds on target while pulses expand outwards from it."""
    a.shape(t0, t0 + hold, cx, cy, circle(r),
            f"\\1a&HFF&\\3c{col(colour)}\\bord{width}\\blur1\\fad(150,300)\\fscx70\\fscy70\\t(0,220,\\fscx100\\fscy100)",
            layer=5, an=5)
    for k in range(pulses):
        s = t0 + 0.1 + k * 0.55
        a.shape(s, s + 0.7, cx, cy, circle(r), f"\\1a&HFF&\\3c{col(colour)}\\bord3\\blur2\\t(0,700,\\fscx190\\fscy190\\3a&HFF&)",
                layer=5, an=5)


def box(a, t0, t1, b, colour=YEL, pad=5, width=3, glow=False, pulse=False):
    x0, y0, x1, y1 = b
    tags = f"\\1a&HFF&\\3c{col(colour)}\\bord{width}\\fad(150,250)"
    if glow:
        a.shape(t0, t1, x0 - pad, y0 - pad, rect(x1 - x0 + 2 * pad, y1 - y0 + 2 * pad),
                f"\\1a&HFF&\\3c{col(colour)}\\bord{width + 5}\\blur6\\3a&H60&\\fad(150,250)" +
                ("\\t(0,350,\\3a&H00&)\\t(350,700,\\3a&H90&)\\t(700,1050,\\3a&H00&)\\t(1050,1400,\\3a&H90&)" if pulse else ""),
                layer=5)
    a.shape(t0, t1, x0 - pad, y0 - pad, rect(x1 - x0 + 2 * pad, y1 - y0 + 2 * pad), tags, layer=6)


def underline(a, t0, t1, b, colour=YEL, dur=0.5):
    x0, y0, x1, y1 = b
    a.shape(t0, t1, x0, y1 + 1.5, rect(x1 - x0, 3), f"\\1c{col(colour)}\\fscx0\\t(0,{int(dur * 1000)},\\fscx100)\\fad(0,300)",
            layer=6)


def callout(a, t0, t1, b, label, colour=YEL, to_x=1360, pad=5):
    """Box on the target, leader along the box's top edge (the gap above a dashboard row), label to the left."""
    x0, y0, x1, y1 = b
    ym = y0 - pad - 1.5
    box(a, t0, t1, b, colour)
    a.shape(t0 + 0.15, t1, 0, 0, quad_line(to_x, ym, x0 - 6, ym, 2.5),
            f"\\1c{col(colour)}\\clip({to_x},0,{x0},{H})\\fad(200,250)", layer=6)
    a.add(t0 + 0.25, t1, f"{{\\an6\\pos({to_x - 16},{ym:.0f})\\fad(200,250)}}{label}", style="Call", layer=6)


def step_events(a, t0, t1, fn, dt=0.02):
    """Stepped shapes for morphs (events on the centisecond grid, so they never overlap)."""
    t0 = round(t0, 2)
    n = max(1, round((t1 - t0) / dt))
    for i in range(n):
        s = round(t0 + i * dt, 2)
        fn(s, (i + 0.5) / n, round(s + dt, 2))


def timed(t0, t1):
    """Event start on the centisecond grid, plus millisecond offsets that put an animation exactly on t0..t1."""
    s = math.floor(t0 * 100) / 100
    return s, int(round((t0 - s) * 1000)), int(round((t1 - s) * 1000))


def build_overlays(cfg, cues, trans):
    a = Ass([style("Shape", "Segoe UI", 20, outline=0),
             style("Kin", "Bahnschrift", 58, colour="FFFFFF", outline_col=CARD, outline_alpha=0x26, border=3,
                   outline=16, an=7),
             style("Call", "Segoe UI Semibold", 30, colour="FFFFFF", outline_col=CARD, outline_alpha=0x26, border=3,
                   outline=10, an=6),
             style("Head", "Bahnschrift", 84, colour="FFFFFF", border=1, outline=0, shadow=2, an=5,
                   back=("000000", 0x60)),
             style("Node", "Bahnschrift", 40, colour="FFFFFF", an=5),
             style("NodeSub", "Segoe UI", 22, colour=OFF, an=5),
             style("Foot", "Segoe UI", 24, colour=OFF, an=5),
             style("ChapNum", "Bahnschrift", 230, colour=YEL, an=6),
             style("ChapTitle", "Bahnschrift", 100, colour="FFFFFF", an=4),
             style("Panel", "Bahnschrift", 46, colour="FFFFFF", an=1),
             style("Tile", "Bahnschrift", 50, colour="FFFFFF", an=7),
             style("TileNum", "Bahnschrift", 30, colour=YEL, an=7),
             style("Next", "Segoe UI", 28, colour=OFF, an=5)])
    P = {pid: (s, e) for pid, s, e, _, _ in cues}

    # Transition edges: a coloured bar riding the diagonal mask, an expanding ring on the iris.
    for t in trans:
        o, d, edge = t["offset"], t["d"], t["edge"]
        s, m0, m1 = timed(o, o + d)
        if t["kind"] == "diag":
            # The mask edge is X + SLANT*Y = (1-P)(W + SLANT*H), moving linearly; the bar is drawn on it and moved
            # with \\move over exactly the same frames.
            span = W + SLANT * H
            path = f"m -9 0 l 9 0 l {9 - SLANT * H:.1f} {H} l {-9 - SLANT * H:.1f} {H}"
            a.add(s, o + d + 0.02, f"{{\\an7\\move(0,0,{span:.1f},0,{m0},{m1})\\bord0\\shad0\\1c{col(edge)}\\blur1.5"
                                   f"\\p1}}{path}{{\\p0}}", layer=9)
        else:
            rmax = 0.8 * math.hypot(W, H)
            a.add(s, o + d + 0.02, f"{{\\an5\\pos({W / 2:.0f},{0.24 * H:.0f})\\bord6\\shad0\\1a&HFF&\\3c{col(YEL)}\\blur1.5"
                                   f"\\fscx0\\fscy0\\t({m0},{m1},\\fscx100\\fscy100)\\p1}}{circle(rmax)}{{\\p0}}", layer=9)
    tw = {i: t for i, t in enumerate(trans)}  # by index, in SEQ order

    # 2 Dolly, 3 Cell: kinetic titles
    kinetic(a, 7.8, 12.6, [("A", "FFFFFF"), ("live,", "FFFFFF"), ("virtual", "FFFFFF"), ("copy", "FFFFFF"),
                           ("of", "FFFFFF"), ("your", YEL), ("line", YEL)])
    kinetic(a, 16.8, 24.6, [("One", "FFFFFF"), ("cell.", "FFFFFF"), ("Every", "FFFFFF"), ("part", "FFFFFF"),
                            ("streams", "FFFFFF"), ("live", YEL), ("data.", YEL)])

    # X1 data-flow screen
    x1 = (shift(27.3) - 4.0, shift(27.3))
    t0, t1 = x1
    a.add(t0 + 0.1, t1, f"{{\\pos(960,300)\\fad(250,200)\\move(960,330,960,300,0,400)}}Every signal, "
                        f"{{\\c{col(YEL)}}}live", style="Head", layer=3)
    nodes = [("Machines", "CNC · conveyors · robot"), ("MQTT", "cell/<station>/<signal>"),
             ("Digital twin", "3D cell, same data"), ("Dashboard", "risk, line state, losses")]
    nx, ny, nw, nh, gap = 180, 470, 300, 150, 120
    for i, (name, sub) in enumerate(nodes):
        x = nx + i * (nw + gap)
        s = t0 + 0.35 + i * 0.18
        a.shape(s, t1, x, ny, rect(nw, nh), f"\\1c{col(CARD)}\\1a&H18&\\fad(200,200)", layer=2)
        a.shape(s, t1, x, ny, rect(nw, 6), f"\\1c{col(YEL)}\\fscx0\\t(0,300,\\fscx100)\\fad(0,200)", layer=3)
        a.add(s, t1, f"{{\\pos({x + nw / 2:.0f},{ny + 62})\\fad(200,200)}}{name}", style="Node", layer=3)
        a.add(s + 0.1, t1, f"{{\\pos({x + nw / 2:.0f},{ny + 112})\\fad(200,200)}}{sub}", style="NodeSub", layer=3)
        if i < 3:
            ax0, ax1, ay = x + nw + 14, x + nw + gap - 14, ny + nh / 2
            a.shape(s + 0.15, t1, ax0, ay - 2, rect(ax1 - ax0, 4),
                    f"\\1c{col(YEL)}\\1a&H40&\\fscx0\\t(0,250,\\fscx100)\\fad(0,200)", layer=3)
            a.shape(s + 0.3, t1, ax1 - 14, ay - 9, "m 0 0 l 14 9 l 0 18", f"\\1c{col(YEL)}\\fad(150,200)", layer=3)
            for k in range(6):  # pulses flowing along the arrow
                ps = s + 0.45 + k * 0.42
                if ps + 0.5 < t1:
                    a.shape(ps, ps + 0.5, 0, 0, circle(6),
                            f"\\an5\\move({ax0},{ay},{ax1 - 12},{ay})\\1c{col(YEL)}\\blur1\\fad(80,80)", layer=4)
    a.add(t0 + 0.6, t1, "{\\pos(960,930)\\fad(300,200)}Demo telemetry: AI4I 2020 dataset (CC BY 4.0), replayed over MQTT",
          style="Foot", layer=3)

    # 5 CNC healthy -> going to fail
    s3 = P["P3"][0]
    ring(a, s3, *STACK["green"], 26, colour=GRN, pulses=1, hold=2.2)
    kinetic(a, s3, shift(39.5) - 2.5 - 0.55, [("Healthy", GRN), ("now.", GRN), ("Failing", YEL), ("soon.", YEL)])

    # Chapter cards
    def chapter(t0, t1, num, title, accent):
        a.add(t0 + 0.05, t1, f"{{\\pos(520,540)\\fad(120,150)\\fscx70\\fscy70\\alpha&HFF&"
                             f"\\t(0,260,\\fscx100\\fscy100\\alpha&H00&)\\c{col(accent)}}}{num}", style="ChapNum", layer=3)
        a.shape(t0 + 0.15, t1, 560, 460, rect(8, 160), f"\\1c{col(accent)}\\org(560,540)\\fscy0\\t(0,250,\\fscy100)\\fad(0,150)",
                layer=3)
        a.add(t0 + 0.25, t1, f"{{\\move(560,540,610,540,0,350)\\clip(576,0,1920,1080)\\fad(150,150)}}{title}",
              style="ChapTitle", layer=3)
    x2 = (shift(39.5) - 2.5, shift(39.5))
    chapter(*x2, "01", "It sees it coming", YEL)
    # The chapter rule travels to the failure-risk bar as the CNC shot comes in (match into the dashboard row).
    rule0, rule1 = (610, 610, 640, 6), (RISK_BAR[0], RISK_BAR[3] + 1.5, RISK_BAR[2] - RISK_BAR[0], 3)
    a.shape(x2[0] + 0.45, x2[1] - 0.35, rule0[0], rule0[1], rect(rule0[2], rule0[3]),
            f"\\1c{col(YEL)}\\fscx0\\t(0,400,\\fscx100)", layer=4)
    def morph_rule(s, frac, e):
        q = frac * frac * (3 - 2 * frac)
        x, y, w_, h_ = [rule0[i] + (rule1[i] - rule0[i]) * q for i in range(4)]
        a.shape(s, e, x, y, rect(w_, h_), f"\\1c{col(YEL)}", layer=9)
    step_events(a, x2[1] - 0.35, x2[1] + 0.25, morph_rule)
    a.shape(x2[1] + 0.25, x2[1] + 0.9, rule1[0], rule1[1], rect(rule1[2], rule1[3]), f"\\1c{col(YEL)}\\fad(0,450)", layer=6)
    x3 = (shift(55.0) - 2.5, shift(55.0))
    chapter(*x3, "02", "Ignore the warning", RED)
    x4 = (shift(72.0) - 2.5, shift(72.0))
    chapter(*x4, "03", "Act on it", GRN)

    # 6 It sees it coming
    w3, w5, w6 = P["W3"][0], P["W5"][0], P["W6"][0]
    underline(a, w3, w3 + 3.6, RISK_BAR)
    box(a, w3 + 0.1, w3 + 3.2, RISK_PCT, glow=True)
    amber = shift(46.0)  # the light turns amber
    ring(a, amber - 0.05, *STACK["amber"], 34, pulses=2, hold=2.3)
    callout(a, w5, x3[0] - 0.6, CHANGE_DUE, "Change the tool now", to_x=1330)
    kinetic(a, w6, x3[0] - 0.55, [("Line", "FFFFFF"), ("still", "FFFFFF"), ("running", GRN)])

    # 7 Ignore it
    fail = shift(59.3)
    kinetic(a, fail, fail + 3.3, [("TOOL", RED), ("FAILURE", RED)], accent=RED)
    box(a, fail, fail + 2.2, CHIP, colour=RED, glow=True, pulse=True)
    lens_t0, lens_t1 = P["F3"][0], P["F3"][0] + 2.4
    lx, ly = LENS_POS
    lw, lh = (CNC_ROW[2] - CNC_ROW[0]) * LENS_SCALE, (CNC_ROW[3] - CNC_ROW[1]) * LENS_SCALE
    a.shape(lens_t0, lens_t1, lx - 3, ly - 3, rect(lw + 6, lh + 6), f"\\1a&HFF&\\3c{col(YEL)}\\bord3\\fad(300,300)", layer=6)
    box(a, lens_t0, lens_t1, CNC_ROW, pad=3)
    a.shape(lens_t0, lens_t1, 0, 0, quad_line(lx + lw + 3, ly - 3, CNC_ROW[0] - 3, CNC_ROW[1] - 3, 2),
            f"\\1c{col(YEL)}\\fad(300,300)", layer=6)
    a.shape(lens_t0, lens_t1, 0, 0, quad_line(lx + lw + 3, ly + lh + 3, CNC_ROW[0] - 3, CNC_ROW[3] + 3, 2),
            f"\\1c{col(YEL)}\\fad(300,300)", layer=6)
    box(a, P["F5"][0], P["F5"][0] + 2.4, LINE_CHIP, colour=RED)
    callout(a, P["F6"][0], x4[0] - 0.6, PARTS_LOST, "Counting every lost part", to_x=1330)

    # 8 Act on it
    a2, a3, a4, a6 = P["A2"][0], P["A3"][0], P["A4"][0], P["A6"][0]
    ring(a, a2, *STACK["amber"], 34, pulses=1, hold=1.6)
    underline(a, a3, a3 + 2.6, RISK_BAR, colour=GRN)
    ring(a, a4, *STACK["green"], 26, colour=GRN, pulses=2, hold=2.0)
    kinetic(a, a3, a6 - 0.15, [("Tool", "FFFFFF"), ("changed.", "FFFFFF"), ("Line", "FFFFFF"), ("keeps", "FFFFFF"),
                               ("running.", GRN)], accent=GRN)
    x5 = (shift(84.5) - 4.0, shift(84.5))
    kinetic(a, a6, x5[0] - 0.5, [("Zero", GRN), ("parts", GRN), ("lost", GRN)], accent=GRN)
    box(a, a6, x5[0] - 0.45, PARTS_LOST, colour=GRN, glow=True)
    # Match into the comparison: the parts-lost box grows into the "acted on" panel.
    x0, y0, x1, y1 = PARTS_LOST
    pw, ph = (LINE_CARD[2] - LINE_CARD[0]) * PANEL_SCALE, (LINE_CARD[3] - LINE_CARD[1]) * PANEL_SCALE
    b0, b1 = (x0 - 5, y0 - 5, x1 - x0 + 10, y1 - y0 + 10), (PANEL_X[1] - 3, PANEL_Y - 3, pw + 6, ph + 6)
    def morph_box(s, frac, e):
        q = frac * frac * (3 - 2 * frac)
        x, y, w_, h_ = [b0[i] + (b1[i] - b0[i]) * q for i in range(4)]
        a.shape(s, e, x, y, rect(w_, h_), f"\\1a&HFF&\\3c{col(GRN)}\\bord3", layer=9)
    step_events(a, x5[0] - 0.45, x5[0] + 0.35, morph_box)

    # X5 comparison
    t0, t1 = x5
    a.add(t0 + 0.1, t1, "{\\pos(960,300)\\fad(250,200)\\move(960,325,960,300,0,400)}Same warning. "
                        f"{{\\c{col(YEL)}}}Two outcomes.", style="Head", layer=3)
    for j, (lab, c) in enumerate((("Ignored", RED), ("Acted on", GRN))):
        a.add(t0 + 0.3, t1, f"{{\\pos({PANEL_X[j]},{PANEL_Y - 14})\\fad(200,200)\\c{col(c)}}}{lab}", style="Panel", layer=3)
        a.shape(t0 + (0.35 if j == 0 else 0.35), t1, PANEL_X[j] - 3, PANEL_Y - 3, rect(pw + 6, ph + 6),
                f"\\1a&HFF&\\3c{col(c)}\\bord3\\fad(250,200)", layer=4)

    # 10 Your line
    y2 = P["Y2"][0]
    s_t0 = shift(94.0)
    kinetic(a, y2, s_t0 - 0.5, [("MQTT", YEL), ("today", "FFFFFF"), ("·", "FFFFFF"), ("OPC", YEL), ("UA", YEL),
                                ("on", "FFFFFF"), ("request", "FFFFFF")])

    # 11 What we build (services)
    t0, t1 = s_t0, shift(101.0)
    a.add(t0 + 0.1, t1, "{\\pos(960,300)\\fad(250,250)\\move(960,325,960,300,0,400)}What we build for "
                        f"{{\\c{col(YEL)}}}manufacturers", style="Head", layer=3)
    tiles = [("01", "Digital\\Ntwins"), ("02", "Predictive\\Nmaintenance"), ("03", "Predictive\\Nsimulations")]
    for i, (n, name) in enumerate(tiles):
        x, y = 210 + i * 510, 420
        s = t0 + 0.45 + i * 0.25
        a.shape(s, t1, x, y, rect(480, 210), f"\\1c{col(CARD)}\\1a&H14&\\fad(250,250)\\move({x},{y + 30},{x},{y},0,350)",
                layer=2, an=7)
        a.shape(s, t1, x, y, rect(480, 6), f"\\1c{col(YEL)}\\fscx0\\t(150,450,\\fscx100)\\fad(0,250)", layer=3)
        a.add(s + 0.1, t1, f"{{\\pos({x + 32},{y + 30})\\fad(200,250)}}{n}", style="TileNum", layer=3)
        a.add(s + 0.15, t1, f"{{\\pos({x + 32},{y + 74})\\fad(200,250)}}{name}", style="Tile", layer=3)
    a.add(t0 + 1.6, t1, "{\\pos(960,720)\\fad(300,250)}Next in this series:  virtual commissioning  ·  "
                        "full monitoring  ·  training & safety", style="Next", layer=3)

    # 12 End card: a light sweep across the call-to-action box once it has faded in.
    end0 = shift(101.0)
    g0 = end0 + 1.95
    a.shape(g0, g0 + 0.7, 0, 0, "m 0 0 l 60 0 l 20 104 l -40 104",
            f"\\1c&HFFFFFF&\\1a&H70&\\blur6\\clip(440,600,1480,704)\\move(380,600,1500,600)", layer=6)
    a.write(cfg.work / "overlays.ass")
    return dict(lens=(lens_t0, lens_t1))


def render_overlay(cfg, info, out):
    lt0, lt1 = info["lens"]
    x0, y0, x1, y1 = CNC_ROW
    lw, lh = round((x1 - x0) * LENS_SCALE), round((y1 - y0) * LENS_SCALE)
    fd = f":fontsdir='{cfg.fonts_esc}'" if cfg.fonts_esc else ""
    f = [f"[0:v]split[b][c]",
         f"[c]crop={x1 - x0}:{y1 - y0}:{x0}:{y0},scale={lw}:{lh}:flags=lanczos,format=rgba,"
         f"fade=t=in:st={lt0:.3f}:d=0.3:alpha=1,fade=t=out:st={lt1 - 0.3:.3f}:d=0.3:alpha=1[lens]",
         f"[b][lens]overlay=x={LENS_POS[0]}:y={LENS_POS[1]}:enable='between(t,{lt0:.3f},{lt1:.3f})'[o]",
         f"[o]ass=overlays.ass{fd},ass=subs.ass{fd},format=yuv420p[v]"]
    (cfg.work / "overlay.txt").write_text(";\n".join(f), encoding="utf-8")
    ff(cfg, ["-i", "base.mp4", "-/filter_complex", "overlay.txt", "-map", "[v]", "-an", "-frames:v", str(round(TOTAL * FPS)), "-c:v", "libx264",
             "-preset", cfg.preset, "-crf", "17", "-r", str(FPS), "-pix_fmt", "yuv420p", str(out)], cwd=cfg.work)
    print("picture done")


# ------------------------------------------------------------------------------------------------ audio
def game_bed(cfg, trans):
    """v02's game audio cut at the inserts; each mid screen carries the ambience of its own footage."""
    takes = dict(A=cfg.take_a, B=cfg.take_b, C=cfg.take_c)
    args = ["-i", str(cfg.v02_game)]
    f, labs, a_prev = [], [], 0.0
    for at, l, cid in INSERTS:
        labs.append(f"g{len(labs)}")
        f.append(f"[0:a]atrim={a_prev}:{at},asetpts=PTS-STARTPTS,afade=t=in:d=0.05,afade=t=out:st={at - a_prev - 0.08:.3f}:"
                 f"d=0.08[{labs[-1]}]")
        c = CLIPS[cid]
        args += ["-i", str(takes[c["src"]])]
        labs.append(f"g{len(labs)}")
        f.append(f"[{len(args) // 2 - 1}:a]atrim={c['t']}:{c['t'] + l},asetpts=PTS-STARTPTS,aresample=48000,"
                 f"aformat=channel_layouts=stereo,afade=t=in:d=0.15,afade=t=out:st={l - 0.15}:d=0.15[{labs[-1]}]")
        a_prev = at
    labs.append(f"g{len(labs)}")
    f.append(f"[0:a]atrim={a_prev}:{V02_TOTAL},asetpts=PTS-STARTPTS,afade=t=in:d=0.05[{labs[-1]}]")
    f.append("".join(f"[{x}]" for x in labs) + f"concat=n={len(labs)}:v=0:a=1,apad=whole_dur={TOTAL},atrim=0:{TOTAL}[bed]")
    last = "bed"
    # Optional transition sounds, low under the ambience: chapter cards and the other masked reveals.
    sfx = []
    for k, t in enumerate(trans):
        if t["kind"] != "diag":
            continue
        chapter_in = k in (3, 5, 7)    # into X2, X3, X4
        path = cfg.sfx_chapter if chapter_in else cfg.sfx_reveal
        if path:
            sfx.append((path, t["offset"] - 0.1, -20 if chapter_in else -24))
    for j, (path, at, gain) in enumerate(sfx):
        args += ["-i", str(path)]
        ix = len(args) // 2 - 1
        f.append(f"[{ix}:a]aresample=48000,aformat=channel_layouts=stereo,volume={gain}dB,"
                 f"adelay={int(at * 1000)}|{int(at * 1000)}[s{j}]")
    if sfx:
        f.append(f"[{last}]" + "".join(f"[s{j}]" for j in range(len(sfx))) +
                 f"amix=inputs={len(sfx) + 1}:normalize=0:duration=first[bed2]")
        last = "bed2"
    f.append(f"[{last}]aformat=sample_rates=48000:channel_layouts=stereo[out]")
    last = "out"
    (cfg.work / "game.txt").write_text(";\n".join(f), encoding="utf-8")
    ff(cfg, [*args, "-/filter_complex", str(cfg.work / "game.txt"), "-map", f"[{last}]", "-ar", "48000",
             "-c:a", "pcm_s16le", str(cfg.work / "game.wav")])


def mix(cfg):
    """build2.mix with explicit sample formats, so older ffmpeg builds can negotiate sidechaincompress.
    Same chain and levels: bed at -30 LUFS, sparks lifted 5 dB, 5:1 ducking, two-pass loudnorm to -14 / -1 dBTP."""
    game_i = build2.integrated(cfg, cfg.work / "game.wav")
    gain = -30 - game_i
    a, b = build2.SPARKS
    fmt = "aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo"
    ff(cfg, ["-i", str(cfg.work / "game.wav"), "-i", str(cfg.work / "voice.wav"), "-filter_complex",
             f"[0:a]{fmt},volume={gain:.2f}dB,volume=enable='between(t,{a},{b})':volume=5dB[g];[1:a]{fmt},asplit=2[sc][v];"
             "[g][sc]sidechaincompress=threshold=0.02:ratio=5:attack=15:release=350[gd];"
             "[v][gd]amix=inputs=2:normalize=0[m]", "-map", "[m]", "-c:a", "pcm_s16le", str(cfg.work / "mix.wav")])
    r = subprocess.run([cfg.ffmpeg, "-hide_banner", "-i", str(cfg.work / "mix.wav"), "-af",
                        f"{fmt},loudnorm=I=-14:TP=-1:LRA=11:print_format=json", "-f", "null", "-"], capture_output=True, text=True)
    m = json.loads(r.stderr[r.stderr.rindex("{"):r.stderr.rindex("}") + 1])
    ff(cfg, ["-i", str(cfg.work / "mix.wav"), "-af",
             f"{fmt},loudnorm=I=-14:TP=-1:LRA=11:measured_I={m['input_i']}:measured_TP={m['input_tp']}:"
             f"measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:"
             "linear=true,aresample=48000,aformat=channel_layouts=stereo", "-c:a", "pcm_s16le", str(cfg.work / "final.wav")])
    return game_i


def audio(cfg, cues, trans):
    game_bed(cfg, trans)
    build2.TOTAL_S = TOTAL
    build2.SPARKS = (shift(58.5), shift(72.0) - 0.05)  # 67.5-81.0 s: lift the spark crackle
    build2.voice(cfg, [(pid, s, e, emph, text) for pid, s, e, emph, text in cues])
    gi = mix(cfg)
    print(f"game bed was {gi:.1f} LUFS")


# ------------------------------------------------------------------------------------------------- main
def parse():
    rec = REPO / "Builds" / "Recordings"
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out", type=pathlib.Path, help="output .mp4")
    ap.add_argument("--stage", default="all", choices=["all", "clips", "base", "overlay", "audio", "mux"])
    ap.add_argument("--v02-work", type=pathlib.Path, default=HERE / "build" / "work",
                    help="build2.py's work folder (picture.mp4, game.wav)")
    ap.add_argument("--take-a", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeA_20261020_052351.mp4")
    ap.add_argument("--take-b", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeB_20261020_052722.mp4")
    ap.add_argument("--take-c", type=pathlib.Path, default=rec / "2DS_FactoryTwin_TakeC_20261020_053354.mp4")
    ap.add_argument("--voice", default="am_michael")
    ap.add_argument("--vo", type=pathlib.Path)
    ap.add_argument("--work", type=pathlib.Path, default=HERE / "build" / "work3")
    ap.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    ap.add_argument("--fonts", default="C:/Windows/Fonts" if os.name == "nt" else "",
                    help="folder with bahnschrift.ttf, segoeui.ttf, seguisb.ttf (libass fontsdir)")
    ap.add_argument("--preset", default="slow", help="x264 preset for the final picture")
    ap.add_argument("--sfx-chapter", type=pathlib.Path, help="whoosh under the chapter-card reveals (CC0, see LICENSES)")
    ap.add_argument("--sfx-reveal", type=pathlib.Path, help="shorter whoosh under the other masked reveals")
    a = ap.parse_args()
    for p in (a.take_a, a.take_b, a.take_c, a.v02_work / "picture.mp4", a.v02_work / "game.wav"):
        if not pathlib.Path(p).is_file():
            ap.error(f"not found: {p} (run build2.py first for the v02 work files)")
    cfg = build2.Config(ffmpeg=a.ffmpeg, take_a=a.take_a.resolve(), take_b=a.take_b.resolve(), take_c=a.take_c.resolve(),
                        logo=pathlib.Path("."), vo=(a.vo or HERE / "build" / "vo" / a.voice).resolve(),
                        work=a.work.resolve(), bahn="", segoe="", segoe_b="")
    cfg.pic = (a.v02_work / "picture.mp4").resolve()
    cfg.v02_game = (a.v02_work / "game.wav").resolve()
    cfg.fonts_esc = str(a.fonts).replace("\\", "/").replace(":", "\\:", 1) if a.fonts else ""
    cfg.preset = a.preset
    cfg.sfx_chapter, cfg.sfx_reveal = a.sfx_chapter, a.sfx_reveal
    return a.out.resolve(), a.stage, cfg


def main():
    out, stage, cfg = parse()
    cfg.work.mkdir(parents=True, exist_ok=True)
    pieces, trans = plan()
    cues = phrase_times(cfg)
    for pid, s, e, emph, text in cues:
        print(f"{pid} {s:6.2f}-{e:6.2f} {'*' if emph else ' '} {text[:60]}")
    for t in trans:
        print(f"transition {t['kind']:4s} {t['offset']:7.3f}-{t['offset'] + t['d']:7.3f}")
    picture = cfg.work / "picture.mp4"
    if stage in ("all", "clips"):
        render_clips(cfg, pieces)
    if stage in ("all", "base"):
        render_base(cfg, pieces, trans)
    if stage in ("all", "overlay"):
        build_subs(cfg, cues)
        info = build_overlays(cfg, cues, trans)
        render_overlay(cfg, info, picture)
    if stage in ("all", "audio"):
        audio(cfg, cues, trans)
    if stage in ("all", "mux"):
        out.parent.mkdir(parents=True, exist_ok=True)
        ff(cfg, ["-i", str(picture), "-i", str(cfg.work / "final.wav"), "-map", "0:v", "-map", "1:a", "-c:v", "copy",
                 "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-shortest", "-movflags", "+faststart", str(out)])
        print("wrote", out)


if __name__ == "__main__":
    main()
