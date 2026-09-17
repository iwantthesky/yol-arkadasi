import json
import math
import os
import sys

import bpy
from mathutils import Vector


def import_model(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    extension = os.path.splitext(path)[1].lower()
    if extension == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif extension in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif extension == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise ValueError(extension)


def bounds_for(objects):
    lo = Vector((math.inf, math.inf, math.inf))
    hi = Vector((-math.inf, -math.inf, -math.inf))
    found = False
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            lo.x, lo.y, lo.z = min(lo.x, point.x), min(lo.y, point.y), min(lo.z, point.z)
            hi.x, hi.y, hi.z = max(hi.x, point.x), max(hi.y, point.y), max(hi.z, point.z)
            found = True
    return (lo, hi) if found else (Vector(), Vector())


argv = sys.argv[sys.argv.index("--") + 1 :]
output = os.path.abspath(argv[0])
models = []
for path in argv[1:]:
    path = os.path.abspath(path)
    import_model(path)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    lo, hi = bounds_for(meshes)
    models.append(
        {
            "path": path,
            "name": os.path.basename(path),
            "mesh_count": len(meshes),
            "vertices": sum(len(obj.data.vertices) for obj in meshes),
            "polygons": sum(len(obj.data.polygons) for obj in meshes),
            "materials": sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}),
            "bounds_min": list(lo),
            "bounds_max": list(hi),
            "dimensions": list(hi - lo),
        }
    )

with open(output, "w", encoding="utf-8") as handle:
    json.dump(models, handle, ensure_ascii=False, indent=2)
print("ANTIGRAVITY_MODEL_INSPECTION_OK")
print(json.dumps({"models": len(models), "output": output}))
