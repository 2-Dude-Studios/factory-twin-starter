"""Builds the lockout padlock, shackle, tag, cable tie and hasp (M12) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/loto_lock_tag.py -- <out.fbx> [<out.blend>]

Every object is named as in the LOTO modelling brief, with its origin on the part's pivot, so LotoProps
(Build VR Rig) picks the meshes up by name from Assets/_Project/Art/LOTO. Geometry is written in Unity's axes
(unity_parts.py). Sizes follow a typical lockout padlock (38 x 45 x 20 mm body, 6 mm shackle), a 75 x 145 mm
danger tag with a brass grommet and nylon tie, and a 1 in scissor hasp.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, arc, args, axis_matrix, boolean_difference, material, save_and_export, setup_scene  # noqa: E402

OUT_FBX, OUT_BLEND = args('LOTO_LockTag.fbx')
setup_scene()

RED = material('LOTO_LockRed', (0.62, 0.02, 0.015), roughness=0.38)
STEEL = material('LOTO_Steel', (0.72, 0.73, 0.75), metallic=1.0, roughness=0.28)
DARK = material('LOTO_Black', (0.02, 0.02, 0.022), roughness=0.6)
LABEL = material('LOTO_Label', (0.92, 0.92, 0.9), roughness=0.55)
TAG_WHITE = material('LOTO_TagWhite', (0.94, 0.94, 0.91), roughness=0.7)
TAG_RED = material('LOTO_TagRed', (0.72, 0.03, 0.02), roughness=0.6)
BRASS = material('LOTO_Brass', (0.78, 0.58, 0.24), metallic=1.0, roughness=0.32)
NYLON = material('LOTO_Nylon', (0.88, 0.87, 0.82), roughness=0.55)
VINYL = material('LOTO_HaspVinyl', (0.75, 0.04, 0.03), roughness=0.5)

# --- Padlock_Shackle: pivot at the top of the shackle (the Padlock's origin, where a socket holds it). A 6 mm
# steel U spanning X: the long leg (x -12.5 mm, the one it swivels on) runs 50 mm down, the short leg 34 mm.
WIRE = 0.003
LEG = 0.0125
ARC_Y = -WIRE - LEG
shackle = Part('Padlock_Shackle')
shackle.tube([(-LEG, -0.050, 0)] + arc((0, ARC_Y, 0), LEG, 180, 0, steps=20) + [(LEG, -0.034, 0)], WIRE, STEEL, segments=14)
shackle.build((0, 0.12, 0))

# --- Padlock_Body: pivot at the body's top centre (30 mm below the Padlock's origin). Red covered body
# 38 x 45 x 20 mm, a steel top plate, a white "locked out" label on both broad faces, the keyway underneath.
body = Part('Padlock_Body')
body.box((0, -0.0235, 0), (0.038, 0.043, 0.020), RED, bevel=0.0035)
body.box((0, -0.0015, 0), (0.034, 0.003, 0.017), STEEL, bevel=0.001, segments=2)
for z in (-0.0101, 0.0101):
    body.box((0, -0.026, z), (0.028, 0.018, 0.0006), LABEL, bevel=0.0002, segments=1)
body.cylinder((0, -0.0455, 0), 0.0055, 0.002, 'y', STEEL)
body.box((0, -0.0462, 0), (0.0014, 0.0006, 0.006), DARK)
body.build((0.08, 0.12, 0))

# --- Tag: pivot at the card's top centre. A 75 x 145 mm card (2 mm) in the YZ plane, its words on the +X face
# (TagLabel writes them): a red header band round a brass grommet, white below.
tag = Part('Tag')
tag.box((0, -0.0095, 0), (0.0021, 0.019, 0.075), TAG_RED, bevel=0.0008, segments=2)
tag.box((0, -0.0825, 0), (0.002, 0.127, 0.075), TAG_WHITE, bevel=0.0008, segments=2)
tag.ring((0, -0.009, 0), 0.0058, 0.0032, 0.0032, 'x', BRASS)
tag.build((0.16, 0.12, 0))

# --- Tag_Tie: pivot at the Padlock's origin. A nylon tie looped round the shackle's short leg, down past the
# body's side to the tag's grommet (the tag's pivot is at (24, -80, 0) mm from here).
tie = Part('Tag_Tie')
loop = [(LEG + 0.0045 * math.cos(math.radians(a)), -0.019 - 0.002 * a / 360, 0.0045 * math.sin(math.radians(a))) for a in range(0, 361, 30)]
tie.tube(loop + [(0.021, -0.028, 0.0), (0.0235, -0.050, 0.0), (0.024, -0.081, 0.0), (0.024, -0.089, 0.0)], 0.0011, NYLON, segments=8)
tie.box((0.024, -0.0935, 0), (0.004, 0.006, 0.0035), NYLON, bevel=0.0006, segments=1)
tie.build((0.24, 0.12, 0))

# --- Hasp_Body: pivot at the isolator's lock tab. A steel plate in the YZ plane hanging below the jaw, three
# 9 mm lock holes along its foot (the middle one 90 mm down, where the hasp socket hangs the padlock), and a
# red vinyl grip on the handle above.
PLATE_X = 0.003
HOLE_Y = -0.09


def _plate(bm):
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((PLATE_X, 0.075, 0.045)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0, -0.0605, 0)), verts=bm.verts)


def _holes(bm):
    for z in (-0.014, 0.0, 0.014):
        verts = bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.0045, radius2=0.0045, depth=0.02)['verts']
        bmesh.ops.rotate(bm, matrix=axis_matrix('x'), verts=verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector((0, HOLE_Y, z)), verts=verts)


hasp = Part('Hasp_Body')
hasp.merge_mesh(boolean_difference('hasp_plate', _plate, _holes), STEEL)
hasp.box((0, -0.034, 0), (0.0062, 0.022, 0.047), VINYL, bevel=0.0025)
hasp.box((0, -0.0215, 0), (0.004, 0.006, 0.02), STEEL, bevel=0.001, segments=1)
hasp.build((0, 0, 0))

# --- Hasp_Jaw: pivot at the lock tab. A steel ring in the XY plane through the tab's hole, closed onto the
# top of the plate.
jaw = Part('Hasp_Jaw')
jaw.tube(arc((0, -0.009, 0), 0.0095, -90, 270, steps=28), 0.0019, STEEL, segments=10, closed=True)
jaw.build((0, 0.03, 0))

save_and_export(OUT_FBX, OUT_BLEND)
