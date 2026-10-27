"""Shared helpers for the headless Blender model scripts (M12): parts built in Unity's axes, exported for Unity.

Geometry is written in Unity space (X east, Y up, Z north; metres) and turned into Blender's on build, so an
FBX exported with export_fbx (Apply Transform) comes into Unity with no rotation and a scale of 1, every object's
origin on its part's pivot. Builders pick the meshes up by object name.
"""
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# Unity (x, y, z) -> Blender (-x, -z, y); the mirror is undone by Unity's own on import.
TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def args(default_fbx):
    """(out.fbx, out.blend or None) from the arguments after '--'."""
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return (argv[0] if argv else default_fbx), (argv[1] if len(argv) > 1 else None)


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    return scene


def material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    mat.diffuse_color = (*color, 1.0)
    return mat


def axis_matrix(axis):
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
        elif plane == 'xz':
            pts.append((center[0] + radius * math.cos(a), center[1], center[2] + radius * math.sin(a)))
        else:  # 'zy'
            pts.append((center[0], center[1] + radius * math.sin(a), center[2] + radius * math.cos(a)))
    return pts


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

    def box(self, center, size, mat, bevel=0.0, segments=3):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments, affect='EDGES', profile=0.5)
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self.merge(bm, mat)

    def cylinder(self, center, radius, depth, axis, mat, segments=24, radius2=None, smooth=False):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius,
                              radius2=radius if radius2 is None else radius2, depth=depth)
        bmesh.ops.rotate(bm, matrix=axis_matrix(axis), verts=bm.verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self.merge(bm, mat, smooth=smooth)

    def sphere(self, center, radius, mat, segments=16):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(6, segments // 2), radius=radius)
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self.merge(bm, mat, smooth=True)

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
        self.merge(bm, mat, smooth=True)

    def ring(self, center, outer, inner, thick, axis, mat, segments=24):
        """A flat washer (grommet, bezel) round an axis."""
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
        bmesh.ops.rotate(bm, matrix=axis_matrix(axis), verts=bm.verts, cent=(0, 0, 0))
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        self.merge(bm, mat, smooth=True)

    def lathe(self, profile, mat, center=(0, 0, 0), segments=32):
        """A solid of revolution round Unity's Y axis; profile is (radius, y) pairs in order, a radius of 0 closing
        that end to a point. Shaded smooth; build(sharp_deg=...) keeps its shoulders crisp."""
        bm = bmesh.new()
        lathe_bm(bm, profile, center, segments)
        self.merge(bm, mat, smooth=True)

    def merge(self, bm, mat, smooth=False):
        index = self.slot(mat)
        for f in bm.faces:
            f.material_index = index
            f.smooth = smooth
        mesh = bpy.data.meshes.new('tmp')
        bm.to_mesh(mesh)
        bm.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def merge_mesh(self, mesh, mat):
        """Adds a finished Blender mesh (Unity space) with one material."""
        index = self.slot(mat)
        for poly in mesh.polygons:
            poly.material_index = index
        self.bm.from_mesh(mesh)

    def build(self, show_at, sharp_deg=None):
        """sharp_deg: shade every face smooth and split the normals where faces meet at more than this angle."""
        bmesh.ops.transform(self.bm, matrix=TO_BLENDER, verts=self.bm.verts)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        if sharp_deg is not None:
            smooth_by_angle(self.bm, sharp_deg)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in self.mats:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        # Spread out for viewing in Blender; Unity uses the mesh about the origin, not the object's position.
        obj.location = TO_BLENDER @ Vector(show_at)
        return obj


def lathe_bm(bm, profile, center=(0, 0, 0), segments=32):
    """Adds a solid of revolution round Unity's Y axis to bm (see Part.lathe)."""
    rings = []
    for r, y in profile:
        if r <= 1e-7:
            rings.append([bm.verts.new((center[0], center[1] + y, center[2]))])
            continue
        rings.append([bm.verts.new((center[0] + r * math.cos(2 * math.pi * s / segments), center[1] + y,
                                    center[2] + r * math.sin(2 * math.pi * s / segments))) for s in range(segments)])
    for r0, r1 in zip(rings, rings[1:]):
        for s in range(segments):
            t = (s + 1) % segments
            if len(r0) == 1 and len(r1) > 1:
                bm.faces.new((r0[0], r1[t], r1[s]))
            elif len(r1) == 1 and len(r0) > 1:
                bm.faces.new((r0[s], r0[t], r1[0]))
            elif len(r0) > 1:
                bm.faces.new((r0[s], r0[t], r1[t], r1[s]))
    faces = [f for f in bm.faces if f.is_valid]
    for ring in (rings[0], rings[-1]):
        if len(ring) > 1:
            faces.append(bm.faces.new(ring))
    # Outward normals now, not only at build: a boolean reads an inside-out cutter as a solid to add.
    bmesh.ops.recalc_face_normals(bm, faces=faces)


def smooth_by_angle(bm, sharp_deg):
    """Smooth shading with hard edges where faces meet at more than sharp_deg (normals must be consistent)."""
    bm.normal_update()
    limit = math.radians(sharp_deg)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0.0) > limit)


def boolean_difference(name, build_target, build_cutters):
    """A Blender mesh (Unity space) of build_target with build_cutters cut out; both build into a bmesh."""
    scene = bpy.context.scene

    def mesh_object(obj_name, build):
        bm = bmesh.new()
        build(bm)
        mesh = bpy.data.meshes.new(obj_name)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(obj_name, mesh)
        scene.collection.objects.link(obj)
        return obj

    target = mesh_object(name + '_target', build_target)
    cutters = mesh_object(name + '_cutters', build_cutters)
    mod = target.modifiers.new('cut', 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.object = cutters
    mod.solver = 'EXACT'
    mod.use_self = True  # cutters may overlap each other (a fork: a round end and a slot to it)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    result = bpy.data.meshes.new_from_object(target.evaluated_get(depsgraph))
    for o in (target, cutters):
        bpy.data.objects.remove(o)
    return result


def save_and_export(out_fbx, out_blend):
    if out_blend:
        bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    bpy.ops.export_scene.fbx(
        filepath=out_fbx,
        use_selection=False,
        object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
    )
    print('[export]', [o.name for o in bpy.context.scene.objects], 'to', out_fbx)
