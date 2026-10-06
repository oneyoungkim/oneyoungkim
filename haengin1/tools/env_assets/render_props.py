# Blender batch: preview renders of normalized props (Workbench, texture color, studio light, object outline).
# For every GLB: <outdir>/<stem>_34.png (front-left three-quarter, like the ref image) and <stem>_front.png (straight front, glTF +Z).
# A 1.74 m grey stand-in (player height) is drawn beside each prop for scale.
# usage: blender -b --python render_props.py -- <outdir> <a.glb> [<b.glb> ...]
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
outdir, files = argv[0], argv[1:]
os.makedirs(outdir, exist_ok=True)

def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.display.shading.show_object_outline = True
    sc.display.shading.object_outline_color = (0.10, 0.08, 0.09)
    sc.display.shading.show_cavity = False
    sc.display.shading.background_type = 'VIEWPORT'
    sc.display.shading.background_color = (0.957, 0.937, 0.902)
    sc.render.resolution_x = sc.render.resolution_y = 640
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'
    return sc

for f in files:
    sc = setup()
    bpy.ops.import_scene.gltf(filepath=f)
    objs = [o for o in sc.objects if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    # player-height stand-in
    bpy.ops.mesh.primitive_cylinder_add(radius=0.18, depth=1.74, location=(mx.x + 0.45, 0, 0.87))
    ref = bpy.context.active_object
    m = bpy.data.materials.new('ref'); m.diffuse_color = (0.55, 0.55, 0.58, 1); ref.data.materials.append(m)
    mx.x += 0.65; mx.z = max(mx.z, 1.74)
    ctr = (mn + mx) / 2
    rad = max((mx - mn).length / 2, 0.5)
    cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 50
    cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
    stem = os.path.splitext(os.path.basename(f))[0]
    for tag, az, el in (('34', -35.0, 20.0), ('front', 0.0, 8.0)):
        a, e = math.radians(az), math.radians(el)
        dist = rad / math.tan(math.radians(36) / 2) * 1.15
        # front is Blender -Y; azimuth negative = toward the prop's left (viewer's left)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        cam.location = ctr + d * dist
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(outdir, f'{stem}_{tag}.png')
        bpy.ops.render.render(write_still=True)
    print('RENDERED', stem, flush=True)
