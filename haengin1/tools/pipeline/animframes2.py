# Blender batch: render frames of the imported GLB animation from the side and 3/4 (workbench).
# usage: blender -b --python animframes.py -- <in.glb> <outdir> <tag> <n_frames>
import bpy, sys, math, os
argv = sys.argv[sys.argv.index('--') + 1:]
src, outdir, tag, n = argv[0], argv[1], argv[2], int(argv[3])
views = argv[4].split(',') if len(argv) > 4 else ['side', 'q']
span = float(argv[5]) if len(argv) > 5 else 0.5
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
act = arm.animation_data.action if arm.animation_data else None
f0, f1 = (int(act.frame_range[0]), int(act.frame_range[1])) if act else (1, 1)
print('action', act and act.name, 'frames', f0, f1, flush=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x = 360; scene.render.resolution_y = 560
cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 2.6
cam = bpy.data.objects.new('cam', cam_data); scene.collection.objects.link(cam); scene.camera = cam
for k in range(n):
    fr = f0 + round((f1 - f0) * k / max(1, n - 1) * span)   # first half of the clip = one stride
    scene.frame_set(fr)
    for vname, ang in [(v, {'front': 0.0, 'side': math.pi / 2, 'q': math.pi / 4}[v]) for v in views]:
        d = 5.0
        # follow the hips so root motion does not leave the frame
        hips = arm.pose.bones.get('Hips')
        hx, hy = (arm.matrix_world @ hips.head).x, (arm.matrix_world @ hips.head).y
        cam.location = (hx + math.sin(ang) * d, hy - math.cos(ang) * d, 0.95)
        cam.rotation_euler = (math.pi / 2, 0, ang)
        scene.render.filepath = os.path.join(outdir, f'{tag}_{vname}_{k}.png')
        bpy.ops.render.render(write_still=True)
print('DONE', flush=True)
