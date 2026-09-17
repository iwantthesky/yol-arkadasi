import bpy, math, json, os
from mathutils import Vector, Matrix

ROOT = r'C:/Users/Engin/Documents/ChatGPT/ÖnAraştırmacı/work/motor-coop'
OUT = ROOT + '/Unity/Assets/Resources/Motorcycle'
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
source = [o for o in scene.objects if o.type in {'MESH', 'CURVE', 'FONT'}
          and not o.name.startswith(('Studio_', 'Moto_Paddock_', 'Moto_Side_Stand_'))]
front = bpy.data.objects['Realistic_Front_Tire']
rear = bpy.data.objects['Realistic_Rear_Tire']
fc = front.matrix_world.translation.copy()
rc = rear.matrix_world.translation.copy()
ground = min((o.matrix_world @ Vector(c)).z for o in [front, rear] for c in o.bound_box)
origin = (fc + rc) * .5
origin.z = ground
# Retain the source geometry; apply scale and orientation only to evaluated copies.
scale = 1.9 / abs(fc.x - rc.x)
conversion = Matrix.Rotation(-math.pi / 2, 4, 'Z') @ Matrix.Scale(scale, 4) @ Matrix.Translation(-origin)
dg = bpy.context.evaluated_depsgraph_get()
grouped = {'Body': [], 'FrontWheel': [], 'RearWheel': []}
materials = []
for mat in bpy.data.materials:
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat.use_nodes else None
    color = list(bsdf.inputs['Base Color'].default_value) if bsdf else list(mat.diffuse_color)
    rough = float(bsdf.inputs['Roughness'].default_value) if bsdf else .5
    metal = float(bsdf.inputs['Metallic'].default_value) if bsdf else 0
    mat.diffuse_color = color
    materials.append({'name': mat.name, 'color': color, 'roughness': rough, 'metallic': metal})
for obj in source:
    evaluated = obj.evaluated_get(dg)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=dg)
    mesh.transform(conversion @ obj.matrix_world)
    copy = bpy.data.objects.new('EXPORT_' + obj.name, mesh)
    scene.collection.objects.link(copy)
    group = 'Body'
    if obj.name.startswith(('Realistic_Front_Tire', 'Realistic_Front_Rim', 'Realistic_Front_Rotor')):
        group = 'FrontWheel'
    elif obj.name.startswith(('Realistic_Rear_Tire', 'Realistic_Rear_Rim', 'Realistic_Rear_Rotor', 'Realistic_Rear_Sprocket')):
        group = 'RearWheel'
    grouped[group].append(copy)
exports = []
for name, objects in grouped.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    combined = bpy.context.object
    combined.name = name
    if name != 'Body':
        pivot = conversion @ (fc if name == 'FrontWheel' else rc)
        combined.data.transform(Matrix.Translation(-pivot))
        combined.location = pivot
    exports.append(combined)
bpy.ops.object.select_all(action='DESELECT')
for obj in exports: obj.select_set(True)
bpy.context.view_layer.objects.active = exports[0]
bpy.ops.export_scene.fbx(filepath=OUT + '/EnginMotor.fbx', use_selection=True,
    object_types={'MESH'}, use_mesh_modifiers=False, bake_anim=False,
    add_leaf_bones=False, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    global_scale=1, mesh_smooth_type='FACE', path_mode='AUTO')
with open(OUT + '/materials.json','w',encoding='utf-8') as f:
    json.dump({'materials':materials},f,ensure_ascii=False,indent=2)
report = {'original_file': bpy.data.filepath, 'source_objects':len(source),
    'scale':scale, 'forward_axis_blender':'-Y', 'wheelbase_m':1.9,
    'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),
    'position':list(o.location)} for o in exports]}
with open(ROOT + '/qa/motor-export.json','w',encoding='utf-8') as f: json.dump(report,f,indent=2)
# Source .blend is never saved. All exported objects belong only to this running copy.
print(json.dumps(report))
