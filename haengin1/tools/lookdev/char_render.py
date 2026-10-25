# 캐릭터 텍스처 단순화 전·후 렌더(docs/14 5장) — Blender 백그라운드, 크레딧 0
# 사용: blender -b --factory-startup --python tools/lookdev/char_render.py -- <Siwoo.fbx> <Tex.jpg> <Shade.jpg> <Face.png> <출력.png> [프레임=60]
# 셀 흉내(UTS 와 같은 규칙): 법선·빛 방향 내적 > 0.08 이면 고유색(Tex), 아니면 1단 그늘(Shade) — 둘 다 Emission(조명 영향 없음)
#   + 뒤집은 껍데기 외곽선(Solidify 4mm, #1A1417, 뒷면만) · 얼굴 = Face_LOD0 에 표정 아틀라스 (0,0) 칸(FaceRig.CellST 와 같은 식)
#   카메라 = 정면 오른쪽 25° 직교, 배경 투명(합칠 때 종이색) · 표준 색 관리(값 그대로)
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
fbx, tex, shade, face_png, dst = argv[:5]
frame = int(argv[5]) if len(argv) > 5 else 60
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ('Body_LOD0', 'Face_LOD0'):
        bpy.data.objects.remove(o, do_unlink=True)
body = bpy.data.objects['Body_LOD0']; face = bpy.data.objects['Face_LOD0']
wscale = body.matrix_world.to_scale()[0]   # FBX 임포트: 아마추어 배율 0.01(메시 자료는 cm) — 배율 적용 대신 두께를 자료 단위로
sc = bpy.context.scene
sc.frame_set(frame)

LDIR = Vector((-0.45, -0.75, 0.55)).normalized()   # 빛: 앞·왼쪽 위(캐릭터 기준 오른쪽 어깨 쪽 그늘)


def img(path, cs='sRGB'):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = cs
    return im


def toon_mat(name, base_path, shade_path):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; ns = nt.nodes; ln = nt.links
    for n in list(ns):
        ns.remove(n)
    out = ns.new('ShaderNodeOutputMaterial')
    em = ns.new('ShaderNodeEmission')
    tb = ns.new('ShaderNodeTexImage'); tb.image = img(base_path)
    ts = ns.new('ShaderNodeTexImage'); ts.image = img(shade_path)
    geo = ns.new('ShaderNodeNewGeometry')
    dot = ns.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; dot.inputs[1].default_value = LDIR
    gt = ns.new('ShaderNodeMath'); gt.operation = 'GREATER_THAN'; gt.inputs[1].default_value = 0.08
    mix = ns.new('ShaderNodeMix'); mix.data_type = 'RGBA'
    ln.new(geo.outputs['Normal'], dot.inputs[0])
    ln.new(dot.outputs['Value'], gt.inputs[0])
    ln.new(gt.outputs[0], mix.inputs['Factor'])
    ln.new(ts.outputs['Color'], mix.inputs['A'])
    ln.new(tb.outputs['Color'], mix.inputs['B'])
    ln.new(mix.outputs['Result'], em.inputs['Color'])
    ln.new(em.outputs[0], out.inputs['Surface'])
    return m


def face_mat(path):
    m = bpy.data.materials.new('FaceMat'); m.use_nodes = True
    nt = m.node_tree; ns = nt.nodes; ln = nt.links
    for n in list(ns):
        ns.remove(n)
    out = ns.new('ShaderNodeOutputMaterial')
    uv = ns.new('ShaderNodeTexCoord')
    mp = ns.new('ShaderNodeMapping'); s = 409 / 2048
    mp.inputs['Scale'].default_value = (s, s, 1); mp.inputs['Location'].default_value = (0, 1 - s, 0)
    t = ns.new('ShaderNodeTexImage'); t.image = img(path); t.extension = 'CLIP'
    em = ns.new('ShaderNodeEmission'); tr = ns.new('ShaderNodeBsdfTransparent'); mx = ns.new('ShaderNodeMixShader')
    ln.new(uv.outputs['UV'], mp.inputs['Vector']); ln.new(mp.outputs[0], t.inputs['Vector'])
    ln.new(t.outputs['Color'], em.inputs['Color'])
    ln.new(t.outputs['Alpha'], mx.inputs[0]); ln.new(tr.outputs[0], mx.inputs[1]); ln.new(em.outputs[0], mx.inputs[2])
    ln.new(mx.outputs[0], out.inputs['Surface'])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m


def outline_mat():
    m = bpy.data.materials.new('Outline'); m.use_nodes = True
    nt = m.node_tree; ns = nt.nodes
    em = ns.new('ShaderNodeEmission'); em.inputs['Color'].default_value = (0x1A / 255, 0x14 / 255, 0x17 / 255, 1)
    em.inputs['Color'].default_value = tuple(((c / 1.0) ** 2.2) for c in (0x1A / 255, 0x14 / 255, 0x17 / 255)) + (1,)
    out = [n for n in ns if n.type == 'OUTPUT_MATERIAL'][0]
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    m.use_backface_culling = True
    return m


body.data.materials.clear(); body.data.materials.append(toon_mat('Body', tex, shade))
ol = outline_mat(); body.data.materials.append(ol)
sol = body.modifiers.new('Outline', 'SOLIDIFY'); sol.thickness = 0.004 / wscale; sol.offset = 1.0; sol.use_flip_normals = True
sol.material_offset = 1; sol.use_rim = False
face.data.materials.clear(); face.data.materials.append(face_mat(face_png))

cam_d = bpy.data.cameras.new('Cam'); cam_d.type = 'ORTHO'; cam_d.ortho_scale = 1.95
cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
tgt = Vector((0, 0, 0.88)); yaw = math.radians(25)
cam.location = tgt + Vector((math.sin(yaw) * 4, -math.cos(yaw) * 4, 0.25))
cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
    try:
        sc.render.engine = eng; break
    except Exception:
        continue
sc.render.resolution_x, sc.render.resolution_y = 760, 1200
sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
sc.render.filepath = dst
bpy.ops.render.render(write_still=True)
print('RENDERED', dst)
