# Blender batch: turn a raw image-to-3D GLB into a game prop.
#   front = glTF +Z (Blender -Y), bottom on z = 0, centered on x/y, real size in meters, faces <= limit,
#   textures <= tex px, matte material (metallic 0, roughness 1), one mesh object named after the prop.
# usage: blender -b --python normalize_prop.py -- <in.glb> <out.glb> <name> <model> <w> <d> <h> <face_limit> [yaw_deg] [tex_px] [fit] [ref.png]
#   ref.png: match the texture palette to the reference picture (Lab mean/std transfer, strength .85)
#   model: tripo | tripo_d | hunyuan | hunyuan_lp | meshy | meshy_lp  (sets the base turn, see BASE_YAW)
#   yaw_deg: extra turn about up after the base turn (fix per prop after looking at the preview)
#   fit: h (scale by height, default) | w | d | max
import bpy, sys, math, json, bmesh
from mathutils import Matrix, Vector

# Front direction of each generator's output, measured 2026-10-06 on the comparison props.
# Tripo faces glTF +X (= Blender +X) -> -90 deg about Z brings it to Blender -Y (= glTF +Z).
BASE_YAW = {'tripo': -90.0, 'tripo_d': -90.0, 'hunyuan': 0.0, 'hunyuan_lp': 0.0, 'meshy': 0.0, 'meshy_lp': 0.0}

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, name, model = argv[0], argv[1], argv[2], argv[3]
W, D, H = float(argv[4]), float(argv[5]), float(argv[6])
limit = int(argv[7])
yaw = float(argv[8]) if len(argv) > 8 else 0.0
tex_px = int(argv[9]) if len(argv) > 9 else 1024
fit = argv[10] if len(argv) > 10 else 'h'

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
for o in list(bpy.context.scene.objects):
    if o != obj:
        bpy.data.objects.remove(o, do_unlink=True)
obj.name = name
obj.data.name = name

R = Matrix.Rotation(math.radians(BASE_YAW.get(model, 0.0) + yaw), 4, 'Z')
obj.data.transform(R)

faces0 = len(obj.data.polygons)
# merge duplicate verts first (some generators split every triangle), then decimate to the limit
bm = bmesh.new(); bm.from_mesh(obj.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.to_mesh(obj.data); bm.free()
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
if tris > limit:
    mod = obj.modifiers.new('dec', 'DECIMATE')
    mod.ratio = max(0.01, limit / tris * 0.98)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier='dec')

vs = [v.co for v in obj.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
size = mx - mn  # Blender: x = width, y = depth, z = height
want = {'h': H / size.z, 'w': W / size.x, 'd': D / size.y}
c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
if fit == 'xyz':   # each axis to its own target (squat or stretched generator output, e.g. trucks)
    S = Matrix.Diagonal((W / size.x, D / size.y, H / size.z, 1.0))
else:              # uniform: h | w | d, or max = smallest ratio (no dimension exceeds its target)
    s = want[fit] if fit in want else min(want.values())
    S = Matrix.Scale(s, 4)
obj.data.transform(S @ Matrix.Translation(-c))
obj.data.update()
for p in obj.data.polygons:
    p.use_smooth = False   # flat normals read better under cel shading; Unity can re-smooth by angle if wanted

# materials: matte, textures capped
for mat in obj.data.materials:
    if not mat or not mat.use_nodes:
        continue
    mat.name = 'M_Prop_' + name
    for nd in mat.node_tree.nodes:
        if nd.type == 'BSDF_PRINCIPLED':
            nd.inputs['Metallic'].default_value = 0.0
            nd.inputs['Roughness'].default_value = 1.0
            for k in ('Metallic', 'Roughness'):
                for l in list(nd.inputs[k].links):
                    mat.node_tree.links.remove(l)
        if nd.type == 'TEX_IMAGE' and nd.image:
            im = nd.image
            if max(im.size) > tex_px:
                im.scale(tex_px, tex_px)
                im.pack()


# palette match: generators (Hunyuan most) bake darker, more saturated albedo than the reference picture.
# Reinhard transfer in Lab (mean/std per channel) from the texture's used pixels to the reference's object pixels.
ref = argv[11] if len(argv) > 11 else ''
recol = None
if ref:
    import numpy as np
    def s2l(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    def l2s(c): c = np.clip(c, 0, None); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]); Mi = np.linalg.inv(M)
    WP = np.array([0.9505, 1.0, 1.089])
    def f(t): return np.where(t > 0.008856, np.cbrt(t), 7.787 * t + 16 / 116)
    def fi(t): return np.where(t > 0.2069, t ** 3, (t - 16 / 116) / 7.787)
    def to_lab(rgb):
        xyz = s2l(rgb) @ M.T / WP; fx, fy, fz = f(xyz[:, 0]), f(xyz[:, 1]), f(xyz[:, 2])
        return np.stack([116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)], 1)
    def from_lab(lab):
        fy = (lab[:, 0] + 16) / 116; fx = fy + lab[:, 1] / 500; fz = fy - lab[:, 2] / 200
        xyz = np.stack([fi(fx), fi(fy), fi(fz)], 1) * WP
        return np.clip(l2s(xyz @ Mi.T), 0, 1)
    rim = bpy.data.images.load(ref)
    rp = np.array(rim.pixels[:], dtype=np.float32).reshape(-1, 4)
    rp = rp[(rp[:, 3] > 0.5) & (rp[:, :3].min(1) < 0.92)][:, :3]
    if len(rp) > 200000:
        rp = rp[np.random.default_rng(0).choice(len(rp), 200000, replace=False)]
    rl = to_lab(rp); rmu, rsd = rl.mean(0), rl.std(0) + 1e-3
    done = set()
    for mat in obj.data.materials:
        for nd in (mat.node_tree.nodes if mat and mat.use_nodes else []):
            if nd.type == 'TEX_IMAGE' and nd.image and nd.image.name not in done:
                im = nd.image; done.add(im.name)
                px = np.array(im.pixels[:], dtype=np.float32).reshape(-1, 4)
                used = px[:, :3].max(1) > 0.03
                tl = to_lab(px[used, :3]); tmu, tsd = tl.mean(0), tl.std(0) + 1e-3
                k = np.clip(rsd / tsd, 0.6, 1.6)
                out = (tl - tmu) * k + rmu
                st = 0.85
                px[used, :3] = from_lab(tl * (1 - st) + out * st)
                im.pixels.foreach_set(px.ravel())
                im.pack()
                recol = {'L': [round(float(tmu[0]), 1), round(float(rmu[0]), 1)], 'ab_sat': [round(float(np.hypot(tmu[1], tmu[2])), 1), round(float(np.hypot(rmu[1], rmu[2])), 1)]}

bpy.ops.object.shade_flat()
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=False, use_selection=False)
vs = [v.co for v in obj.data.vertices]
dims = [max(v.x for v in vs) - min(v.x for v in vs), max(v.y for v in vs) - min(v.y for v in vs), max(v.z for v in vs) - min(v.z for v in vs)]
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
texs = sorted({tuple(nd.image.size) for m in obj.data.materials if m and m.use_nodes for nd in m.node_tree.nodes if nd.type == 'TEX_IMAGE' and nd.image})
print('NORM ' + json.dumps({'name': name, 'model': model, 'faces_in': faces0, 'tris': tris, 'w': round(dims[0], 3), 'd': round(dims[1], 3),
                            'h': round(dims[2], 3), 'want': [W, D, H], 'fit': fit, 'tex': texs, 'yaw': yaw, 'recolor_L_from_to': recol}), flush=True)
