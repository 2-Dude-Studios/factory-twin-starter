"""Builds the fire drill's portable extinguishers and the training station's stand (M15) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/fire_extinguishers.py -- <out.fbx> [<out.blend>]

Three extinguishers, each as separate parts so a hand can work them: <Kind>_Body (pivot at the carry-handle grip,
the body hanging below it), <Kind>_Hose (from the valve down the right side to the nozzle clip), <Kind>_Lever (pivot
on its hinge pin), <Kind>_Pin (the ring pin with its tamper seal, pivot at the pin's head) and <Kind>_Nozzle (pivot
at the tip, pointing +Z; the stream leaves from there). Kinds: Ext_ABC (5 lb dry chemical, red, gauge, short hose),
Ext_CO2 (5 lb, red, no gauge, black discharge horn on a swivel), Ext_Water (2.5 gal stored pressure, stainless,
gauge, long hose, fill valve). Held, the grip is in the hand and the nozzle points forward.

Detail (M15 polish): a label wrapped round the front with the agent, the class pictographs (A green triangle,
B red square, C blue circle; a class it is not for shown grey and struck through), its rating and the P-A-S-S
steps; a gauge with its recharge, charged and overcharged zones and a needle on charged; hex collar, discharge
port, pin boss and grip ridges on the valve; crimped ferrules on the hose; a ribbed nozzle with a hex coupling,
or a ribbed horn on a swivel; an inspection tag on the left side with its grommet and punch rows.

Fire_Stand is the training station: a red board on two posts with three hooks on backplates, a header for the
sign, placards behind the hook labels, a fire blanket on the right post, an inspection log and an extinguisher
sign on the left, and a keep-clear marking on the floor in front. Pivot on the floor at its centre, facing +Z.
Geometry in Unity's axes (unity_parts.py). No branding. Lettering in Inter (SIL OFL 1.1, bundled with Blender).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector  # noqa: E402
from unity_parts import Part, arc, args, load_font, material, save_and_export, setup_scene, text_mesh  # noqa: E402

OUT_FBX, OUT_BLEND = args('Fire_Extinguishers.fbx')
setup_scene()
FONT = load_font('Inter.woff2')
print('[font]', FONT.filepath if FONT else 'built-in')

RED = material('Fire_Red', (0.70, 0.03, 0.02), roughness=0.35)
STAINLESS = material('Fire_Stainless', (0.80, 0.81, 0.83), metallic=1.0, roughness=0.22)
STEEL = material('Fire_Steel', (0.62, 0.63, 0.65), metallic=1.0, roughness=0.3)
BLACK = material('Fire_Black', (0.02, 0.02, 0.022), roughness=0.55)
RUBBER = material('Fire_Rubber', (0.035, 0.035, 0.038), roughness=0.85)
LABEL = material('Fire_Label', (0.93, 0.93, 0.90), roughness=0.6)
INK = material('Fire_Ink', (0.03, 0.03, 0.035), roughness=0.6)
MARK_A = material('Fire_ClassA', (0.05, 0.55, 0.12), roughness=0.5)
MARK_B = material('Fire_ClassB', (0.80, 0.05, 0.03), roughness=0.5)
MARK_C = material('Fire_ClassC', (0.05, 0.22, 0.75), roughness=0.5)
MARK_OFF = material('Fire_ClassOff', (0.55, 0.56, 0.58), roughness=0.6)
GAUGE_FACE = material('Fire_GaugeFace', (0.95, 0.95, 0.93), roughness=0.4)
GAUGE_GREEN = material('Fire_GaugeGreen', (0.10, 0.65, 0.15), roughness=0.4)
GAUGE_RED = material('Fire_GaugeRed', (0.85, 0.08, 0.05), roughness=0.4)
TAG = material('Fire_InspectionTag', (0.95, 0.80, 0.10), roughness=0.6)
SEAL = material('Fire_Seal', (0.85, 0.10, 0.08), roughness=0.5)
BOARD = material('Fire_Board', (0.72, 0.05, 0.03), roughness=0.5)
SIGN_WHITE = material('Fire_SignWhite', (0.96, 0.96, 0.94), roughness=0.5)
PLACARD = material('Fire_Placard', (0.10, 0.11, 0.12), roughness=0.6)
YELLOW = material('Fire_SafetyYellow', (0.95, 0.72, 0.02), roughness=0.55)
PAPER = material('Fire_Paper', (0.97, 0.97, 0.95), roughness=0.8)
HARDBOARD = material('Fire_Hardboard', (0.42, 0.27, 0.13), roughness=0.75)

MARKS = {'A': MARK_A, 'B': MARK_B, 'C': MARK_C}
_WIDTH_PER_SIZE = {}


def text_width(string):
    """Width of the string at size 1 (metres), measured once."""
    if string not in _WIDTH_PER_SIZE:
        import bpy
        mesh = text_mesh(string, 1.0, 0.001, FONT)
        xs = [v.co.x for v in mesh.vertices]
        _WIDTH_PER_SIZE[string] = (max(xs) - min(xs)) if xs else 1.0
        bpy.data.meshes.remove(mesh)
    return _WIDTH_PER_SIZE[string]


def fit_text(part, string, center, size, max_width, mat, right=(1, 0, 0), up=(0, 1, 0), depth=0.0004):
    """Lettering at size, made smaller if it would be wider than max_width."""
    size = min(size, max_width / text_width(string))
    part.text(string, center, size, mat, right=right, up=up, depth=depth, font=FONT)


def wrap(cx, cz, radius):
    """Maps a label sheet (x along the label as read, y up, z out of the surface) onto the body round its axis,
    centred on the front (+Z). Read from the front, the line runs towards -X."""
    def to_body(v):
        theta = -v.x / radius
        r = radius + v.z
        return Vector((cx + r * math.sin(theta), v.y, cz + r * math.cos(theta)))
    return to_body


def band(part, cx, cz, radius, y0, y1, half_deg, thick, mat, segments=24):
    """A curved strip on the body's front: a label or the label's header."""
    import bmesh
    bm = bmesh.new()
    rows = []
    for r in (radius, radius + thick):
        ring = []
        for i in range(segments + 1):
            a = math.radians(-half_deg + 2 * half_deg * i / segments)
            x, z = cx + r * math.sin(a), cz + r * math.cos(a)
            ring.append((bm.verts.new((x, y0, z)), bm.verts.new((x, y1, z))))
        rows.append(ring)
    inner, outer = rows
    for i in range(segments):
        bm.faces.new((outer[i][0], outer[i + 1][0], outer[i + 1][1], outer[i][1]))
        bm.faces.new((inner[i][1], inner[i + 1][1], inner[i + 1][0], inner[i][0]))
        bm.faces.new((inner[i][0], inner[i + 1][0], outer[i + 1][0], outer[i][0]))
        bm.faces.new((outer[i][1], outer[i + 1][1], inner[i + 1][1], inner[i][1]))
    for end in (0, segments):
        bm.faces.new((inner[end][0], outer[end][0], outer[end][1], inner[end][1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    part.merge(bm, mat)


def pictograph(sheet, cls, rated, at, size):
    """NFPA 10 class mark in the label's frame: A green triangle, B red square, C blue circle, a white letter."""
    x, y, z = at
    mat = MARKS[cls] if rated else MARK_OFF
    if cls == 'A':
        sheet.prism((x, y - size * 0.08, z), size * 0.62, 3, 0.0012, 'z', mat)
        letter_y = y - size * 0.15
    elif cls == 'B':
        sheet.box((x, y, z), (size * 0.92, size * 0.92, 0.0012), mat)
        letter_y = y
    else:
        sheet.prism((x, y, z), size * 0.5, 24, 0.0012, 'z', mat)
        letter_y = y
    sheet.text(cls, (x, letter_y, z + 0.0008), size * (0.48 if cls == 'A' else 0.62), SIGN_WHITE, depth=0.0004,
               font=FONT)
    if not rated:
        d = size * 0.5
        sheet.tube([(x - d, y - d, z + 0.0016), (x + d, y + d, z + 0.0016)], size * 0.07, MARK_B, segments=6)


def label(body, cx, cz, radius, top_y, height, agent, sub, classes, rating):
    """The label wrapped round the front: header, agent, class pictographs, rating, the P-A-S-S steps."""
    width = radius * 1.3
    half_deg = math.degrees(width / 2 / radius)
    thick = 0.0012
    bottom_y = top_y - height
    band(body, cx, cz, radius, bottom_y, top_y, half_deg, thick, LABEL)
    band(body, cx, cz, radius + thick, top_y - height * 0.17, top_y - height * 0.02, half_deg * 0.94, 0.0005, MARK_B)

    sheet = Part('label_sheet')
    face = thick + 0.0004
    fit_text(sheet, 'FIRE EXTINGUISHER', (0, top_y - height * 0.095, face + 0.0005), height * 0.075, width * 0.86,
             SIGN_WHITE)
    fit_text(sheet, agent, (0, top_y - height * 0.30, face), height * 0.20, width * 0.82, INK, depth=0.0006)
    fit_text(sheet, sub, (0, top_y - height * 0.43, face), height * 0.065, width * 0.86, INK)
    size = min(width * 0.24, height * 0.15)
    for i, c in enumerate('ABC'):
        pictograph(sheet, c, c in classes, ((i - 1) * size * 1.25, top_y - height * 0.60, face), size)
    fit_text(sheet, rating, (0, top_y - height * 0.755, face), height * 0.07, width * 0.8, INK)
    tile = min(width * 0.17, height * 0.11)
    for i, letter in enumerate('PASS'):
        x = (i - 1.5) * tile * 1.25
        sheet.box((x, top_y - height * 0.89, face), (tile, tile, 0.0008), INK)
        sheet.text(letter, (x, top_y - height * 0.89, face + 0.0007), tile * 0.75, SIGN_WHITE, depth=0.0004, font=FONT)
    body.absorb(sheet, wrap(cx, cz, radius))


def gauge(body, at):
    """Pressure gauge facing +X: bezel, face, recharge-charged-overcharged zones, needle on charged."""
    gx, gy, gz = at
    body.cylinder(at, 0.014, 0.008, 'x', STEEL, segments=24)
    body.ring((gx + 0.0042, gy, gz), 0.0145, 0.0115, 0.0016, 'x', STEEL, segments=24)
    fx = gx + 0.0045
    body.cylinder((fx, gy, gz), 0.0115, 0.0012, 'x', GAUGE_FACE, segments=24)
    zx = fx + 0.0008
    for start, end, mat in ((-62, -22, GAUGE_RED), (-22, 22, GAUGE_GREEN), (22, 62, GAUGE_RED)):
        pts = [(zx, gy + 0.0085 * math.cos(math.radians(a)), gz + 0.0085 * math.sin(math.radians(a)))
               for a in range(start, end + 1, 8)]
        body.tube(pts, 0.0011, mat, segments=5)
    body.tube([(zx + 0.0004, gy - 0.0015, gz), (zx + 0.0004, gy + 0.0078, gz + 0.0006)], 0.0005, INK, segments=5)
    body.sphere((zx + 0.0004, gy, gz), 0.0012, INK, segments=8)


def extinguisher(kind, x, radius, height, body_mat, spec):
    """One extinguisher. The grip is the origin; the body's axis is 70 mm forward of it (+Z), its top 70 mm down."""
    axis_z = 0.07
    top = -0.07
    bottom = top - height
    show = (x, 0.75, 0)
    nozzle = spec['nozzle']

    body = Part(f'{kind}_Body')
    # Shell: flat base, rolled foot, straight side, domed shoulder up to a lipped neck.
    shoulder = top - radius * 0.62
    dome = [(radius * math.cos(math.radians(a)), shoulder + radius * 0.58 * math.sin(math.radians(a)))
            for a in range(0, 81, 10) if radius * math.cos(math.radians(a)) >= 0.031]
    neck_y = dome[-1][1]
    profile = [(0, bottom), (radius * 0.90, bottom), (radius * 0.97, bottom + 0.004), (radius, bottom + 0.012)]
    profile += dome + [(0.029, neck_y + 0.002), (0.027, neck_y + 0.003), (0.027, top), (0, top)]
    body.lathe(profile, body_mat, center=(0, 0, axis_z), segments=48)
    boot = spec.get('boot', 0.008)
    body.ring((0, bottom + boot / 2, axis_z), radius * 1.012, radius * 0.86, boot, 'y', RUBBER, segments=48)
    if body_mat is STAINLESS:
        body.ring((0, shoulder, axis_z), radius + 0.0012, radius - 0.002, 0.003, 'y', STAINLESS, segments=48)

    # Valve: neck collar, hex nut, forged valve body with its discharge port (right) and pin boss (left).
    body.cylinder((0, top + 0.010, axis_z), 0.026, 0.020, 'y', BLACK, segments=32)
    body.cylinder((0, top + 0.024, axis_z), 0.021, 0.008, 'y', STEEL, segments=6)
    body.cylinder((0, top + 0.040, axis_z), 0.019, 0.034, 'y', STEEL, segments=24)
    body.cylinder((0.021, top + 0.034, axis_z), 0.0075, 0.012, 'x', STEEL, segments=12)
    body.cylinder((-0.021, top + 0.052, axis_z - 0.010), 0.0055, 0.006, 'x', STEEL, segments=12)
    if spec.get('fill_valve'):
        body.cylinder((0, top + 0.034, axis_z - 0.022), 0.003, 0.008, 'z', STEEL, segments=8)
        body.cylinder((0, top + 0.034, axis_z - 0.027), 0.0035, 0.004, 'z', BLACK, segments=8)
    if spec.get('burst_disc'):
        body.cylinder((-0.021, top + 0.030, axis_z + 0.006), 0.006, 0.006, 'x', STEEL, segments=6)
    # Carry handle back to the grip, with grip ridges underneath and a stop at its rear end.
    body.box((0, top + 0.050, axis_z - 0.055), (0.026, 0.010, 0.120), BLACK, bevel=0.003)
    for k in range(-2, 3):
        body.box((0, top + 0.0438, axis_z - 0.055 + k * 0.018), (0.022, 0.0025, 0.004), RUBBER, bevel=0.0008,
                 segments=1)
    body.box((0, top + 0.040, axis_z - 0.112), (0.024, 0.016, 0.008), BLACK, bevel=0.002)

    # Label wrapped round the front.
    label_h = height * 0.46
    label_top = (top + bottom) / 2 + label_h / 2 + height * 0.02
    label(body, 0, axis_z, radius, label_top, label_h, spec['agent'], spec['sub'], spec['classes'], spec['rating'])
    if spec.get('gauge'):
        gauge(body, (0.024, top + 0.044, axis_z + 0.012))

    # Inspection tag on a cord from the neck, hanging on the left side: grommet, header, punch rows.
    tag_x = -(radius + 0.003)
    tag_y = top - 0.075
    tag_z = axis_z + 0.012
    body.tube([(-0.02, top + 0.03, axis_z), (-0.04, top + 0.0, axis_z + 0.006), (tag_x - 0.0005, tag_y + 0.04, tag_z)],
              0.0011, INK, segments=6)
    body.box((tag_x, tag_y, tag_z), (0.002, 0.085, 0.050), TAG, bevel=0.0007, segments=1)
    body.ring((tag_x - 0.0012, tag_y + 0.036, tag_z), 0.004, 0.0022, 0.0006, 'x', STEEL, segments=12)
    face_x = tag_x - 0.0012
    right, up = (0, 0, -1), (0, 1, 0)
    fit_text(body, 'INSPECTED', (face_x, tag_y + 0.024, tag_z), 0.007, 0.044, INK, right=right, up=up)
    for row in range(4):
        y = tag_y + 0.010 - row * 0.012
        body.box((face_x, y, tag_z), (0.0006, 0.0007, 0.042), INK, segments=1)
        for col in range(6):
            body.box((face_x - 0.0002, y - 0.0045, tag_z - 0.0175 + col * 0.007), (0.0008, 0.003, 0.003), INK,
                     segments=1)

    # The nozzle clip low on the right side: a base and two prongs.
    clip_y = bottom + 0.16
    body.box((radius + 0.004, clip_y, axis_z), (0.010, 0.022, 0.030), BLACK, bevel=0.002)
    for side_z in (-0.011, 0.011):
        body.box((radius + 0.012, clip_y, axis_z + side_z), (0.010, 0.020, 0.005), BLACK, bevel=0.0015)
    body.build(show, sharp_deg=40)

    # Hose (its own part, hidden while the extinguisher is held): from the discharge port, down the body's right
    # side to the clip, crimped ferrules at both ends. Same origin as the body; its lowest point is where the
    # stowed nozzle starts, its highest is the port.
    hose = Part(f'{kind}_Hose')
    side = radius + 0.014
    r_hose = nozzle['hose']
    hose.tube([(0.020, top + 0.035, axis_z), (0.040, top + 0.030, axis_z), (side * 0.85, top + 0.005, axis_z),
               (side, top - 0.05, axis_z), (side, clip_y + 0.012, axis_z)], r_hose, RUBBER, segments=12)
    hose.cylinder((0.034, top + 0.0335, axis_z), r_hose * 1.45, 0.016, 'x', STEEL, segments=12)
    hose.cylinder((side, clip_y + 0.032, axis_z), r_hose * 1.4, 0.020, 'y', STEEL, segments=12)
    hose.build(show)

    # Lever: squeezed down onto the carry handle; hinge at the valve's rear, grip ridges on top, a lip at the end.
    lever_mat = STEEL if body_mat is STAINLESS else RED
    lever = Part(f'{kind}_Lever')
    lever.box((0, 0.006, -0.055), (0.024, 0.010, 0.118), lever_mat, bevel=0.003)
    lever.box((0, -0.004, -0.004), (0.020, 0.016, 0.012), STEEL, bevel=0.002)
    lever.cylinder((0, -0.004, -0.004), 0.004, 0.028, 'x', STEEL, segments=12)
    for k in range(5):
        lever.box((0, 0.0118, -0.088 + k * 0.012), (0.022, 0.0018, 0.004), lever_mat, bevel=0.0006, segments=1)
    lever.box((0, 0.0105, -0.111), (0.024, 0.009, 0.007), lever_mat, bevel=0.002)
    lever.build((show[0], show[1] + top + 0.064, show[2] + axis_z - 0.002))

    # Pin through the valve, ring outward on the left; the tamper seal's tie loops from ring to handle, its bead
    # at the end.
    pin = Part(f'{kind}_Pin')
    pin.cylinder((0.022, 0, 0), 0.0022, 0.050, 'x', STEEL, segments=10)
    pin.ring((-0.012, 0, 0), 0.014, 0.0105, 0.0035, 'z', STEEL, segments=24)
    pin.tube([(-0.020, -0.006, 0), (-0.024, -0.016, -0.010), (-0.014, -0.020, -0.030)], 0.0012, SEAL, segments=6)
    pin.sphere((-0.014, -0.020, -0.031), 0.0032, SEAL, segments=10)
    pin.build((show[0] - 0.020, show[1] + top + 0.052, show[2] + axis_z - 0.010))

    # Nozzle (or horn): pivot at the tip, the stream along +Z. Shown in front of the body here, as held.
    tip = nozzle['tip']
    length = nozzle['length']
    noz = Part(f'{kind}_Nozzle')
    if nozzle['horn']:
        # Narrow where the hose joins (behind), wide at the mouth (the tip); ribs, a lip, a swivel, the handle.
        r0, r1 = 0.018, 0.055
        noz.cylinder((0, 0, -length / 2), r0, length, 'z', BLACK, segments=32, radius2=r1)
        for t in (0.25, 0.5, 0.75):
            r = r0 + (r1 - r0) * t
            noz.ring((0, 0, -length * (1 - t)), r + 0.0028, r - 0.002, 0.004, 'z', BLACK, segments=32)
        noz.ring((0, 0, -0.002), r1 + 0.003, r1 - 0.004, 0.004, 'z', BLACK, segments=32)
        noz.sphere((0, 0, -length - 0.004), 0.015, STEEL, segments=16)
        noz.box((0, -0.035, -length - 0.02), (0.022, 0.07, 0.03), BLACK, bevel=0.004)  # horn handle
        for k in range(4):
            noz.box((0, -0.050 + k * 0.012, -length - 0.0355), (0.020, 0.004, 0.002), RUBBER, segments=1)
    else:
        noz.cylinder((0, 0, -length / 2), 0.010, length, 'z', RUBBER, segments=20, radius2=0.007)
        noz.cylinder((0, 0, -length + 0.006), 0.0115, 0.012, 'z', STEEL, segments=6)
        ribs = max(2, int((length - 0.03) / 0.014))
        for k in range(ribs):
            z = -length + 0.022 + k * 0.014
            r = 0.010 - 0.003 * (z + length) / length
            noz.ring((0, 0, z), r + 0.0015, r - 0.001, 0.003, 'z', RUBBER, segments=20)
        noz.cylinder((0, 0, -0.004), 0.008, 0.008, 'z', STEEL, segments=20)
        noz.ring((0, 0, -0.0005), 0.0072, 0.004, 0.001, 'z', BLACK, segments=20)
    noz.build((show[0] + tip[0], show[1] + tip[1], show[2] + tip[2]))


extinguisher('Ext_ABC', -0.45, 0.064, 0.36, RED, {
    'agent': 'ABC', 'sub': 'DRY CHEMICAL', 'classes': 'ABC', 'rating': '2-A:10-B:C', 'gauge': True,
    'nozzle': {'tip': (0.07, -0.14, 0.32), 'length': 0.07, 'hose': 0.008, 'horn': False}})
extinguisher('Ext_CO2', 0.0, 0.068, 0.46, RED, {
    'agent': 'CO2', 'sub': 'CARBON DIOXIDE', 'classes': 'BC', 'rating': '5-B:C', 'boot': 0.030, 'burst_disc': True,
    'nozzle': {'tip': (0.07, -0.12, 0.42), 'length': 0.20, 'hose': 0.010, 'horn': True}})
extinguisher('Ext_Water', 0.45, 0.085, 0.50, STAINLESS, {
    'agent': 'WATER', 'sub': 'STORED PRESSURE', 'classes': 'A', 'rating': '2-A', 'gauge': True, 'fill_valve': True,
    'nozzle': {'tip': (0.07, -0.16, 0.40), 'length': 0.12, 'hose': 0.009, 'horn': False}})

# --- Fire_Stand: 1.2 m wide red board on two posts, hooks at 1.32 m (the top of a 5 lb unit below 1.53 m, NFPA 10),
# a white header panel for the sign, a black kick plate. Pivot on the floor, centre; the board faces +Z.
stand = Part('Fire_Stand')
for sx in (-0.56, 0.56):
    stand.box((sx, 0.95, -0.03), (0.06, 1.9, 0.06), BLACK, bevel=0.004)
    stand.box((sx, 0.01, -0.03), (0.30, 0.02, 0.36), BLACK, bevel=0.003)
    for bx in (-0.11, 0.11):
        for bz in (-0.15, 0.09):
            stand.cylinder((sx + bx, 0.022, -0.03 + bz), 0.008, 0.006, 'y', STEEL, segments=6)
    stand.box((sx, 1.905, -0.03), (0.066, 0.01, 0.066), BLACK, bevel=0.002)
stand.box((0, 1.0, 0.0), (1.2, 1.3, 0.025), BOARD, bevel=0.004)
# White trim round the board's face.
for (cx, cy, w, h) in ((0, 1.64, 1.18, 0.012), (0, 0.36, 1.18, 0.012), (-0.59, 1.0, 0.012, 1.28), (0.59, 1.0, 0.012, 1.28)):
    stand.box((cx, cy, 0.0135), (w, h, 0.002), SIGN_WHITE, segments=1)
stand.box((0, 1.78, 0.015), (1.1, 0.22, 0.006), SIGN_WHITE, bevel=0.002, segments=1)
stand.box((0, 1.78, 0.0135), (1.13, 0.25, 0.003), BOARD, segments=1)
stand.box((0, 0.40, 0.015), (1.1, 0.06, 0.006), BLACK)
for hx in (-0.38, 0.0, 0.38):
    # Hook on a backplate with two bolts; a dark placard behind the hook's label (drawn in Unity at 0.60 m).
    stand.box((hx, 1.32, 0.0155), (0.07, 0.075, 0.006), STEEL, bevel=0.002)
    for by in (1.347, 1.293):
        stand.cylinder((hx, by, 0.0195), 0.0055, 0.003, 'z', STEEL, segments=6)
    stand.box((hx, 1.32, 0.03), (0.06, 0.03, 0.05), STEEL, bevel=0.004)
    stand.tube(arc((hx, 1.32, 0.06), 0.02, 180, 360, plane='zy', steps=8), 0.005, STEEL, segments=8)
    stand.box((hx, 0.60, 0.015), (0.34, 0.15, 0.004), PLACARD, bevel=0.002, segments=1)

# Fire blanket in its case on the right post: case, pull tabs, label.
stand.box((0.6, 1.15, -0.03), (0.03, 0.06, 0.04), STEEL)
stand.box((0.67, 1.15, -0.03), (0.14, 0.22, 0.07), BOARD, bevel=0.008)
stand.box((0.67, 1.19, 0.0055), (0.12, 0.05, 0.002), SIGN_WHITE, segments=1)
fit_text(stand, 'FIRE BLANKET', (0.67, 1.19, 0.007), 0.016, 0.11, BOARD, right=(-1, 0, 0))
for tx in (0.64, 0.70):
    stand.box((tx, 1.022, -0.005), (0.016, 0.05, 0.004), BLACK, bevel=0.001)

# Extinguisher sign (our drawing: a white extinguisher and hose on red) and the inspection log on the left post.
stand.box((-0.6, 1.45, -0.03), (0.03, 0.05, 0.04), STEEL)
stand.box((-0.67, 1.45, -0.012), (0.15, 0.15, 0.004), BOARD, bevel=0.002, segments=1)
stand.box((-0.67, 1.43, -0.0095), (0.035, 0.075, 0.002), SIGN_WHITE, bevel=0.006, segments=2)
stand.box((-0.67, 1.476, -0.0095), (0.016, 0.014, 0.002), SIGN_WHITE, segments=1)
stand.tube([(-0.662, 1.478, -0.009), (-0.64, 1.47, -0.009), (-0.632, 1.43, -0.009), (-0.635, 1.40, -0.009)], 0.0028,
           SIGN_WHITE, segments=6)
stand.box((-0.6, 1.08, -0.03), (0.03, 0.05, 0.04), STEEL)
stand.box((-0.69, 1.08, -0.012), (0.17, 0.24, 0.006), HARDBOARD, bevel=0.003, segments=1)
stand.box((-0.69, 1.07, -0.0085), (0.15, 0.19, 0.0012), PAPER, segments=1)
stand.box((-0.69, 1.19, -0.007), (0.07, 0.025, 0.008), STEEL, bevel=0.003)
fit_text(stand, 'INSPECTION LOG', (-0.69, 1.152, -0.0078), 0.014, 0.13, INK, right=(-1, 0, 0))
for row in range(8):
    stand.box((-0.69, 1.125 - row * 0.018, -0.0078), (0.13, 0.0012, 0.0006), INK, segments=1)

# Keep-clear marking on the floor in front of the extinguishers: yellow and black stripes, the words in yellow.
stripe = 0.06
for z0, z1, x0, x1 in ((0.12, 0.17, -0.6, 0.6), (0.67, 0.72, -0.6, 0.6)):
    n = int(round((x1 - x0) / stripe))
    for i in range(n):
        stand.box((x0 + (i + 0.5) * stripe, 0.0015, (z0 + z1) / 2), (stripe, 0.003, z1 - z0),
                  YELLOW if i % 2 == 0 else BLACK, segments=1)
for x0 in (-0.6, 0.55):
    n = int(round((0.67 - 0.17) / stripe))
    for i in range(n):
        stand.box((x0 + 0.025, 0.0015, 0.17 + (i + 0.5) * (0.5 / n)), (0.05, 0.003, 0.5 / n),
                  YELLOW if i % 2 == 1 else BLACK, segments=1)
fit_text(stand, 'KEEP CLEAR', (0, 0.0034, 0.42), 0.11, 0.9, YELLOW, right=(-1, 0, 0), up=(0, 0, -1), depth=0.0012)
stand.build((0, 0, -0.8), sharp_deg=30)

save_and_export(OUT_FBX, OUT_BLEND)
