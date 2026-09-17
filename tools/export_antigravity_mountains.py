import math
import os
import sys

import bpy
from mathutils import Vector


def bounds(meshes):
    lo = Vector((math.inf, math.inf, math.inf))
    hi = Vector((-math.inf, -math.inf, -math.inf))
    for obj in meshes:
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            lo.x, lo.y, lo.z = min(lo.x, point.x), min(lo.y, point.y), min(lo.z, point.z)
            hi.x, hi.y, hi.z = max(hi.x, point.x), max(hi.y, point.y), max(hi.z, point.z)
    return lo, hi


argv = sys.argv[sys.argv.index("--") + 1 :]
out_dir = os.path.abspath(argv[0])
os.makedirs(out_dir, exist_ok=True)

for source in argv[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    lo, hi = bounds(meshes)
    offset = Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -lo.z))
    for obj in bpy.context.scene.objects:
        if obj.parent is None:
            obj.location += offset
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    name = os.path.splitext(os.path.basename(source))[0]
    destination = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=destination,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
    )
    print("EXPORTED", destination)

print("ANTIGRAVITY_MOUNTAIN_EXPORT_OK")
