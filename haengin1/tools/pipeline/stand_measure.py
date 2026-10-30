# Blender 배치: 리깅 GLB 의 메시 휴지 자세와 원본 클립(243)에서 서 있는 자세를 잰다(Unity StandSetup.Measure 와 같은 잣대, 2026-10-31)
# 앞 = -Y(glTF +Z), 위 = +Z. 머리 가운데 = 머리 뼈 위쪽 메시의 앞뒤 끝 가운데
# 사용: blender -b --factory-startup --python stand_measure.py -- <glb> <라벨> <out.tsv(이어 씀)>
import bpy, sys, math
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
glb, label, out = a[0], a[1], a[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
scene = bpy.context.scene
fr = (1, 1)
if arm.animation_data and arm.animation_data.action:
    fr = tuple(int(x) for x in arm.animation_data.action.frame_range)

def J(n, tail=False):
    b = arm.pose.bones[n]
    return arm.matrix_world @ (b.tail if tail else b.head)

def measure():
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    vs = [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    F = lambda p: -p.y    # 앞
    lua, rua, lul, rul = J('LeftArm'), J('RightArm'), J('LeftUpLeg'), J('RightUpLeg')
    sh = (lua + rua) / 2
    hip = (lul + rul) / 2
    head = J('Head')
    end = J('head_end') if 'head_end' in arm.pose.bones else head + Vector((0, 0, .22))
    ln = max(.12, (end - head).length)
    c = head + (end - head) * .45
    zs = [F(v) for v in vs if (v - c).length_squared <= ln * ln * .5 and v.z >= head.z + ln * .05]
    ear = (min(zs) + max(zs)) / 2 if zs else F(c)
    def ang(p, q, r):
        u, v = q - p, r - q
        return math.degrees(u.angle(v))
    down = (hip - sh).normalized()
    armL, armR = math.degrees((J('LeftForeArm') - lua).angle(down)), math.degrees((J('RightForeArm') - rua).angle(down))
    def front(v):   # 앞에서 본 각도(앞뒤 축 지움)
        return math.degrees(Vector((v.x, 0, v.z)).angle(Vector((down.x, 0, down.z))))
    armF = max(front(J('LeftForeArm') - lua), front(J('RightForeArm') - rua))
    kneeL, kneeR = ang(lul, J('LeftLeg'), J('LeftFoot')), ang(rul, J('RightLeg'), J('RightFoot'))
    upper = J('Spine')
    neck = J('neck')
    return dict(ear=(ear - F(sh)) * 100, earb=(F(head) - F(sh)) * 100, sh=(F(sh) - F(hip)) * 100, knee=max(kneeL, kneeR), arm=armF, arm3=max(armL, armR),
                dy=abs(lua.z - rua.z) * 100, prot=(F(sh) - F(upper)) * 100, bend=math.degrees((upper - hip).angle(neck - upper)))

rows = []
arm.data.pose_position = 'REST'
rows.append(('메시 휴지', measure()))
arm.data.pose_position = 'POSE'
worst = None
for i in range(8):
    scene.frame_set(fr[0] + (fr[1] - fr[0]) * i // 8)
    m = measure()
    worst = m if worst is None else {k: (m[k] if abs(m[k]) > abs(worst[k]) else worst[k]) for k in m}
rows.append(('Blender 243', worst))
with open(out, 'a', encoding='utf-8') as f:
    for n, m in rows:
        f.write(f"{label}\t{n}\t{m['ear']:.1f}\t{m['earb']:.1f}\t{m['sh']:.1f}\t{m['knee']:.1f}\t{m['arm']:.1f}\t{m['arm3']:.1f}\t{m['dy']:.1f}\t{m['prot']:.1f}\t{m['bend']:.1f}\n")
print('STAND', label, fr, flush=True)
