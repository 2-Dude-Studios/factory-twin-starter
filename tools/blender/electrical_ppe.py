"""Builds the electrical PPE kit and the UPS feed panel (M27, ahead of M20) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/electrical_ppe.py -- <out.fbx> [<out.blend>]

Our own low-poly models, no third-party content and no branding (so no row in docs/LICENSES.md). Every object is
named for ElectricalXrSetup, which picks the meshes up by name from Assets/_Project/Art/Electrical, with its origin
where the hand holds it. Geometry is written in Unity's axes (unity_parts.py): X east, Y up, Z north, metres.
The kit is the arc flash label's PPE category 2 (demo study DS-7: 6.1 cal/cm2): arc-rated balaclava, safety
glasses, hearing protection, hard hat with an arc-rated face shield, arc-rated jacket, and Class 0 rubber
insulating gloves with leather protectors.
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
WHITE = material('PPE_HatWhite', (0.90, 0.90, 0.88), roughness=0.4)
SHIELD = material('PPE_ShieldGreen', (0.22, 0.38, 0.26), metallic=0.1, roughness=0.12)
BRACKET = material('PPE_BracketGrey', (0.20, 0.21, 0.22), roughness=0.5)
RUBBER = material('PPE_GloveRubber', (0.85, 0.30, 0.04), roughness=0.55)
LEATHER = material('PPE_GloveLeather', (0.55, 0.38, 0.20), roughness=0.8)
STEEL = material('PPE_Steel', (0.70, 0.71, 0.73), metallic=1.0, roughness=0.3)
PANEL_GREY = material('UPS_PanelGrey', (0.62, 0.64, 0.66), roughness=0.5)
PANEL_DARK = material('UPS_PanelDark', (0.12, 0.13, 0.14), roughness=0.5)
LABEL_YELLOW = material('UPS_LabelYellow', (0.95, 0.78, 0.05), roughness=0.5)
LABEL_INK = material('UPS_LabelInk', (0.02, 0.02, 0.02), roughness=0.6)
LEVER_RED = material('UPS_LeverRed', (0.72, 0.04, 0.03), roughness=0.45)


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
    for i in range(steps):
        bm.faces.new((outer[2 * i], outer[2 * i + 2], inner[2 * i + 2], inner[2 * i]))
        bm.faces.new((outer[2 * i + 1], inner[2 * i + 1], inner[2 * i + 3], outer[2 * i + 3]))
    bm.faces.new((outer[0], inner[0], inner[1], outer[1]))
    bm.faces.new((outer[-2], outer[-1], inner[-1], inner[-2]))
    part.merge(bm, mat, smooth=True)


# --- PPE_Balaclava: arc-rated hood folded over a head form's shape; origin at its middle, where a hand holds it.
bal = Part('PPE_Balaclava')
bal.lathe([(0.0, 0.17), (0.05, 0.165), (0.08, 0.14), (0.096, 0.10), (0.097, 0.04), (0.088, 0.0),
           (0.072, -0.04), (0.078, -0.10), (0.064, -0.125), (0.0, -0.125)], NAVY, segments=24)
bal.box((0, 0.045, 0.088), (0.10, 0.07, 0.02), FACE, bevel=0.012)
bal.build((0.0, 1.0, 0.0), sharp_deg=60)

# --- PPE_SafetyGlasses: wrap-round, clear-grey lenses; origin at the bridge.
gl = Part('PPE_SafetyGlasses')
for x in (-0.032, 0.032):
    gl.box((x, 0.0, 0.0), (0.054, 0.036, 0.004), LENS, bevel=0.006)
gl.tube([(-0.062, 0.012, -0.004), (-0.032, 0.02, 0.002), (0.0, 0.016, 0.003), (0.032, 0.02, 0.002), (0.062, 0.012, -0.004)],
        0.0025, FRAME, segments=8)
for s in (-1, 1):
    gl.tube([(s * 0.064, 0.01, -0.006), (s * 0.07, 0.01, -0.06), (s * 0.068, 0.0, -0.11), (s * 0.06, -0.025, -0.125)],
            0.0025, FRAME, segments=8)
gl.build((0.3, 1.0, 0.0))

# --- PPE_HearingProtection: ear muffs, band over the top; origin at the band's middle.
em = Part('PPE_HearingProtection')
em.tube(arc((0, -0.085, 0), 0.085, 0, 180, plane='xy', steps=18), 0.006, FRAME, segments=10)
for s in (-1, 1):
    em.cylinder((s * 0.09, -0.09, 0.0), 0.042, 0.035, 'x', CUP, segments=20)
    em.cylinder((s * 0.071, -0.09, 0.0), 0.036, 0.008, 'x', FRAME, segments=20)
em.build((0.6, 1.0, 0.0))

# --- PPE_HardHatShield: hard hat with an arc-rated face shield on its brackets; origin at the crown's centre.
hh = Part('PPE_HardHatShield')
hh.lathe([(0.0, 0.135), (0.06, 0.13), (0.095, 0.10), (0.11, 0.05), (0.113, 0.01), (0.10, 0.0), (0.0, 0.0)], WHITE, segments=28)
hh.box((0.0, 0.004, 0.115), (0.16, 0.008, 0.06), WHITE, bevel=0.003)
for s in (-1, 1):
    hh.box((s * 0.112, 0.0, 0.03), (0.012, 0.03, 0.05), BRACKET, bevel=0.003)
curved_panel(hh, 0.145, -62, 62, -0.005, -0.235, 0.003, SHIELD, center=(0.0, 0.0, 0.0))
hh.box((0.0, -0.002, 0.145), (0.16, 0.012, 0.012), BRACKET)
hh.build((0.9, 1.0, 0.0), sharp_deg=55)

# --- PPE_ArcJacket: on a hanger; origin at the top of the hook.
jk = Part('PPE_ArcJacket')
jk.tube([(0.0, 0.0, 0.0), (0.0, 0.03, 0.02), (0.0, 0.05, 0.0), (0.0, 0.03, -0.02), (0.0, -0.01, -0.01), (0.0, -0.05, 0.0)],
        0.003, STEEL, segments=8)
jk.tube([(-0.22, -0.11, 0.0), (0.0, -0.05, 0.0), (0.22, -0.11, 0.0)], 0.006, STEEL, segments=8)
jk.box((0.0, -0.44, 0.0), (0.46, 0.66, 0.11), NAVY, bevel=0.03)
jk.box((0.0, -0.105, 0.0), (0.16, 0.05, 0.12), NAVY_DARK, bevel=0.015)
for s in (-1, 1):
    jk.tube([(s * 0.22, -0.15, 0.0), (s * 0.29, -0.40, 0.01), (s * 0.31, -0.68, 0.02)], 0.055, NAVY, segments=14)
    jk.cylinder((s * 0.31, -0.60, 0.02), 0.058, 0.03, 'y', REFLECT, segments=14)
for y in (-0.55, -0.70):
    jk.box((0.0, y, 0.0), (0.47, 0.03, 0.115), REFLECT)
jk.box((0.0, -0.44, 0.058), (0.012, 0.6, 0.004), FRAME)
jk.build((1.3, 1.0, 0.0), sharp_deg=50)


# --- PPE_Gloves: a pair, Class 0 rubber with leather protectors over them, fingers up; origin between the cuffs.
def glove(part, x, thumb_side):
    part.cylinder((x, -0.07, 0.0), 0.05, 0.12, 'y', RUBBER, segments=16, radius2=0.044)
    part.cylinder((x, 0.0, 0.0), 0.046, 0.05, 'y', LEATHER, segments=16, radius2=0.042)
    part.box((x, 0.065, 0.0), (0.088, 0.09, 0.034), LEATHER, bevel=0.012)
    for i, dx in enumerate((-0.03, -0.01, 0.01, 0.03)):
        length = (0.062, 0.074, 0.07, 0.056)[i]
        part.cylinder((x + dx, 0.11 + length / 2, 0.0), 0.0095, length, 'y', LEATHER, segments=8)
    part.tube([(x + thumb_side * 0.044, 0.05, 0.0), (x + thumb_side * 0.062, 0.085, 0.004), (x + thumb_side * 0.066, 0.11, 0.006)],
              0.011, LEATHER, segments=8)


gv = Part('PPE_Gloves')
glove(gv, -0.065, -1)
glove(gv, 0.065, 1)
gv.build((1.8, 1.0, 0.0), sharp_deg=60)

# --- UPS_Panel: the 120 V UPS feed's breaker panel on a post beside the cabinet; origin on the floor under it.
# The breaker (UPS_Breaker, its own object) sits in the panel's window at y 1.32; its lockout tab (UPS_HaspTab)
# beside it, where the padlock hangs when the lever is down (OFF).
up = Part('UPS_Panel')
up.box((0.0, 0.7, 0.0), (0.07, 1.4, 0.07), PANEL_DARK)
up.box((0.0, 0.002, 0.0), (0.32, 0.004, 0.32), PANEL_DARK)
up.box((0.0, 1.30, 0.06), (0.36, 0.46, 0.14), PANEL_GREY, bevel=0.01)
up.box((0.0, 1.32, 0.132), (0.10, 0.16, 0.006), PANEL_DARK)
up.box((0.0, 1.47, 0.134), (0.30, 0.09, 0.003), LABEL_YELLOW)
up.text('UPS FEED  CB-U1', (0.0, 1.487, 0.1362), 0.026, LABEL_INK, right=(-1, 0, 0))
up.text('120 V CONTROL', (0.0, 1.455, 0.1362), 0.022, LABEL_INK, right=(-1, 0, 0))
up.text('ON', (0.0, 1.415, 0.1352), 0.018, LABEL_INK, right=(-1, 0, 0))
up.text('OFF', (0.0, 1.225, 0.1352), 0.018, LABEL_INK, right=(-1, 0, 0))
up.build((0.0, 0.0, 1.0))

br = Part('UPS_Breaker')
br.box((0.0, 0.0, 0.012), (0.03, 0.024, 0.024), PANEL_DARK)
br.box((0.0, 0.035, 0.03), (0.022, 0.07, 0.016), LEVER_RED, bevel=0.004)
br.build((0.3, 1.32, 1.0))

tab = Part('UPS_HaspTab')
tab.box((0.0, 0.0, 0.0), (0.03, 0.05, 0.004), LEVER_RED, bevel=0.003)
tab.ring((0.0, -0.012, 0.0), 0.0065, 0.0045, 0.006, 'z', STEEL, segments=14)
tab.build((0.4, 1.32, 1.0))

save_and_export(OUT_FBX, OUT_BLEND)
