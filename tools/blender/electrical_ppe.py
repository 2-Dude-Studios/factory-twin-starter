"""Builds the electrical PPE kit, the UPS feed panel and the PPE board (M27, ahead of M20) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/electrical_ppe.py -- <out.fbx> [<out.blend>]

Our own low-poly models, no third-party content and no branding (so no row in docs/LICENSES.md). Every object is
named for ElectricalXrSetup, which picks the meshes up by name from Assets/_Project/Art/Electrical, with its origin
where the hand holds it. Geometry is written in Unity's axes (unity_parts.py): X east, Y up, Z north, metres; a face
meant to be read faces +Z and its lettering runs along -X (as you face it, your right is -X).
The kit is the arc flash label's PPE category 2 (demo study DS-7: 6.1 cal/cm2): arc-rated balaclava, safety
glasses, hearing protection, hard hat with an arc-rated face shield, arc-rated jacket, and Class 0 rubber
insulating gloves with leather protectors. Second pass (2026-11-13): more detail on every item, the UPS panel's
breaker module apart from its lever, and the PPE board for the gear bench.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, arc, args, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('Electrical_PPE.fbx')
setup_scene()

NAVY = material('PPE_ArcNavy', (0.06, 0.10, 0.20), roughness=0.85)
NAVY_DARK = material('PPE_ArcNavyDark', (0.03, 0.05, 0.10), roughness=0.9)
REFLECT = material('PPE_Reflective', (0.78, 0.80, 0.80), metallic=0.4, roughness=0.35)
FACE = material('PPE_Opening', (0.01, 0.01, 0.012), roughness=0.95)
LENS = material('PPE_Lens', (0.30, 0.36, 0.40), metallic=0.2, roughness=0.08)
FRAME = material('PPE_FrameBlack', (0.02, 0.02, 0.025), roughness=0.5)
CUP = material('PPE_MuffRed', (0.70, 0.06, 0.04), roughness=0.45)
CUSHION = material('PPE_Cushion', (0.05, 0.05, 0.055), roughness=0.75)
WHITE = material('PPE_HatWhite', (0.90, 0.90, 0.88), roughness=0.4)
SHIELD = material('PPE_ShieldGreen', (0.22, 0.38, 0.26), metallic=0.1, roughness=0.12)
BRACKET = material('PPE_BracketGrey', (0.20, 0.21, 0.22), roughness=0.5)
RUBBER = material('PPE_GloveRubber', (0.85, 0.30, 0.04), roughness=0.55)
LEATHER = material('PPE_GloveLeather', (0.55, 0.38, 0.20), roughness=0.8)
LEATHER_DARK = material('PPE_GloveLeatherDark', (0.36, 0.24, 0.12), roughness=0.85)
STEEL = material('PPE_Steel', (0.70, 0.71, 0.73), metallic=1.0, roughness=0.3)
LABEL_WHITE = material('PPE_LabelWhite', (0.93, 0.93, 0.90), roughness=0.6)
LABEL_RED = material('PPE_LabelRed', (0.75, 0.05, 0.04), roughness=0.5)
PANEL_GREY = material('UPS_PanelGrey', (0.62, 0.64, 0.66), roughness=0.5)
PANEL_DARK = material('UPS_PanelDark', (0.12, 0.13, 0.14), roughness=0.5)
LABEL_YELLOW = material('UPS_LabelYellow', (0.95, 0.78, 0.05), roughness=0.5)
LABEL_INK = material('UPS_LabelInk', (0.02, 0.02, 0.02), roughness=0.6)
LEVER_RED = material('UPS_LeverRed', (0.72, 0.04, 0.03), roughness=0.45)
CONDUIT = material('UPS_Conduit', (0.55, 0.57, 0.58), metallic=0.8, roughness=0.35)
BOARD = material('Bench_Board', (0.16, 0.18, 0.20), roughness=0.7)
BOARD_BAND = material('Bench_Band', (0.10, 0.45, 0.95), roughness=0.5)

READ = (-1, 0, 0)  # lettering on a face that looks +Z runs along -X


def curved_panel(part, radius, start_deg, end_deg, top, bottom, thick, mat, steps=14, center=(0, 0, 0)):
    """A curved sheet round Unity's Y axis (the face shield): angle 0 faces +Z (north, forward)."""
    bm = bmesh.new()
    outer, inner = [], []
    for i in range(steps + 1):
        a = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        for y in (top, bottom):
            for r, ring in ((radius, outer), (radius - thick, inner)):
                ring.append(bm.verts.new((center[0] + r * math.sin(a), center[1] + y, center[2] + r * math.cos(a))))

    def quad(ring, i, flip):
        a, b, c, d = ring[2 * i], ring[2 * i + 1], ring[2 * i + 3], ring[2 * i + 2]
        bm.faces.new((a, b, c, d) if not flip else (d, c, b, a))
    for i in range(steps):
        quad(outer, i, False)
        quad(inner, i, True)
        bm.faces.new((outer[2 * i], outer[2 * i + 2], inner[2 * i + 2], inner[2 * i]))
        bm.faces.new((outer[2 * i + 1], inner[2 * i + 1], inner[2 * i + 3], outer[2 * i + 3]))
    bm.faces.new((outer[0], inner[0], inner[1], outer[1]))
    bm.faces.new((outer[-2], outer[-1], inner[-1], inner[-2]))
    part.merge(bm, mat, smooth=True)


def shield_edge(radius, start_deg, end_deg, y, steps=14, center=(0, 0, 0)):
    """Points along the shield's curved edge at height y."""
    return [(center[0] + radius * math.sin(math.radians(start_deg + (end_deg - start_deg) * i / steps)), center[1] + y,
             center[2] + radius * math.cos(math.radians(start_deg + (end_deg - start_deg) * i / steps))) for i in range(steps + 1)]


def ellipse(center, rx, ry, steps=20):
    """A closed ellipse in the XY plane at center (for tube(closed=True))."""
    return [(center[0] + rx * math.cos(2 * math.pi * i / steps), center[1] + ry * math.sin(2 * math.pi * i / steps), center[2])
            for i in range(steps)]


# --- PPE_Balaclava: arc-rated hood on a head form; a bound face opening, seams and its rating label at the nape.
bal = Part('PPE_Balaclava')
bal.lathe([(0.0, 0.172), (0.03, 0.17), (0.058, 0.162), (0.078, 0.145), (0.091, 0.122), (0.098, 0.09), (0.099, 0.05),
           (0.094, 0.015), (0.085, -0.02), (0.074, -0.045), (0.07, -0.07), (0.076, -0.10), (0.092, -0.125),
           (0.106, -0.145), (0.0, -0.145)], NAVY, segments=32)
bal.box((0, 0.048, 0.086), (0.104, 0.07, 0.03), FACE, bevel=0.016)
bal.tube(ellipse((0.0, 0.048, 0.098), 0.055, 0.038), 0.0055, NAVY_DARK, segments=8, closed=True)
bal.tube([(0.0, 0.168, 0.0), (0.0, 0.12, -0.075), (0.0, 0.05, -0.098), (0.0, -0.05, -0.08), (0.0, -0.14, -0.1)],
         0.0018, NAVY_DARK, segments=6)
bal.box((0.0, -0.09, -0.08), (0.035, 0.022, 0.003), LABEL_WHITE)
bal.text('ARC 12', (0.0, -0.09, -0.0818), 0.008, LABEL_INK, right=(1, 0, 0), up=(0, 1, 0))
bal.build((0.0, 1.0, 0.0), sharp_deg=60)

# --- PPE_SafetyGlasses: wrap-round, brow bar, nose pads, side shields, hinged temples; origin at the bridge.
gl = Part('PPE_SafetyGlasses')
for s in (-1, 1):
    gl.box((s * 0.031, -0.002, 0.002), (0.052, 0.038, 0.003), LENS, bevel=0.007)
    gl.box((s * 0.066, -0.002, -0.012), (0.004, 0.034, 0.028), LENS, bevel=0.003)   # side shield
    gl.box((s * 0.068, 0.012, -0.004), (0.008, 0.008, 0.012), FRAME, bevel=0.002)   # hinge block
    gl.tube([(s * 0.069, 0.012, -0.01), (s * 0.072, 0.012, -0.06), (s * 0.07, 0.006, -0.105), (s * 0.062, -0.018, -0.125)],
            0.0028, FRAME, segments=8)
    gl.sphere((s * 0.009, -0.016, -0.004), 0.0045, CUSHION, segments=8)             # nose pad
gl.box((0.0, 0.018, 0.004), (0.13, 0.008, 0.008), FRAME, bevel=0.003)                # brow bar
gl.tube([(-0.012, 0.004, 0.004), (0.0, 0.0, 0.006), (0.012, 0.004, 0.004)], 0.0025, FRAME, segments=8)
gl.build((0.3, 1.0, 0.0))

# --- PPE_HearingProtection: ear muffs: a padded band over the top, sliders, cups with cushions; origin at the band.
em = Part('PPE_HearingProtection')
em.tube(arc((0, -0.085, 0), 0.085, 0, 180, plane='xy', steps=22), 0.0045, STEEL, segments=8)
em.tube(arc((0, -0.085, 0), 0.091, 40, 140, plane='xy', steps=14), 0.008, CUSHION, segments=10)
for s in (-1, 1):
    em.box((s * 0.088, -0.07, 0.0), (0.008, 0.05, 0.014), FRAME, bevel=0.002)      # slider
    em.cylinder((s * 0.088, -0.095, 0.0), 0.006, 0.02, 'z', FRAME, segments=10)    # yoke pivot
    em.cylinder((s * 0.098, -0.11, 0.0), 0.044, 0.032, 'x', CUP, segments=24)
    em.cylinder((s * 0.116, -0.11, 0.0), 0.038, 0.004, 'x', FRAME, segments=24)    # cup face ring
    em.ring((s * 0.078, -0.11, 0.0), 0.044, 0.024, 0.012, 'x', CUSHION, segments=24)
em.build((0.6, 1.0, 0.0))

# --- PPE_HardHatShield: ribbed hard hat, suspension, slot brackets, an arc-rated face shield in a frame and chin
# guard; origin at the crown's centre.
hh = Part('PPE_HardHatShield')
hh.lathe([(0.0, 0.138), (0.04, 0.136), (0.07, 0.124), (0.092, 0.104), (0.106, 0.076), (0.113, 0.042), (0.116, 0.012),
          (0.118, 0.004), (0.0, 0.004)], WHITE, segments=36)
hh.tube(arc((0, 0.0, 0.0), 0.118, 0, 360, plane='xz', steps=36)[:-1], 0.004, WHITE, segments=8, closed=True)
for x in (-0.03, 0.0, 0.03):
    hh.tube([(x, 0.03, 0.112), (x, 0.09, 0.098), (x, 0.13, 0.05), (x, 0.142, 0.0), (x, 0.13, -0.05), (x, 0.09, -0.098),
             (x, 0.03, -0.112)], 0.0035, WHITE, segments=6)
hh.box((0.0, 0.006, 0.128), (0.17, 0.008, 0.05), WHITE, bevel=0.004)                 # peak
hh.tube(arc((0, 0.025, 0.0), 0.098, 0, 360, plane='xz', steps=30)[:-1], 0.006, FRAME, segments=8, closed=True)
for s in (-1, 1):
    hh.box((s * 0.117, 0.02, 0.03), (0.012, 0.028, 0.045), BRACKET, bevel=0.003)   # accessory slot
    hh.cylinder((s * 0.127, 0.01, 0.045), 0.012, 0.012, 'x', FRAME, segments=16)   # pivot knob
    hh.tube([(s * 0.124, 0.008, 0.05), (s * 0.13, -0.004, 0.09), (s * 0.12, -0.01, 0.13)], 0.005, BRACKET, segments=8)
curved_panel(hh, 0.145, -62, 62, -0.008, -0.232, 0.003, SHIELD, steps=18)
hh.tube(shield_edge(0.146, -62, 62, -0.006, steps=18), 0.006, BRACKET, segments=8)  # top frame
hh.tube(shield_edge(0.146, -62, 62, -0.234, steps=18), 0.0035, FRAME, segments=6)   # chin binding
for a in (-62, 62):
    x, z = 0.146 * math.sin(math.radians(a)), 0.146 * math.cos(math.radians(a))
    hh.tube([(x, -0.006, z), (x, -0.234, z)], 0.0035, FRAME, segments=6)
hh.box((0.0, -0.02, 0.15), (0.05, 0.016, 0.003), LABEL_WHITE)
hh.text('ARC 12', (0.0, -0.02, 0.1518), 0.009, LABEL_INK, right=READ)
hh.build((0.9, 1.0, 0.0), sharp_deg=55)

# --- PPE_ArcJacket: on a hanger; collar, storm flap and zip, pockets, cuffs, reflective bands, its rating patch;
# origin at the top of the hook.
jk = Part('PPE_ArcJacket')
jk.tube([(0.0, 0.0, 0.0), (0.0, 0.03, 0.02), (0.0, 0.05, 0.0), (0.0, 0.03, -0.02), (0.0, -0.01, -0.01), (0.0, -0.05, 0.0)],
        0.003, STEEL, segments=8)
jk.tube([(-0.22, -0.11, 0.0), (0.0, -0.05, 0.0), (0.22, -0.11, 0.0)], 0.006, STEEL, segments=8)
jk.box((0.0, -0.46, 0.0), (0.46, 0.62, 0.11), NAVY, bevel=0.03)                      # body
for s in (-1, 1):
    jk.box((s * 0.15, -0.165, 0.0), (0.17, 0.08, 0.105), NAVY, bevel=0.03)           # shoulder slope
    jk.tube([(s * 0.22, -0.16, 0.0), (s * 0.27, -0.30, 0.012), (s * 0.30, -0.48, 0.022), (s * 0.31, -0.68, 0.03)],
            0.055, NAVY, segments=16)                                                 # sleeve
    jk.cylinder((s * 0.31, -0.70, 0.03), 0.05, 0.04, 'y', NAVY_DARK, segments=16)    # cuff
    jk.cylinder((s * 0.305, -0.60, 0.027), 0.057, 0.03, 'y', REFLECT, segments=16)   # sleeve band
    jk.box((s * 0.12, -0.63, 0.058), (0.13, 0.11, 0.006), NAVY_DARK, bevel=0.004)    # pocket
    jk.box((s * 0.12, -0.575, 0.062), (0.135, 0.03, 0.008), NAVY, bevel=0.003)       # pocket flap
jk.tube(arc((0.0, -0.11, 0.0), 0.065, 200, 340, plane='xz', steps=12), 0.018, NAVY_DARK, segments=10)  # collar
jk.box((0.0, -0.46, 0.059), (0.045, 0.6, 0.006), NAVY_DARK)                          # storm flap
jk.box((0.008, -0.46, 0.063), (0.006, 0.58, 0.002), FRAME)                           # zip
jk.box((0.008, -0.18, 0.066), (0.012, 0.022, 0.004), STEEL)                          # zip puller
for y in (-0.52, -0.70):
    jk.box((0.0, y, 0.0), (0.47, 0.03, 0.115), REFLECT)
jk.box((-0.13, -0.30, 0.058), (0.10, 0.06, 0.004), LABEL_WHITE)                      # rating patch, your left chest
jk.text('ARC RATED', (-0.13, -0.288, 0.0602), 0.011, LABEL_INK, right=READ)
jk.text('12 cal/cm2', (-0.13, -0.31, 0.0602), 0.011, LABEL_INK, right=READ)
jk.build((1.3, 1.0, 0.0), sharp_deg=50)


# --- PPE_Gloves: a pair, Class 0 rubber gloves with leather protectors over them, fingers up; origin between them.
def finger(part, base, length, bend, radius):
    x, y, z = base
    part.tube([(x, y, z), (x, y + length * 0.45, z + bend * 0.4), (x, y + length * 0.8, z + bend), (x, y + length, z + bend * 1.3)],
              radius, LEATHER, segments=8)
    part.sphere((x, y + length, z + bend * 1.3), radius * 0.98, LEATHER, segments=8)


def glove(part, x, thumb_side):
    part.cylinder((x, -0.075, 0.0), 0.052, 0.13, 'y', RUBBER, segments=20, radius2=0.044)        # rubber gauntlet
    part.ring((x, -0.14, 0.0), 0.056, 0.046, 0.01, 'y', RUBBER, segments=20)                      # rolled cuff
    part.box((x + thumb_side * 0.0, -0.10, 0.05), (0.03, 0.016, 0.004), LABEL_RED)               # Class 0 label
    part.text('CLASS 0', (x, -0.10, 0.0525), 0.006, LABEL_WHITE, right=(1, 0, 0))
    part.cylinder((x, -0.006, 0.0), 0.047, 0.03, 'y', LEATHER_DARK, segments=18, radius2=0.044)  # protector cuff
    part.box((x, 0.02, 0.0), (0.098, 0.018, 0.04), LEATHER_DARK, bevel=0.006)                    # wrist strap
    part.box((x - thumb_side * 0.035, 0.02, 0.022), (0.016, 0.014, 0.004), STEEL)                # buckle
    part.box((x, 0.068, 0.0), (0.09, 0.085, 0.034), LEATHER, bevel=0.013)                        # palm
    for i, dx in enumerate((-0.031, -0.0105, 0.0105, 0.031)):
        length = (0.06, 0.074, 0.07, 0.054)[::thumb_side][i]
        finger(part, (x + dx, 0.105, 0.0), length, 0.006, 0.0095)
    part.tube([(x + thumb_side * 0.04, 0.05, 0.004), (x + thumb_side * 0.062, 0.08, 0.008), (x + thumb_side * 0.07, 0.11, 0.012)],
              0.0115, LEATHER, segments=8)
    part.sphere((x + thumb_side * 0.07, 0.11, 0.012), 0.0112, LEATHER, segments=8)


gv = Part('PPE_Gloves')
glove(gv, -0.065, -1)
glove(gv, 0.065, 1)
gv.build((1.8, 1.0, 0.0), sharp_deg=60)

# --- UPS_Panel: the 120 V UPS feed's breaker panel on a post beside the cabinet; origin on the floor under it.
# CB-U1's module sits in the panel's window; its lever (UPS_Breaker, its own object) pivots at y 1.32, z 0.135; the
# lockout tab (UPS_HaspTab) beside it.
up = Part('UPS_Panel')
up.box((0.0, 0.7, 0.0), (0.07, 1.4, 0.07), PANEL_DARK)                                # post
up.box((0.0, 0.006, 0.0), (0.32, 0.012, 0.32), PANEL_DARK)                            # base plate
for x, z in ((-0.12, -0.12), (0.12, -0.12), (-0.12, 0.12), (0.12, 0.12)):
    up.cylinder((x, 0.016, z), 0.009, 0.01, 'y', STEEL, segments=8)                   # anchor bolts
up.box((0.0, 1.30, 0.06), (0.36, 0.46, 0.14), PANEL_GREY, bevel=0.01)                  # enclosure
up.box((0.0, 1.522, 0.1315), (0.35, 0.002, 0.002), PANEL_DARK)                         # door seam (top)
up.box((0.0, 1.078, 0.1315), (0.35, 0.002, 0.002), PANEL_DARK)                         # door seam (bottom)
for dx in (-0.172, 0.172):
    up.box((dx, 1.30, 0.1315), (0.002, 0.44, 0.002), PANEL_DARK)                       # door seams (sides)
for x, y in ((-0.16, 1.51), (0.16, 1.51), (-0.16, 1.09), (0.16, 1.09)):
    up.cylinder((x, y, 0.132), 0.006, 0.004, 'z', STEEL, segments=10)                 # screws
for y in (1.18, 1.42):
    up.cylinder((0.182, y, 0.12), 0.006, 0.05, 'y', STEEL, segments=10)               # hinges, on your left
for dx, dy, sx, sy in ((0.0, 0.085, 0.12, 0.012), (0.0, -0.085, 0.12, 0.012), (0.054, 0.0, 0.012, 0.18), (-0.054, 0.0, 0.012, 0.18)):
    up.box((dx, 1.32 + dy, 0.134), (sx, sy, 0.006), PANEL_DARK)                        # window frame
up.box((0.0, 1.32, 0.10), (0.11, 0.035, 0.012), STEEL)                                 # DIN rail
up.box((0.0, 1.32, 0.115), (0.045, 0.09, 0.04), PANEL_DARK, bevel=0.003)              # breaker module
up.text('CB-U1', (0.0, 1.362, 0.1358), 0.009, LABEL_WHITE, right=READ)
up.box((0.0, 1.47, 0.134), (0.30, 0.09, 0.003), LABEL_YELLOW)                          # nameplate
up.text('UPS FEED  CB-U1', (0.0, 1.487, 0.1362), 0.026, LABEL_INK, right=READ)
up.text('120 V CONTROL', (0.0, 1.455, 0.1362), 0.022, LABEL_INK, right=READ)
up.text('ON', (0.0, 1.415, 0.1352), 0.018, LABEL_INK, right=READ)
up.text('OFF', (0.0, 1.225, 0.1352), 0.018, LABEL_INK, right=READ)
up.box((0.0, 1.13, 0.134), (0.22, 0.05, 0.003), LABEL_RED)                             # danger label
up.text('DANGER  120 V', (0.0, 1.13, 0.1362), 0.02, LABEL_WHITE, right=READ)
up.tube([(0.0, 1.53, 0.04), (0.0, 1.62, 0.04), (0.0, 2.6, 0.04)], 0.012, CONDUIT, segments=10)  # conduit up
up.cylinder((0.0, 1.545, 0.04), 0.018, 0.03, 'y', CONDUIT, segments=10)               # fitting
up.box((0.04, 0.04, 0.04), (0.03, 0.03, 0.02), STEEL)                                   # ground lug
up.build((0.0, 0.0, 1.0))

br = Part('UPS_Breaker')
br.cylinder((0.0, 0.0, 0.0), 0.008, 0.05, 'x', PANEL_DARK, segments=12)               # pivot
br.box((0.0, 0.032, 0.006), (0.024, 0.064, 0.016), LEVER_RED, bevel=0.004)             # lever
for y in (0.046, 0.054, 0.062):
    br.box((0.0, y, 0.0145), (0.025, 0.003, 0.003), LABEL_INK)                         # grip ridges
br.build((0.3, 1.32, 1.0))

tab = Part('UPS_HaspTab')
tab.box((0.0, 0.0, 0.0), (0.032, 0.06, 0.004), LEVER_RED, bevel=0.003)
for i, y in enumerate((-0.012, -0.024)):
    tab.ring((0.0, y, 0.0), 0.0065, 0.0045, 0.006, 'z', STEEL, segments=14)
tab.box((0.0, 0.026, -0.004), (0.02, 0.012, 0.006), STEEL)
tab.build((0.4, 1.32, 1.0))

# --- PPE_Board: hangs on the gear bench's back board, facing the trainee: the kit in the order it goes on, and where
# the two padlocks hang. Origin at its centre. The bench rotates it to face the trainee (its +Z).
pb = Part('PPE_Board')
pb.box((0.0, 0.0, 0.0), (1.5, 0.66, 0.012), BOARD)
pb.box((0.0, 0.29, 0.007), (1.5, 0.08, 0.004), BOARD_BAND)
pb.text('ARC FLASH PPE  CATEGORY 2  -  PUT IT ON IN THIS ORDER', (0.0, 0.29, 0.0095), 0.034, LABEL_WHITE, right=READ)
steps = ['1  BALACLAVA', '2  SAFETY GLASSES', '3  HEARING PROTECTION', '4  HARD HAT + FACE SHIELD', '5  ARC JACKET', '6  GLOVES (LAST)']
for i, label in enumerate(steps):
    col, row = i % 2, i // 2
    u = -0.38 + col * 0.62          # as you face it, your right is -X: the first column on your left
    pb.box((-u, 0.17 - row * 0.085, 0.007), (0.56, 0.065, 0.003), PANEL_DARK)
    pb.text(label, (-u, 0.17 - row * 0.085, 0.0092), 0.03, LABEL_WHITE, right=READ)
pb.box((0.52, -0.16, 0.007), (0.3, 0.06, 0.003), LABEL_RED)
pb.text('PADLOCKS  Q1  /  UPS', (0.52, -0.16, 0.0092), 0.026, LABEL_WHITE, right=READ)
pb.text('6.1 cal/cm2 at 455 mm  -  demo study DS-7  -  training demo', (0.0, -0.29, 0.0092), 0.022, LABEL_WHITE, right=READ)
pb.build((2.5, 1.0, 0.0))

# --- PPE_CoatStand: beside the gear bench, where the arc-rated jacket hangs; origin on the floor. The hook's tip,
# where the hanger's hook sits, is at (0, 1.75, -0.13).
cs = Part('PPE_CoatStand')
cs.cylinder((0.0, 0.012, 0.0), 0.2, 0.024, 'y', PANEL_DARK, segments=24)
cs.cylinder((0.0, 0.92, 0.0), 0.018, 1.82, 'y', STEEL, segments=14)
cs.sphere((0.0, 1.84, 0.0), 0.026, PANEL_DARK, segments=12)
cs.tube([(0.0, 1.78, 0.0), (0.0, 1.78, -0.10), (0.0, 1.765, -0.135), (0.0, 1.745, -0.13)], 0.008, STEEL, segments=8)
cs.box((0.0, 1.30, -0.02), (0.12, 0.05, 0.003), LABEL_WHITE)
cs.text('ARC JACKET', (0.0, 1.30, -0.0218), 0.016, LABEL_INK, right=(1, 0, 0))
cs.build((3.3, 0.0, 0.0))

save_and_export(OUT_FBX, OUT_BLEND)
