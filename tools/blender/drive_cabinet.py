"""Builds the cell's drive cabinet DCP-1 (Rev 7, M19) and exports it for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/drive_cabinet.py -- <out.fbx> [<out.blend>]

Our own model, no third-party content and no branding: a floor-standing 0.8 x 2.0 x 0.45 m enclosure for a US 480 V
three-phase supply with 120 V control. Inside, on the mounting plate: the main disconnect Q1 (line terminals on top,
load side down to the distribution block X1 L1 L2 L3), the two conveyor VFDs U1 (infeed) and U2 (outfeed) with their
DC bus terminals and charge LEDs, the control transformer T1, the transfer relay K1, the control terminals X2, the
UPS feed terminals X3 (orange blocks and wire: NFPA 79's colour for a circuit that stays live with the disconnect
off), the ground bar PE and wire ducts. The door is hinged on its left as you face it, with Q1's through-door rotary
handle (red on yellow) and a drawing pocket inside.

Objects (pivots, Unity space: X east, Y up, Z north; the cabinet faces +Z, its pivot on the floor at the centre):
  Cab_Body            pivot at the origin
  Cab_Door            pivot on the hinge line (x +0.40, z +0.235)
  Cab_Handle          pivot on the handle's axis, on the door's face (turns about Z: ON up, OFF to the side)
  Cab_Led_U1/U2       the DC bus charge LEDs, each its own object so Unity can light it
  Probe_<point>       one tiny marker per probe point (names follow TwoDude.FactoryTwin.Twin.ProbePoint), at the
                      terminal screw's face; Unity keeps the transform and drops the mesh
Geometry in Unity's axes (unity_parts.py). As you face the cabinet your right is -X, so positions below are written
with u = your right (x = -u).
"""
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, args, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('Drive_Cabinet.fbx')
setup_scene()

ENCLOSURE = material('Elec_Enclosure', (0.78, 0.79, 0.77), roughness=0.55)   # light grey, RAL 7035-like
PLATE = material('Elec_Plate', (0.70, 0.71, 0.72), metallic=0.8, roughness=0.35)  # galvanised mounting plate
DEVICE = material('Elec_Device', (0.18, 0.19, 0.20), roughness=0.5)
BLACK = material('Elec_Black', (0.02, 0.02, 0.022), roughness=0.6)
DUCT = material('Elec_Duct', (0.55, 0.56, 0.56), roughness=0.7)
STEEL = material('Elec_Steel', (0.65, 0.66, 0.68), metallic=1.0, roughness=0.3)
COPPER = material('Elec_Copper', (0.72, 0.42, 0.22), metallic=1.0, roughness=0.35)
ORANGE = material('Elec_Orange', (0.95, 0.38, 0.02), roughness=0.5)
YELLOW = material('Elec_Yellow', (0.95, 0.75, 0.05), roughness=0.5)
RED = material('Elec_Red', (0.75, 0.04, 0.03), roughness=0.4)
DISPLAY = material('Elec_Display', (0.10, 0.35, 0.18), roughness=0.2)
LED = material('Elec_Led', (1.0, 0.15, 0.05), roughness=0.3)
WHITE = material('Elec_White', (0.93, 0.93, 0.90), roughness=0.6)

W, H, D = 0.80, 2.00, 0.45
PLINTH = 0.10
FRONT = D / 2            # front of the frame, z +0.225
PLATE_FACE = -0.195      # mounting plate face


def x(u):
    """Unity x for a position u to your right as you face the cabinet."""
    return -u


GREEN_YELLOW = material('Elec_GreenYellow', (0.25, 0.60, 0.10), roughness=0.5)


def text(part, s, u, y, z, size, mat, align='CENTER', depth=0.0006):
    """Raised lettering facing the door (+Z), read left to right as you face the cabinet; (u, y) is its anchor."""
    curve = bpy.data.curves.new('txt', type='FONT')
    curve.body = s
    curve.size = size
    curve.extrude = depth / 2
    curve.align_x = align
    curve.align_y = 'CENTER'
    curve.resolution_u = 3  # lettering is a few mm tall: low curve resolution keeps the VR triangle budget
    obj = bpy.data.objects.new('txt', curve)
    bpy.context.scene.collection.objects.link(obj)
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(obj)
    bpy.data.curves.remove(curve)
    # Font space (x right, y up, z out) to Unity: your right is -X, out is +Z.
    for v in mesh.vertices:
        fx, fy, fz = v.co
        v.co = (x(u + fx), y + fy, z + depth / 2 + fz)
    part.merge_mesh(mesh, mat)
    bpy.data.meshes.remove(mesh)


body = Part('Cab_Body')

# --- Enclosure: plinth, back, sides, roof, floor, front frame lip ---------------------------------------------------
body.box((0, PLINTH / 2, 0), (W, PLINTH, D), BLACK, bevel=0.004)
body.box((0, (PLINTH + H) / 2, -D / 2 + 0.01), (W, H - PLINTH, 0.02), ENCLOSURE)
for sx in (-1, 1):
    body.box((sx * (W / 2 - 0.01), (PLINTH + H) / 2, 0), (0.02, H - PLINTH, D), ENCLOSURE, bevel=0.003)
body.box((0, H - 0.01, 0), (W, 0.02, D), ENCLOSURE, bevel=0.004)
body.box((0, PLINTH + 0.01, 0), (W - 0.04, 0.02, D - 0.02), ENCLOSURE)
for sx in (-1, 1):  # frame lip round the door opening
    body.box((sx * (W / 2 - 0.03), (PLINTH + H) / 2, FRONT - 0.01), (0.03, H - PLINTH - 0.04, 0.02), ENCLOSURE)
body.box((0, H - 0.035, FRONT - 0.01), (W - 0.04, 0.03, 0.02), ENCLOSURE)
body.box((0, PLINTH + 0.035, FRONT - 0.01), (W - 0.04, 0.03, 0.02), ENCLOSURE)
# Cable gland plate on the roof's inside.
body.box((0, H - 0.03, -0.05), (0.5, 0.01, 0.2), DEVICE)

# --- Mounting plate --------------------------------------------------------------------------------------------------
body.box((0, 1.04, PLATE_FACE - 0.005), (0.72, 1.80, 0.01), PLATE)

# --- Wire ducts (slotted grey ducts) ---------------------------------------------------------------------------------
for y in (0.98, 0.64, 0.36):
    body.box((0, y, PLATE_FACE + 0.03), (0.68, 0.05, 0.06), DUCT, bevel=0.002)
for u in (-0.335, 0.335):
    body.box((x(u), 1.14, PLATE_FACE + 0.03), (0.04, 1.56, 0.06), DUCT, bevel=0.002)
# Finger slots along the duct edges, dark so they read as gaps.
for y in (0.98, 0.64, 0.36):
    for k in range(27):
        for edge in (-1, 1):
            body.box((x(-0.325 + 0.025 * k), y + edge * 0.0255, PLATE_FACE + 0.035), (0.008, 0.002, 0.05), BLACK)


def screw(part, u, y, z, mat=STEEL, r=0.0045):
    """A terminal screw head facing the door."""
    part.cylinder((x(u), y, z), r, 0.004, 'z', mat, segments=10)


def wire(part, points_u, radius, mat):
    """A conductor along (u, y, z) points."""
    part.tube([(x(u), y, z) for (u, y, z) in points_u], radius, mat, segments=8)


probes = {}  # probe name -> Unity position of the screw face


def probe(name, u, y, z):
    probes[name] = (x(u), y, z)


# --- Q1, the main disconnect (rotary, through-door operated) ---------------------------------------------------------
Q1_U, Q1_Y = 0.20, 1.29
q1_front = PLATE_FACE + 0.12
body.box((x(Q1_U), Q1_Y, PLATE_FACE + 0.06), (0.15, 0.22, 0.12), DEVICE, bevel=0.006)
body.box((x(Q1_U), Q1_Y, q1_front + 0.002), (0.07, 0.07, 0.004), BLACK)
# Shaft from Q1 to the door handle.
body.box((x(Q1_U), Q1_Y, (q1_front + FRONT) / 2), (0.01, 0.01, FRONT - q1_front - 0.01), STEEL)
# Line side: three terminals on top, incoming cables down from the roof gland. Live even with Q1 open.
for i, label in enumerate(('LineL1', 'LineL2', 'LineL3')):
    u = Q1_U - 0.045 + 0.045 * i
    body.box((x(u), Q1_Y + 0.125, PLATE_FACE + 0.07), (0.03, 0.03, 0.08), BLACK, bevel=0.002)
    screw(body, u, Q1_Y + 0.125, PLATE_FACE + 0.112)
    probe(label, u, Q1_Y + 0.125, PLATE_FACE + 0.114)
    wire(body, [(u, Q1_Y + 0.14, PLATE_FACE + 0.07), (u, Q1_Y + 0.25, PLATE_FACE + 0.07), (u, H - 0.04, -0.05)], 0.008, BLACK)
# Red line-side warning plate above Q1.
body.box((x(Q1_U), Q1_Y + 0.31, PLATE_FACE + 0.002), (0.14, 0.035, 0.003), RED)
text(body, 'LINE SIDE LIVE', Q1_U, Q1_Y + 0.31, PLATE_FACE + 0.0035, 0.011, WHITE)
text(body, 'Q1', Q1_U, Q1_Y - 0.075, PLATE_FACE + 0.122, 0.018, WHITE)
for i, label in enumerate(('L1', 'L2', 'L3')):
    text(body, label, Q1_U - 0.045 + 0.045 * i, Q1_Y + 0.155, PLATE_FACE + 0.11, 0.008, WHITE)

# --- X1, the distribution block on the load side ---------------------------------------------------------------------
X1_Y = 1.06
body.box((x(Q1_U), X1_Y, PLATE_FACE + 0.035), (0.15, 0.05, 0.07), DEVICE, bevel=0.003)
text(body, 'X1', Q1_U + 0.1, X1_Y, PLATE_FACE + 0.07, 0.014, WHITE)
for i, label in enumerate(('L1', 'L2', 'L3')):
    u = Q1_U - 0.045 + 0.045 * i
    text(body, label, u, X1_Y - 0.018, PLATE_FACE + 0.07, 0.007, WHITE)
    screw(body, u, X1_Y, PLATE_FACE + 0.072, mat=COPPER, r=0.006)
    probe(label, u, X1_Y, PLATE_FACE + 0.074)
    wire(body, [(u, Q1_Y - 0.11, PLATE_FACE + 0.07), (u, X1_Y + 0.03, PLATE_FACE + 0.06)], 0.007, BLACK)

# --- U1 and U2, the conveyor VFDs ------------------------------------------------------------------------------------
VFD_Y0, VFD_Y1 = 1.42, 1.86
leds = {}
for name, cu in (('U1', -0.24), ('U2', -0.05)):
    cy = (VFD_Y0 + VFD_Y1) / 2
    vfd_front = PLATE_FACE + 0.18
    body.box((x(cu), cy, PLATE_FACE + 0.09), (0.16, VFD_Y1 - VFD_Y0, 0.18), DEVICE, bevel=0.006)
    for f in range(5):  # heatsink fins on the left side
        body.box((x(cu - 0.083), cy, PLATE_FACE + 0.02 + f * 0.03), (0.006, VFD_Y1 - VFD_Y0 - 0.06, 0.012), STEEL)
    body.box((x(cu), VFD_Y1 - 0.09, vfd_front + 0.002), (0.10, 0.06, 0.004), BLACK)       # keypad bezel
    body.box((x(cu), VFD_Y1 - 0.08, vfd_front + 0.005), (0.07, 0.025, 0.002), DISPLAY)    # display
    for k in range(3):
        body.box((x(cu - 0.03 + 0.03 * k), VFD_Y1 - 0.11, vfd_front + 0.005), (0.016, 0.012, 0.003), DEVICE)
    leds[name] = (x(cu + 0.05), VFD_Y1 - 0.03, vfd_front + 0.004)
    text(body, name, cu - 0.045, VFD_Y1 - 0.03, vfd_front, 0.016, WHITE)
    text(body, 'CHARGE', cu + 0.05, VFD_Y1 - 0.048, vfd_front, 0.0055, WHITE)
    for k in range(7):  # cooling slots in the front cover
        body.box((x(cu), VFD_Y0 + 0.05 + k * 0.022, vfd_front + 0.0008), (0.11, 0.008, 0.002), BLACK)
    # Shock warning: a yellow triangle-ish plate and its wait time (the demo's, not a real drive's).
    body.box((x(cu), VFD_Y0 + 0.22, vfd_front + 0.001), (0.12, 0.035, 0.002), YELLOW)
    text(body, 'DC BUS: WAIT FOR LED OFF', cu, VFD_Y0 + 0.225, vfd_front + 0.002, 0.0062, BLACK)
    text(body, 'TEST BEFORE TOUCH', cu, VFD_Y0 + 0.212, vfd_front + 0.002, 0.0062, BLACK)
    body.ring((x(cu + 0.05), VFD_Y1 - 0.03, vfd_front + 0.002), 0.008, 0.005, 0.004, 'z', BLACK, segments=16)
    # Terminal strip under the drive: R S T in, DC+ DC- (the DC bus), U V W out.
    body.box((x(cu), VFD_Y0 - 0.03, PLATE_FACE + 0.06), (0.16, 0.06, 0.12), BLACK, bevel=0.003)
    for k, tu in enumerate((-0.065, -0.045, -0.025, -0.008, 0.008, 0.025, 0.045, 0.065)):
        is_dc = k in (3, 4)
        screw(body, cu + tu, VFD_Y0 - 0.03, PLATE_FACE + 0.122, mat=RED if is_dc else STEEL, r=0.004)
        mark = ('R', 'S', 'T', '+', '-', 'U', 'V', 'W')[k]
        text(body, mark, cu + tu, VFD_Y0 - 0.048, PLATE_FACE + 0.12, 0.007, RED if is_dc else WHITE)
    probe('Vfd%sDcPlus' % name[1], cu - 0.008, VFD_Y0 - 0.03, PLATE_FACE + 0.124)
    probe('Vfd%sDcMinus' % name[1], cu + 0.008, VFD_Y0 - 0.03, PLATE_FACE + 0.124)
    # Supply from X1 up to the drive.
    wire(body, [(Q1_U - 0.045, X1_Y + 0.02, PLATE_FACE + 0.05), (Q1_U - 0.08, X1_Y + 0.08, PLATE_FACE + 0.03),
                (cu - 0.045, VFD_Y0 - 0.07, PLATE_FACE + 0.04), (cu - 0.045, VFD_Y0 - 0.04, PLATE_FACE + 0.06)], 0.005, BLACK)

# --- Control: DIN rail, T1, K1, X2 (control) and X3 (UPS feed, orange) ------------------------------------------------
body.box((0, 0.80, PLATE_FACE + 0.004), (0.62, 0.035, 0.008), STEEL)
body.box((x(-0.22), 0.80, PLATE_FACE + 0.065), (0.13, 0.15, 0.12), DEVICE, bevel=0.004)        # T1
for k in (-0.03, 0.03):
    body.cylinder((x(-0.22 + k), 0.80, PLATE_FACE + 0.13), 0.025, 0.02, 'z', COPPER, segments=16)
body.box((x(-0.05), 0.80, PLATE_FACE + 0.04), (0.05, 0.09, 0.07), DEVICE, bevel=0.003)          # K1
body.box((x(-0.05), 0.82, PLATE_FACE + 0.076), (0.03, 0.02, 0.003), WHITE)
text(body, 'K1', -0.05, 0.82, PLATE_FACE + 0.0775, 0.011, BLACK)
text(body, 'T1  480/120 V', -0.22, 0.89, PLATE_FACE + 0.125, 0.009, WHITE)

body.box((0, 0.52, PLATE_FACE + 0.004), (0.62, 0.035, 0.008), STEEL)
for k in range(10):                                                                           # X2 blocks
    u = -0.29 + 0.016 * k
    body.box((x(u), 0.52, PLATE_FACE + 0.035), (0.012, 0.07, 0.06), DUCT, bevel=0.001)
    screw(body, u, 0.545, PLATE_FACE + 0.066, r=0.0035)
text(body, 'X2  CONTROL 120 V', -0.22, 0.47, PLATE_FACE + 0.01, 0.009, BLACK)
probe('ControlHot', -0.29, 0.545, PLATE_FACE + 0.068)
probe('ControlNeutral', -0.274, 0.545, PLATE_FACE + 0.068)
for k in range(4):                                                                            # X3 blocks, orange
    u = 0.03 + 0.016 * k
    body.box((x(u), 0.52, PLATE_FACE + 0.035), (0.012, 0.07, 0.06), ORANGE, bevel=0.001)
    screw(body, u, 0.545, PLATE_FACE + 0.066, r=0.0035)
text(body, 'X3  UPS FEED', 0.055, 0.47, PLATE_FACE + 0.01, 0.009, ORANGE)
probe('UpsHot', 0.03, 0.545, PLATE_FACE + 0.068)
probe('UpsNeutral', 0.046, 0.545, PLATE_FACE + 0.068)
# The UPS feed comes in from the roof down the right-hand duct, then to X3 and on to K1: orange all the way.
wire(body, [(0.30, H - 0.04, -0.06), (0.30, 1.6, PLATE_FACE + 0.04), (0.30, 0.60, PLATE_FACE + 0.04),
            (0.046, 0.49, PLATE_FACE + 0.04)], 0.004, ORANGE)
wire(body, [(0.03, 0.56, PLATE_FACE + 0.04), (-0.02, 0.70, PLATE_FACE + 0.03), (-0.05, 0.76, PLATE_FACE + 0.04)], 0.0035, ORANGE)
# T1's secondary to K1, K1 to X2.
wire(body, [(-0.18, 0.75, PLATE_FACE + 0.04), (-0.08, 0.77, PLATE_FACE + 0.04)], 0.0035, BLACK)
wire(body, [(-0.06, 0.755, PLATE_FACE + 0.04), (-0.20, 0.60, PLATE_FACE + 0.04), (-0.29, 0.56, PLATE_FACE + 0.04)], 0.0035, BLACK)

# --- PE ground bar ---------------------------------------------------------------------------------------------------
body.box((x(0.15), 0.24, PLATE_FACE + 0.02), (0.30, 0.02, 0.01), COPPER)
for k in range(8):
    u = 0.02 + 0.037 * k
    screw(body, u, 0.24, PLATE_FACE + 0.026, r=0.004)
probe('Ground', 0.02, 0.24, PLATE_FACE + 0.028)
body.box((x(0.15), 0.215, PLATE_FACE + 0.002), (0.05, 0.015, 0.003), GREEN_YELLOW)
text(body, 'PE', 0.15, 0.215, PLATE_FACE + 0.0035, 0.01, BLACK)

body.build((0, 0, 0), sharp_deg=35)

# --- Door: hinged on your left (+X), front face at z +0.245 ----------------------------------------------------------
HINGE = (W / 2, (PLINTH + H) / 2, FRONT + 0.01)
door = Part('Cab_Door')
hx, hy, hz = HINGE


def rel(p):
    return (p[0] - hx, p[1] - hy, p[2] - hz)


door.box(rel((0, (PLINTH + H) / 2, FRONT + 0.01)), (W - 0.004, H - PLINTH - 0.004, 0.02), ENCLOSURE, bevel=0.004)
for y in (0.45, 1.65):
    door.cylinder(rel((W / 2 + 0.004, y, FRONT + 0.012)), 0.008, 0.08, 'y', STEEL, segments=12)
door.box(rel((x(Q1_U), Q1_Y, FRONT + 0.0215)), (0.13, 0.13, 0.003), YELLOW, bevel=0.002)         # handle plate
door.box(rel((x(Q1_U), Q1_Y - 0.085, FRONT + 0.024)), (0.03, 0.03, 0.006), STEEL, bevel=0.001)   # lockout hasp
door.ring(rel((x(Q1_U), Q1_Y - 0.09, FRONT + 0.028)), 0.006, 0.0035, 0.004, 'z', BLACK, segments=12)
door.cylinder(rel((x(0.36), 0.62, FRONT + 0.023)), 0.012, 0.006, 'z', BLACK, segments=12)          # latch
door_text = []  # (string, u, y, size, material) on the door's face
door_text.append(('ON', Q1_U + 0.038, Q1_Y + 0.05, 0.011, BLACK))   # handle up = ON, at 9 o'clock = OFF
door_text.append(('OFF', Q1_U - 0.042, Q1_Y - 0.047, 0.011, BLACK))
door.box(rel((0, 1.93, FRONT + 0.0215)), (0.30, 0.05, 0.003), WHITE, bevel=0.001)                  # nameplate
door_text.append(('DRIVE CABINET  DCP-1', 0, 1.935, 0.016, BLACK))
door_text.append(('480 V 3~  /  120 V CONTROL', 0, 1.915, 0.009, BLACK))
for s, u, y, size, mat in door_text:
    door_bm_text = Part('tmp_text')
    text(door_bm_text, s, u, y, FRONT + 0.023, size, mat)
    # Move the lettering into the door's hinge space and merge it.
    bmesh.ops.translate(door_bm_text.bm, vec=(-hx, -hy, -hz), verts=door_bm_text.bm.verts)
    m = bpy.data.meshes.new('tmp')
    door_bm_text.bm.to_mesh(m)
    door_bm_text.bm.free()
    door.merge_mesh(m, mat)
    bpy.data.meshes.remove(m)
# Door ground strap (green/yellow braid) from the door's inside to the frame, near the lower hinge.
door.tube([rel((W / 2 - 0.05, 0.30, FRONT - 0.002)), rel((W / 2 - 0.03, 0.27, FRONT - 0.03)),
           rel((W / 2 - 0.02, 0.30, FRONT - 0.05))], 0.004, GREEN_YELLOW, segments=8)
# Door stiffener on the inside.
door.box(rel((0, 1.30, FRONT - 0.004)), (0.6, 0.03, 0.012), ENCLOSURE)
door.box(rel((0, 0.30, FRONT + 0.0215)), (0.26, 0.20, 0.004), BLACK, bevel=0.002)                  # filter fan grille
for k in range(6):
    door.box(rel((0, 0.22 + 0.032 * k, FRONT + 0.0235)), (0.24, 0.008, 0.002), DEVICE)
door.box(rel((x(-0.05), 0.75, FRONT - 0.004)), (0.30, 0.36, 0.012), BLACK, bevel=0.002)            # drawing pocket
door.box(rel((x(-0.05), 0.80, FRONT - 0.011)), (0.24, 0.30, 0.002), WHITE)
door.build(HINGE, sharp_deg=35)

# --- Handle: red, ON points up; pivot on its axis on the door's face --------------------------------------------------
HANDLE_AT = (x(Q1_U), Q1_Y, FRONT + 0.023)
handle = Part('Cab_Handle')
handle.cylinder((0, 0, 0.012), 0.03, 0.024, 'z', RED, segments=24)
handle.box((0, 0.055, 0.02), (0.028, 0.10, 0.018), RED, bevel=0.005)
handle.box((0, 0.0, 0.0255), (0.012, 0.012, 0.002), BLACK)
handle.build(HANDLE_AT, sharp_deg=40)

# --- DC bus charge LEDs ----------------------------------------------------------------------------------------------
for name, at in leds.items():
    led = Part('Cab_Led_' + name)
    led.cylinder((0, 0, 0), 0.0055, 0.006, 'z', LED, segments=12)
    led.build(at)

# --- Probe markers: a 2 mm box each, at the screw face; Unity keeps only the transform -------------------------------
for name, at in probes.items():
    marker = Part('Probe_' + name)
    marker.box((0, 0, 0), (0.002, 0.002, 0.002), WHITE)
    marker.build(at)

print('[probes]', sorted(probes))
print('[triangles]', {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.context.scene.objects
                     if o.type == 'MESH' and not o.name.startswith('Probe_')})
save_and_export(OUT_FBX, OUT_BLEND)
