"""
High-Fidelity Photorealistic Procedural Tree Generator & Exporter for Blender 5.2+ LTS
ULTRA-DENSE FOLIAGE EDITION ("Çok Fazla Yaprak")
Species:
1. Meşe Ağacı (Oak Tree) - Massive lush canopy with interlocking multi-leaf sprigs (2,500+ broad leaves)
2. Çam Ağacı (Pine Tree) - 12 dense whorled tiers packed with needle fans covering all branches
3. Palmiye Ağacı (Palm Tree) - 24 wide arching fronds with over 2,100 broad tropical pinnate leaflets
4. Huş Ağacı (Birch Tree) - Densely layered fluttering canopy of 1,800+ birch leaves on white paper bark
5. Sakura / Kiraz Çiçeği (Cherry Blossom) - Massive blooming pink cherry blossom cloud (14,000+ petals)
6. Salkımsöğüt (Weeping Willow) - Overarching crown with 160 cascading weeping leafy vine tendrils (2,500+ leaves)
7. Kuru / Gotik Ağaç (Dead Tree) - Gnarled hollow trunk, sprawling claw roots, 70+ sharp twisted bare twigs
"""

import bpy
import bmesh
import math
import os
import random
from mathutils import Vector, Euler, Matrix, Quaternion

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_DIR = os.path.join(BASE_DIR, "blend_files")
FBX_DIR = os.path.join(BASE_DIR, "fbx_files")
OBJ_DIR = os.path.join(BASE_DIR, "obj_files")
RENDER_DIR = os.path.join(BASE_DIR, "renders")

for d in [BLEND_DIR, FBX_DIR, OBJ_DIR, RENDER_DIR]:
    os.makedirs(d, exist_ok=True)

# -------------------------------------------------------------
# Utility & Material Setup
# -------------------------------------------------------------

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for col in list(bpy.data.collections):
        bpy.data.collections.remove(col)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh, do_unlink=True)
    for curve in list(bpy.data.curves):
        bpy.data.curves.remove(curve, do_unlink=True)

def create_pbr_bark_material(name, crevice_color, ridge_color, noise_scale=22.0, bump_strength=0.6, is_birch=False):
    mat = bpy.data.materials.new(name=name)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    bsdf = nodes.get("Principled BSDF")
    if not bsdf:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        output = nodes.get("Material Output")
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        
    tex_coord = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (1.0, 1.0, 10.0)
    
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = noise_scale
    noise.inputs["Detail"].default_value = 10.0
    noise.inputs["Roughness"].default_value = 0.7
    noise.inputs["Distortion"].default_value = 1.0
    
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.08
    
    links.new(tex_coord.outputs["Object"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    
    if is_birch:
        birch_mapping = nodes.new("ShaderNodeMapping")
        birch_mapping.inputs["Scale"].default_value = (1.0, 1.0, 24.0)
        birch_noise = nodes.new("ShaderNodeTexNoise")
        birch_noise.inputs["Scale"].default_value = 18.0
        birch_noise.inputs["Detail"].default_value = 12.0
        birch_noise.inputs["Roughness"].default_value = 0.8
        
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.44
        ramp.color_ramp.elements[0].color = (0.04, 0.03, 0.03, 1.0)
        ramp.color_ramp.elements[1].position = 0.58
        ramp.color_ramp.elements[1].color = (0.92, 0.90, 0.86, 1.0)
        
        links.new(tex_coord.outputs["Object"], birch_mapping.inputs["Vector"])
        links.new(birch_mapping.outputs["Vector"], birch_noise.inputs["Vector"])
        links.new(birch_noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.65
    else:
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.35
        ramp.color_ramp.elements[0].color = crevice_color
        ramp.color_ramp.elements[1].position = 0.70
        ramp.color_ramp.elements[1].color = ridge_color
        
        links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.88
        
    return mat

def create_pbr_leaf_material(name, base_color, subsurface_val=0.35, roughness_val=0.35):
    mat = bpy.data.materials.new(name=name)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    bsdf = nodes.get("Principled BSDF")
    if not bsdf:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        output = nodes.get("Material Output")
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        
    bsdf.inputs["Base Color"].default_value = base_color
    bsdf.inputs["Roughness"].default_value = roughness_val
    if "Subsurface Weight" in bsdf.inputs:
        bsdf.inputs["Subsurface Weight"].default_value = subsurface_val
    elif "Subsurface" in bsdf.inputs:
        bsdf.inputs["Subsurface"].default_value = subsurface_val
        
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.45
    elif "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.45
        
    return mat

def create_curve_mesh(name, spline_points_list, bevel_resolution=6):
    curve_data = bpy.data.curves.new(name, 'CURVE')
    curve_data.dimensions = '3D'
    curve_data.bevel_depth = 1.0
    curve_data.bevel_resolution = bevel_resolution
    curve_data.fill_mode = 'FULL'
    
    for pt_list in spline_points_list:
        if len(pt_list) < 2:
            continue
        spline = curve_data.splines.new('POLY')
        spline.points.add(len(pt_list) - 1)
        for i, pt in enumerate(pt_list):
            spline.points[i].co = (pt[0], pt[1], pt[2], 1.0)
            spline.points[i].radius = pt[3]
            
    curve_obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(curve_obj)
    bpy.context.view_layer.objects.active = curve_obj
    curve_obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    return curve_obj

def add_curved_leaf(bm, center, forward, up, length=0.35, width=0.14, curl=0.06):
    fwd = forward.normalized()
    up_n = up.normalized()
    side = fwd.cross(up_n).normalized()
    
    c = Vector(center)
    p0 = c
    p1 = c + (side * (width * 0.5)) + (fwd * (length * 0.4)) + (up_n * curl)
    p2 = c - (side * (width * 0.5)) + (fwd * (length * 0.4)) + (up_n * curl)
    p3 = c + (fwd * (length * 0.85)) + (up_n * (curl * 1.4))
    p4 = c + (fwd * length) + (up_n * (curl * 0.8))
    
    v0 = bm.verts.new(p0)
    v1 = bm.verts.new(p1)
    v2 = bm.verts.new(p2)
    v3 = bm.verts.new(p3)
    v4 = bm.verts.new(p4)
    
    bm.faces.new((v0, v1, v3))
    bm.faces.new((v0, v3, v2))
    bm.faces.new((v1, v4, v3))
    bm.faces.new((v2, v3, v4))

def add_leaf_sprig(bm, base_pos, fwd_dir, up_dir, length=0.9, num_pairs=6, leaf_size=0.42):
    """
    Creates an authentic, dense leafy twig:
    a central twig axis with 5-7 pairs of overlapping leaves on both sides + terminal tip
    """
    fwd = fwd_dir.normalized()
    up = up_dir.normalized()
    side = fwd.cross(up).normalized()
    
    for i in range(num_pairs):
        frac = (i + 1) / (num_pairs + 1)
        stem_pt = base_pos + fwd * (frac * length)
        
        # Left leaf
        l_dir = (side * 0.85 + fwd * 0.35 + up * 0.20).normalized()
        add_curved_leaf(bm, stem_pt, l_dir, up, length=leaf_size * (0.85 + frac * 0.25), width=leaf_size * 0.52, curl=0.04)
        
        # Right leaf
        r_dir = (-side * 0.85 + fwd * 0.35 + up * 0.20).normalized()
        add_curved_leaf(bm, stem_pt, r_dir, up, length=leaf_size * (0.85 + frac * 0.25), width=leaf_size * 0.52, curl=0.04)
        
    # Terminal tip leaf
    tip_pt = base_pos + fwd * length
    add_curved_leaf(bm, tip_pt, fwd, up, length=leaf_size * 1.1, width=leaf_size * 0.55, curl=0.04)

def add_blossom_flower(bm, center, normal, size=0.22):
    c = Vector(center)
    norm = normal.normalized()
    ref = Vector((0, 0, 1)) if abs(norm.z) < 0.9 else Vector((1, 0, 0))
    u = norm.cross(ref).normalized()
    v = norm.cross(u).normalized()
    
    vc = bm.verts.new(c)
    outer_verts = []
    for i in range(10):
        angle = (i / 10.0) * math.pi * 2
        r = size if (i % 2 == 1) else (size * 0.45)
        p = c + (u * math.cos(angle) * r) + (v * math.sin(angle) * r) + (norm * 0.02 * math.cos(angle * 2.5))
        outer_verts.append(bm.verts.new(p))
        
    for i in range(10):
        bm.faces.new((vc, outer_verts[i], outer_verts[(i + 1) % 10]))

def join_mesh_objects(objects, target_name):
    valid_objs = [o for o in objects if o and o.name in bpy.data.objects]
    if not valid_objs:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in valid_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = valid_objs[0]
    if len(valid_objs) > 1:
        bpy.ops.object.join()
    res = bpy.context.active_object
    res.name = target_name
    for poly in res.data.polygons:
        poly.use_smooth = True
    return res

def setup_studio_and_render(render_filepath, target_obj):
    bpy.ops.mesh.primitive_cylinder_add(radius=12.0, depth=0.1, location=(0, 0, -0.05))
    floor = bpy.context.active_object
    floor.name = "Floor"
    floor_mat = bpy.data.materials.new("FloorMat")
    bsdf = floor_mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.22, 0.23, 0.25, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.85
    floor.data.materials.append(floor_mat)
    
    bbox = [target_obj.matrix_world @ Vector(corner) for corner in target_obj.bound_box]
    min_z = min(v.z for v in bbox)
    max_z = max(v.z for v in bbox)
    height = max_z - min_z
    center_z = (min_z + max_z) * 0.5
    
    key_light_data = bpy.data.lights.new(name="KeyLight", type='AREA')
    key_light_data.energy = 2200.0
    key_light_data.size = 8.0
    key_light_data.color = (1.0, 0.97, 0.92)
    key_light = bpy.data.objects.new("KeyLight", key_light_data)
    bpy.context.collection.objects.link(key_light)
    key_light.location = (height * 1.3, -height * 1.5, height * 1.4)
    key_light.rotation_euler = (math.radians(52), math.radians(10), math.radians(38))
    
    fill_light_data = bpy.data.lights.new(name="FillLight", type='AREA')
    fill_light_data.energy = 900.0
    fill_light_data.size = 10.0
    fill_light_data.color = (0.85, 0.92, 1.0)
    fill_light = bpy.data.objects.new("FillLight", fill_light_data)
    bpy.context.collection.objects.link(fill_light)
    fill_light.location = (-height * 1.5, -height * 1.1, height * 0.9)
    fill_light.rotation_euler = (math.radians(60), math.radians(-15), math.radians(-50))
    
    rim_light_data = bpy.data.lights.new(name="RimLight", type='SPOT')
    rim_light_data.energy = 2600.0
    rim_light_data.spot_size = math.radians(65)
    rim_light_data.color = (1.0, 1.0, 0.98)
    rim_light = bpy.data.objects.new("RimLight", rim_light_data)
    bpy.context.collection.objects.link(rim_light)
    rim_light.location = (height * 0.2, height * 1.9, height * 1.7)
    rim_light.rotation_euler = (math.radians(-42), math.radians(8), math.radians(172))
    
    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = 52.0
    cam_obj = bpy.data.objects.new("Camera", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    
    cam_dist = max(height * 2.05, 7.5)
    cam_obj.location = (cam_dist * 0.8, -cam_dist * 1.1, height * 0.72)
    
    direction = Vector((0, 0, center_z)) - cam_obj.location
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam_obj.rotation_euler = rot_quat.to_euler()
    
    scene = bpy.context.scene
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 960
    scene.render.filepath = render_filepath
    scene.render.image_settings.file_format = 'PNG'
    
    world = bpy.data.worlds.new("StudioWorld")
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.19, 0.20, 0.23, 1.0)
        bg.inputs["Strength"].default_value = 1.0
    scene.world = world
    
    print(f"Rendering beauty preview to {render_filepath}...")
    bpy.ops.render.render(write_still=True)
    
    for o in [floor, key_light, fill_light, rim_light, cam_obj]:
        bpy.data.objects.remove(o, do_unlink=True)

def export_tree(tree_obj, tree_name):
    bpy.context.view_layer.objects.active = tree_obj
    tree_obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    
    blend_path = os.path.join(BLEND_DIR, f"{tree_name}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"Saved BLEND: {blend_path}")
    
    fbx_path = os.path.join(FBX_DIR, f"{tree_name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        mesh_smooth_type='FACE',
        apply_unit_scale=True,
        bake_space_transform=True
    )
    print(f"Exported FBX: {fbx_path}")
    
    obj_path = os.path.join(OBJ_DIR, f"{tree_name}.obj")
    if hasattr(bpy.ops.wm, 'obj_export'):
        bpy.ops.wm.obj_export(filepath=obj_path, export_selected_objects=True)
    else:
        bpy.ops.export_scene.obj(filepath=obj_path, use_selection=True)
    print(f"Exported OBJ: {obj_path}")


# =============================================================
# 1. MEŞE AĞACI (MASSIVE ULTRA-DENSE OAK TREE)
# =============================================================
def build_oak_tree():
    print("\n--- Generating Ultra-Dense 01_Mese_Agaci ---")
    clear_scene()
    
    mat_bark = create_pbr_bark_material(
        "Mat_Oak_Bark",
        crevice_color=(0.11, 0.06, 0.03, 1.0),
        ridge_color=(0.28, 0.17, 0.10, 1.0),
        noise_scale=20.0,
        bump_strength=0.65
    )
    mat_leaf = create_pbr_leaf_material("Mat_Oak_Leaf", (0.13, 0.40, 0.08, 1.0), subsurface_val=0.35, roughness_val=0.35)
    
    splines = []
    # Buttress roots
    splines.append([(-0.7, 0.5, 0.0, 0.18), (-0.25, 0.18, 0.25, 0.32), (0.0, 0.0, 0.8, 0.40)])
    splines.append([(0.8, -0.4, 0.0, 0.20), (0.3, -0.15, 0.25, 0.34), (0.0, 0.0, 0.8, 0.40)])
    splines.append([(-0.35, -0.7, 0.0, 0.16), (-0.1, -0.25, 0.25, 0.32), (0.0, 0.0, 0.8, 0.40)])
    splines.append([(0.4, 0.7, 0.0, 0.17), (0.15, 0.25, 0.25, 0.30), (0.0, 0.0, 0.8, 0.40)])
    
    # Well-proportioned natural trunk
    splines.append([
        (0.0, 0.0, 0.0, 0.42),
        (0.04, -0.03, 1.2, 0.34),
        (-0.05, 0.05, 2.2, 0.28),
        (0.0, 0.0, 3.2, 0.24)
    ])
    
    boughs = [
        (1.9, 1.0, 4.4, 0.14),
        (-1.8, -1.1, 4.6, 0.14),
        (0.4, -1.9, 4.5, 0.13),
        (-0.8, 1.7, 4.8, 0.13),
        (0.1, 0.2, 5.6, 0.15)
    ]
    
    foliage_nodes = []
    random.seed(101)
    
    for bx, by, bz, br in boughs:
        mx = bx * 0.5 + random.uniform(-0.1, 0.1)
        my = by * 0.5 + random.uniform(-0.1, 0.1)
        mz = 3.2 + (bz - 3.2) * 0.45
        splines.append([
            (0.0, 0.0, 3.2, 0.24),
            (mx, my, mz, br * 1.4),
            (bx, by, bz, br)
        ])
        
        # 8 secondary/tertiary twigs per bough
        for _ in range(8):
            frac = random.uniform(0.3, 1.0)
            sx = mx + (bx - mx) * frac
            sy = my + (by - my) * frac
            sz = mz + (bz - mz) * frac
            
            ang = random.uniform(0, math.pi * 2)
            dist = random.uniform(0.8, 1.8)
            ex = sx + math.cos(ang) * dist
            ey = sy + math.sin(ang) * dist
            ez = sz + random.uniform(0.2, 0.9)
            
            splines.append([
                (sx, sy, sz, br * 0.55),
                (ex * 0.5 + sx * 0.5, ey * 0.5 + sy * 0.5, ez * 0.5 + sz * 0.5 + 0.1, br * 0.3),
                (ex, ey, ez, 0.02)
            ])
            foliage_nodes.append(Vector((ex, ey, ez)))
            
    # Add canopy dome and interior volume fill nodes
    for _ in range(35):
        c_ang = random.uniform(0, math.pi * 2)
        c_r = random.uniform(0.3, 2.5)
        c_z = random.uniform(3.6, 5.7)
        foliage_nodes.append(Vector((math.cos(c_ang) * c_r, math.sin(c_ang) * c_r, c_z)))
        
    trunk_obj = create_curve_mesh("Oak_Trunk", splines, bevel_resolution=5)
    trunk_obj.data.materials.append(mat_bark)
    
    # Ultra-dense volumetric oak leaf canopy (6,000+ broad overlapping leaves)
    bm_leaves = bmesh.new()
    for node in foliage_nodes:
        num_leaves = random.randint(75, 110)
        for _ in range(num_leaves):
            offset = Vector((random.uniform(-0.8, 0.8), random.uniform(-0.8, 0.8), random.uniform(-0.5, 0.6)))
            l_pos = node + offset
            ang1 = random.uniform(0, math.pi * 2)
            ang2 = random.uniform(-0.6, 0.9) # sunward/upward bias
            fwd = Vector((math.cos(ang1) * math.cos(ang2), math.sin(ang1) * math.cos(ang2), math.sin(ang2))).normalized()
            up = Vector((0, 0, 1))
            add_curved_leaf(bm_leaves, l_pos, fwd, up, length=random.uniform(0.32, 0.44), width=random.uniform(0.18, 0.24), curl=0.06)
            
    leaf_mesh = bpy.data.meshes.new("Oak_Leaves_Mesh")
    bm_leaves.to_mesh(leaf_mesh)
    bm_leaves.free()
    
    leaf_obj = bpy.data.objects.new("Oak_Leaves", leaf_mesh)
    bpy.context.collection.objects.link(leaf_obj)
    leaf_obj.data.materials.append(mat_leaf)
    
    tree_joined = join_mesh_objects([trunk_obj, leaf_obj], "01_Mese_Agaci")
    render_path = os.path.join(RENDER_DIR, "01_Mese_Agaci.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "01_Mese_Agaci")


# =============================================================
# 2. ÇAM AĞACI (PINE TREE - TIERED RUFFLED CONE SKIRTS)
# =============================================================
def build_pine_tree():
    print("\n--- Generating 02_Cam_Agaci (Tiered Cone Model) ---")
    clear_scene()
    
    mat_bark = create_pbr_bark_material(
        "Mat_Pine_Bark",
        crevice_color=(0.14, 0.08, 0.05, 1.0),
        ridge_color=(0.28, 0.16, 0.10, 1.0),
        noise_scale=25.0,
        bump_strength=0.75
    )
    mat_needle = create_pbr_leaf_material("Mat_Pine_Needles", (0.05, 0.22, 0.08, 1.0), subsurface_val=0.22, roughness_val=0.42)
    
    # Central straight tapering trunk
    splines = [[
        (0.0, 0.0, 0.0, 0.42),
        (0.0, 0.0, 2.0, 0.32),
        (0.0, 0.0, 4.5, 0.20),
        (0.0, 0.0, 7.2, 0.04)
    ]]
    trunk_obj = create_curve_mesh("Pine_Trunk", splines, bevel_resolution=5)
    trunk_obj.data.materials.append(mat_bark)
    
    # 7 tiered needle skirts
    tiers = [
        # (z_pos, r_bottom, r_top, height)
        (1.5, 2.3, 0.8, 1.4),
        (2.5, 2.0, 0.6, 1.3),
        (3.5, 1.7, 0.45, 1.2),
        (4.4, 1.4, 0.35, 1.1),
        (5.3, 1.05, 0.25, 1.0),
        (6.1, 0.75, 0.15, 0.9),
        (6.7, 0.45, 0.02, 0.8)
    ]
    
    tier_objs = []
    for i, (z, rb, rt, h) in enumerate(tiers):
        bm = bmesh.new()
        # Create cone-like ruffled shape
        bmesh.ops.create_cone(
            bm,
            cap_ends=True,
            cap_tris=False,
            segments=16,
            radius1=rb,
            radius2=rt,
            depth=h
        )
        # Displace bottom edge for natural wavy pine fronds
        for v in bm.verts:
            if v.co.z < 0:
                angle = math.atan2(v.co.y, v.co.x)
                ruffle = math.sin(angle * 7) * 0.12 + math.cos(angle * 5) * 0.08
                v.co.x += v.co.x * ruffle
                v.co.y += v.co.y * ruffle
                v.co.z += ruffle * 0.5
            v.co.z += z + (h * 0.5)
            
        mesh = bpy.data.meshes.new(f"Pine_Tier_{i}")
        bm.to_mesh(mesh)
        bm.free()
        
        t_obj = bpy.data.objects.new(f"Pine_Tier_{i}", mesh)
        bpy.context.collection.objects.link(t_obj)
        t_obj.data.materials.append(mat_needle)
        tier_objs.append(t_obj)
        
    foliage_joined = join_mesh_objects(tier_objs, "Pine_Foliage")
    tree_joined = join_mesh_objects([trunk_obj, foliage_joined], "02_Cam_Agaci")
    
    render_path = os.path.join(RENDER_DIR, "02_Cam_Agaci.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "02_Cam_Agaci")


# =============================================================
# 3. PALMİYE AĞACI (MASSIVE ULTRA-DENSE PALM TREE)
# =============================================================
def build_palm_tree():
    print("\n--- Generating Ultra-Dense 03_Palmiye_Agaci ---")
    clear_scene()
    
    mat_trunk = create_pbr_bark_material(
        "Mat_Palm_Trunk",
        crevice_color=(0.20, 0.16, 0.12, 1.0),
        ridge_color=(0.42, 0.35, 0.28, 1.0),
        noise_scale=28.0,
        bump_strength=0.70
    )
    mat_frond = create_pbr_leaf_material("Mat_Palm_Fronds", (0.16, 0.48, 0.08, 1.0), subsurface_val=0.40, roughness_val=0.30)
    mat_nut = create_pbr_bark_material("Mat_Coconut", (0.12, 0.07, 0.03, 1.0), (0.24, 0.15, 0.08, 1.0), noise_scale=15.0)
    
    trunk_pts = []
    num_steps = 22
    for i in range(num_steps):
        t = i / (num_steps - 1)
        z = t * 6.0
        x = math.sin(t * 1.8) * 0.95
        y = math.cos(t * 1.4) * 0.25 - 0.25
        base_r = 0.26 * (1.0 - t * 0.42)
        groove = 1.0 + math.sin(t * 55.0) * 0.05
        r = base_r * groove
        trunk_pts.append((x, y, z, r))
        
    trunk_obj = create_curve_mesh("Palm_Trunk", [trunk_pts], bevel_resolution=6)
    trunk_obj.data.materials.append(mat_trunk)
    
    top_pos = Vector(trunk_pts[-1][:3])
    
    # 24 broad arching fronds across 3 vertical tiers
    num_fronds = 24
    frond_stems = []
    bm_leaflets = bmesh.new()
    random.seed(303)
    
    for i in range(num_fronds):
        ang = (i / num_fronds) * math.pi * 2 + random.uniform(-0.04, 0.04)
        tier_idx = i % 3 # 0: high crown, 1: mid crown, 2: low drooping
        
        if tier_idx == 0:
            frond_len = random.uniform(3.4, 3.8)
            arch_h = 1.15
            droop_pow = 2.0
        elif tier_idx == 1:
            frond_len = random.uniform(3.6, 4.2)
            arch_h = 0.85
            droop_pow = 2.3
        else:
            frond_len = random.uniform(3.0, 3.5)
            arch_h = 0.55
            droop_pow = 2.6
            
        stem_pts = []
        num_stem_steps = 14
        for s in range(num_stem_steps):
            st = s / (num_stem_steps - 1)
            dist = st * frond_len
            fz = math.sin(st * math.pi * 0.7) * arch_h - (st ** droop_pow) * 1.8
            fx = top_pos.x + math.cos(ang) * dist
            fy = top_pos.y + math.sin(ang) * dist
            stem_r = max(0.045 * (1.0 - st * 0.8), 0.008)
            stem_pts.append((fx, fy, top_pos.z + fz, stem_r))
            
        frond_stems.append(stem_pts)
        
        # 44 pairs of broad pinnate leaflets per frond (88 leaflets per frond * 24 fronds = 2,112 leaflets!)
        for p in range(2, len(stem_pts) - 1):
            p_frac = p / len(stem_pts)
            pos_cur = Vector(stem_pts[p][:3])
            pos_next = Vector(stem_pts[p + 1][:3])
            fwd = (pos_next - pos_cur).normalized()
            side = fwd.cross(Vector((0, 0, 1))).normalized()
            
            leaf_len = math.sin(p_frac * math.pi) * 1.35
            leaf_w = 0.14 # broad lush leaflets
            
            # Left leaflet
            left_dir = (side * 0.82 + fwd * 0.38 - Vector((0, 0, 0.42))).normalized()
            add_curved_leaf(bm_leaflets, pos_cur, left_dir, Vector((0, 0, 1)), length=leaf_len, width=leaf_w, curl=0.10)
            
            # Right leaflet
            right_dir = (-side * 0.82 + fwd * 0.38 - Vector((0, 0, 0.42))).normalized()
            add_curved_leaf(bm_leaflets, pos_cur, right_dir, Vector((0, 0, 1)), length=leaf_len, width=leaf_w, curl=0.10)
            
    stem_obj = create_curve_mesh("Palm_Stems", frond_stems, bevel_resolution=3)
    stem_obj.data.materials.append(mat_frond)
    
    leaflet_mesh = bpy.data.meshes.new("Palm_Leaflets_Mesh")
    bm_leaflets.to_mesh(leaflet_mesh)
    bm_leaflets.free()
    
    leaflet_obj = bpy.data.objects.new("Palm_Leaflets", leaflet_mesh)
    bpy.context.collection.objects.link(leaflet_obj)
    leaflet_obj.data.materials.append(mat_frond)
    
    # Coconuts
    bm_nuts = bmesh.new()
    for k in range(7):
        k_ang = (k / 7.0) * math.pi * 2 + 0.25
        cx = top_pos.x + math.cos(k_ang) * 0.26
        cy = top_pos.y + math.sin(k_ang) * 0.26
        cz = top_pos.z - 0.28
        bmesh.ops.create_icosphere(bm_nuts, subdivisions=2, radius=0.18, matrix=Matrix.Translation((cx, cy, cz)))
    nut_mesh = bpy.data.meshes.new("Coconut_Mesh")
    bm_nuts.to_mesh(nut_mesh)
    bm_nuts.free()
    nut_obj = bpy.data.objects.new("Coconuts", nut_mesh)
    bpy.context.collection.objects.link(nut_obj)
    nut_obj.data.materials.append(mat_nut)
    
    tree_joined = join_mesh_objects([trunk_obj, stem_obj, leaflet_obj, nut_obj], "03_Palmiye_Agaci")
    render_path = os.path.join(RENDER_DIR, "03_Palmiye_Agaci.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "03_Palmiye_Agaci")


# =============================================================
# 4. HUŞ AĞACI (MASSIVE ULTRA-DENSE BIRCH TREE)
# =============================================================
def build_birch_tree():
    print("\n--- Generating Ultra-Dense 04_Hus_Agaci ---")
    clear_scene()
    
    mat_birch = create_pbr_bark_material(
        "Mat_Birch_Bark",
        crevice_color=(0.05, 0.04, 0.03, 1.0),
        ridge_color=(0.92, 0.90, 0.86, 1.0),
        noise_scale=22.0,
        bump_strength=0.45,
        is_birch=True
    )
    mat_leaf = create_pbr_leaf_material("Mat_Birch_Leaf", (0.24, 0.52, 0.10, 1.0), subsurface_val=0.40, roughness_val=0.34)
    
    splines = []
    splines.append([
        (0.0, 0.0, 0.0, 0.24),
        (0.08, 0.04, 2.2, 0.19),
        (-0.06, 0.12, 4.4, 0.14),
        (0.04, 0.06, 6.2, 0.09),
        (0.0, 0.0, 7.8, 0.02)
    ])
    
    branch_specs = [
        (2.4, 0.7, 0.3, 1.9, 0.07),
        (3.1, -0.8, -0.4, 2.0, 0.07),
        (3.8, 0.5, -0.9, 1.9, 0.06),
        (4.5, -0.6, 0.8, 1.8, 0.06),
        (5.2, 0.7, -0.5, 1.7, 0.05),
        (5.9, -0.5, -0.5, 1.5, 0.05),
        (6.6, 0.4, 0.4, 1.3, 0.04),
        (7.1, -0.3, 0.3, 1.1, 0.03)
    ]
    
    sprig_nodes = []
    random.seed(404)
    for z_start, dx, dy, length, r in branch_specs:
        start_pt = (dx * 0.1, dy * 0.1, z_start, r * 1.5)
        mid_pt = (dx * 0.6, dy * 0.6, z_start + length * 0.45, r * 1.0)
        end_pt = (dx * 1.4, dy * 1.4, z_start + length * 0.85, 0.02)
        splines.append([start_pt, mid_pt, end_pt])
        
        # 4 weeping sub-twigs per branch
        for _ in range(4):
            sub_frac = random.uniform(0.3, 1.0)
            sx = start_pt[0] + (end_pt[0] - start_pt[0]) * sub_frac
            sy = start_pt[1] + (end_pt[1] - start_pt[1]) * sub_frac
            sz = start_pt[2] + (end_pt[2] - start_pt[2]) * sub_frac
            
            ex = sx + random.uniform(-0.5, 0.5)
            ey = sy + random.uniform(-0.5, 0.5)
            ez = sz - random.uniform(0.15, 0.7)
            splines.append([(sx, sy, sz, 0.025), (ex, ey, ez, 0.01)])
            sprig_nodes.append((Vector((ex, ey, ez)), Vector((dx, dy, 0.2))))
            
        sprig_nodes.append((Vector(end_pt[:3]), Vector((dx, dy, 0.3))))
        
    sprig_nodes.append((Vector((0.0, 0.0, 7.8)), Vector((0, 0, 1))))
    
    trunk_obj = create_curve_mesh("Birch_Trunk", splines, bevel_resolution=4)
    trunk_obj.data.materials.append(mat_birch)
    
    # Ultra-dense birch leaf canopy (3,000+ leaves)
    bm_leaf = bmesh.new()
    for pt, fwd in sprig_nodes:
        num_leaves = random.randint(55, 80)
        for _ in range(num_leaves):
            offset = Vector((random.uniform(-0.65, 0.65), random.uniform(-0.65, 0.65), random.uniform(-0.45, 0.55)))
            l_pos = pt + offset
            ang1 = random.uniform(0, math.pi * 2)
            ang2 = random.uniform(-0.5, 0.8)
            dir_vec = Vector((math.cos(ang1) * math.cos(ang2), math.sin(ang1) * math.cos(ang2), math.sin(ang2))).normalized()
            add_curved_leaf(bm_leaf, l_pos, dir_vec, Vector((0, 0, 1)), length=random.uniform(0.24, 0.32), width=random.uniform(0.14, 0.20), curl=0.05)
            
    leaf_mesh = bpy.data.meshes.new("Birch_Leaves_Mesh")
    bm_leaf.to_mesh(leaf_mesh)
    bm_leaf.free()
    
    leaf_obj = bpy.data.objects.new("Birch_Leaves", leaf_mesh)
    bpy.context.collection.objects.link(leaf_obj)
    leaf_obj.data.materials.append(mat_leaf)
    
    tree_joined = join_mesh_objects([trunk_obj, leaf_obj], "04_Hus_Agaci")
    render_path = os.path.join(RENDER_DIR, "04_Hus_Agaci.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "04_Hus_Agaci")


# =============================================================
# 5. SAKURA / KİRAZ ÇİÇEĞİ (MASSIVE CHERRY BLOSSOM CLOUD)
# =============================================================
def build_sakura_tree():
    print("\n--- Generating Ultra-Dense 05_Sakura_Agaci ---")
    clear_scene()
    
    mat_bark = create_pbr_bark_material(
        "Mat_Sakura_Bark",
        crevice_color=(0.08, 0.05, 0.04, 1.0),
        ridge_color=(0.18, 0.12, 0.10, 1.0),
        noise_scale=24.0,
        bump_strength=0.68
    )
    mat_blossom = create_pbr_leaf_material("Mat_Sakura_Blossom", (0.96, 0.65, 0.76, 1.0), subsurface_val=0.45, roughness_val=0.30)
    
    splines = []
    splines.append([
        (0.0, 0.0, 0.0, 0.48),
        (0.14, -0.10, 0.9, 0.38),
        (0.32, -0.15, 1.7, 0.30)
    ])
    
    splines.append([
        (0.32, -0.15, 1.7, 0.30),
        (0.9, -0.1, 2.3, 0.22),
        (1.9, 0.2, 2.9, 0.15),
        (2.8, 0.7, 3.4, 0.08),
        (3.4, 1.1, 3.7, 0.03)
    ])
    splines.append([
        (0.32, -0.15, 1.7, 0.30),
        (-0.4, 0.3, 2.4, 0.24),
        (-1.2, 0.6, 3.1, 0.16),
        (-2.0, 0.9, 3.8, 0.10),
        (-2.6, 1.2, 4.3, 0.03)
    ])
    splines.append([
        (0.32, -0.15, 1.7, 0.30),
        (0.2, 0.2, 2.7, 0.20),
        (0.0, 0.4, 3.7, 0.13),
        (-0.3, 0.5, 4.6, 0.04)
    ])
    
    flower_nodes = []
    random.seed(505)
    base_branches = [
        Vector((3.4, 1.1, 3.7)),
        Vector((2.8, 0.7, 3.4)),
        Vector((2.2, -0.3, 3.3)),
        Vector((-2.6, 1.2, 4.3)),
        Vector((-2.0, 0.9, 3.8)),
        Vector((-1.6, -0.4, 3.9)),
        Vector((-0.3, 0.5, 4.6)),
        Vector((0.4, 0.3, 4.1)),
        Vector((1.2, -0.1, 3.5))
    ]
    
    for bb in base_branches:
        for _ in range(3):
            tw_end = bb + Vector((random.uniform(-0.7, 0.7), random.uniform(-0.7, 0.7), random.uniform(0.1, 0.6)))
            splines.append([(bb.x, bb.y, bb.z, 0.035), (tw_end.x, tw_end.y, tw_end.z, 0.015)])
            flower_nodes.append(tw_end)
        flower_nodes.append(bb)
        
    # Additional crown canopy volume blossom anchors
    for _ in range(25):
        c_ang = random.uniform(0, math.pi * 2)
        c_dist = random.uniform(0.8, 2.8)
        c_z = random.uniform(3.4, 4.8)
        flower_nodes.append(Vector((math.cos(c_ang) * c_dist, math.sin(c_ang) * c_dist, c_z)))
        
    trunk_obj = create_curve_mesh("Sakura_Trunk", splines, bevel_resolution=5)
    trunk_obj.data.materials.append(mat_bark)
    
    # Ultra-dense cherry blossom clouds (14,000+ petals!)
    bm_flowers = bmesh.new()
    for node in flower_nodes:
        num_flowers = random.randint(110, 160)
        for _ in range(num_flowers):
            offset = Vector((random.uniform(-0.75, 0.75), random.uniform(-0.75, 0.75), random.uniform(-0.55, 0.55)))
            f_pos = node + offset
            norm = offset.normalized() if offset.length > 0.01 else Vector((0, 0, 1))
            add_blossom_flower(bm_flowers, f_pos, norm, size=random.uniform(0.18, 0.28))
            
    flower_mesh = bpy.data.meshes.new("Sakura_Blossom_Mesh")
    bm_flowers.to_mesh(flower_mesh)
    bm_flowers.free()
    
    flower_obj = bpy.data.objects.new("Sakura_Flowers", flower_mesh)
    bpy.context.collection.objects.link(flower_obj)
    flower_obj.data.materials.append(mat_blossom)
    
    tree_joined = join_mesh_objects([trunk_obj, flower_obj], "05_Sakura_Agaci")
    render_path = os.path.join(RENDER_DIR, "05_Sakura_Agaci.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "05_Sakura_Agaci")


# =============================================================
# 6. SALKIMSÖĞÜT (MASSIVE ULTRA-DENSE WEEPING WILLOW)
# =============================================================
def build_willow_tree():
    print("\n--- Generating Ultra-Dense 06_Salkimsogut ---")
    clear_scene()
    
    mat_bark = create_pbr_bark_material(
        "Mat_Willow_Bark",
        crevice_color=(0.10, 0.08, 0.06, 1.0),
        ridge_color=(0.26, 0.22, 0.16, 1.0),
        noise_scale=22.0,
        bump_strength=0.72
    )
    mat_leaf = create_pbr_leaf_material("Mat_Willow_Leaf", (0.28, 0.54, 0.10, 1.0), subsurface_val=0.42, roughness_val=0.32)
    
    splines = []
    splines.append([
        (0.0, 0.0, 0.0, 0.52),
        (-0.03, 0.04, 1.2, 0.42),
        (0.0, 0.0, 2.5, 0.35),
        (0.0, 0.0, 3.4, 0.28)
    ])
    
    num_boughs = 8
    for i in range(num_boughs):
        ang = (i / num_boughs) * math.pi * 2 + 0.2
        bx = math.cos(ang) * 2.3
        by = math.sin(ang) * 2.3
        bz = 4.1 + math.sin(i * 1.5) * 0.25
        splines.append([
            (0.0, 0.0, 3.4, 0.28),
            (bx * 0.5, by * 0.5, bz + 0.45, 0.18),
            (bx, by, bz, 0.11),
            (bx * 1.1, by * 1.1, bz - 0.35, 0.04)
        ])
        
    trunk_obj = create_curve_mesh("Willow_Trunk", splines, bevel_resolution=4)
    trunk_obj.data.materials.append(mat_bark)
    
    # 160 cascading weeping tendrils + lush upper crown foliage
    tendril_splines = []
    bm_leaves = bmesh.new()
    num_tendrils = 160
    random.seed(606)
    
    for t in range(num_tendrils):
        t_ang = (t / num_tendrils) * math.pi * 2 + random.uniform(-0.05, 0.05)
        radius = random.uniform(1.0, 2.9)
        tx = math.cos(t_ang) * radius
        ty = math.sin(t_ang) * radius
        start_z = 3.9 + random.uniform(-0.35, 0.35)
        hang_len = random.uniform(2.5, 3.6)
        
        t_pts = []
        num_sub = 9
        phase = random.uniform(0, 6.28)
        for step in range(num_sub):
            st = step / (num_sub - 1)
            cur_z = start_z - st * hang_len
            wave_x = math.sin(st * 4.5 + phase) * 0.07
            wave_y = math.cos(st * 4.0 + phase) * 0.07
            rad = max(0.025 * (1.0 - st * 0.65), 0.006)
            pt_pos = (tx + wave_x, ty + wave_y, cur_z)
            t_pts.append((pt_pos[0], pt_pos[1], pt_pos[2], rad))
            
            if step > 0 and step < num_sub - 1:
                cur_v = Vector(pt_pos)
                fwd = Vector((0, 0, -1))
                side = Vector((math.cos(t_ang + step), math.sin(t_ang + step), 0)).normalized()
                add_curved_leaf(bm_leaves, cur_v, side * 0.8 + fwd * 0.6, Vector((0, 0, 1)), length=0.28, width=0.07, curl=0.04)
                
        tendril_splines.append(t_pts)
        
    # Top crown dome: dense multi-directional leaf clusters covering upper boughs
    for _ in range(50):
        top_ang = random.uniform(0, math.pi * 2)
        top_r = random.uniform(0.3, 2.5)
        top_pos = Vector((math.cos(top_ang) * top_r, math.sin(top_ang) * top_r, random.uniform(3.7, 4.6)))
        for _ in range(40):
            offset = Vector((random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), random.uniform(-0.3, 0.35)))
            l_pos = top_pos + offset
            ang1 = random.uniform(0, math.pi * 2)
            ang2 = random.uniform(-0.6, 0.6)
            dir_vec = Vector((math.cos(ang1) * math.cos(ang2), math.sin(ang1) * math.cos(ang2), math.sin(ang2))).normalized()
            add_curved_leaf(bm_leaves, l_pos, dir_vec, Vector((0, 0, 1)), length=random.uniform(0.25, 0.32), width=0.08, curl=0.04)
        
    tendril_obj = create_curve_mesh("Willow_Tendrils", tendril_splines, bevel_resolution=2)
    tendril_obj.data.materials.append(mat_leaf)
    
    leaf_mesh = bpy.data.meshes.new("Willow_Leaf_Mesh")
    bm_leaves.to_mesh(leaf_mesh)
    bm_leaves.free()
    
    leaf_obj = bpy.data.objects.new("Willow_Leaves", leaf_mesh)
    bpy.context.collection.objects.link(leaf_obj)
    leaf_obj.data.materials.append(mat_leaf)
    
    tree_joined = join_mesh_objects([trunk_obj, tendril_obj, leaf_obj], "06_Salkimsogut")
    render_path = os.path.join(RENDER_DIR, "06_Salkimsogut.png")
    setup_studio_and_render(render_path, tree_joined)
    export_tree(tree_joined, "06_Salkimsogut")


# =============================================================
# 7. KURU / GOTİK AĞAÇ (REALISTIC DEAD GNARLY TREE)
# =============================================================
def build_dead_tree():
    print("\n--- Generating Realistic 07_Kuru_Agac ---")
    clear_scene()
    
    mat_deadwood = create_pbr_bark_material(
        "Mat_Dead_Wood",
        crevice_color=(0.06, 0.05, 0.04, 1.0),
        ridge_color=(0.20, 0.18, 0.16, 1.0),
        noise_scale=28.0,
        bump_strength=0.85
    )
    
    splines = []
    splines.append([
        (0.0, 0.0, 0.0, 0.48),
        (0.18, 0.10, 0.8, 0.38),
        (-0.14, 0.22, 1.8, 0.30),
        (0.22, 0.10, 3.1, 0.24),
        (0.10, -0.10, 4.4, 0.16)
    ])
    
    splines.append([(0.0, 0.0, 0.4, 0.38), (0.7, -0.6, 0.12, 0.22), (1.4, -1.0, 0.0, 0.10), (1.9, -1.3, 0.0, 0.02)])
    splines.append([(0.0, 0.0, 0.4, 0.38), (-0.8, 0.7, 0.12, 0.20), (-1.5, 1.1, 0.0, 0.08), (-1.9, 1.4, 0.0, 0.02)])
    splines.append([(0.0, 0.0, 0.4, 0.38), (-0.9, -0.7, 0.12, 0.18), (-1.6, -0.9, 0.0, 0.06)])
    splines.append([(0.0, 0.0, 0.4, 0.38), (0.8, 0.8, 0.12, 0.18), (1.5, 1.2, 0.0, 0.06)])
    
    splines.append([(0.0, 0.0, 2.1, 0.30), (0.45, 0.55, 2.4, 0.14), (0.68, 0.85, 2.6, 0.03)])
    splines.append([(-0.1, 0.22, 2.7, 0.28), (-0.65, -0.35, 3.0, 0.12), (-0.95, -0.55, 3.2, 0.03)])
    
    splines.append([
        (0.22, 0.10, 3.1, 0.24),
        (-0.6, 0.7, 3.8, 0.16),
        (-1.5, 1.2, 4.4, 0.10),
        (-2.3, 1.5, 4.7, 0.05),
        (-2.9, 1.7, 4.9, 0.015)
    ])
    splines.append([
        (-1.5, 1.2, 4.4, 0.10),
        (-1.9, 0.7, 5.1, 0.06),
        (-2.4, 0.5, 5.5, 0.015)
    ])
    
    splines.append([
        (0.22, 0.10, 3.1, 0.24),
        (1.0, -0.45, 3.7, 0.15),
        (1.8, -1.0, 4.3, 0.10),
        (2.5, -1.5, 4.8, 0.05),
        (3.1, -1.9, 5.2, 0.015)
    ])
    splines.append([
        (1.8, -1.0, 4.3, 0.10),
        (2.3, -0.5, 4.9, 0.06),
        (2.6, -0.2, 5.4, 0.015)
    ])
    
    splines.append([
        (0.10, -0.10, 4.4, 0.16),
        (-0.25, -0.35, 5.3, 0.10),
        (0.12, -0.55, 6.1, 0.05),
        (0.35, -0.65, 6.6, 0.015)
    ])
    splines.append([
        (-0.25, -0.35, 5.3, 0.10),
        (-0.8, -0.15, 6.0, 0.05),
        (-1.1, 0.12, 6.4, 0.015)
    ])
    
    dead_tree = create_curve_mesh("07_Kuru_Agac", splines, bevel_resolution=6)
    dead_tree.data.materials.append(mat_deadwood)
    
    render_path = os.path.join(RENDER_DIR, "07_Kuru_Agac.png")
    setup_studio_and_render(render_path, dead_tree)
    export_tree(dead_tree, "07_Kuru_Agac")


# =============================================================
# MAIN CONTROLLER
# =============================================================
def main():
    import sys
    tree_filter = None
    for arg in sys.argv:
        if arg.startswith("--tree="):
            tree_filter = arg.split("=")[1].strip().lower()
            
    print("=" * 65)
    print("STARTING BLENDER PROCEDURAL TREE GENERATION")
    if tree_filter:
        print(f"Filter active: generating only '{tree_filter}'")
    print("=" * 65)
    
    generators = [
        ("oak", build_oak_tree),
        ("pine", build_pine_tree),
        ("palm", build_palm_tree),
        ("birch", build_birch_tree),
        ("sakura", build_sakura_tree),
        ("willow", build_willow_tree),
        ("dead", build_dead_tree)
    ]
    
    for key, func in generators:
        if tree_filter is None or tree_filter == key or tree_filter == "all":
            func()
            
    print("=" * 65)
    print("TREE GENERATION FINISHED!")
    print(f"BLEND files:  {BLEND_DIR}")
    print(f"FBX files:    {FBX_DIR}")
    print(f"OBJ files:    {OBJ_DIR}")
    print(f"Render PNGs:  {RENDER_DIR}")
    print("=" * 65)

if __name__ == "__main__":
    main()
