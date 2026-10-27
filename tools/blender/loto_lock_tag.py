"""Builds the lockout padlock, shackle, tag, cable tie and hasp (M12) and exports them for Unity.

Run headless:  blender -b --factory-startup -P tools/blender/loto_lock_tag.py -- <out.fbx> [<out.blend>]

Every object is named as in the LOTO modelling brief, with its origin on the part's pivot, so LotoProps
(Build VR Rig) picks the meshes up by name from Assets/_Project/Art/LOTO. Geometry is written in Unity's axes
(X east, Y up, Z north; metres) and turned into Blender's here, so the FBX (Apply Transform) comes in with no
rotation and a scale of 1. Sizes follow a typical lockout padlock (38 x 45 x 20 mm body, 6 mm shackle), a
75 x 145 mm danger tag with a brass grommet and nylon tie, and a 1 in scissor hasp.
"""
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT_FBX = argv[0] if argv else 'LOTO_LockTag.fbx'
OUT_BLEND = argv[1] if len(argv) > 1 else None

# Unity (x, y, z) -> Blender (-x, -z, y); the mirror is undone by Unity's own on import.
TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0


def material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    mat.diffuse_color = (*color, 1.0)
    return mat


RED = material('LOTO_LockRed', (0.62, 0.02, 0.015), roughness=0.38)
STEEL = material('LOTO_Steel', (0.72, 0.73, 0.75), metallic=1.0, roughness=0.28)
DARK = material('LOTO_Black', (0.02, 0.02, 0.022), roughness=0.6)
LABEL = material('LOTO_Label', (0.92, 0.92, 0.9), roughness=0.55)
TAG_WHITE = material('LOTO_TagWhite', (0.94, 0.94, 0.91), roughness=0.7)
TAG_RED = material('LOTO_TagRed', (0.72, 0.03, 0.02), roughness=0.6)
BRASS = material('LOTO_Brass', (0.78, 0.58, 0.24), metallic=1.0, roughness=0.32)
NYLON = material('LOTO_Nylon', (0.88, 0.87, 0.82), roughness=0.55)
VINYL = material('LOTO_HaspVinyl', (0.75, 0.04, 0.03), roughness=0.5)


class Part:
    """One exported object: a bmesh in Unity space with a list of materials."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def add(self, geom_verts, mat):
        index = self.slot(mat)
        faces = {f for v in geom_verts for f in v.link_faces}
        for f in faces:
            f.material_index = index

    def box(self, center, size, mat, bevel=0.0, segments=3):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments, affect='EDGES', profile=0.5)
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self._merge(bm, mat)

    def cylinder(self, center, radius, depth, axis, mat, segments=24):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=depth)
        bmesh.ops.rotate(bm, matrix=_axis_matrix(axis), verts=bm.verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self._merge(bm, mat)

    def tube(self, points, radius, mat, segments=12, closed=False):
        """A round wire along a polyline (Unity space)."""
        bm = bmesh.new()
        rings = []
        count = len(points)
        for i, p in enumerate(points):
            p = Vector(p)
            prev = Vector(points[i - 1]) if (i > 0 or closed) else p
            nxt = Vector(points[(i + 1) % count]) if (i < count - 1 or closed) else p
            tangent = (nxt - prev).normalized()
            helper = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((1, 0, 0))
            side = tangent.cross(helper).normalized()
            up = side.cross(tangent).normalized()
            ring = []
            for s in range(segments):
                a = 2 * math.pi * s / segments
                ring.append(bm.verts.new(p + radius * (math.cos(a) * side + math.sin(a) * up)))
            rings.append(ring)
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for r0, r1 in pairs:
            for s in range(segments):
                bm.faces.new((r0[s], r0[(s + 1) % segments], r1[(s + 1) % segments], r1[s]))
        if not closed:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        self._merge(bm, mat, smooth=True)

    def ring(self, center, outer, inner, thick, axis, mat, segments=24):
        """A flat washer (grommet) round an axis."""
        bm = bmesh.new()
        outer_v, inner_v = [], []
        for z in (-thick / 2, thick / 2):
            o, n = [], []
            for s in range(segments):
                a = 2 * math.pi * s / segments
                o.append(bm.verts.new((outer * math.cos(a), outer * math.sin(a), z)))
                n.append(bm.verts.new((inner * math.cos(a), inner * math.sin(a), z)))
            outer_v.append(o)
            inner_v.append(n)
        for s in range(segments):
            t = (s + 1) % segments
            bm.faces.new((outer_v[1][s], outer_v[1][t], inner_v[1][t], inner_v[1][s]))
            bm.faces.new((outer_v[0][t], outer_v[0][s], inner_v[0][s], inner_v[0][t]))
            bm.faces.new((outer_v[0][s], outer_v[0][t], outer_v[1][t], outer_v[1][s]))
            bm.faces.new((inner_v[0][t], inner_v[0][s], inner_v[1][s], inner_v[1][t]))
        bmesh.ops.rotate(bm, matrix=_axis_matrix(axis), verts=bm.verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self._merge(bm, mat, smooth=True)

    def _merge(self, bm, mat, smooth=False):
        index = self.slot(mat)
        for f in bm.faces:
            f.material_index = index
            f.smooth = smooth
        mesh = bpy.data.meshes.new('tmp')
        bm.to_mesh(mesh)
        bm.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def build(self, show_at):
        bmesh.ops.transform(self.bm, matrix=TO_BLENDER, verts=self.bm.verts)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in self.mats:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        scene.collection.objects.link(obj)
        # Spread out for viewing in Blender; Unity uses the mesh about the origin, not the object's position.
        obj.location = TO_BLENDER @ Vector(show_at)
        return obj


def _axis_matrix(axis):
    """Turns a +Z-aligned primitive onto a Unity axis ('x', 'y' or 'z')."""
    if axis == 'x':
        return Matrix.Rotation(math.radians(90), 3, 'Y')
    if axis == 'y':
        return Matrix.Rotation(math.radians(90), 3, 'X')
    return Matrix.Identity(3)


def arc(center, radius, start_deg, end_deg, plane='xy', steps=16):
    pts = []
    for i in range(steps + 1):
        a = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        if plane == 'xy':
            pts.append((center[0] + radius * math.cos(a), center[1] + radius * math.sin(a), center[2]))
        else:  # 'zy'
            pts.append((center[0], center[1] + radius * math.sin(a), center[2] + radius * math.cos(a)))
    return pts


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
hasp = Part('Hasp_Body')
PLATE_X = 0.003
HOLE_Y = -0.09


def _mesh_object(name, build):
    bm = bmesh.new()
    build(bm)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def _plate(bm):
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((PLATE_X, 0.075, 0.045)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0, -0.0605, 0)), verts=bm.verts)


def _cutters(bm):
    for z in (-0.014, 0.0, 0.014):
        verts = bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.0045, radius2=0.0045, depth=0.02)['verts']
        bmesh.ops.rotate(bm, matrix=_axis_matrix('x'), verts=verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector((0, HOLE_Y, z)), verts=verts)


plate_obj = _mesh_object('hasp_plate', _plate)
cutter_obj = _mesh_object('hasp_cutters', _cutters)
holes = plate_obj.modifiers.new('holes', 'BOOLEAN')
holes.operation = 'DIFFERENCE'
holes.object = cutter_obj
holes.solver = 'EXACT'
depsgraph = bpy.context.evaluated_depsgraph_get()
evaluated = plate_obj.evaluated_get(depsgraph)
holed = bpy.data.meshes.new_from_object(evaluated)
hasp.slot(STEEL)
for poly in holed.polygons:
    poly.material_index = 0
hasp.bm.from_mesh(holed)
for o in (plate_obj, cutter_obj):
    bpy.data.objects.remove(o)
bpy.data.meshes.remove(holed)
hasp.box((0, -0.034, 0), (0.0062, 0.022, 0.047), VINYL, bevel=0.0025)
hasp.box((0, -0.0215, 0), (0.004, 0.006, 0.02), STEEL, bevel=0.001, segments=1)
hasp.build((0, 0, 0))

# --- Hasp_Jaw: pivot at the lock tab. A steel ring in the XY plane through the tab's hole, closed onto the
# top of the plate.
jaw = Part('Hasp_Jaw')
jaw.tube(arc((0, -0.009, 0), 0.0095, -90, 270, steps=28), 0.0019, STEEL, segments=10, closed=True)
jaw.build((0, 0.03, 0))

if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=False,
    object_types={'MESH'},
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=True,
    axis_forward='-Z',
    axis_up='Y',
    mesh_smooth_type='FACE',
    use_mesh_modifiers=True,
)
print('[loto_lock_tag] exported', [o.name for o in scene.objects], 'to', OUT_FBX)
