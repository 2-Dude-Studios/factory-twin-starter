"""Builds our own six-axis cobot (M29) for the pick station and exports it for Unity: one mesh per arm link.

Run headless:  blender -b --factory-startup -P tools/blender/cobot.py -- <out.fbx> [<out.blend>]

Our own low-poly design, no third-party content and no branding: it replaces the UR5e visual meshes so nothing of
UR's look ships. Only the joint layout is kept (cell_arm.urdf: link lengths 0.425 / 0.3922 m, the wrist offsets),
because the baked poses, the zones and the drills were sized on it; that layout is a set of numbers, not a design.
The look is ours: tapered rounded-box links with a graphite stripe down each side (not round tubes), white
crowned joint covers with a graphite hub and a safety-orange rim, graphite bands at the joint seams.

Geometry is written in ROS axes for the arm at its zero pose (x forward, y left, z up; the URDF's base frame),
each piece tagged with the link it moves with. Each link's mesh is then moved into that link's own frame and
turned into Unity's axes the way the URDF importer does (ROS (x, y, z) -> Unity (-y, z, x)), so in Unity the mesh
sits under its link GameObject with no offset or rotation. CobotSetup (Tools > Factory Twin > Robot) picks the
meshes up by name: Cobot_<link>.
"""
import math
import os
import sys

import bmesh
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, args, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('Cobot.fbx')
setup_scene()

WHITE = material('Cobot_Shell', (0.86, 0.87, 0.88), roughness=0.45)
GRAPHITE = material('Cobot_Joint', (0.10, 0.11, 0.12), roughness=0.55)
ACCENT = material('Cobot_Accent', (0.95, 0.42, 0.06), roughness=0.5)
STEEL = material('Cobot_Flange', (0.55, 0.56, 0.58), metallic=0.8, roughness=0.35)

SEG = 32  # round sections


def frame(xyz=(0, 0, 0), rpy=(0, 0, 0)):
    return Matrix.Translation(xyz) @ Euler(rpy, 'XYZ').to_matrix().to_4x4()


# The link frames at the zero pose, from cell_arm.urdf's joints (parent @ origin).
PI = math.pi
LINKS = {'base_link_inertia': frame(rpy=(0, 0, PI))}
LINKS['shoulder_link'] = LINKS['base_link_inertia'] @ frame((0, 0, 0.1625))
LINKS['upper_arm_link'] = LINKS['shoulder_link'] @ frame(rpy=(PI / 2, 0, 0))
LINKS['forearm_link'] = LINKS['upper_arm_link'] @ frame((-0.425, 0, 0))
LINKS['wrist_1_link'] = LINKS['forearm_link'] @ frame((-0.3922, 0, 0.1333))
LINKS['wrist_2_link'] = LINKS['wrist_1_link'] @ frame((0, -0.0997, 0), (PI / 2, 0, 0))
LINKS['wrist_3_link'] = LINKS['wrist_2_link'] @ frame((0, 0.0996, 0), (1.570796327, PI, PI))

# Per link: a bmesh in world (ROS, zero pose) with material slots.
pieces = {name: [] for name in LINKS}


def basis(axis):
    """Two unit vectors square to axis."""
    a = Vector(axis).normalized()
    helper = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    u = a.cross(helper).normalized()
    return a, u, a.cross(u).normalized()


def lathe(link, start, axis, profile, mat, segments=SEG):
    """A solid of revolution round axis from start; profile is (radius, distance along axis) pairs in order."""
    a, u, v = basis(axis)
    s = Vector(start)
    bm = bmesh.new()
    rings = []
    for r, d in profile:
        c = s + a * d
        if r <= 1e-7:
            rings.append([bm.verts.new(c)])
            continue
        rings.append([bm.verts.new(c + r * (math.cos(2 * PI * k / segments) * u + math.sin(2 * PI * k / segments) * v))
                      for k in range(segments)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segments):
            t = (k + 1) % segments
            if len(r0) == 1 and len(r1) > 1:
                bm.faces.new((r0[0], r1[t], r1[k]))
            elif len(r1) == 1 and len(r0) > 1:
                bm.faces.new((r0[k], r0[t], r1[0]))
            elif len(r0) > 1:
                bm.faces.new((r0[k], r0[t], r1[t], r1[k]))
    for ring in (rings[0], rings[-1]):
        if len(ring) > 2:
            bm.faces.new(ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    pieces[link].append((bm, mat))


def housing(link, start, axis, length, radius, cap_end=True, cap_start=False, chamfer=0.008):
    """A joint housing: a white drum with chamfered ends; a graphite cap and an orange ring on the capped ends."""
    c = chamfer
    lathe(link, start, axis, [(0, 0), (radius - c, 0), (radius, c), (radius, length - c), (radius - c, length),
                              (0, length)], WHITE)
    a = Vector(axis).normalized()
    for capped, at, out in ((cap_end, length, 1), (cap_start, 0, -1)):
        if not capped:
            continue
        base = Vector(start) + a * at
        d = a * out
        # A white crowned cover with a graphite hub and an orange ring round its rim.
        lathe(link, base, d, [(0, -0.001), (radius - 0.006, -0.001), (radius - 0.008, 0.004),
                              (radius * 0.55, 0.009), (0, 0.010)], WHITE)
        lathe(link, base, d, [(0, 0.008), (radius * 0.3, 0.008), (radius * 0.3, 0.013), (0, 0.013)], GRAPHITE)
        lathe(link, base, d, [(radius - 0.007, -0.001), (radius - 0.001, -0.001), (radius - 0.001, 0.003),
                              (radius - 0.007, 0.003)], ACCENT)


def band(link, start, axis, length, radius):
    """The graphite band where two links meet (the joint's seam)."""
    lathe(link, start, axis, [(0, 0), (radius, 0), (radius, length), (0, length)], GRAPHITE)


def beam(link, p0, p1, up, sec0, sec1, mat=WHITE, steps=4, points=24, stripe=True):
    """A tapered link of rounded-rectangle (superellipse) section from p0 to p1; sec = (width, height), height along
    up. stripe: a graphite strip along each flat side (the link's signature)."""
    p0, p1 = Vector(p0), Vector(p1)
    a = (p1 - p0).normalized()
    upv = (Vector(up) - a * a.dot(Vector(up))).normalized()
    side = a.cross(upv).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(steps + 1):
        t = i / steps
        w = sec0[0] + (sec1[0] - sec0[0]) * t
        h = sec0[1] + (sec1[1] - sec0[1]) * t
        c = p0 + (p1 - p0) * t
        ring = []
        for k in range(points):
            ang = 2 * PI * k / points
            ca, sa = math.cos(ang), math.sin(ang)
            n = 4.0  # superellipse exponent: a rounded box, not a tube
            x = math.copysign(abs(ca) ** (2 / n), ca) * w / 2
            y = math.copysign(abs(sa) ** (2 / n), sa) * h / 2
            ring.append(bm.verts.new(c + x * side + y * upv))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(points):
            t = (k + 1) % points
            bm.faces.new((r0[k], r0[t], r1[t], r1[k]))
    bm.faces.new(rings[0])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    pieces[link].append((bm, mat))
    if stripe:
        inset = 0.04 * (p1 - p0).length
        for sgn in (1, -1):
            q0 = p0 + a * inset + side * sgn * (sec0[0] / 2)
            q1 = p1 - a * inset + side * sgn * (sec1[0] / 2)
            beam(link, q0, q1, up, (0.006, sec0[1] * 0.28), (0.006, sec1[1] * 0.28), GRAPHITE, steps=1, points=12,
                 stripe=False)


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
NY, NZ = (0, -1, 0), (0, 0, -1)
SHOULDER_Z = 0.1625
ELBOW_X = 0.425
WRIST_X = 0.8172
W1_Y = 0.1333
W2_Z = 0.0628

# Base: a graphite mounting flange and a white drum with the orange ring under the first joint.
lathe('base_link_inertia', (0, 0, 0), Z, [(0, 0), (0.09, 0), (0.09, 0.012), (0.08, 0.016), (0, 0.016)], GRAPHITE)
lathe('base_link_inertia', (0, 0, 0), Z, [(0, 0.016), (0.074, 0.016), (0.074, 0.074), (0.068, 0.086),
                                          (0, 0.086)], WHITE)
lathe('base_link_inertia', (0, 0, 0), Z, [(0.0745, 0.05), (0.0755, 0.05), (0.0755, 0.06), (0.0745, 0.06)], ACCENT)
band('base_link_inertia', (0, 0, 0.086), Z, 0.012, 0.062)

# Shoulder (turns about z): a column up to the lift joint and the lift joint's inner drum along y.
housing('shoulder_link', (0, 0, 0.098), Z, 0.064, 0.062, cap_end=False)
housing('shoulder_link', (0, -0.066, SHOULDER_Z), Y, 0.13, 0.064, cap_end=False, cap_start=True)

# Upper arm (turns about y through the shoulder): its drum outboard of the shoulder, the link, the elbow drum.
band('upper_arm_link', (0, 0.064, SHOULDER_Z), Y, 0.012, 0.058)
housing('upper_arm_link', (0, 0.076, SHOULDER_Z), Y, 0.13, 0.064)
housing('upper_arm_link', (ELBOW_X, 0.072, SHOULDER_Z), Y, 0.13, 0.056)
beam('upper_arm_link', (0.03, 0.140, SHOULDER_Z), (ELBOW_X - 0.03, 0.137, SHOULDER_Z), Z, (0.076, 0.118),
     (0.064, 0.082))

# Forearm (turns about y at the elbow): the elbow's inner drum, the link, the first wrist's inner drum.
band('forearm_link', (ELBOW_X, 0.060, SHOULDER_Z), Y, 0.012, 0.05)
housing('forearm_link', (ELBOW_X, -0.05, SHOULDER_Z), Y, 0.11, 0.056, cap_end=False, cap_start=True)
housing('forearm_link', (WRIST_X, -0.042, SHOULDER_Z), Y, 0.10, 0.045, cap_end=False, cap_start=True)
beam('forearm_link', (ELBOW_X + 0.03, 0.004, SHOULDER_Z), (WRIST_X - 0.025, 0.004, SHOULDER_Z), Z, (0.064, 0.094),
     (0.054, 0.066))

# Wrist 1 (turns about y): its outer drum and a neck down to the second wrist.
band('wrist_1_link', (WRIST_X, 0.058, SHOULDER_Z), Y, 0.01, 0.04)
housing('wrist_1_link', (WRIST_X, 0.068, SHOULDER_Z), Y, 0.108, 0.045)
lathe('wrist_1_link', (WRIST_X, W1_Y, SHOULDER_Z), NZ, [(0, 0), (0.042, 0), (0.042, 0.05), (0, 0.05)], WHITE)

# Wrist 2 (turns about the vertical): its drum, capped below, with a side arm out to the last joint.
band('wrist_2_link', (WRIST_X, W1_Y, 0.112), NZ, 0.01, 0.04)
housing('wrist_2_link', (WRIST_X, W1_Y, 0.102), NZ, 0.078, 0.045)
housing('wrist_2_link', (WRIST_X, W1_Y, W2_Z), Y, 0.052, 0.044, cap_end=False)

# Wrist 3 (turns about y): the tool drum and the steel tool flange.
band('wrist_3_link', (WRIST_X, 0.185, W2_Z), Y, 0.008, 0.04)
lathe('wrist_3_link', (WRIST_X, 0.193, W2_Z), Y, [(0, 0), (0.044, 0), (0.044, 0.02), (0.04, 0.03), (0, 0.03)], WHITE)
lathe('wrist_3_link', (WRIST_X, 0.193, W2_Z), Y, [(0.0442, 0.006), (0.0446, 0.006), (0.0446, 0.014),
                                                   (0.0442, 0.014)], ACCENT)
lathe('wrist_3_link', (WRIST_X, 0.223, W2_Z), Y, [(0, 0), (0.0315, 0), (0.0315, 0.01), (0, 0.01)], STEEL)

# ROS (x, y, z) -> Unity (-y, z, x), as the URDF importer maps a link frame.
ROS_TO_UNITY = Matrix(((0, -1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))

tris = 0
for i, (link, items) in enumerate(pieces.items()):
    part = Part('Cobot_' + link)
    to_link = ROS_TO_UNITY @ LINKS[link].inverted()
    for bm, mat in items:
        bmesh.ops.transform(bm, matrix=to_link, verts=bm.verts)
        part.merge(bm, mat)
    obj = part.build((i * 0.4, 0, 0), sharp_deg=40)
    tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
print('[cobot] triangles', tris)
save_and_export(OUT_FBX, OUT_BLEND)
