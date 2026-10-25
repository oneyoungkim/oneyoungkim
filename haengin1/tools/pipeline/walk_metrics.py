# 걷기 후보 비교(2026-10-25 걸음 교체): GLB 동작을 프레임마다 재서 '여성스러움' 판정에 쓰는 숫자를 낸다 + 옆·앞 8프레임 그림
# 사용: blender -b --factory-startup --python tools/pipeline/walk_metrics.py -- <out_dir> <a.glb> [b.glb …]
# 숫자(한 주기 — 왼발이 가장 앞에 오는 두 순간 사이):
#   sway  골반 좌우 흔들림(cm, 진행 방향 옆)        roll  골반 옆 기울기 진폭(°)      twist 골반 수평 비틀기 진폭(°)
#   arm   손 앞뒤 흔들림(cm, 골반 기준)              cross 손이 몸 가운데선을 넘는 정도(cm, + 면 넘음)
#   lean  상체 앞 숙임 평균(°, 골반→목)              shoulder 어깨 비틀기 진폭(°)
#   stride 보폭(m, 한 걸음 발목 앞뒤 거리)            cadence 분당 걸음 수        speed 디딤발 기준 진행 속도(m/s)
import bpy, sys, os, math, json
import numpy as np

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
    fps = sc.render.fps / sc.render.fps_base
    P = arm.pose.bones
    mw = arm.matrix_world

    def W(b, tail=False):
        return np.array(tuple(mw @ (P[b].tail if tail else P[b].head)))

    rows = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        hm = mw @ P['Hips'].matrix
        rows.append(dict(hips=W('Hips'), neck=W('neck'), lh=W('LeftHand'), rh=W('RightHand'), lf=W('LeftFoot'), rf=W('RightFoot'),
                         ls=W('LeftArm'), rs=W('RightArm'), lu=W('LeftUpLeg'), ru=W('RightUpLeg'), hm=np.array(hm)))
    H = np.array([r['hips'] for r in rows])
    # 진행 방향: 골반 이동(루트 이동 있음) 또는 발 앞뒤 축(제자리 동작)
    mv = H[-1, :2] - H[0, :2]
    if np.linalg.norm(mv) > 0.3:
        fwd = mv / np.linalg.norm(mv)
    else:
        fwd = np.array([0.0, -1.0])        # Blender 정면 −Y
    side = np.array([-fwd[1], fwd[0]])
    def F(p): return p[:2] @ fwd
    def S(p): return p[:2] @ side
    LF = np.array([F(r['lf']) - F(r['hips']) for r in rows])
    peaks = [i for i in range(1, len(LF) - 1) if LF[i] >= LF[i - 1] and LF[i] >= LF[i + 1] and LF[i] > LF.max() - 0.05]
    # 가까운 봉우리 합치기
    pk = []
    for i in peaks:
        if not pk or i - pk[-1] > 5:
            pk.append(i)
    a, b = (pk[0], pk[1]) if len(pk) >= 2 else (0, len(rows) - 1)
    cyc = rows[a:b + 1]
    n = len(cyc)
    Hs = np.array([S(r['hips']) for r in cyc])
    sway = (Hs.max() - Hs.min()) * 100
    def axis_angles(r):
        m = r['hm'][:3, :3]
        x = m[:, 0]; y = m[:, 1]   # 뼈 x(옆), y(뼈 방향 — 골반은 위)
        up = np.array([0, 0, 1.0])
        roll = math.degrees(math.atan2(x[2], math.hypot(x[0], x[1])))
        yaw = math.degrees(math.atan2(x[:2] @ fwd, x[:2] @ side))
        return roll, yaw
    ang = np.array([axis_angles(r) for r in cyc])
    roll = (ang[:, 0].max() - ang[:, 0].min()) / 2
    tw = (ang[:, 1].max() - ang[:, 1].min()) / 2
    arms = np.array([F(r['lh']) - F(r['hips']) for r in cyc] + [F(r['rh']) - F(r['hips']) for r in cyc])
    armsw = (np.ptp(np.array([F(r['lh']) - F(r['hips']) for r in cyc])) + np.ptp(np.array([F(r['rh']) - F(r['hips']) for r in cyc]))) / 2 * 100
    # 손이 가운데선 넘기: 왼손 옆 좌표가 골반 가운데 쪽으로 얼마나(+)
    lh_side = np.array([S(r['lh']) - S(r['hips']) for r in cyc]); rh_side = np.array([S(r['rh']) - S(r['hips']) for r in cyc])
    sgn = np.sign(np.mean(lh_side)) or 1.0
    cross = max(-(lh_side * sgn).min(), (rh_side * sgn).max()) * 100     # 양수 = 손이 몸 가운데선을 넘음
    lean = np.mean([math.degrees(math.atan2(F(r['neck']) - F(r['hips']), r['neck'][2] - r['hips'][2])) for r in cyc])
    sh = np.array([math.degrees(math.atan2((r['ls'] - r['rs'])[:2] @ fwd, (r['ls'] - r['rs'])[:2] @ side)) for r in cyc])
    shoulder = np.ptp(sh) / 2
    st = np.array([F(r['lf']) - F(r['rf']) for r in cyc])
    stride = np.ptp(st) / 2
    per = (b - a) / fps
    cadence = 120.0 / per if per > 0 else 0
    speed = stride * 2 / per if per > 0 else 0
    res[tag] = dict(frames=[f0, f1], cycle_s=round(per, 3), sway_cm=round(sway, 1), roll_deg=round(roll, 1), twist_deg=round(tw, 1), arm_cm=round(armsw, 1),
                    cross_cm=round(cross, 1), lean_deg=round(lean, 1), shoulder_deg=round(shoulder, 1), stride_m=round(stride, 3), cadence=round(cadence), speed=round(speed, 2))
    print('WALK', tag, json.dumps(res[tag]), flush=True)
    # 그림: 옆·앞 8프레임(한 주기)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = 240; sc.render.resolution_y = 420
    cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = 2.3
    cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    for k in range(8):
        fr = f0 + a + round((b - a) * k / 8)
        sc.frame_set(fr)
        h = mw @ P['Hips'].head
        for vname, d2 in (('side', side), ('front', -fwd)):
            cam.location = (h.x + d2[0] * 5, h.y + d2[1] * 5, 0.95)
            yaw = math.atan2(-d2[0], d2[1])
            cam.rotation_euler = (math.pi / 2, 0, yaw + math.pi)
            sc.render.filepath = os.path.join(out, f'{tag}_{vname}_{k}.png')
            bpy.ops.render.render(write_still=True)
json.dump(res, open(os.path.join(out, 'walk_metrics.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('DONE')
