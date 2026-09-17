import os
import math
import numpy as np
import bpy

# ==============================================================================
# CONFIGURATION & DIRECTORIES
# ==============================================================================
BASE_DIR = r"C:\Users\Engin\.gemini\antigravity\scratch\blender_mountains"
EXPORTS_DIR = os.path.join(BASE_DIR, "exports")
os.makedirs(EXPORTS_DIR, exist_ok=True)

# Clear existing scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# Set render settings (EEVEE)
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'

# ==============================================================================
# VECTORIZED PROCEDURAL NOISE ENGINE
# ==============================================================================
def hash2d(ix, iy, seed=0):
    n = ix * 374761393 + iy * 668265263 + seed * 1274126177
    n = (n ^ (n >> 13)) * 1274126177
    val = ((n ^ (n >> 16)) & 0x7FFFFFFF) / 0x3FFFFFFF - 1.0
    return val

def value_noise_2d(x, y, seed=0):
    ix = np.floor(x).astype(np.int64)
    iy = np.floor(y).astype(np.int64)
    fx = x - ix
    fy = y - iy
    wx = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0)
    wy = fy * fy * fy * (fy * (fy * 6.0 - 15.0) + 10.0)

    v00 = hash2d(ix, iy, seed)
    v10 = hash2d(ix + 1, iy, seed)
    v01 = hash2d(ix, iy + 1, seed)
    v11 = hash2d(ix + 1, iy + 1, seed)

    top = v00 + wx * (v10 - v00)
    bot = v01 + wx * (v11 - v01)
    return top + wy * (bot - top)

def fbm_2d(x, y, octaves=6, lacunarity=2.0, persistence=0.5, seed=0):
    val = np.zeros_like(x)
    amp = 1.0
    total_amp = 0.0
    cx, cy = x.copy(), y.copy()
    cos_a = np.cos(0.6457)
    sin_a = np.sin(0.6457)
    for i in range(octaves):
        val += amp * value_noise_2d(cx, cy, seed + i * 101)
        total_amp += amp
        amp *= persistence
        nx = (cx * cos_a - cy * sin_a) * lacunarity
        ny = (cx * sin_a + cy * cos_a) * lacunarity
        cx, cy = nx, ny
    return val / total_amp

def ridged_fbm_2d(x, y, octaves=6, lacunarity=2.0, persistence=0.5, seed=0):
    val = np.zeros_like(x)
    amp = 1.0
    total_amp = 0.0
    cx, cy = x.copy(), y.copy()
    cos_a = np.cos(0.6457)
    sin_a = np.sin(0.6457)
    for i in range(octaves):
        n = value_noise_2d(cx, cy, seed + i * 101)
        r = 1.0 - np.abs(n)
        r = r * r
        val += amp * r
        total_amp += amp
        amp *= persistence
        nx = (cx * cos_a - cy * sin_a) * lacunarity
        ny = (cx * sin_a + cy * cos_a) * lacunarity
        cx, cy = nx, ny
    return val / total_amp

# ==============================================================================
# HEIGHTMAP GENERATORS
# ==============================================================================
def create_alpine_heights(X, Y, R, falloff):
    spine1 = np.exp(-((Y - 0.25 * X - 0.035 * X**2) / 2.0)**2)
    spine2 = np.exp(-((X + 0.45 * Y) / 2.2)**2)
    spine3 = np.exp(-((Y + 0.65 * X) / 2.5)**2)
    spines = np.maximum(spine1, np.maximum(spine2 * 0.75, spine3 * 0.7))

    ridges = ridged_fbm_2d(X * 0.28, Y * 0.28, octaves=7, persistence=0.52, seed=42)
    crag_detail = ridged_fbm_2d(X * 0.65, Y * 0.65, octaves=4, seed=108)
    micro = fbm_2d(X * 1.3, Y * 1.3, octaves=3, seed=55)

    horn = 3.6 * np.exp(-(R / 2.3)**2)

    Z = (6.0 * ridges * (0.45 + 0.55 * spines) + horn + 1.2 * crag_detail + 0.3 * micro) * falloff
    return np.maximum(Z, 0.0)

def create_volcano_heights(X, Y, R, falloff):
    theta = np.arctan2(Y, X)
    cone = 6.6 / (1.0 + (R / 2.5)**1.9)

    rim_noise = 0.35 * np.sin(5.0 * theta) + 0.2 * np.cos(8.0 * theta)
    r_caldera = 1.85 + rim_noise
    
    # Crater bowl
    in_caldera = R < r_caldera
    crater_depth = np.zeros_like(R)
    crater_depth[in_caldera] = 3.2 * (1.0 - (R[in_caldera] / r_caldera[in_caldera])**2)**1.1

    # Central cinder cone
    dome = np.where(R < 0.65, 1.1 * np.exp(-(R / 0.28)**2), 0.0)

    # Deep radial lava chutes / ravines
    gullies = 0.65 * np.sin(8.0 * theta + 1.2 * np.sin(3.0 * theta)) * np.clip(R / 3.2, 0.0, 1.0) * np.exp(-R / 5.2)
    rugged_rock = 0.45 * ridged_fbm_2d(X * 0.45, Y * 0.45, octaves=4, seed=333)

    Z = (cone - crater_depth + dome + gullies + rugged_rock) * falloff
    return np.maximum(Z, 0.0)

def create_mesa_heights(X, Y, R, falloff):
    theta = np.arctan2(Y, X)
    r_rim = 5.2 + 1.2 * np.cos(3.0 * theta) + 0.7 * np.sin(5.0 * theta) + 0.35 * np.cos(7.0 * theta)
    u = R / np.maximum(r_rim, 0.1)

    Z = np.zeros_like(R)
    top_noise = 0.04 * fbm_2d(X * 0.4, Y * 0.4, octaves=3, seed=888)
    z_plateau = 4.8 + top_noise

    # Region 1: Flat table top (flat plateau)
    mask_flat = (u <= 0.65)
    Z[mask_flat] = z_plateau[mask_flat]

    # Region 2: Stepped canyon cliff walls
    mask_cliff = (u > 0.65) & (u <= 0.88)
    t_cliff = (u[mask_cliff] - 0.65) / (0.88 - 0.65)
    raw_h = z_plateau[mask_cliff] * (1.0 - t_cliff) + 1.8 * t_cliff
    terraces = 0.35 * np.sin(raw_h * math.pi / 0.75)
    vertical_grooves = 0.12 * np.sin(theta[mask_cliff] * 24.0)
    Z[mask_cliff] = raw_h + terraces + vertical_grooves

    # Region 3: Talus apron sloping to ground
    mask_talus = (u > 0.88)
    t_talus = np.clip((u[mask_talus] - 0.88) / 0.75, 0.0, 1.0)
    Z[mask_talus] = 1.8 * (1.0 - t_talus)**2.0

    # Only apply falloff at outer base (R > 7.5) so plateau is 100% flat
    edge_falloff = np.clip((9.2 - R) / 1.5, 0.0, 1.0)
    edge_falloff = edge_falloff * edge_falloff * (3.0 - 2.0 * edge_falloff)
    Z = np.where(R > 7.2, Z * edge_falloff, Z)
    return np.maximum(Z, 0.0)

def create_highlands_heights(X, Y, R, falloff):
    hill1 = 3.2 * np.exp(-(((X + 2.0)**2 + (Y - 1.0)**2) / 18.0))
    hill2 = 2.7 * np.exp(-(((X - 3.5)**2 + (Y + 2.0)**2) / 16.0))
    hill3 = 2.4 * np.exp(-(((X - 1.0)**2 + (Y - 4.0)**2) / 14.0))
    hills = hill1 + hill2 + hill3

    undulations = 1.6 * fbm_2d(X * 0.2, Y * 0.2, octaves=5, persistence=0.45, seed=777)
    gentle_ridges = 0.7 * ridged_fbm_2d(X * 0.25, Y * 0.25, octaves=3, seed=321)

    Z = (hills + undulations + gentle_ridges) * falloff
    return np.maximum(Z, 0.0)

# ==============================================================================
# GEOLOGICAL SHADING & COLOR SYNTHESIS
# ==============================================================================
def calc_alpine_colors(X, Y, Z, Nz):
    N = len(Z)
    colors = np.zeros((N, 4), dtype=np.float32)
    colors[:, 3] = 1.0

    n_rock = fbm_2d(X * 0.8, Y * 0.8, octaves=4, seed=10)
    
    c_granite_dark = np.array([0.14, 0.15, 0.17])
    c_granite_mid  = np.array([0.30, 0.32, 0.35])
    c_scree        = np.array([0.25, 0.23, 0.21])
    c_snow         = np.array([0.97, 0.98, 1.00])
    c_ice_blue     = np.array([0.75, 0.86, 0.96])

    cliff_factor = np.clip((0.68 - Nz) / 0.22, 0.0, 1.0)[:, None]
    rock = c_granite_mid * (1.0 - cliff_factor) + c_granite_dark * cliff_factor

    scree_factor = np.clip((1.5 - Z) / 1.5, 0.0, 1.0)[:, None]
    rock = rock * (1.0 - scree_factor) + c_scree * scree_factor

    snow_elev = np.clip((Z - 2.6 + 0.5 * n_rock) / 1.5, 0.0, 1.0)
    snow_slope = np.clip((Nz - 0.45) / 0.20, 0.0, 1.0)
    snow_factor = (snow_elev * snow_slope)[:, None]

    snow_tint = c_snow * 0.88 + c_ice_blue * 0.12
    final_rgb = rock * (1.0 - snow_factor) + snow_tint * snow_factor
    colors[:, :3] = np.clip(final_rgb, 0.0, 1.0)
    return colors

def calc_volcano_colors(X, Y, Z, Nz, R):
    N = len(Z)
    colors = np.zeros((N, 4), dtype=np.float32)
    colors[:, 3] = 1.0

    n_rock = fbm_2d(X * 0.7, Y * 0.7, octaves=3, seed=20)
    
    c_basalt_dark = np.array([0.08, 0.08, 0.09])
    c_basalt_mid  = np.array([0.18, 0.17, 0.19])
    c_tephra_red  = np.array([0.48, 0.18, 0.12])
    c_sulfur_rim  = np.array([0.92, 0.78, 0.15])
    c_lava_glow   = np.array([1.00, 0.26, 0.02])

    t_cone = np.clip((Z - 0.5) / 4.5 + 0.15 * n_rock, 0.0, 1.0)[:, None]
    body_color = c_basalt_dark * t_cone + c_basalt_mid * (1.0 - t_cone)

    gully_factor = np.clip(np.sin(np.arctan2(Y, X) * 8.0), 0.0, 1.0)
    tephra_mask = (gully_factor * np.clip((Z - 1.0) / 3.2, 0.0, 1.0))[:, None]
    body_color = body_color * (1.0 - 0.7 * tephra_mask) + c_tephra_red * (0.7 * tephra_mask)

    crater_mask = (R < 1.15) & (Z < 2.9)
    sulfur_mask = (R >= 1.15) & (R < 1.75) & (Z > 3.0)

    lava_veins = (value_noise_2d(X * 3.8, Y * 3.8, seed=77) > 0.0).astype(float)[:, None]
    crater_lava = c_basalt_dark * (1.0 - lava_veins) + c_lava_glow * lava_veins

    c_mask = crater_mask[:, None]
    s_mask = sulfur_mask[:, None]

    final_rgb = body_color * (~c_mask & ~s_mask) + crater_lava * c_mask + c_sulfur_rim * s_mask
    colors[:, :3] = np.clip(final_rgb, 0.0, 1.0)
    return colors

def calc_mesa_colors(X, Y, Z, Nz):
    N = len(Z)
    colors = np.zeros((N, 4), dtype=np.float32)
    colors[:, 3] = 1.0

    n_layer = fbm_2d(X * 0.5, Y * 0.5, octaves=3, seed=33)
    strata = np.mod(Z + 0.12 * n_layer, 1.20) / 1.20

    c_red   = np.array([0.76, 0.24, 0.15])
    c_terra = np.array([0.86, 0.44, 0.24])
    c_buff  = np.array([0.90, 0.78, 0.58])
    c_shale = np.array([0.40, 0.16, 0.10])
    c_ochre = np.array([0.80, 0.58, 0.34])

    cliff_color = np.zeros((N, 3), dtype=np.float32)
    for idx, s in enumerate(strata):
        if s < 0.25:
            t = s / 0.25
            cliff_color[idx] = c_red * (1.0 - t) + c_terra * t
        elif s < 0.50:
            t = (s - 0.25) / 0.25
            cliff_color[idx] = c_terra * (1.0 - t) + c_buff * t
        elif s < 0.75:
            t = (s - 0.50) / 0.25
            cliff_color[idx] = c_buff * (1.0 - t) + c_shale * t
        else:
            t = (s - 0.75) / 0.25
            cliff_color[idx] = c_shale * (1.0 - t) + c_ochre * t

    top_soil = np.array([0.68, 0.56, 0.42])
    plateau_mask = np.clip((Z - 4.5) / 0.25, 0.0, 1.0) * np.clip((Nz - 0.82) / 0.15, 0.0, 1.0)
    p_mask = plateau_mask[:, None]

    final_rgb = cliff_color * (1.0 - p_mask) + top_soil * p_mask
    colors[:, :3] = np.clip(final_rgb, 0.0, 1.0)
    return colors

def calc_highlands_colors(X, Y, Z, Nz):
    N = len(Z)
    colors = np.zeros((N, 4), dtype=np.float32)
    colors[:, 3] = 1.0

    n_grass = fbm_2d(X * 0.7, Y * 0.7, octaves=3, seed=44)
    
    grass_valley = np.array([0.16, 0.30, 0.10])
    grass_slope  = np.array([0.32, 0.52, 0.18])
    grass_ridge  = np.array([0.52, 0.60, 0.24])
    peat_rock    = np.array([0.35, 0.32, 0.26])

    t_elev = np.clip((Z - 0.5) / 3.2 + 0.15 * n_grass, 0.0, 1.0)[:, None]
    turf = grass_valley * (1.0 - t_elev) + (grass_slope * 0.55 + grass_ridge * 0.45) * t_elev

    rock_mask = np.clip((0.70 - Nz) / 0.25, 0.0, 1.0)[:, None]
    final_rgb = turf * (1.0 - rock_mask) + peat_rock * rock_mask

    colors[:, :3] = np.clip(final_rgb, 0.0, 1.0)
    return colors

# ==============================================================================
# MESH & MATERIAL CREATION
# ==============================================================================
def create_mountain_material(mat_name, is_volcano=False):
    mat = bpy.data.materials.new(mat_name)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    attr = nodes.new('ShaderNodeAttribute')
    attr.attribute_name = 'Color'

    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    links.new(attr.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85

    if is_volcano:
        sep = nodes.new('ShaderNodeSeparateColor')
        links.new(attr.outputs['Color'], sep.inputs['Color'])

        comp_r = nodes.new('ShaderNodeMath')
        comp_r.operation = 'GREATER_THAN'
        comp_r.inputs[1].default_value = 0.90
        links.new(sep.outputs['Red'], comp_r.inputs[0])

        links.new(attr.outputs['Color'], bsdf.inputs['Emission Color'])

        mult = nodes.new('ShaderNodeMath')
        mult.operation = 'MULTIPLY'
        mult.inputs[1].default_value = 14.0
        links.new(comp_r.outputs['Value'], mult.inputs[0])
        links.new(mult.outputs['Value'], bsdf.inputs['Emission Strength'])

    return mat

def build_mountain(name, height_func, color_func, location, is_volcano=False, res=180, size=20.0):
    print(f'Building {name}...')
    step = size / (res - 1)
    lin = np.linspace(-size/2, size/2, res)
    X_grid, Y_grid = np.meshgrid(lin, lin)
    R_grid = np.sqrt(X_grid**2 + Y_grid**2)

    rho = R_grid / 9.2
    falloff_raw = np.clip(1.0 - rho, 0.0, 1.0)
    falloff = falloff_raw * falloff_raw * (3.0 - 2.0 * falloff_raw)

    Z_grid = height_func(X_grid, Y_grid, R_grid, falloff)
    border_mask = (R_grid > 9.2)
    Z_grid[border_mask] = -0.05

    # Compute Normals
    dz_dx = np.zeros_like(Z_grid)
    dz_dy = np.zeros_like(Z_grid)

    dz_dx[:, 1:-1] = (Z_grid[:, 2:] - Z_grid[:, :-2]) / (2.0 * step)
    dz_dx[:, 0]    = (Z_grid[:, 1] - Z_grid[:, 0]) / step
    dz_dx[:, -1]   = (Z_grid[:, -1] - Z_grid[:, -2]) / step

    dz_dy[1:-1, :] = (Z_grid[2:, :] - Z_grid[:-2, :]) / (2.0 * step)
    dz_dy[0, :]    = (Z_grid[1, :] - Z_grid[0, :]) / step
    dz_dy[-1, :]   = (Z_grid[-1, :] - Z_grid[-2, :]) / step

    norm = np.sqrt(dz_dx**2 + dz_dy**2 + 1.0)
    Nz_grid = 1.0 / norm

    # Flatten
    X_flat = X_grid.flatten()
    Y_flat = Y_grid.flatten()
    Z_flat = Z_grid.flatten()
    Nz_flat = Nz_grid.flatten()
    R_flat = R_grid.flatten()

    if is_volcano:
        colors = color_func(X_flat, Y_flat, Z_flat, Nz_flat, R_flat)
    else:
        colors = color_func(X_flat, Y_flat, Z_flat, Nz_flat)

    edge_idx = np.where(R_flat > 9.2)[0]
    colors[edge_idx, :3] = np.array([0.05, 0.05, 0.06])

    verts = [ (float(x), float(y), float(z)) for x, y, z in zip(X_flat, Y_flat, Z_flat) ]

    faces = []
    for r in range(res - 1):
        for c in range(res - 1):
            v0 = r * res + c
            v1 = r * res + (c + 1)
            v2 = (r + 1) * res + (c + 1)
            v3 = (r + 1) * res + c
            faces.append((v0, v1, v2, v3))

    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    mesh.polygons.foreach_set('use_smooth', [True] * len(mesh.polygons))

    color_attr = mesh.color_attributes.new(name='Color', type='FLOAT_COLOR', domain='POINT')
    color_attr.data.foreach_set('color', colors.flatten())

    obj = bpy.data.objects.new(name, mesh)
    obj.location = (location[0], location[1], location[2] + 0.02)
    bpy.context.scene.collection.objects.link(obj)

    mat = create_mountain_material('MAT_' + name, is_volcano=is_volcano)
    obj.data.materials.append(mat)

    return obj

# ==============================================================================
# PEDESTALS, PLAQUES & LABELS
# ==============================================================================
def create_square_plinth(name, location, size=21.4, height=0.6):
    half = size / 2.0
    z_bot = -height
    z_top = 0.0
    verts = [
        (-half, -half, z_bot), (half, -half, z_bot), (half, half, z_bot), (-half, half, z_bot),
        (-half, -half, z_top), (half, -half, z_top), (half, half, z_top), (-half, half, z_top)
    ]
    faces = [
        (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)
    ]
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    bpy.context.scene.collection.objects.link(obj)
    return obj

def create_nameplate_plaque(name, location, width=17.0, depth=1.6, height=0.35):
    half_w = width / 2.0
    half_d = depth / 2.0
    verts = [
        (-half_w, -half_d, 0.0), (half_w, -half_d, 0.0), (half_w, half_d, 0.0), (-half_w, half_d, 0.0),
        (-half_w, -half_d, height), (half_w, -half_d, height), (half_w, half_d, height), (-half_w, half_d, height)
    ]
    faces = [
        (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)
    ]
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.rotation_euler = (math.radians(35), 0, 0)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def create_pedestal_material():
    mat = bpy.data.materials.new('MAT_Pedestal')
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (0.05, 0.05, 0.06, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.35
    bsdf.inputs['Metallic'].default_value = 0.15
    return mat

def create_plaque_material():
    mat = bpy.data.materials.new('MAT_Plaque')
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (0.08, 0.08, 0.09, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.25
    bsdf.inputs['Metallic'].default_value = 0.60
    return mat

def create_gold_material():
    mat = bpy.data.materials.new('MAT_Gold_Label')
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (0.95, 0.86, 0.60, 1.0)
    bsdf.inputs['Metallic'].default_value = 0.90
    bsdf.inputs['Roughness'].default_value = 0.20
    return mat

def create_text_label(text, location, rotation=(math.radians(35), 0, 0), size=0.82):
    curve = bpy.data.curves.new('LabelCurve_' + text[:8], type='FONT')
    curve.body = text
    curve.size = size
    curve.extrude = 0.05
    curve.align_x = 'CENTER'
    obj = bpy.data.objects.new('Text_' + text[:8], curve)
    obj.location = location
    obj.rotation_euler = rotation
    bpy.context.scene.collection.objects.link(obj)
    return obj

def create_studio_floor():
    verts = [(-90, -90, -0.65), (90, -90, -0.65), (90, 90, -0.65), (-90, 90, -0.65)]
    faces = [(0, 1, 2, 3)]
    mesh = bpy.data.meshes.new('StudioFloor_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('StudioFloor', mesh)
    bpy.context.scene.collection.objects.link(obj)

    mat = bpy.data.materials.new('MAT_StudioFloor')
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (0.03, 0.032, 0.038, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.75
    obj.data.materials.append(mat)
    return obj

# ==============================================================================
# CAMERAS & LIGHTING
# ==============================================================================
def create_camera(name, location, target, lens=38):
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam_data.clip_end = 500
    cam_obj = bpy.data.objects.new(name, cam_data)
    cam_obj.location = location
    bpy.context.scene.collection.objects.link(cam_obj)

    empty = bpy.data.objects.new(name + '_Target', None)
    empty.location = target
    bpy.context.scene.collection.objects.link(empty)

    constraint = cam_obj.constraints.new(type='TRACK_TO')
    constraint.target = empty
    constraint.track_axis = 'TRACK_NEGATIVE_Z'
    constraint.up_axis = 'UP_Y'
    return cam_obj

def setup_lighting():
    world = bpy.data.worlds.new('MountainWorld')
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.04, 0.05, 0.09, 1.0)
    bg.inputs['Strength'].default_value = 0.7

    sun_data = bpy.data.lights.new('SunKey', type='SUN')
    sun_data.energy = 4.5
    sun_data.color = (1.0, 0.94, 0.86)
    sun_obj = bpy.data.objects.new('SunKey', sun_data)
    sun_obj.rotation_euler = (math.radians(50), math.radians(20), math.radians(-38))
    bpy.context.scene.collection.objects.link(sun_obj)

    fill_data = bpy.data.lights.new('SkyFill', type='SUN')
    fill_data.energy = 1.5
    fill_data.color = (0.55, 0.72, 0.95)
    fill_obj = bpy.data.objects.new('SkyFill', fill_data)
    fill_obj.rotation_euler = (math.radians(72), math.radians(-15), math.radians(140))
    bpy.context.scene.collection.objects.link(fill_obj)

# ==============================================================================
# MAIN ORCHESTRATION
# ==============================================================================
def main():
    print('=== EXECUTING REFINED MOUNTAIN GENERATION ===')
    create_studio_floor()
    setup_lighting()

    ped_mat = create_pedestal_material()
    plaque_mat = create_plaque_material()
    gold_mat = create_gold_material()

    configs = [
        {
            'name': 'Alpine_Mountain',
            'label': '1. KARLI ALP DAĞI',
            'height_func': create_alpine_heights,
            'color_func': calc_alpine_colors,
            'location': (-13.5, 13.5, 0.0),
            'is_volcano': False,
            # Camera angled from South-West looking up at the summit
            'cam_loc': (-3.0, 3.0, 6.0),
            'cam_target': (-13.5, 13.5, 3.8),
            'cam_lens': 45
        },
        {
            'name': 'Volcano_Mountain',
            'is_volcano': True,
            'label': '2. STRATOVOLKAN VE KRATER',
            'height_func': create_volcano_heights,
            'color_func': calc_volcano_colors,
            'location': (13.5, 13.5, 0.0),
            # High-angle camera looking directly down into the crater
            'cam_loc': (3.0, 3.0, 16.0),
            'cam_target': (13.5, 13.5, 3.0),
            'cam_lens': 42
        },
        {
            'name': 'Desert_Mesa',
            'label': '3. KANYON MASA DAĞI',
            'height_func': create_mesa_heights,
            'color_func': calc_mesa_colors,
            'location': (-13.5, -13.5, 0.0),
            # Oblique angle looking at the cliff terraces and flat top
            'cam_loc': (-3.0, -3.0, 7.5),
            'cam_target': (-13.5, -13.5, 3.0),
            'cam_lens': 42
        },
        {
            'name': 'Rolling_Highlands',
            'label': '4. YEŞİL YAYLA TEPELERİ',
            'height_func': create_highlands_heights,
            'color_func': calc_highlands_colors,
            'location': (13.5, -13.5, 0.0),
            # Rolling perspective across the green hills
            'cam_loc': (3.0, -3.0, 6.5),
            'cam_target': (13.5, -13.5, 2.0),
            'cam_lens': 42
        }
    ]

    mountain_objs = []
    close_up_cameras = []

    for cfg in configs:
        loc = cfg['location']
        obj = build_mountain(
            cfg['name'],
            cfg['height_func'],
            cfg['color_func'],
            loc,
            is_volcano=cfg.get('is_volcano', False),
            res=180,
            size=20.0
        )
        mountain_objs.append(obj)

        ped = create_square_plinth('Plinth_' + cfg['name'], loc, size=21.4, height=0.6)
        ped.data.materials.append(ped_mat)

        plaque_loc = (loc[0], loc[1] - 11.4, 0.0)
        plaque = create_nameplate_plaque('Plaque_' + cfg['name'], plaque_loc, width=17.0, depth=1.6, height=0.35)
        plaque.data.materials.append(plaque_mat)

        text_loc = (loc[0], loc[1] - 11.4, 0.38)
        lbl = create_text_label(cfg['label'], text_loc, rotation=(math.radians(35), 0, 0), size=0.82)
        lbl.data.materials.append(gold_mat)

        cam = create_camera(
            'Camera_' + cfg['name'],
            cfg['cam_loc'],
            cfg['cam_target'],
            lens=cfg['cam_lens']
        )
        close_up_cameras.append((cam, cfg['name']))

    # Main Showcase Overview Camera
    showcase_cam = create_camera(
        'Camera_Showcase',
        location=(0.0, -52.0, 38.0),
        target=(0.0, -4.0, 1.0),
        lens=33
    )

    # Export individual mountain models
    print('Exporting mountain models to OBJ & GLB...')
    for obj in mountain_objs:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

        obj_path = os.path.join(EXPORTS_DIR, f'{obj.name}.obj')
        glb_path = os.path.join(EXPORTS_DIR, f'{obj.name}.glb')

        try:
            bpy.ops.wm.obj_export(filepath=obj_path, export_selected_objects=True)
            print(f'Exported OBJ: {obj_path}')
        except Exception as e:
            print(f'OBJ export error for {obj.name}: {e}')

        try:
            bpy.ops.export_scene.gltf(filepath=glb_path, use_selection=True)
            print(f'Exported GLB: {glb_path}')
        except Exception as e:
            print(f'GLTF export error for {obj.name}: {e}')

    # Save complete .blend file
    blend_path = os.path.join(BASE_DIR, 'mountains_showcase.blend')
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f'Blender scene saved: {blend_path}')

    # Render Preview Images
    print('Rendering preview images...')
    scene = bpy.context.scene
    scene.camera = showcase_cam
    showcase_img = os.path.join(BASE_DIR, 'mountains_showcase_preview.png')
    scene.render.filepath = showcase_img
    bpy.ops.render.render(write_still=True)
    print(f'Rendered showcase image: {showcase_img}')

    for cam, m_name in close_up_cameras:
        scene.camera = cam
        img_path = os.path.join(BASE_DIR, f'{m_name.lower()}_preview.png')
        scene.render.filepath = img_path
        bpy.ops.render.render(write_still=True)
        print(f'Rendered close-up: {img_path}')

    print('=== ALL MOUNTAIN TASKS COMPLETED SUCCESSFULLY ===')

if __name__ == '__main__':
    main()


