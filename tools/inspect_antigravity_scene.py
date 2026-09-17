import json
import math
import os
import sys

import bpy
from mathutils import Vector


def args_after_separator():
    argv = sys.argv
    return argv[argv.index("--") + 1 :] if "--" in argv else []


def world_bounds(obj):
    if not getattr(obj, "bound_box", None):
        return None
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


out_dir = os.path.abspath(args_after_separator()[0])
os.makedirs(out_dir, exist_ok=True)
scene = bpy.context.scene

objects = []
global_lo = Vector((math.inf, math.inf, math.inf))
global_hi = Vector((-math.inf, -math.inf, -math.inf))
for obj in scene.objects:
    bounds = world_bounds(obj)
    if bounds:
        lo, hi = bounds
        global_lo.x = min(global_lo.x, lo.x)
        global_lo.y = min(global_lo.y, lo.y)
        global_lo.z = min(global_lo.z, lo.z)
        global_hi.x = max(global_hi.x, hi.x)
        global_hi.y = max(global_hi.y, hi.y)
        global_hi.z = max(global_hi.z, hi.z)
    else:
        lo = hi = None
    data = {
        "name": obj.name,
        "type": obj.type,
        "collection": [collection.name for collection in obj.users_collection],
        "location": list(obj.location),
        "rotation_euler": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "bounds_min": list(lo) if lo else None,
        "bounds_max": list(hi) if hi else None,
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    }
    if obj.type == "MESH":
        data.update(
            vertices=len(obj.data.vertices),
            edges=len(obj.data.edges),
            polygons=len(obj.data.polygons),
        )
    objects.append(data)

if not math.isfinite(global_lo.x):
    global_lo = Vector((-5, -5, -5))
    global_hi = Vector((5, 5, 5))

center = (global_lo + global_hi) * 0.5
size = global_hi - global_lo
radius = max(size.length * 0.5, 5.0)

inventory = {
    "blend_file": bpy.data.filepath,
    "blender_version": bpy.app.version_string,
    "scene": scene.name,
    "collections": [collection.name for collection in bpy.data.collections],
    "materials": [material.name for material in bpy.data.materials],
    "world_bounds_min": list(global_lo),
    "world_bounds_max": list(global_hi),
    "world_size": list(size),
    "object_count": len(objects),
    "objects": objects,
}
with open(os.path.join(out_dir, "inventory.json"), "w", encoding="utf-8") as handle:
    json.dump(inventory, handle, ensure_ascii=False, indent=2)

camera_data = bpy.data.cameras.new("CodexInspectionCamera")
camera = bpy.data.objects.new("CodexInspectionCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = center + Vector((radius * 0.95, -radius * 1.25, radius * 0.75))
camera_data.lens = 52
camera_data.clip_end = max(radius * 10, 1000)
look_at(camera, center)

sun_data = bpy.data.lights.new("CodexInspectionSun", "SUN")
sun_data.energy = 2.0
sun_data.angle = math.radians(12)
sun = bpy.data.objects.new("CodexInspectionSun", sun_data)
scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(32), math.radians(-18), math.radians(-28))

area_data = bpy.data.lights.new("CodexInspectionFill", "AREA")
area_data.energy = max(radius * radius * 3.0, 800)
area_data.shape = "DISK"
area_data.size = max(radius, 10)
area = bpy.data.objects.new("CodexInspectionFill", area_data)
scene.collection.objects.link(area)
area.location = center + Vector((-radius * 0.5, -radius * 0.4, radius * 1.5))
look_at(area, center)

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1440
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = os.path.join(out_dir, "overview.png")
scene.render.film_transparent = False
scene.world.color = (0.04, 0.06, 0.09)
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

print("ANTIGRAVITY_INSPECTION_OK")
print(json.dumps({"objects": len(objects), "size": list(size), "output": out_dir}))
