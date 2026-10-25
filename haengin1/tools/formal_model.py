# 정식 모델 후처리(docs/09_M3_버티컬슬라이스_설계.md 4-2·4-3, 6-1 6단계) — 리깅 GLB(Tripo + 힉스필드 리깅, 손가락 뼈 없음) → Unity 용 FBX + 작업 자료
# 사용: "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tools/formal_model.py -- <src.glb> <dst.fbx> <클립 이름> <작업 폴더> [LOD0 삼각형 상한]
#   예) ... -- concept/art/3d/anim/siwoo_idle246.glb unity/.../Siwoo/Siwoo.fbx Idle %TEMP%/formal/siwoo 58000
# 하는 일(크레딧 0 — 기존 메시 후처리만, 7장 결정 4):
#   ① LOD0 줄이기: 삼각형 상한(기본 58,000 — 4-2 'LOD0 ≤ 60k')까지 Decimate(손·얼굴 앞면은 지킴 — 정점 그룹)
#   ② 손가락 뼈: 손 정점(가장 큰 가중치가 손 뼈)을 tools/hand_keys.py 의 analyze 로 엄지·검지~새끼·손바닥으로 나누고, 손가락마다 3마디 뼈
#      (Humanoid 이름: LeftHandThumb1~3 · LeftHandIndex1~3 · LeftHandMiddle1~3 · LeftHandRing1~3 · LeftHandPinky1~3, 오른손도) + 부드러운 가중치
#      (손가락 사이 6mm 가우스, 마디 앞뒤 1cm, 손가락 시작 1.5cm 램프 — 남는 몫은 손 뼈). 손바닥 방향 표시 뼈 LeftHandPalm → LeftHandPalmN(변형 안 함)
#      — Unity HandPoser 가 이 두 점으로 '손바닥 쪽' 을 안다(뼈 축 변환에 기대지 않음)
#   ③ 얼굴 데칼 메시 Face: 머리 뼈 가중치 + 앞을 보는 면(headfront 뼈 방향)을 복제해 노멀 방향으로 0.5mm 띄운 껍데기, 전용 UV = 앞에서 본 정사영(얼굴 상자 0~1)
#      → 표정 아틀라스 칸(tools/face_tex.py)을 Unity 가 재질 오프셋으로 바꿔 끼운다. 몸 텍스처의 눈·입 지우기도 face_tex.py(이 스크립트가 넘기는 face.npz 로)
#   ④ LOD: Body_LOD0(위) · Body_LOD1(50%) · Body_LOD2(20%) · Face_LOD0 · Face_LOD1 — 이름 끝 _LODn 이라 Unity 가 LODGroup 을 만든다
#   ⑤ FBX 내보내기(glb2fbx.py 와 같은 설정: 배율 FBX_SCALE_ALL, 정면 +Z, 손가락 뼈 포함, 클립 1개 — 루프 이음새 처리 같음)
#   작업 폴더에 남기는 것: tex.jpg(원본 텍스처 그대로) · face.npz(얼굴 삼각형의 원래 UV·데칼 UV) · info.json(키·머리 높이·삼각형 수·손가락 길이 등)
import bpy, bmesh, sys, os, json, math
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, clip, work = argv[0], argv[1], argv[2], argv[3]
target = int(argv[4]) if len(argv) >= 5 else 58000
# 바이블 등신 맞추기(선택): [목표 등신] [배율 1 에서 잰 두개골 머리 높이 m] — face_tex.py 가 Face.json 'head_h_skull' 로 남긴 값
heads_target = float(argv[5]) if len(argv) >= 7 else 0.0
head_h0 = float(argv[6]) if len(argv) >= 7 else 0.0
os.makedirs(work, exist_ok=True)
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hand_keys as hk

info = dict(src=os.path.basename(src))
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30
sc.render.fps_base = 1.0
bpy.ops.import_scene.gltf(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm][0]
for o in list(bpy.data.objects):
    if o not in (arm, body):
        bpy.data.objects.remove(o, do_unlink=True)
body.name = 'Body_LOD0'
body.data.name = 'Body_LOD0'
vl = bpy.context.view_layer

# 원본 텍스처(JPEG 그대로)
img = next((i for i in bpy.data.images if i.packed_file is not None), None) or (bpy.data.images[0] if bpy.data.images else None)
if img is not None and img.packed_file is not None:
    with open(os.path.join(work, 'tex.jpg'), 'wb') as f:
        f.write(img.packed_file.data)
    info['tex'] = list(img.size)

# 휴지 자세에서 작업(아마추어 변형 끔)
arm.data.pose_position = 'REST'
for m in body.modifiers:
    if m.type == 'ARMATURE':
        m.show_viewport = False
vl.update()


def tri_count(o):
    dg = bpy.context.evaluated_depsgraph_get()
    oe = o.evaluated_get(dg)
    me = oe.to_mesh()
    n = sum(len(p.vertices) - 2 for p in me.polygons)
    oe.to_mesh_clear()
    return n


def dominant(o):
    """정점마다 (가장 큰 가중치 그룹 이름, 그 가중치)"""
    names = {g.index: g.name for g in o.vertex_groups}
    dom = []
    for v in o.data.vertices:
        best, w = None, 0.0
        for g in v.groups:
            if g.weight > w:
                best, w = names.get(g.group), g.weight
        dom.append(best)
    return dom


HEAD = ('Head', 'head_end', 'headfront')


def world_co(o):
    return np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])


# ⓪ 바이블 등신 맞추기(FUJIMOTO 2-1, 09 4-2 #1): 머리(머리 뼈 가중치 몫만큼)를 머리 뼈 뿌리 기준으로 s 배 → 몸 전체를 k 배로 원래 키에 되돌림.
#   두개골 높이 h 와 정수리·턱끝은 모두 머리 뼈 100% 정점이라 s 배가 그대로 h·s 가 된다. 키 H, 뿌리~정수리 r 이면
#   (H + (s-1)r) / (h s) = T  →  s = (H - r) / (T h - r).  목 정점은 가중치만큼 섞여 이음매가 부드럽다.
if heads_target > 0 and head_h0 > 0:
    hd0 = arm.data.bones.get('Head')
    amw = arm.matrix_world
    piv = np.array(tuple(amw @ hd0.head_local))
    co0 = world_co(body)
    H0 = float(co0[:, 2].max() - co0[:, 2].min())
    zmin = float(co0[:, 2].min())
    r_ = float(co0[:, 2].max() - piv[2])
    s_ = (H0 - r_) / (heads_target * head_h0 - r_)
    k_ = H0 / (H0 + (s_ - 1.0) * r_)
    names = {g.index: g.name for g in body.vertex_groups}
    inv = body.matrix_world.inverted()
    base = np.array([0.0, 0.0, zmin])
    from mathutils import Vector
    for v, p in zip(body.data.vertices, co0):
        tot = sum(g.weight for g in v.groups) or 1.0
        w = sum(g.weight for g in v.groups if names.get(g.group) in HEAD) / tot
        q = p + w * (s_ - 1.0) * (p - piv)
        q = base + k_ * (q - base)
        v.co = inv @ Vector(tuple(q))
    # 뼈: 머리 뼈와 그 아래(head_end·headfront)는 뿌리 기준 s 배, 그다음 전부 k 배
    ainv = amw.inverted()
    vl_ = bpy.context.view_layer
    vl_.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    under = set(HEAD)
    for eb in arm.data.edit_bones:
        for attr in ('head', 'tail'):
            p = np.array(tuple(amw @ getattr(eb, attr)))
            if eb.name in under and not (eb.name == 'Head' and attr == 'head'):
                p = piv + s_ * (p - piv)
            p = base + k_ * (p - base)
            setattr(eb, attr, ainv @ Vector(tuple(p)))
    bpy.ops.object.mode_set(mode='OBJECT')
    # 클립의 뼈 위치 키도 k 배(엉덩이 이동 — 균일 배율이라 뼈 공간에서도 그대로 곱함)
    if arm.animation_data and arm.animation_data.action:
        act = arm.animation_data.action
        fcs = []
        for ly in getattr(act, 'layers', []):
            for st in ly.strips:
                for cb in st.channelbags:
                    fcs += list(cb.fcurves)
        if not fcs and hasattr(act, 'fcurves'):
            fcs = list(act.fcurves)
        for fc in fcs:
            if fc.data_path.endswith('.location'):
                for kp in fc.keyframe_points:
                    kp.co[1] *= k_
                    kp.handle_left[1] *= k_
                    kp.handle_right[1] *= k_
    info['head_scale'] = s_
    info['body_rescale'] = k_
    info['heads_target'] = heads_target
    info['head_h_before'] = head_h0
    info['height_before'] = H0
    vl.update()

mw = body.matrix_world
co_all = np.array([tuple(mw @ v.co) for v in body.data.vertices])
info['height'] = float(co_all[:, 2].max() - co_all[:, 2].min())
info['tris_src'] = tri_count(body)

# 앞 방향 = headfront 뼈(머리 → 얼굴 앞)
hf = arm.data.bones.get('headfront')
hd = arm.data.bones.get('Head')
if hf is not None:
    fv = np.array(tuple(arm.matrix_world.to_3x3() @ (hf.tail_local - hf.head_local)))
    fv[2] = 0.0
else:
    fv = np.array([0.0, -1.0, 0.0])
front = fv / np.linalg.norm(fv)
up = np.array([0.0, 0.0, 1.0])
side = np.cross(-front, up)          # 앞에서 보는 사람의 오른쪽
info['front'] = front.tolist()


def protect_group(o):
    """손·얼굴 앞면 = 0(지킴), 나머지 1 — Decimate 정점 그룹"""
    dom = dominant(o)
    co = np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])
    nrm = np.array([tuple((o.matrix_world.to_3x3() @ v.normal).normalized()) for v in o.data.vertices])
    head = np.array([d in HEAD for d in dom])
    hc = co[head].mean(0) if head.any() else co.mean(0)
    face = head & (nrm @ front > 0.2) & ((co - hc) @ front > 0.0)
    hand = np.array([d in ('LeftHand', 'RightHand') for d in dom])
    keep = face | hand
    g = o.vertex_groups.get('DecimateW') or o.vertex_groups.new(name='DecimateW')
    g.add([int(i) for i in np.where(~keep)[0]], 1.0, 'REPLACE')
    g.add([int(i) for i in np.where(keep)[0]], 0.0, 'REPLACE')
    return int(keep.sum())


def decimate(o, ratio, use_group):
    mod = o.modifiers.new('Decimate', 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    if use_group:
        mod.vertex_group = 'DecimateW'
        mod.vertex_group_factor = 1.0
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
        bpy.ops.object.modifier_move_to_index(modifier='Decimate', index=0)
    return mod


def apply_mod(o, name):
    vl.objects.active = o
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
        bpy.ops.object.modifier_apply(modifier=name)


# ① LOD0 줄이기
if info['tris_src'] > target:
    kept = protect_group(body)
    mod = decimate(body, 0.75, True)
    lo, hi = 0.2, 1.0
    for _ in range(14):
        mod.ratio = 0.5 * (lo + hi)
        n = tri_count(body)
        if n > target:
            hi = mod.ratio
        else:
            lo = mod.ratio
        if abs(n - target) < 0.006 * target and n <= target:
            break
    mod.ratio = lo
    apply_mod(body, 'Decimate')
    info['decimate_ratio'] = lo
    info['kept_verts'] = kept
info['tris_lod0_body'] = tri_count(body)

# ② 손가락 뼈
FINGERS = ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')
JOINT = (0.0, 0.45, 0.73)
RAMP, BLEND = 0.015, 0.010


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def seg_weights(s, length):
    j1, j2 = JOINT[1] * length, JOINT[2] * length
    a = smooth((s - j1 + BLEND) / (2 * BLEND))
    b = smooth((s - j2 + BLEND) / (2 * BLEND))
    return np.stack([1 - a, a * (1 - b), b], 1)


inv = arm.matrix_world.inverted()
plans = {}
for sd in ('Left', 'Right'):
    hand = sd + 'Hand'
    idx, co, label, fr = hk.analyze(body, arm, hand, None)
    psign = hk.palm_side(co, label, fr)
    a, w, n, wrist = fr['a'], fr['w'], fr['n'], fr['wrist']
    p = n * psign
    rel = co - wrist
    u = rel @ a
    chains = {}
    for k, nm in enumerate(FINGERS):
        m = label == k
        if m.sum() < 8:
            raise SystemExit(f'{hand}: {nm} 정점이 {int(m.sum())}개라 손가락 뼈를 만들 수 없음')
        d, base, length = hk._finger_axis(co[m], wrist)
        chains[nm] = dict(d=d, base=base, length=float(length), n=int(m.sum()))
    # 2026-10-25 새끼 줄기 고침: 정점 묶음 PCA 가 손 옆 날(손바닥 가장자리)까지 먹으면 축이 손등 쪽으로 눕는다(태오·배달복 오른손 새끼 —
    # D13 '어긋난 줄기 R16'). 가운데 손가락 축과 30° 넘게 벌어진 줄기는 그 축을 가운데 손가락 쪽으로 돌리고(옆 벌어짐 12° 까지만 남김)
    # 그 손가락 정점을 새 축에 다시 투영해 시작점·길이를 잰다
    mid = chains['Middle']['d']
    fixed = []
    for nm in ('Index', 'Ring', 'Pinky'):
        ch = chains[nm]
        ang = math.degrees(math.acos(float(np.clip(ch['d'] @ mid, -1.0, 1.0))))
        if ang <= 30.0:
            continue
        sel = (label == FINGERS.index(nm)) & (u > fr['u_k'] - 0.004)        # 밑 마디선 앞(손가락 몸통)만 — 손바닥 가장자리 정점이 축을 끌어당기지 않게
        if sel.sum() < 8:
            sel = label == FINGERS.index(nm)
        P = co[sel]
        cP = P.mean(0)
        # 옆 벌어짐 방향 = 이웃 손가락(검지·약지 ← 가운데, 새끼 ← 약지) 정점 중심에서 멀어지는 쪽(예전 축의 옆 성분은 이웃 쪽을 가리켜 굽히면 겹침)
        nb = 'Ring' if nm == 'Pinky' else 'Middle'
        side = cP - co[label == FINGERS.index(nb)].mean(0)
        side = side - mid * (side @ mid)
        side = side - p * (side @ p)            # 손바닥 쪽 성분은 빼고 옆 벌어짐만
        sn = np.linalg.norm(side)
        dnew = mid * math.cos(math.radians(5.0)) + (side / sn) * math.sin(math.radians(5.0)) if sn > 1e-6 else mid.copy()
        dnew = dnew / np.linalg.norm(dnew)
        t = (P - cP) @ dnew
        t0, t1 = np.quantile(t, 0.01), np.quantile(t, 0.995)
        ch.update(d=dnew, base=cP + dnew * t0, length=float(t1 - t0))
        fixed.append(f'{nm} {ang:.0f}°')
    if fixed:
        info.setdefault('finger_axis_fixed', {})[sd] = fixed
    # 손바닥 가운데(밑 마디선 앞 절반) · 손바닥 쪽 표시
    palm = (label == -1) & (u > 0.15 * fr['L']) & (u < fr['u_k'])
    pc = co[palm].mean(0) if palm.any() else wrist + a * 0.5 * fr['u_k']
    plans[sd] = dict(idx=idx, co=co, label=label, fr=fr, p=p, chains=chains, palm=pc)

bpy.ops.object.select_all(action='DESELECT')
vl.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
from mathutils import Vector


def L(x):
    return inv @ Vector(tuple(x))


for sd, pl in plans.items():
    hb = eb[sd + 'Hand']
    for nm, ch in pl['chains'].items():
        parent = hb
        pts = [ch['base'] + ch['d'] * ch['length'] * j for j in (0.0, JOINT[1], JOINT[2], 1.0)]
        for k in range(3):
            b = eb.new(f'{sd}Hand{nm}{k + 1}')
            b.head = L(pts[k]); b.tail = L(pts[k + 1])
            b.align_roll(L(pl['p']) - L(np.zeros(3)))
            b.parent = parent
            b.use_connect = k > 0
            parent = b
    pb = eb.new(f'{sd}HandPalm')
    pb.head = L(pl['palm']); pb.tail = L(pl['palm'] + pl['p'] * 0.02)
    pb.parent = hb; pb.use_deform = False
    pn = eb.new(f'{sd}HandPalmN')
    pn.head = L(pl['palm'] + pl['p'] * 0.03); pn.tail = L(pl['palm'] + pl['p'] * 0.04)
    pn.parent = pb; pn.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')

# 가중치
finfo = {}
for sd, pl in plans.items():
    hand = sd + 'Hand'
    idx, co, label, fr = pl['idx'], pl['co'], pl['label'], pl['fr']
    hg = body.vertex_groups[hand]
    hw = np.zeros(len(idx))
    for j, vi in enumerate(idx):
        for g in body.data.vertices[int(vi)].groups:
            if g.group == hg.index:
                hw[j] = g.weight
    rel = co - fr['wrist']
    u = rel @ fr['a']
    tw = fr['thumb_w']
    fbase = smooth((u - fr['u_k']) / RAMP)
    # 네 손가락 소속(손 넓이 쪽 거리 — 손가락 중심선까지, 가우스 6mm)
    cents = []
    for nm in FINGERS[1:]:
        ch = pl['chains'][nm]
        cents.append(ch)
    D = []
    for ch in cents:
        r = co - ch['base']
        along = r @ ch['d']
        perp = r - np.outer(along, ch['d'])
        D.append(np.linalg.norm(perp - np.outer(perp @ pl['p'], pl['p']), axis=1))   # 손바닥 쪽 성분은 빼고 옆 거리만
    D = np.stack(D, 1)
    mem = np.exp(-(D / 0.006) ** 2) + 1e-9
    mem /= mem.sum(1, keepdims=True)
    parts = {}
    total = np.zeros(len(idx))
    # 엄지
    ch = pl['chains']['Thumb']
    s = (co - ch['base']) @ ch['d']
    tb = tw * smooth(s / RAMP + 0.5)
    sw = seg_weights(s, ch['length'])
    for k in range(3):
        parts[f'{sd}HandThumb{k + 1}'] = tb * sw[:, k]
    total += tb
    for fi, nm in enumerate(FINGERS[1:]):
        ch = pl['chains'][nm]
        s = (co - ch['base']) @ ch['d']
        fw = (1 - tw) * fbase * mem[:, fi]
        sw = seg_weights(s, ch['length'])
        for k in range(3):
            parts[f'{sd}Hand{nm}{k + 1}'] = fw * sw[:, k]
        total += fw
    remain = np.clip(1 - total, 0, 1)
    for bn, pw in parts.items():
        g = body.vertex_groups.get(bn) or body.vertex_groups.new(name=bn)
        for j in np.where(pw * hw > 1e-4)[0]:
            g.add([int(idx[j])], float(pw[j] * hw[j]), 'REPLACE')
    for j, vi in enumerate(idx):
        hg.add([int(vi)], float(remain[j] * hw[j]), 'REPLACE')
    finfo[sd] = {nm: dict(length=round(c['length'], 4), verts=c['n']) for nm, c in pl['chains'].items()}
    finfo[sd]['hand_verts'] = int(len(idx))
info['fingers'] = finfo

# ③ 얼굴 데칼
dom = dominant(body)
me = body.data
co = np.array([tuple(mw @ v.co) for v in me.vertices])
head = np.array([d in HEAD for d in dom])
hc = co[head].mean(0)
uv = me.uv_layers.active.data
polys = []
for poly in me.polygons:
    vs = list(poly.vertices)
    if not all(head[v] for v in vs):
        continue
    nn = np.array(tuple((mw.to_3x3() @ poly.normal).normalized()))
    c = co[vs].mean(0)
    if nn @ front < 0.25 or (c - hc) @ front < 0.0:
        continue
    polys.append(poly.index)
fv_idx = sorted({v for pi in polys for v in me.polygons[pi].vertices})
P = co[fv_idx]
lat = (P - hc) @ side
x0, x1 = float(np.quantile(lat, 0.0)), float(np.quantile(lat, 1.0))
z0, z1 = float(P[:, 2].min()), float(P[:, 2].max())
info['face_box'] = dict(x0=x0, x1=x1, z0=z0, z1=z1, width=x1 - x0, height=z1 - z0, center=hc.tolist())
info['head_top'] = float(co[head][:, 2].max())
info['chin'] = z0
# 원래 UV · 데칼 UV 삼각형(얼굴 텍스처 작업용)
tris_o, tris_d, tris_p = [], [], []
for pi in polys:
    poly = me.polygons[pi]
    li = list(poly.loop_indices)
    vs = list(poly.vertices)
    for k in range(1, len(vs) - 1):
        tri = (0, k, k + 1)
        tris_o.append([tuple(uv[li[t]].uv) for t in tri])
        pts = [co[vs[t]] for t in tri]
        tris_d.append([(((pp - hc) @ side - x0) / (x1 - x0), (pp[2] - z0) / (z1 - z0)) for pp in pts])
        tris_p.append([tuple(pp) for pp in pts])
# 몸 전체 삼각형의 원래 UV + 부위(0 몸·옷 · 1 머리 · 2 손 · 3 얼굴 앞면) — 부위 마스크·외곽선 굵기 텍스처용
fset = set(polys)
all_uv, all_cls = [], []
for poly in me.polygons:
    vs = list(poly.vertices)
    li = list(poly.loop_indices)
    dd = [dom[v] for v in vs]
    cls = 3 if poly.index in fset else 1 if all(d in HEAD for d in dd) else 2 if all(d in ('LeftHand', 'RightHand') or (d or '').startswith(('LeftHand', 'RightHand')) for d in dd) else 0
    for k in range(1, len(vs) - 1):
        all_uv.append([tuple(uv[li[t]].uv) for t in (0, k, k + 1)])
        all_cls.append(cls)
np.savez(os.path.join(work, 'face.npz'), orig=np.array(tris_o, dtype=np.float32), decal=np.array(tris_d, dtype=np.float32), pos=np.array(tris_p, dtype=np.float32),
         all_uv=np.array(all_uv, dtype=np.float32), all_cls=np.array(all_cls, dtype=np.int8))
info['face_tris'] = len(tris_o)

# 데칼 껍데기에서 '덧댄 겹'(피부 앞에 떠 있는 앞머리·옆머리 카드)은 뺀다: 데칼 UV 는 앞에서 본 정사영이라 앞머리 카드도 같은 자리의
#   눈 그림을 받아 머리카락 위에 눈이 그려진다(그림 칸이 투명이 아닌 곳). 앞에서 본 384² 래스터의 '가장 뒤 겹' 깊이보다 3mm 넘게
#   앞에 있는 면(꼭짓점·가운데 4점 중 3점 이상)이 덧댄 겹 — face_tex.py 가 텍스처를 고칠 때 쓰는 '가장 뒤 겹'과 같은 규칙.
RV = 384
dmin = np.full((RV, RV), np.inf)
tri_poly = []
for pi in polys:
    vs_ = list(me.polygons[pi].vertices)
    for k in range(1, len(vs_) - 1):
        tri_poly.append((pi, (vs_[0], vs_[k], vs_[k + 1])))


def dec_uv(pp):
    return np.array([((pp - hc) @ side - x0) / (x1 - x0), (pp[2] - z0) / (z1 - z0)])


for pi, tv in tri_poly:
    A = np.array([dec_uv(co[v]) * (RV - 1) for v in tv])
    D = np.array([co[v] @ front for v in tv])
    xa, xb = int(np.floor(A[:, 0].min())), int(np.ceil(A[:, 0].max()))
    ya, yb = int(np.floor(A[:, 1].min())), int(np.ceil(A[:, 1].max()))
    xs, ys = np.meshgrid(np.arange(max(xa, 0), min(xb, RV - 1) + 1), np.arange(max(ya, 0), min(yb, RV - 1) + 1))
    xs, ys = xs.ravel(), ys.ravel()
    den = (A[1, 1] - A[2, 1]) * (A[0, 0] - A[2, 0]) + (A[2, 0] - A[1, 0]) * (A[0, 1] - A[2, 1])
    if abs(den) < 1e-9 or xs.size == 0:
        continue
    l0 = ((A[1, 1] - A[2, 1]) * (xs - A[2, 0]) + (A[2, 0] - A[1, 0]) * (ys - A[2, 1])) / den
    l1 = ((A[2, 1] - A[0, 1]) * (xs - A[2, 0]) + (A[0, 0] - A[2, 0]) * (ys - A[2, 1])) / den
    l2 = 1 - l0 - l1
    m_ = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
    d_ = l0 * D[0] + l1 * D[1] + l2 * D[2]
    np.minimum.at(dmin, (ys[m_], xs[m_]), d_[m_])
over = set()
for pi in polys:
    vs_ = list(me.polygons[pi].vertices)
    pts = [co[v] for v in vs_] + [co[vs_].mean(0)]
    n_over = 0
    for pp in pts:
        q = np.clip(np.round(dec_uv(pp) * (RV - 1)).astype(int), 0, RV - 1)
        if pp @ front - dmin[q[1], q[0]] > 0.003:
            n_over += 1
    if n_over >= 3:
        over.add(pi)
info['face_overlay_polys'] = len(over)

# 복제 → 얼굴 면만 남김(덧댄 겹 뺌) → 0.5mm 띄움 → 데칼 UV
face = body.copy()
face.data = body.data.copy()
face.name = 'Face_LOD0'
face.data.name = 'Face_LOD0'
sc.collection.objects.link(face)
bm = bmesh.new()
bm.from_mesh(face.data)
keep = set(polys) - over
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
bm.verts.ensure_lookup_table()
bm.normal_update()
for v in bm.verts:
    v.co = v.co + v.normal * 0.0005
uvl = bm.loops.layers.uv.active
mwi = mw.inverted()
for f in bm.faces:
    for lp in f.loops:
        pw = np.array(tuple(mw @ lp.vert.co))
        lp[uvl].uv = (((pw - hc) @ side - x0) / (x1 - x0), (pw[2] - z0) / (z1 - z0))
bm.to_mesh(face.data)
bm.free()
face.data.materials.clear()
fm = bpy.data.materials.new('Face')
face.data.materials.append(fm)
body.data.materials.clear()
bmat = bpy.data.materials.new('Body')
body.data.materials.append(bmat)
info['tris_face'] = tri_count(face)

# ④ LOD1·LOD2(LOD0 에서 줄임 — 가중치 그대로) · Face_LOD1(복사)
lods = []
for k, r in ((1, 0.5), (2, 0.2)):
    o = body.copy(); o.data = body.data.copy()
    o.name = f'Body_LOD{k}'; o.data.name = o.name
    sc.collection.objects.link(o)
    decimate(o, r, False)
    apply_mod(o, 'Decimate')
    lods.append(o)
    info[f'tris_lod{k}'] = tri_count(o)
f1 = face.copy(); f1.data = face.data.copy()
f1.name = 'Face_LOD1'; f1.data.name = 'Face_LOD1'
sc.collection.objects.link(f1)
f2 = face.copy(); f2.data = face.data.copy()            # 멀리서도 눈·입이 있게(몸 텍스처의 눈·입은 지워져 있다)
f2.name = 'Face_LOD2'; f2.data.name = 'Face_LOD2'
sc.collection.objects.link(f2)
for o in [body, face, f1, f2] + lods:
    g = o.vertex_groups.get('DecimateW')
    if g:
        o.vertex_groups.remove(g)
    for m in o.modifiers:
        if m.type == 'ARMATURE':
            m.show_viewport = True
            m.object = arm
    o.parent = arm
info['tris_lod0'] = info['tris_lod0_body'] + info['tris_face']
arm.data.pose_position = 'POSE'

# ⑤ 클립 루프 이음새(glb2fbx.py 와 같음) + 내보내기
act = arm.animation_data.action
act.name = clip


def fcurves(a_):
    for L_ in a_.layers:
        for st in L_.strips:
            for cb in st.channelbags:
                for fc in cb.fcurves:
                    yield fc


fcs = list(fcurves(act))
f0, f1_ = (round(x) for x in act.frame_range)
quat = {}
for fc in fcs:
    if fc.data_path.endswith('rotation_quaternion'):
        quat.setdefault(fc.data_path, {})[fc.array_index] = fc


def same(fc):
    a_, b_ = fc.evaluate(f0), fc.evaluate(f1_)
    if fc.data_path.endswith('rotation_quaternion'):
        g = quat[fc.data_path]
        if sum(g[i].evaluate(f0) * g[i].evaluate(f1_) for i in range(4)) < 0:
            b_ = -b_
        return abs(a_ - b_) < 2e-3
    return abs(a_ - b_) < 2e-3 * max(1.0, abs(a_))


closed = all(same(fc) for fc in fcs)
if not closed:
    for fc in fcs:
        v = fc.evaluate(f0)
        if fc.data_path.endswith('rotation_quaternion'):
            g = quat[fc.data_path]
            if sum(g[i].evaluate(f0) * g[i].evaluate(f1_) for i in range(4)) < 0:
                v = -v
        fc.keyframe_points.insert(f1_ + 1, v, options={'FAST'})
    for fc in fcs:
        fc.update()
sc.frame_start, sc.frame_end = int(f0), int(f1_) + (0 if closed else 1)
# 2026-10-25: 손가락만 고쳐 다시 뽑을 때 예전 FBX 의 얼굴 데칼(Face_LOD0~2)을 그대로 옮겨 붙임(환경 변수 FORMAL_KEEP_FACE = 예전 FBX).
# 얼굴 데칼은 표정 아틀라스·Face.json 과 짝이라, 다시 만들면(덧댄 겹 판정이 달라지면) Unity 쪽 칸이 어긋난다
keep_face = os.environ.get('FORMAL_KEEP_FACE')
if keep_face:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=keep_face, automatic_bone_orientation=False)
    newobjs = [o for o in bpy.data.objects if o not in before]
    olds = {o.name.split('.')[0]: o for o in newobjs if o.type == 'MESH' and o.name.startswith('Face_LOD')}
    repl = []
    for cur in (face, f1, f2):
        nm = cur.name
        o = olds.get(nm)
        if o is None:
            raise SystemExit('예전 FBX 에 ' + nm + ' 없음')
        mwo = o.matrix_world.copy()
        o.parent = arm
        o.matrix_world = mwo
        mods = [md for md in o.modifiers if md.type == 'ARMATURE']
        if not mods:
            mods = [o.modifiers.new('Armature', 'ARMATURE')]
        for md in mods:
            md.object = arm
        bpy.data.objects.remove(cur, do_unlink=True)
        o.name = nm
        o.data.name = nm
        repl.append(o)
    face, f1, f2 = repl
    for o in newobjs:
        if o not in repl:
            bpy.data.objects.remove(o, do_unlink=True)
    info['face_kept_from'] = os.path.basename(keep_face)
    info['face_kept_bbox_z'] = [round(min((face.matrix_world @ v.co).z for v in face.data.vertices), 4), round(max((face.matrix_world @ v.co).z for v in face.data.vertices), 4)]
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for o in [body, face, f1, f2] + lods:
    o.select_set(True)
vl.objects.active = arm
os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False,
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)
with open(os.path.join(work, 'info.json'), 'w', encoding='utf-8') as f:
    json.dump(info, f, ensure_ascii=False, indent=1)
print('FORMAL', json.dumps({k: v for k, v in info.items() if k not in ('fingers',)}, ensure_ascii=False))
print('FINGERS', json.dumps(finfo, ensure_ascii=False))
print('WROTE', dst)
