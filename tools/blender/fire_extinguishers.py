"""Builds the fire drill's portable extinguishers and the training station's stand (M15) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/fire_extinguishers.py -- <out.fbx> [<out.blend>]

Three extinguishers, each as separate parts so a hand can work them later: <Kind>_Body (pivot at the carry-handle
grip, the body hanging below it), <Kind>_Lever (pivot on its hinge pin), <Kind>_Pin (the ring pin with its tamper
seal, pivot at the pin's head) and <Kind>_Nozzle (pivot at the tip, pointing +Z; the stream leaves from there).
Kinds: Ext_ABC (5 lb dry chemical, red, gauge, short hose), Ext_CO2 (5 lb, red, no gauge, black discharge horn),
Ext_Water (2.5 gal stored pressure, stainless, gauge, long hose). Held, the grip is in the hand and the nozzle
points forward. Fire_Stand is the training station: a red board on two posts with three hooks and a header for
the sign, pivot on the floor at its centre, facing +Z. Geometry in Unity's axes (unity_parts.py). No branding;
the labels carry only the class marks (A green, B red, C blue), drawn by us.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, arc, args, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('Fire_Extinguishers.fbx')
setup_scene()

RED = material('Fire_Red', (0.70, 0.03, 0.02), roughness=0.35)
STAINLESS = material('Fire_Stainless', (0.80, 0.81, 0.83), metallic=1.0, roughness=0.22)
STEEL = material('Fire_Steel', (0.62, 0.63, 0.65), metallic=1.0, roughness=0.3)
BLACK = material('Fire_Black', (0.02, 0.02, 0.022), roughness=0.55)
LABEL = material('Fire_Label', (0.93, 0.93, 0.90), roughness=0.6)
MARK_A = material('Fire_ClassA', (0.05, 0.55, 0.12), roughness=0.5)
MARK_B = material('Fire_ClassB', (0.80, 0.05, 0.03), roughness=0.5)
MARK_C = material('Fire_ClassC', (0.05, 0.22, 0.75), roughness=0.5)
GAUGE_FACE = material('Fire_GaugeFace', (0.95, 0.95, 0.93), roughness=0.4)
GAUGE_GREEN = material('Fire_GaugeGreen', (0.10, 0.65, 0.15), roughness=0.4)
TAG = material('Fire_InspectionTag', (0.95, 0.80, 0.10), roughness=0.6)
SEAL = material('Fire_Seal', (0.85, 0.10, 0.08), roughness=0.5)
BOARD = material('Fire_Board', (0.72, 0.05, 0.03), roughness=0.5)
SIGN_WHITE = material('Fire_SignWhite', (0.96, 0.96, 0.94), roughness=0.5)

MARKS = {'A': MARK_A, 'B': MARK_B, 'C': MARK_C}


def extinguisher(kind, x, radius, height, body_mat, classes, gauge, nozzle):
    """One extinguisher. The grip is the origin; the body's axis is 70 mm forward of it (+Z), its top 70 mm down."""
    axis_z = 0.07
    top = -0.07
    bottom = top - height
    show = (x, 0.75, 0)

    body = Part(f'{kind}_Body')
    # Shell: flat base with a rolled foot ring, straight side, domed shoulder to the neck.
    body.lathe([(0, bottom), (radius * 0.92, bottom), (radius, bottom + 0.01), (radius, top - radius * 0.55),
                (radius * 0.75, top - radius * 0.15), (0.024, top), (0, top)], body_mat, center=(0, 0, axis_z), segments=40)
    body.ring((0, bottom + 0.004, axis_z), radius * 1.01, radius * 0.86, 0.008, 'y', BLACK, segments=40)
    # Valve: collar, body and the fixed carry handle running back to the grip.
    body.cylinder((0, top + 0.012, axis_z), 0.026, 0.024, 'y', BLACK, segments=24)
    body.cylinder((0, top + 0.040, axis_z), 0.019, 0.034, 'y', STEEL, segments=20)
    body.box((0, top + 0.050, axis_z - 0.055), (0.026, 0.010, 0.120), BLACK, bevel=0.003)
    # Label on the front: white panel with the class marks it is rated for.
    mid = (top + bottom) / 2
    body.box((0, mid, axis_z + radius + 0.001), (radius * 1.2, height * 0.42, 0.003), LABEL, bevel=0.001, segments=1)
    for i, c in enumerate(classes):
        cx = (i - (len(classes) - 1) / 2) * radius * 0.38
        body.box((cx, mid + height * 0.12, axis_z + radius + 0.003), (radius * 0.30, radius * 0.30, 0.002), MARKS[c])
    if gauge:
        gauge_at = (0.024, top + 0.044, axis_z + 0.012)
        body.cylinder(gauge_at, 0.014, 0.008, 'x', STEEL, segments=20)
        body.cylinder((gauge_at[0] + 0.0045, gauge_at[1], gauge_at[2]), 0.011, 0.001, 'x', GAUGE_FACE, segments=20)
        body.box((gauge_at[0] + 0.0052, gauge_at[1] + 0.004, gauge_at[2]), (0.0005, 0.007, 0.003), GAUGE_GREEN)
    # Inspection tag on a cord from the neck.
    body.tube([(-0.02, top + 0.03, axis_z), (-0.045, top - 0.02, axis_z + 0.01)], 0.0012, BLACK, segments=6)
    body.box((-0.05, top - 0.065, axis_z + 0.012), (0.050, 0.085, 0.002), TAG, bevel=0.0008, segments=1)
    # A clip on the right side, low down, where the nozzle is stowed.
    clip_y = bottom + 0.16
    body.box((radius + 0.004, clip_y, axis_z), (0.010, 0.020, 0.026), BLACK, bevel=0.002)
    body.build(show, sharp_deg=40)

    # Hose (its own part, hidden while the extinguisher is held): from the valve's right side, down the body's
    # right side to the clip. Same origin as the body; its lowest point is where the stowed nozzle starts.
    hose = Part(f'{kind}_Hose')
    side = radius + 0.014
    hose.tube([(0.020, top + 0.035, axis_z), (0.040, top + 0.030, axis_z), (side * 0.85, top + 0.005, axis_z),
               (side, top - 0.05, axis_z), (side, clip_y + 0.012, axis_z)], nozzle['hose'], BLACK, segments=10)
    hose.build(show)

    # Lever: squeezed down onto the carry handle; hinge at the valve's rear.
    lever = Part(f'{kind}_Lever')
    lever.box((0, 0.006, -0.055), (0.024, 0.010, 0.118), STEEL if body_mat is STAINLESS else RED, bevel=0.003)
    lever.box((0, -0.004, -0.004), (0.020, 0.016, 0.012), STEEL, bevel=0.002)
    lever.build((show[0], show[1] + top + 0.064, show[2] + axis_z - 0.002))

    # Pin through the valve, ring outward on the left; the tamper seal's tie loops from ring to handle.
    pin = Part(f'{kind}_Pin')
    pin.cylinder((0.022, 0, 0), 0.0022, 0.050, 'x', STEEL, segments=10)
    pin.ring((-0.012, 0, 0), 0.014, 0.0105, 0.0035, 'z', STEEL, segments=20)
    pin.tube([(-0.020, -0.006, 0), (-0.024, -0.016, -0.010), (-0.014, -0.020, -0.030)], 0.0012, SEAL, segments=6)
    pin.build((show[0] - 0.020, show[1] + top + 0.052, show[2] + axis_z - 0.010))

    # Nozzle (or horn): pivot at the tip, the stream along +Z. Shown in front of the body here, as held.
    tip = nozzle['tip']
    noz = Part(f'{kind}_Nozzle')
    if nozzle['horn']:
        # Narrow where the hose joins (behind), wide at the mouth (the tip).
        noz.cylinder((0, 0, -nozzle['length'] / 2), 0.018, nozzle['length'], 'z', BLACK, segments=28, radius2=0.055)
        noz.box((0, -0.035, -nozzle['length'] - 0.02), (0.022, 0.07, 0.03), BLACK, bevel=0.004)  # horn handle
    else:
        noz.cylinder((0, 0, -nozzle['length'] / 2), 0.010, nozzle['length'], 'z', BLACK, segments=16, radius2=0.007)
        noz.cylinder((0, 0, -0.004), 0.008, 0.008, 'z', STEEL, segments=16)
    noz.build((show[0] + tip[0], show[1] + tip[1], show[2] + tip[2]))


# (x for viewing, radius, height, body material, classes, gauge, nozzle)
extinguisher('Ext_ABC', -0.45, 0.064, 0.36, RED, 'ABC', True,
             {'tip': (0.07, -0.14, 0.32), 'length': 0.07, 'hose': 0.008, 'horn': False})
extinguisher('Ext_CO2', 0.0, 0.068, 0.46, RED, 'BC', False,
             {'tip': (0.07, -0.12, 0.42), 'length': 0.20, 'hose': 0.010, 'horn': True})
extinguisher('Ext_Water', 0.45, 0.085, 0.50, STAINLESS, 'A', True,
             {'tip': (0.07, -0.16, 0.40), 'length': 0.12, 'hose': 0.009, 'horn': False})

# --- Fire_Stand: 1.2 m wide red board on two posts, hooks at 0.95 m (top of a 5 lb unit below 1.53 m, NFPA 10),
# a white header panel for the sign, a black kick plate. Pivot on the floor, centre; the board faces +Z.
stand = Part('Fire_Stand')
for sx in (-0.56, 0.56):
    stand.box((sx, 0.95, -0.03), (0.06, 1.9, 0.06), BLACK, bevel=0.004)
    stand.box((sx, 0.01, -0.03), (0.30, 0.02, 0.36), BLACK, bevel=0.003)
stand.box((0, 1.0, 0.0), (1.2, 1.3, 0.025), BOARD, bevel=0.004)
stand.box((0, 1.78, 0.015), (1.1, 0.22, 0.006), SIGN_WHITE, bevel=0.002, segments=1)
stand.box((0, 0.40, 0.015), (1.1, 0.06, 0.006), BLACK)
for hx in (-0.38, 0.0, 0.38):
    stand.box((hx, 1.32, 0.03), (0.06, 0.03, 0.05), STEEL, bevel=0.004)
    stand.tube(arc((hx, 1.32, 0.06), 0.02, 180, 360, plane='zy', steps=8), 0.005, STEEL, segments=8)
stand.build((0, 0, -0.8), sharp_deg=30)

save_and_export(OUT_FBX, OUT_BLEND)
