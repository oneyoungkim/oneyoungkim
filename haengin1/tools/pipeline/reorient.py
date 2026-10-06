# Blender batch: rotate an unrigged Tripo GLB so the character faces glTF +Z (standard front), feet on the ground.
# Tripo output faces glTF +X, which is Blender +X; turning -90 deg about Blender Z maps +X to -Y (= glTF +Z).
# usage: blender -b --python reorient.py -- <in.glb> <out.glb> <height_m>
import bpy, sys, math
from mathutils import Matrix
argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, height = argv[0], argv[1], float(argv[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
# bake parents/transforms into the meshes first
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
R = Matrix.Rotation(-math.pi / 2, 4, 'Z')
for m in meshes:
    m.data.transform(R)
    m.data.update()
# scale to height and stand on z=0, centered in x/y
zs = [ (m.matrix_world @ v.co) for m in meshes for v in m.data.vertices ]
minz = min(p.z for p in zs); maxz = max(p.z for p in zs)
cx = (min(p.x for p in zs) + max(p.x for p in zs)) / 2; cy = (min(p.y for p in zs) + max(p.y for p in zs)) / 2
s = height / (maxz - minz)
T = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -minz))
for m in meshes:
    m.data.transform(T); m.data.update()
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=False)
xs = [ (m.matrix_world @ v.co) for m in meshes for v in m.data.vertices ]
print('DIMS x %.3f y %.3f z %.3f' % (max(p.x for p in xs)-min(p.x for p in xs), max(p.y for p in xs)-min(p.y for p in xs), max(p.z for p in xs)-min(p.z for p in xs)), flush=True)
print('EXPORTED', dst, flush=True)
