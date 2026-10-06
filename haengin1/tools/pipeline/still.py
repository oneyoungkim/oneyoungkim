import bpy, sys, math, os
argv = sys.argv[sys.argv.index('--') + 1:]
src, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 700; sc.render.resolution_y = 700
cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cd.ortho_scale = 2.3
cam = bpy.data.objects.new('c', cd); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (0, -4, 0.92); cam.rotation_euler = (math.pi / 2, 0, 0)
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
