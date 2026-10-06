# Blender batch: pose a rigged GLB inside Blender and render front/side views (workbench), to judge skinning.
# usage: blender -b --python posetest.py -- <in.glb> <outdir> <tag>
import bpy, sys, math, os
from mathutils import Matrix, Vector
argv = sys.argv[sys.argv.index('--') + 1:]
src, outdir, tag = argv[0], argv[1], argv[2]
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
body = max((o for o in bpy.context.scene.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
print('bones', [b.name for b in arm.data.bones], flush=True)
if arm.animation_data:
    arm.animation_data.action = None
    for t in list(arm.animation_data.nla_tracks): arm.animation_data.nla_tracks.remove(t)
    print('cleared imported animation', flush=True)
mods = [m.type for m in body.modifiers]
print('body mods', mods, 'groups', len(body.vertex_groups), 'parent', body.parent and body.parent.name, flush=True)

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')

def rot_world(name, axis, ang):
    pb = arm.pose.bones.get(name)
    if not pb:
        print('missing bone', name); return
    bpy.context.view_layer.update()
    mw = arm.matrix_world
    head_w = mw @ pb.head
    R = Matrix.Translation(head_w) @ Matrix.Rotation(ang, 4, Vector(axis)) @ Matrix.Translation(-head_w)
    pb.matrix = mw.inverted() @ R @ mw @ pb.matrix
    bpy.context.view_layer.update()

def reset():
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

# world axes in Blender: +Z up. The model's front: find from the GLB (Tripo faces +X in glTF = -Y? we just render 4 sides)
# model faces Blender -Y (glTF +Z); character's left = +X; up = +Z
poses = {
    'rest': [],
    'armsdown': [('LeftArm', (0, 1, 0), 0.75), ('RightArm', (0, 1, 0), -0.75)],
    'guard': [('LeftArm', (0, 1, 0), 0.55), ('LeftArm', (1, 0, 0), 0.9), ('LeftForeArm', (1, 0, 0), 1.6),
              ('RightArm', (0, 1, 0), -0.55), ('RightArm', (1, 0, 0), 0.8), ('RightForeArm', (1, 0, 0), 1.8),
              ('LeftUpLeg', (1, 0, 0), 0.35), ('LeftLeg', (1, 0, 0), -0.5), ('Spine01', (0, 0, 1), 0.25)],
    'punch': [('RightArm', (0, 1, 0), -0.6), ('RightArm', (1, 0, 0), 1.45), ('Spine01', (0, 0, 1), -0.35),
              ('LeftArm', (0, 1, 0), 0.6), ('LeftArm', (1, 0, 0), 0.9), ('LeftForeArm', (1, 0, 0), 1.7)],
    'squat': [('LeftUpLeg', (1, 0, 0), 1.6), ('LeftLeg', (1, 0, 0), -2.2), ('RightUpLeg', (1, 0, 0), 1.6), ('RightLeg', (1, 0, 0), -2.2)],
}
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x = 600; scene.render.resolution_y = 900
cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 2.2
cam = bpy.data.objects.new('cam', cam_data); scene.collection.objects.link(cam); scene.camera = cam
bpy.ops.object.mode_set(mode='OBJECT')
for pname, ops in poses.items():
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    reset()
    for n, ax, a in ops:
        rot_world(n, ax, a)
    bpy.ops.object.mode_set(mode='OBJECT')
    for vname, ang in (('a', 0), ('b', math.pi / 2), ('c', math.pi / 4)):
        d = 4.0
        cam.location = (math.sin(ang) * d, -math.cos(ang) * d, 0.9)
        cam.rotation_euler = (math.pi / 2, 0, ang)
        scene.render.filepath = os.path.join(outdir, f'{tag}_{pname}_{vname}.png')
        bpy.ops.render.render(write_still=True)
print('DONE', flush=True)
