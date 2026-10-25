# 앉기 후보 비교(2026-10-25 — 반 친구가 책상에 엎드린 듯 보임): GLB 앉기 동작의 상체 숙임·머리 높이를 재고 옆·앞 그림을 뽑는다
# 사용: blender -b --factory-startup --python tools/pipeline/sit_metrics.py -- <out_dir> <a.glb> [b.glb …]
# 숫자(클립 전체 프레임):
#   lean  상체 앞 숙임(°, 골반→목 이 수직과 이루는 각) 평균·최대     head  골반 위 머리 높이(m) 평균·최소
#   fwd   머리가 골반보다 앞으로 나간 거리(m) 평균                  hand  손 높이(골반 기준, m) 평균 — 무릎·책상 위 손 자리
# 그림: <out>/<tag>_<view>_<k>.png (옆 side · 앞 front, 6 장씩)
import bpy, sys, os, math, json

argv = sys.argv[sys.argv.index('--') + 1:]
out = argv[0]
os.makedirs(out, exist_ok=True)
res = {}
for src in argv[1:]:
    tag = os.path.splitext(os.path.basename(src))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    sc = bpy.context.scene
    arm = next(o for o in sc.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    P = arm.pose.bones
    mw = arm.matrix_world

    def W(b):
        return mw @ P[b].head

    lean, head, fwd, hand = [], [], [], []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        hp, nk, hd = W('Hips'), W('neck') if 'neck' in P else W('Neck'), W('Head')
        v = nk - hp
        # 정면 = -Y(블렌더 glTF 가져오기: +Z 위, 앞 = -Y)
        lean.append(math.degrees(math.atan2(math.hypot(v.x, v.y), v.z)))
        head.append(hd.z - hp.z)
        fwd.append(-(hd.y - hp.y))
        hs = [W(n) for n in ('LeftHand', 'RightHand') if n in P]
        if hs:
            hand.append(sum(h.z for h in hs) / len(hs) - hp.z)
    res[tag] = dict(frames=f1 - f0 + 1, lean_mean=round(sum(lean) / len(lean), 1), lean_max=round(max(lean), 1),
                    head_mean=round(sum(head) / len(head), 3), head_min=round(min(head), 3), fwd_mean=round(sum(fwd) / len(fwd), 3),
                    hand_mean=round(sum(hand) / max(1, len(hand)), 3), hips_z=round(W('Hips').z, 3))
    # 그림
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 300, 420
    cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    for k in range(6):
        fr = f0 + round((f1 - f0) * k / 5)
        sc.frame_set(fr)
        hp = W('Hips')
        for view, ang in (('side', math.pi / 2), ('front', 0.0)):
            d = 5.0
            cam.location = (hp.x + math.sin(ang) * d, hp.y - math.cos(ang) * d, 0.75)
            cam.rotation_euler = (math.pi / 2, 0, ang)
            sc.render.filepath = os.path.join(out, '%s_%s_%d.png' % (tag, view, k))
            bpy.ops.render.render(write_still=True)
    print('[sit]', tag, json.dumps(res[tag]), flush=True)
with open(os.path.join(out, 'sit_metrics.json'), 'w', encoding='utf-8') as f:
    json.dump(res, f, ensure_ascii=False, indent=1)
