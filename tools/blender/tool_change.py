"""Builds the CNC tool change (M12): the spindle head, the two tools and the tool cart, exported for Unity.

Run headless:
  blender -b --factory-startup -P tools/blender/tool_change.py -- <LOTO_ToolChange.fbx> [<out.blend>]

The parts go to Assets/_Project/Art/LOTO, where LotoProps picks them up by name: Spindle_Nose, ToolCart, Tool_Holder
(both tools share it) and the cutters Tool_Cutter_Worn and Tool_Cutter_New. Pivots match LotoProps:
  - Spindle_Nose: the nose's centre; its face (the tool's seat) is 15 mm below, the CNC's front face 110 mm north.
  - Tool_Holder and the cutters: the holder's gauge face, the top of its flange; the taper and pull stud rise above
    it into the spindle, the cutter hangs below. A 40-taper holder with an ER collet chuck, no maker's marks.
  - ToolCart: on the floor under its middle; the tools stand in a fork rack, flange on the fork 1.05 m up, at
    x -0.1 (the worn tool's slot, red flag) and x +0.1 (the new tool's, green flag).
Geometry is in Unity's axes (unity_parts.py).
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, args, boolean_difference, lathe_bm, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('LOTO_ToolChange.fbx')
setup_scene()

SHARP = 35  # degrees: shoulders, flats and box edges stay crisp, revolved and fluted surfaces smooth

GROUND = material('Tool_GroundSteel', (0.8, 0.81, 0.83), metallic=1.0, roughness=0.18)
HOLDER = material('Tool_HolderSteel', (0.6, 0.61, 0.63), metallic=1.0, roughness=0.3)
CARBIDE = material('Tool_Carbide', (0.5, 0.51, 0.53), metallic=0.8, roughness=0.3)
CARBIDE_DULL = material('Tool_CarbideDull', (0.36, 0.36, 0.37), metallic=0.7, roughness=0.6)
TIN = material('Tool_TiNCoating', (0.86, 0.64, 0.22), metallic=1.0, roughness=0.25)
TIN_WORN = material('Tool_TiNWorn', (0.45, 0.4, 0.3), metallic=0.8, roughness=0.6)
BURNT = material('Tool_BurntEdge', (0.17, 0.14, 0.18), metallic=0.6, roughness=0.7)
MACHINE = material('Spindle_MachinePaint', (0.3, 0.34, 0.38), roughness=0.45)
DARK_STEEL = material('Detail_DarkSteel', (0.18, 0.19, 0.2), metallic=0.8, roughness=0.45)
BLACK = material('Detail_Black', (0.02, 0.02, 0.022), roughness=0.6)
COOLANT = material('Spindle_CoolantHose', (0.95, 0.42, 0.05), roughness=0.45)
SAFETY_YELLOW = material('Detail_SafetyYellow', (0.95, 0.72, 0.04), roughness=0.5)
CART_PAINT = material('Cart_Paint', (0.12, 0.25, 0.42), roughness=0.4)
CART_STEEL = material('Cart_Zinc', (0.55, 0.57, 0.6), metallic=1.0, roughness=0.35)
RUBBER = material('Cart_Rubber', (0.03, 0.03, 0.03), roughness=0.85)
MAT = material('Cart_RubberMat', (0.07, 0.075, 0.08), roughness=0.9)
TOTE = material('Cart_ToteBlue', (0.05, 0.35, 0.7), roughness=0.55)
FLAG_RED = material('Cart_FlagRed', (0.8, 0.06, 0.04), roughness=0.5)
FLAG_GREEN = material('Cart_FlagGreen', (0.06, 0.6, 0.18), roughness=0.5)


def cube(bm, center, size):
    verts = bmesh.ops.create_cube(bm, size=1.0)['verts']
    bmesh.ops.scale(bm, vec=Vector(size), verts=verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=verts)


def cylinder(bm, center, radius, depth, segments=32):
    """A cylinder along Unity's Y."""
    lathe_bm(bm, [(0, -depth / 2), (radius, -depth / 2), (radius, depth / 2), (0, depth / 2)], center, segments)


def bolt_heads(part, centers, axis, radius=0.004, depth=0.003, mat=DARK_STEEL):
    for c in centers:
        part.cylinder(c, radius, depth, axis, mat, segments=6)


# ===================== Spindle head =====================
# Spindle_Nose: the turning nose with its 7/24 bore and two drive keys, the housing with its flange, the head
# casting bolted to the CNC's front face, the motor on top and a coolant hose aimed at the cutter, kept on the
# machine side so it is clear of the hand reaching for the tool.
spindle = Part('Spindle_Nose')
spindle.lathe([(0, 0.053), (0.0125, 0.053), (0.0225, -0.015), (0.046, -0.015), (0.05, -0.011), (0.05, 0.012),
               (0.0535, 0.012), (0.0535, 0.06), (0, 0.06)], GROUND, segments=48)
for sx in (-1, 1):
    spindle.box((sx * 0.031, -0.019, 0), (0.012, 0.008, 0.0155), DARK_STEEL, bevel=0.0008, segments=1)
# The housing starts as a ring inside the nose, so no face of it crosses the empty bore.
spindle.lathe([(0.04, 0.07), (0.04, 0.019), (0.068, 0.019), (0.068, 0.03), (0.078, 0.03), (0.078, 0.042), (0.068, 0.042),
               (0.068, 0.17), (0, 0.17)], MACHINE, segments=48)
bolt_heads(spindle, [(0.073 * math.cos(math.radians(a)), 0.0435, 0.073 * math.sin(math.radians(a)))
                     for a in range(30, 360, 60)], 'y', radius=0.0035)
# Head casting and the plate on the machine's face.
spindle.box((0, 0.13, 0.055), (0.16, 0.2, 0.09), MACHINE, bevel=0.006)
spindle.box((0, 0.12, 0.104), (0.24, 0.3, 0.012), MACHINE, bevel=0.002, segments=1)
bolt_heads(spindle, [(sx * 0.1, 0.12 + sy * 0.125, 0.0965) for sx in (-1, 1) for sy in (-1, 1)], 'z', radius=0.006, depth=0.004)
# Warning plate on the casting's east side.
spindle.box((0.0805, 0.16, 0.055), (0.0012, 0.05, 0.05), BLACK)
spindle.box((0.0812, 0.16, 0.055), (0.0008, 0.044, 0.044), SAFETY_YELLOW)
# Motor: finned body, end cap and terminal box.
fins = [(0, 0.17), (0.06, 0.17)]
for i in range(12):
    y = 0.175 + i * 0.012
    fins += [(0.06, y), (0.065, y + 0.002), (0.065, y + 0.008), (0.06, y + 0.01)]
fins += [(0.06, 0.325), (0.052, 0.335), (0, 0.335)]
spindle.lathe(fins, BLACK, segments=48)
spindle.box((0.075, 0.26, 0), (0.035, 0.06, 0.06), BLACK, bevel=0.003)
spindle.cylinder((0.0935, 0.255, 0), 0.008, 0.008, 'x', DARK_STEEL, segments=6)
# Coolant hose: segmented, from the housing's back down to a nozzle aimed at the cutter.
hose = [(-0.06, 0.06, 0.05), (-0.068, 0.02, 0.06), (-0.062, -0.03, 0.055), (-0.046, -0.07, 0.042), (-0.03, -0.098, 0.028)]
spindle.tube(hose, 0.0042, COOLANT, segments=12)
for a, b in zip(hose, hose[1:]):
    for t in (0.0, 0.33, 0.66):
        p = Vector(a).lerp(Vector(b), t)
        spindle.sphere(p, 0.0056, COOLANT, segments=12)
spindle.cylinder((-0.06, 0.07, 0.045), 0.007, 0.012, 'y', DARK_STEEL, segments=6)
nozzle_dir = (Vector((0, -0.13, 0)) - Vector(hose[-1])).normalized()
spindle.tube([hose[-1], tuple(Vector(hose[-1]) + nozzle_dir * 0.014)], 0.0025, COOLANT, segments=12)
spindle.build((0, 0, 0), sharp_deg=SHARP)

# ===================== Tool holder =====================
# A 40-taper holder: pull stud, 7/24 taper, V-flange with two drive slots, ER collet chuck body and nut.
HOLDER_PROFILE = [
    (0, -0.072), (0.0105, -0.072), (0.0105, -0.0705), (0.021, -0.0705), (0.0235, -0.068), (0.0235, -0.05),
    (0.021, -0.0485), (0.019, -0.0485), (0.0195, -0.03), (0.0215, -0.0175), (0.029, -0.0165), (0.0318, -0.0155),
    (0.0318, -0.0125), (0.027, -0.008), (0.0318, -0.0035), (0.0318, -0.0008), (0.03, 0.0), (0.0222, 0.0),
    (0.0222, 0.002), (0.0124, 0.068), (0.0095, 0.068), (0.0095, 0.0755), (0.0065, 0.0775), (0.0065, 0.0825),
    (0.0095, 0.0855), (0.0095, 0.0905), (0.006, 0.093), (0, 0.093),
]


def _holder(bm):
    lathe_bm(bm, HOLDER_PROFILE, segments=48)


def _holder_cuts(bm):
    for sx in (-1, 1):  # drive slots across the flange, on the spindle's keys
        cube(bm, (sx * 0.036, -0.008, 0), (0.025, 0.02, 0.016))
    for cx, cz in ((1, 0), (-1, 0), (0, 1), (0, -1)):  # spanner slots round the collet nut
        cube(bm, (cx * 0.0245, -0.059, cz * 0.0245), (0.006 if cx else 0.007, 0.012, 0.007 if cx else 0.006))


holder = Part('Tool_Holder')
holder.merge_mesh(boolean_difference('holder', _holder, _holder_cuts), HOLDER)
holder.lathe([(0, -0.0721), (0.0135, -0.0721), (0.0135, -0.0715), (0, -0.0715)], DARK_STEEL, segments=32)  # collet face
holder.build((0.3, 0, 0), sharp_deg=SHARP)


# ===================== Cutters =====================
# A 12 mm four-flute end mill: ground shank in the collet, flutes on a 30 degree helix. The new one is gold TiN;
# the worn one has lost its coating, its corners are rounded off, one edge is chipped and its tip is burnt.
FLUTES, RADIUS, LEAD = 4, 0.006, 0.065
FLUTE_TOP = -0.098


def _flute_radius(theta, y, tip, worn):
    dist = FLUTE_TOP - y
    turns = FLUTES * theta / (2 * math.pi) + FLUTES * dist / LEAD
    u = turns % 1.0
    k = 1.0 - 0.03 * u / 0.5 if u < 0.5 else 1.0 - 0.3 * math.sin(math.pi * (u - 0.5) / 0.5)
    k = 1.0 - (1.0 - k) * min(1.0, dist / 0.003)  # the flutes run out into the shank
    if worn:
        to_tip = y - tip
        if to_tip < 0.004:
            k *= 0.8 + 0.2 * (to_tip / 0.004)
        if to_tip < 0.007 and int(turns) % FLUTES == 1 and u < 0.5:
            k *= 0.85
    return RADIUS * k


def _fluted(part, y0, y1, tip, worn, mat, segments=64, step=0.001):
    bm = bmesh.new()
    count = max(2, int(round((y0 - y1) / step)) + 1)
    rings = []
    for i in range(count):
        y = y0 + (y1 - y0) * i / (count - 1)
        ring = []
        for s in range(segments):
            theta = 2 * math.pi * s / segments
            r = _flute_radius(theta, y, tip, worn)
            ring.append(bm.verts.new((r * math.cos(theta), y, r * math.sin(theta))))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for s in range(segments):
            t = (s + 1) % segments
            bm.faces.new((r0[s], r0[t], r1[t], r1[s]))
    for ring in (rings[0], rings[-1]):
        hub = bm.verts.new((0, ring[0].co.y, 0))
        for s in range(segments):
            bm.faces.new((ring[s], ring[(s + 1) % segments], hub))
    part.merge(bm, mat, smooth=True)


def cutter(name, tip, worn):
    part = Part(name)
    part.lathe([(0, -0.064), (0.0055, -0.064), (RADIUS, -0.0645), (RADIUS, FLUTE_TOP), (0, FLUTE_TOP)],
               CARBIDE_DULL if worn else CARBIDE, segments=48)
    band = tip + 0.005
    _fluted(part, FLUTE_TOP, band, tip, worn, TIN_WORN if worn else TIN)
    _fluted(part, band, tip, tip, worn, BURNT if worn else TIN)
    part.build((0.3 if not worn else 0.36, 0, 0), sharp_deg=SHARP)


cutter('Tool_Cutter_Worn', -0.1295, True)
cutter('Tool_Cutter_New', -0.131, False)

# ===================== Tool cart =====================
# A steel trolley 0.56 x 0.36 m: four swivel casters, corner posts, bottom and middle shelves, a two-drawer unit,
# a matted top tray with a lip and a fork rack holding the tools by their flanges. The push handle is on the
# west end, away from the control station.
cart = Part('ToolCart')
W, D = 0.56, 0.36
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * 0.235, sz * 0.135
        cart.cylinder((x, 0.04, z), 0.04, 0.026, 'x', RUBBER, segments=28)
        cart.cylinder((x, 0.04, z), 0.016, 0.03, 'x', CART_STEEL, segments=16)
        for side in (-1, 1):
            cart.box((x + side * 0.018, 0.065, z - 0.008), (0.003, 0.05, 0.028), CART_STEEL)
        cart.box((x, 0.091, z - 0.008), (0.04, 0.004, 0.034), CART_STEEL)
        cart.cylinder((x, 0.1, z), 0.008, 0.016, 'y', CART_STEEL, segments=12)
        cart.box((sx * (W / 2 - 0.0125), 0.49, sz * (D / 2 - 0.0125)), (0.025, 0.78, 0.025), CART_PAINT, bevel=0.002, segments=1)
for y in (0.13, 0.47):
    cart.box((0, y, 0), (W, 0.02, D), CART_PAINT, bevel=0.002, segments=1)
    cart.box((0, y + 0.02, -D / 2 + 0.005), (W - 0.05, 0.02, 0.01), CART_PAINT)
# Drawers under the top.
cart.box((0, 0.73, 0.01), (W - 0.05, 0.24, D - 0.04), CART_PAINT)
for y in (0.67, 0.79):
    cart.box((0, y, -D / 2 + 0.003), (W - 0.07, 0.105, 0.008), CART_PAINT, bevel=0.003)
    cart.box((0, y + 0.02, -D / 2 - 0.006), (0.16, 0.012, 0.012), CART_STEEL, bevel=0.002)
    cart.box((-0.2, y + 0.025, -D / 2 - 0.0012), (0.05, 0.02, 0.001), CART_STEEL)
# Top tray with lip and mat.
cart.box((0, 0.87, 0), (W + 0.02, 0.02, D + 0.02), CART_PAINT, bevel=0.003)
cart.box((0, 0.8815, 0), (W - 0.02, 0.003, D - 0.02), MAT)
for sz in (-1, 1):
    cart.box((0, 0.895, sz * (D / 2 + 0.005)), (W + 0.02, 0.03, 0.01), CART_PAINT, bevel=0.002, segments=1)
for sx in (-1, 1):
    cart.box((sx * (W / 2 + 0.005), 0.895, 0), (0.01, 0.03, D + 0.02), CART_PAINT, bevel=0.002, segments=1)
# Push handle on the west end.
handle = [(-W / 2, 0.9, -0.14), (-W / 2 - 0.04, 0.93, -0.14), (-W / 2 - 0.06, 0.95, -0.12),
          (-W / 2 - 0.06, 0.95, 0.12), (-W / 2 - 0.04, 0.93, 0.14), (-W / 2, 0.9, 0.14)]
cart.tube(handle, 0.012, CART_STEEL, segments=16)
# A tote on the bottom shelf.
cart.box((0.1, 0.2, 0.01), (0.26, 0.12, 0.2), TOTE, bevel=0.004)
cart.box((0.1, 0.262, 0.01), (0.27, 0.006, 0.21), TOTE, bevel=0.002, segments=1)
# Fork rack: two uprights and a plate with a fork per tool, open toward the front (south).
RACK_TOP = 1.05 - 0.0035


def _rack(bm):
    cube(bm, (0, RACK_TOP - 0.004, 0), (0.46, 0.008, 0.1))


def _forks(bm):
    for x in (-0.1, 0.1):
        cylinder(bm, (x, RACK_TOP - 0.004, 0), 0.0285, 0.03, segments=40)
        cube(bm, (x, RACK_TOP - 0.004, -0.04), (0.057, 0.03, 0.08))


cart.merge_mesh(boolean_difference('rack', _rack, _forks), CART_STEEL)
for sx in (-1, 1):
    cart.box((sx * 0.225, (0.88 + RACK_TOP - 0.008) / 2, 0.0), (0.012, RACK_TOP - 0.008 - 0.88, 0.08), CART_PAINT)
    bolt_heads(cart, [(sx * 0.225, RACK_TOP + 0.001, z) for z in (-0.025, 0.025)], 'y', radius=0.005, depth=0.003)
# Slot flags behind each fork: red where the worn tool goes, green where the new one waits.
for x, flag in ((-0.1, FLAG_RED), (0.1, FLAG_GREEN)):
    cart.box((x, RACK_TOP + 0.02, 0.045), (0.05, 0.04, 0.004), flag, bevel=0.0015, segments=1)
cart.build((0, 0, 0.6), sharp_deg=SHARP)

save_and_export(OUT_FBX, OUT_BLEND)
