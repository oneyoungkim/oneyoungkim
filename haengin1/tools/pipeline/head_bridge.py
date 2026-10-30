# Blender batch: 코치 v3 머리 잇기 2판(2026-10-30) — 겹침 0, 한 덩어리 메시
#   head_swap.py(새 머리·옛 목을 겹쳐 둠)는 경계가 들쭉날쭉해 게임 외곽선·먹선이 검은 선으로 그렸음 → 이음 방식을 바꿈
#   1) 새 머리: 턱 끝 아래 cutH(3.5cm) 경사 평면으로 자르고 아래 전부 지움(경계 = 닫힌 평면 고리 1개)
#   2) v2 몸: 옷깃 위 cutB(최대 4.5cm) 같은 기울기 평면 위의 머리·목을 지움(경계 = 닫힌 평면 고리 1개) — 두 메시는 어디서도 겹치지 않음
#   3) 두 고리의 모양을 평균 쪽으로 위아래 3cm 에 걸쳐 맞춘 뒤 3~4단 띠로 이어 한 메시로 합침, 정점을 붙여 법선을 다시 계산(매끈)
#   4) 가중치: 새 머리 = Head(고리 쪽 .6 → 3cm 위 1), 몸 = v2 그대로, 띠 = 보간
#   5) 옆면 앞뒤 자리는 코끝·턱 끝 정렬(4c8a7f0), 높이는 턱 끝을 v2 턱 끝에 맞춤(턱 끝~옷깃 거리 = v2)
#   6) <out>.npz 에 삼각형·정점 값을 남겨 neck_paint.py 가 이음 띠 ±3cm 를 같은 피부색으로 칠함
# 사용: blender -b --factory-startup --python head_bridge.py -- <body_rigged.glb> <bust_tripo.glb> <atlas.jpg> <out.glb> [json 인자]
#   json: head_swap.py 와 같은 scale·pitchDeg·alignDy·eyeL…·drop…·nose… + yawDeg(머리 돌아감 바로잡기), centerDx, cutH, cutB, gapMin, tilt(도), rings, chinDz, collarBack, morphL
#   순서: head_bridge.py → neck_paint.py → glb_swap_tex.py → glb2fbx → glbtex → face_masks.py → face_shade_outw.py … <목 마스크> → (서진수) mesh_dump.py·hair_blond.py
import bpy, bmesh, sys, json, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]
body_p, bust_p, atlas_p, out_p = a[:4]
opt = json.loads(a[4]) if len(a) > 4 else {}
ATW, ATH, HS = 6144, 4096, 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=body_p)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = max((o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm), key=lambda o: len(o.data.vertices))
act = arm.animation_data.action if arm.animation_data else None
if arm.animation_data: arm.animation_data.action = None
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update()
neck_b = arm.matrix_world @ arm.pose.bones['neck'].head
head_b = arm.matrix_world @ arm.pose.bones['Head'].head

def tex_np(obj):
    mat = obj.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
    w, h = img.size; px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
    return img, px.reshape(h, w, 4)[:, :, :3]

def vert_colors(obj, pix):
    me = obj.data; uv = me.uv_layers.active.data; h, w = pix.shape[:2]
    first = np.zeros(len(me.vertices), int)
    li = np.array([l.vertex_index for l in me.loops]); order = np.arange(len(li))[::-1]; first[li[order]] = order
    uvs = np.array([tuple(uv[i].uv) for i in first])
    return pix[np.clip((uvs[:, 1] * h).astype(int), 0, h - 1), np.clip((uvs[:, 0] * w).astype(int), 0, w - 1)]

def neck_profile(P, col, z0, z1, cx, cy, rmax=0.11, skin=None, widest=False):
    sel = (P[:, 2] > z0) & (P[:, 2] < z1) & (np.hypot(P[:, 0] - cx, P[:, 1] - cy) < rmax)
    isskin = np.linalg.norm(col - skin, axis=1) < 0.16
    best = None
    for z in np.linspace(z0, z1, 48):
        m = sel & isskin & (np.abs(P[:, 2] - z) < 0.006)
        if m.sum() < 12: continue
        xs = P[m, 0]; wdt = np.percentile(xs, 97) - np.percentile(xs, 3)
        if best is None or (wdt > best[1] if widest else wdt < best[1]): best = (z, wdt, float(np.median(P[m, 0])), float(np.median(P[m, 1])))
    return best

def face_skin(P, col, z0, z1):
    m = (P[:, 2] > z0) & (P[:, 2] < z1) & (col.sum(1) > 1.2)
    idx = np.nonzero(m)[0]; idx = idx[np.argsort(P[idx, 1])][:400]
    return np.median(col[idx], 0)

mw = body.matrix_world
PB = np.array([tuple(mw @ v.co) for v in body.data.vertices])
_, bpix = tex_np(body)
CB = vert_colors(body, bpix)
skin_b = face_skin(PB, CB, head_b.z + 0.06, head_b.z + 0.11)
hb_ = neck_profile(PB, CB, head_b.z + 0.03, head_b.z + 0.16, head_b.x, head_b.y, rmax=0.14, skin=skin_b, widest=True)
zb, wb = hb_[0], hb_[1]; bx = head_b.x; hcy = hb_[3]

# ── 새 머리: 크기·자리(head_swap.py 와 같음)
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=bust_p)
new = [o for o in bpy.data.objects if o not in before]
bust = max((o for o in new if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
bpy.context.view_layer.update()
mwb = bust.matrix_world.copy()
for o in new:
    if o != bust and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
bust.parent = None
bust.data.transform(mwb); bust.matrix_world = Matrix.Identity(4)
bust.data.transform(Matrix.Rotation(-math.pi / 2, 4, 'Z'))
PN = np.array([tuple(v.co) for v in bust.data.vertices])
_, npix = tex_np(bust)
CN = vert_colors(bust, npix)
ztop, zbot = PN[:, 2].max(), PN[:, 2].min(); hh = ztop - zbot
cxn, cyn = float(np.median(PN[:, 0])), float(np.median(PN[:, 1]))
skin_n = face_skin(PN, CN, zbot + 0.62 * hh, zbot + 0.75 * hh)
nn = neck_profile(PN, CN, zbot + 0.45 * hh, zbot + 0.85 * hh, cxn, cyn, rmax=0.5 * hh, skin=skin_n, widest=True)
zn, wn, nx, _ = nn
ny = float(np.median(PN[(np.abs(PN[:, 2] - zn) < 0.02 * hh), 1]))
s = wb / wn * float(opt.get('scale', 1.0))
bust.data.transform(Matrix.Translation((bx, hcy, zb)) @ Matrix.Scale(s, 4) @ Matrix.Translation((-nx, -ny, -zn)))
PN = np.array([tuple(v.co) for v in bust.data.vertices])

def landmarks(P, cx, zmin):
    m = (P[:, 2] > zmin)
    mid = P[m & (np.abs(P[:, 0] - cx) < 0.015)]
    nose = mid[np.argmin(mid[:, 1])]
    lowf = mid[(mid[:, 1] < nose[1] + 0.05) & (mid[:, 2] < nose[2] - 0.02)]
    chin = lowf[np.argmin(lowf[:, 2])] if len(lowf) else nose
    return {'nose': nose, 'chin': chin}
gname0 = {g.index: g.name for g in body.vertex_groups}
wh0 = np.array([sum(g.weight for g in v.groups if gname0.get(g.group) in ('Head', 'head_end', 'headfront')) for v in body.data.vertices])
Lo = landmarks(PB[wh0 > 0.5], head_b.x, head_b.z + 0.02)
if 'pitchDeg' in opt:                                               # 고개 숙이기(+): 머리 뼈 피벗을 축으로 메시만 돌림
    hp = Vector(head_b)
    bust.data.transform(Matrix.Translation(hp) @ Matrix.Rotation(math.radians(opt['pitchDeg']), 4, 'X') @ Matrix.Translation(-hp))
    PN = np.array([tuple(v.co) for v in bust.data.vertices])
Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
dy = ((Lo['nose'][1] - Ln['nose'][1]) + (Lo['chin'][1] - Ln['chin'][1])) / 2 + float(opt.get('alignDy', 0.0))
bust.data.transform(Matrix.Translation((0, dy, 0))); PN = np.array([tuple(v.co) for v in bust.data.vertices])

# ── 얼굴 형상 고치기(정면 쪽만, head_swap.py 와 같음)
def smooth(r, R): return np.where(r < R, (1 - (r / R) ** 2) ** 2, 0.0)
if 'eyeL' in opt or 'drop' in opt or 'noseDrop' in opt:
    P = PN.copy(); front = np.clip((head_b.y - P[:, 1]) / 0.03, 0, 1); d = np.zeros_like(P)
    if 'eyeL' in opt:
        exL, exR, ez = opt['eyeL'], opt['eyeR'], opt['eyeZ']; R = opt.get('R', 0.03); mid = (exL + exR) / 2
        for ex in (exL, exR):
            w = smooth(np.hypot(P[:, 0] - ex, P[:, 2] - ez), R) * front
            d[:, 0] += (1 if ex < mid else -1) * opt.get('shift', 0.0) * w
            d[:, 2] += opt.get('lift', 0.0) * w
    if 'drop' in opt:
        zt, zbt, dr = opt['dropTop'], opt['dropBot'], opt['drop']
        t = np.clip((zt - P[:, 2]) / (zt - zbt), 0, 1); below = np.clip(1 - (zbt - P[:, 2]) / 0.04, 0, 1)
        # 좌우 감쇠의 가운데 = 얼굴 가운데(두 눈 중점). 머리 뼈 x(0)로 두면 얼굴이 옆으로 비낀 사람은 한쪽만 내려가 입이 기울고 턱 끝이 쏠림(이윤준 2cm → 입 +8°, 턱 1.7cm)
        cxf = (opt['eyeL'] + opt['eyeR']) / 2 if 'eyeL' in opt else bx
        d[:, 2] -= dr * np.where(P[:, 2] > zbt, t, below) * front * np.clip(1 - np.abs(P[:, 0] - cxf) / 0.085, 0, 1)
    if 'noseDrop' in opt:
        d[:, 2] -= opt['noseDrop'] * smooth(np.hypot(P[:, 0] - opt['noseX'], P[:, 2] - opt['noseZ']), opt.get('noseR', 0.022)) * front
    for i, v in enumerate(bust.data.vertices): v.co += Vector(d[i])
    PN = np.array([tuple(v.co) for v in bust.data.vertices])

# ── 얼굴 바로 세우기(2026-10-30 이윤준 '삐뚤어짐'): yawDeg = 머리 전체 돌아감을 머리 뼈 축으로 바로잡음(대칭면 맞춤으로 잰 값), 코끝을 몸 가운데(x)로
if 'yawDeg' in opt:
    hp = Vector(head_b)
    bust.data.transform(Matrix.Translation(hp) @ Matrix.Rotation(math.radians(-opt['yawDeg']), 4, 'Z') @ Matrix.Translation(-hp))
    PN = np.array([tuple(v.co) for v in bust.data.vertices])
dx_c = 0.0
for _ in range(3):                                                   # 코끝 찾는 창이 코끝을 따라오도록 몇 번
    Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
    d_ = float(head_b.x - Ln['nose'][0]); dx_c += d_
    bust.data.transform(Matrix.Translation((d_, 0, 0))); PN = np.array([tuple(v.co) for v in bust.data.vertices])
Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
dy2 = ((Lo['nose'][1] - Ln['nose'][1]) + (Lo['chin'][1] - Ln['chin'][1])) / 2 + float(opt.get('alignDy', 0.0))
ex_ = float(opt.get('centerDx', 0.0))
bust.data.transform(Matrix.Translation((ex_, dy2, 0))); PN = np.array([tuple(v.co) for v in bust.data.vertices]); Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
print('CENTER head x %+.1fmm y %+.1fmm → nose x %.4f' % ((dx_c + ex_) * 1000, dy2 * 1000, Ln['nose'][0]), flush=True)
# ── 높이: 턱 끝을 v2 턱 끝에 맞춤(턱 끝~옷깃 거리 = v2 — 머리가 어깨에 얹힌 듯 목이 짧아 보이던 것)
Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
dz = float(Lo['chin'][2] - Ln['chin'][2]) + float(opt.get('chinDz', 0.0))
bust.data.transform(Matrix.Translation((0, 0, dz))); bust.data.update()
PN = np.array([tuple(v.co) for v in bust.data.vertices]); Ln = landmarks(PN, head_b.x, head_b.z + 0.02)
z_chin = float(Ln['chin'][2])
print('NOSE x v2 %.4f v3 %.4f chin x v2 %.4f v3 %.4f' % (Lo['nose'][0], Ln['nose'][0], Lo['chin'][0], Ln['chin'][0]), flush=True)
print('CHIN v2 %.3f v3 before %.3f → lift %+.1fmm; top v2 %.3f v3 %.3f; nose dy %+.1f chin dy %+.1f mm' % (Lo['chin'][2], z_chin - dz, dz * 1000, PB[:, 2].max(), PN[:, 2].max(),
    (Ln['nose'][1] - Lo['nose'][1]) * 1000, (Ln['chin'][1] - Lo['chin'][1]) * 1000), flush=True)

# ── 목 가운데 축·옷깃 높이
rb = np.hypot(PB[:, 0] - neck_b.x, PB[:, 1] - neck_b.y)
chest = (np.abs(PB[:, 2] - (neck_b.z - 0.10)) < 0.03) & (np.abs(PB[:, 0] - neck_b.x) < 0.06)
cidx = np.nonzero(chest)[0]; cidx = cidx[np.argsort(PB[cidx, 1])][:300]
shirt_b = np.median(CB[cidx], 0)
clothB = (np.linalg.norm(CB - shirt_b, axis=1) < 0.2) | ((CB.min(1) > 0.55) & (CB.max(1) - CB.min(1) < 0.15))
best_ = None
for zz in np.arange(neck_b.z, head_b.z + 0.05, 0.005):
    m = (np.abs(PB[:, 2] - zz) < 0.003) & (rb < 0.14)
    if m.sum() < 20: continue
    wdt = np.percentile(PB[m, 0], 99) - np.percentile(PB[m, 0], 1)
    if best_ is None or wdt < best_[0]: best_ = (wdt, zz, m)
z_mid = float(best_[1]); xs, ys = PB[best_[2], 0], PB[best_[2], 1]
AX = ((np.percentile(xs, 1) + np.percentile(xs, 99)) / 2, (np.percentile(ys, 1) + np.percentile(ys, 99)) / 2)
bvh_o = BVHTree.FromPolygons([Vector(p) for p in PB], [tuple(p.vertices) for p in body.data.polygons])
for _ in range(2):                                                   # 그 가운데에서 36 방향으로 쏴 닿는 목 겉면 = 반지름, 닿은 점 가운데로 다시 맞춤
    c0 = Vector((AX[0], AX[1], z_mid))
    Hh = np.array([tuple(h_[0]) for h_ in (bvh_o.ray_cast(c0, Vector((math.cos(q), math.sin(q), 0.0)), 0.3) for q in np.linspace(0, 2 * math.pi, 36, endpoint=False)) if h_[0] is not None])
    R_neck = float(np.median(np.hypot(Hh[:, 0] - AX[0], Hh[:, 1] - AX[1]))); AX = (float(Hh[:, 0].mean()), float(Hh[:, 1].mean()))
rcB = np.hypot(PB[:, 0] - AX[0], PB[:, 1] - AX[1]); angB = np.degrees(np.arctan2(PB[:, 1] - AX[1], PB[:, 0] - AX[0]))
def collar_at(ang, wid=20):
    m = ~clothB & (rcB < R_neck + 0.02) & (PB[:, 2] > neck_b.z - 0.10) & (PB[:, 2] < z_chin) & (np.abs(((angB - ang + 180) % 360) - 180) < wid)
    return float(np.percentile(PB[m, 2], 3)) if m.sum() >= 3 else None
collar_f = collar_at(-90); collar_b = float(opt.get('collarBack', collar_at(90) or collar_f + 0.04))
depth = 2 * R_neck
tilt = float(opt['tilt']) if 'tilt' in opt else float(np.clip(math.degrees(math.atan2(collar_b - collar_f, depth)), 8.0, 30.0))
ta = math.radians(tilt)
n_pl = Vector((0.0, -math.sin(ta), math.cos(ta)))                    # 평면 법선(위·앞) — 평면은 뒤로 갈수록 높아짐
e1 = Vector((1.0, 0.0, 0.0)); e2 = n_pl.cross(e1).normalized()        # 평면 안 좌우·앞뒤
if e2.y < 0: e2 = -e2
print('AXIS %.3f %.3f neck r %.3f (z %.3f) collar front %.3f back %.3f tilt %.1f° chin→collar(front) %.1fcm' % (AX[0], AX[1], R_neck, z_mid, collar_f, collar_b, tilt, (z_chin - collar_f) * 100), flush=True)

def front_y(P, mask, z):
    m = mask & (np.abs(P[:, 0] - AX[0]) < 0.02) & (np.abs(P[:, 2] - z) < 0.008) & (np.hypot(P[:, 0] - AX[0], P[:, 1] - AX[1]) < 0.16)
    return float(P[m, 1].min())
cutH = float(opt.get('cutH', 0.035)); gapMin = float(opt.get('gapMin', 0.012))
z_h = z_chin - cutH; P0_h = Vector((AX[0], front_y(PN, np.ones(len(PN), bool), z_h), z_h))
cutB = float(opt.get('cutB', 0.045))
for _ in range(40):
    z_b = collar_f + cutB; P0_b = Vector((AX[0], front_y(PB, ~clothB, z_b), z_b))
    if n_pl.dot(P0_h - P0_b) >= gapMin or cutB < 0.015: break
    cutB -= 0.001
gap = n_pl.dot(P0_h - P0_b)
print('PLANES head: chin-%.1fcm at y %.3f | body: collar+%.1fcm at y %.3f | gap %.1fcm | body plane above back collar %.1fcm' % (cutH * 100, P0_h.y, cutB * 100, P0_b.y, gap * 100,
    (z_b + math.tan(ta) * (AX[1] + R_neck - P0_b.y) - collar_b) * 100), flush=True)

# ── 자르기
def comps(bm):
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen: continue
        st = [f]; seen.add(f.index); cur = []
        while st:
            g = st.pop(); cur.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); st.append(h)
        out.append(cur)
    return out

def boundary_loops(bm):
    be = [e for e in bm.edges if len(e.link_faces) == 1]
    nxt = {}
    for e in be:
        nxt.setdefault(e.verts[0], []).append(e); nxt.setdefault(e.verts[1], []).append(e)
    used = set(); loops = []
    for e0 in be:
        if e0 in used: continue
        v0, v = e0.verts; lp = [v0]; e = e0; used.add(e0)
        while v != v0:
            lp.append(v)
            cand = [x for x in nxt[v] if x not in used]
            if not cand: break
            e = cand[0]; used.add(e); v = e.other_vert(v)
        loops.append(lp)
    return loops

def loop_stats(lp):
    P = np.array([tuple(v.co) for v in lp]); per = float(np.linalg.norm(P - np.roll(P, 1, 0), axis=1).sum())
    return P.mean(0), per

def cap_loop(bm, lp, uvl):
    es = [bm.edges.get((lp[i], lp[(i + 1) % len(lp)])) for i in range(len(lp))]
    es = list(set(e for e in es if e is not None))
    if len(es) < 3: return
    r = bmesh.ops.holes_fill(bm, edges=es, sides=0)
    if not r['faces']:                                                # 머리카락 끝·Tripo 작은 구멍: 못 메우면 가운데 점 부채꼴로(정면에서 검은 점으로 보임)
        c_ = sum((v.co for v in lp), Vector()) / len(lp); nv = bm.verts.new(c_); r = {'faces': []}
        for e_ in es:
            try: r['faces'].append(bm.faces.new((e_.verts[0], e_.verts[1], nv)))
            except ValueError: pass
    for f in r['faces']:
        for l in f.loops:
            for l2 in l.vert.link_loops:
                if l2.face != f: l[uvl].uv = l2[uvl].uv; break

def is_neck_loop(c, per):
    return np.hypot(c[0] - AX[0], c[1] - AX[1]) < R_neck and per > 0.15

# 새 머리
bm = bmesh.new(); bm.from_mesh(bust.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=P0_h, plane_no=n_pl, dist=1e-7, clear_outer=False, clear_inner=True)
bm.faces.ensure_lookup_table(); bm.faces.index_update()
cs = comps(bm); cs.sort(key=len, reverse=True); kill = []
for c in cs[1:]:
    zs = [v.co.z for f in c for v in f.verts]
    if max(zs) < z_chin + 0.03 or len(c) < 12: kill += c            # 턱 아래·아주 작은 조각(머리카락 조각은 남김)
print('HEAD comps', len(cs), 'removed faces', len(kill), flush=True)
if kill: bmesh.ops.delete(bm, geom=list(set(kill)), context='FACES')
uvl = bm.loops.layers.uv.active
lps = boundary_loops(bm); st = [(loop_stats(l), l) for l in lps]
mainh = [x for x in st if is_neck_loop(x[0][0], x[0][1])]
print('HEAD loops', len(lps), 'perims', [round(x[0][1], 3) for x in sorted(st, key=lambda q: -q[0][1])][:8], flush=True)
mainh = max(mainh, key=lambda x: x[0][1])[1]
for x in st:
    if x[1] is not mainh: cap_loop(bm, x[1], uvl)
bm.to_mesh(bust.data); bm.free(); bust.data.update()

# 몸
bm = bmesh.new(); bm.from_mesh(body.data); bm.transform(mw)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
near = [v for v in bm.verts if math.hypot(v.co.x - AX[0], v.co.y - AX[1]) < 0.30]; ns = set(near)
ne = [e for e in bm.edges if e.verts[0] in ns and e.verts[1] in ns]
nf = [f for f in bm.faces if all(v in ns for v in f.verts)]
r = bmesh.ops.bisect_plane(bm, geom=near + ne + nf, plane_co=P0_b, plane_no=n_pl, dist=1e-7, clear_outer=False, clear_inner=False)
cut = set(g for g in r['geom_cut'] if isinstance(g, bmesh.types.BMEdge))
top = max((v for v in bm.verts if math.hypot(v.co.x - AX[0], v.co.y - AX[1]) < 0.2), key=lambda v: v.co.z)
seen = set(top.link_faces); st_ = list(seen)
while st_:
    f = st_.pop()
    for e in f.edges:
        if e in cut: continue
        for g in e.link_faces:
            if g not in seen: seen.add(g); st_.append(g)
zlow = min(n_pl.dot(v.co - P0_b) for f in seen for v in f.verts)
print('BODY delete faces', len(seen), 'of', len(bm.faces), 'lowest above plane %.1fmm' % (zlow * 1000), flush=True)
bmesh.ops.delete(bm, geom=list(seen), context='FACES')
bm.faces.ensure_lookup_table(); bm.faces.index_update()
cs = comps(bm); cs.sort(key=len, reverse=True); kill = []
for c in cs[1:]:
    Pc = np.array([tuple(v.co) for f in c for v in f.verts])
    if np.hypot(Pc[:, 0] - AX[0], Pc[:, 1] - AX[1]).min() < 0.16 and len(c) < 400 and abs(n_pl.dot(Vector(Pc.mean(0)) - P0_b)) < 0.12: kill += c   # 지우고 떠 남은 옛 머리카락 조각
print('BODY comps', len(cs), 'removed faces', len(kill), flush=True)
if kill: bmesh.ops.delete(bm, geom=list(set(kill)), context='FACES')
uvl = bm.loops.layers.uv.active
lps = boundary_loops(bm); st = [(loop_stats(l), l) for l in lps]
print('BODY loops', len(lps), 'perims', [round(x[0][1], 3) for x in sorted(st, key=lambda q: -q[0][1])][:8], flush=True)
mainb = max([x for x in st if is_neck_loop(x[0][0], x[0][1])], key=lambda x: x[0][1])[1]
for x in st:
    if x[1] is not mainb and x[0][1] < 0.3: cap_loop(bm, x[1], uvl)
bm.transform(mw.inverted()); bm.to_mesh(body.data); bm.free(); body.data.update()

# ── 합치기 전: UV 아틀라스·재질·가중치
for l in body.data.uv_layers.active.data: l.uv = (l.uv.x * 4096 / ATW, l.uv.y)
for l in bust.data.uv_layers.active.data: l.uv = ((4096 + l.uv.x * HS) / ATW, 1 - HS / ATH + l.uv.y * HS / ATH)
body.data.uv_layers.active.name = 'UVMap'; bust.data.uv_layers.active.name = 'UVMap'
atlas = bpy.data.images.load(atlas_p)
mat = body.data.materials[0]
for nd in mat.node_tree.nodes:
    if nd.type == 'TEX_IMAGE': nd.image = atlas
bust.data.materials.clear(); bust.data.materials.append(mat)
for p in bust.data.polygons: p.material_index = 0
for g in body.vertex_groups: bust.vertex_groups.new(name=g.name)
gH, gN = bust.vertex_groups['Head'], bust.vertex_groups['neck']
gH.add(list(range(len(bust.data.vertices))), 1.0, 'REPLACE')
for v in bust.data.vertices:
    d = n_pl.dot(v.co - P0_h)
    if d < 0.03:
        t = max(0.0, d / 0.03); t = t * t * (3 - 2 * t); wH = 0.6 + 0.4 * t
        gH.add([v.index], wH, 'REPLACE'); gN.add([v.index], 1 - wH, 'REPLACE')
bust.parent = arm; bust.matrix_parent_inverse = arm.matrix_world.inverted()
bust.modifiers.new('Armature', 'ARMATURE').object = arm
bpy.ops.object.select_all(action='DESELECT')
bust.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
for i in range(len(body.data.materials) - 1, 0, -1): body.data.materials.pop(index=i)

# ── 띠로 잇기(한 메시 안에서)
mw = body.matrix_world; mwi = mw.inverted()
bm = bmesh.new(); bm.from_mesh(body.data); bm.transform(mw)
uvl = bm.loops.layers.uv.active; dvl = bm.verts.layers.deform.verify()
lps = boundary_loops(bm); st = [(loop_stats(l), l) for l in lps]
def pick(P0):
    c_ = [x for x in st if is_neck_loop(x[0][0], x[0][1]) and abs(n_pl.dot(Vector(x[0][0]) - P0)) < 0.003]
    return max(c_, key=lambda x: x[0][1])[1]
LH = pick(P0_h); LB = pick(P0_b)
def polar(lp):
    P = np.array([tuple(v.co) for v in lp])
    U = P @ np.array(e1); V = P @ np.array(e2)
    A = U * np.roll(V, -1) - np.roll(U, -1) * V; ar = A.sum() / 2
    cu = ((U + np.roll(U, -1)) * A).sum() / (6 * ar); cv = ((V + np.roll(V, -1)) * A).sum() / (6 * ar)
    if ar < 0: lp = lp[::-1]; U = U[::-1]; V = V[::-1]
    th = np.arctan2(V - cv, U - cu)
    i0 = int(np.argmin(np.abs(((th + math.pi / 2 + math.pi) % (2 * math.pi)) - math.pi)))   # 앞(−V) 에서 시작
    lp = lp[i0:] + lp[:i0]; th = np.roll(th, -i0); U = np.roll(U, -i0); V = np.roll(V, -i0)
    un = np.unwrap(th); r = np.hypot(U - cu, V - cv)
    back = int((np.diff(un) < -1e-4).sum())
    return lp, cu, cv, un - un[0] + (th[0] + math.pi / 2), r, back
LH, cuH, cvH, thH, rH, backH = polar(LH); LB, cuB, cvB, thB, rB, backB = polar(LB)
print('LOOP head n %d centre %.3f %.3f r %.3f..%.3f backtrack %d | body n %d centre %.3f %.3f r %.3f..%.3f backtrack %d' % (len(LH), cuH, cvH, rH.min(), rH.max(), backH, len(LB), cuB, cvB, rB.min(), rB.max(), backB), flush=True)
NR = 120
TG = np.linspace(0, 2 * math.pi, NR, endpoint=False)
def rfun(th, r):
    o = np.argsort(th % (2 * math.pi)); t_ = (th % (2 * math.pi))[o]; r_ = r[o]
    return lambda q: np.interp(np.asarray(q) % (2 * math.pi), t_, r_, period=2 * math.pi)
def sm_circ(x, k):
    return np.array([x[(np.arange(i_ - k, i_ + k + 1)) % len(x)].mean() for i_ in range(len(x))])
rHg = sm_circ(rfun(thH, rH)(TG), 2); rBg = sm_circ(rfun(thB, rB)(TG), 2)
E1, E2 = np.array(e1), np.array(e2); NP = np.array(n_pl)
# 수건: 몸 고리가 수건 겉을 지나는 각도(이윤준)는 수건을 건드리지 않고, 띠가 수건 윗단(둥근 어깨)을 이룸. 나머지 각도는 두 고리를 평균 모양으로 맞춤
def is_towel_col(c): return (c.min() > 0.55) and (c.max() - c.min() < 0.15)
UVS = bm.loops.layers.uv.active
def vert_col(v):
    f_ = next(iter(v.link_faces)); l_ = next(l for l in f_.loops if l.vert == v); uv_ = l_[UVS].uv
    h_, w_ = bpix.shape[:2]; return bpix[min(h_ - 1, int(uv_.y * h_)), min(w_ - 1, int(uv_.x * 6144 / 4096 * w_))]
towB = np.array([is_towel_col(vert_col(v)) for v in LB], float)
tauG = np.interp(TG, thB % (2 * math.pi), towB, period=2 * math.pi) if False else np.array([towB[int(np.argmin(np.abs(((thB % (2 * math.pi)) - q + math.pi) % (2 * math.pi) - math.pi)))] for q in TG])
has_towel = tauG.mean() > 0.08
tauRaw = (sm_circ(tauG.astype(float), 4) > 0.5).astype(float)        # 띠 색(수건/피부)은 실제 수건 칸만(앞뒤 9칸 다수결 — 칸마다 뒤바뀌면 톱니로 보임), 모양 맞추기는 조금 넓게
tauG = np.clip(sm_circ(np.array([tauG[(np.arange(i_ - 4, i_ + 5)) % NR].max() for i_ in range(NR)]), 4), 0, 1) if has_towel else np.zeros(NR)
print('TOWEL share of body ring %.2f' % float(tauG.mean()), flush=True)
rMg = (rHg + rBg) / 2
rTH = rMg * (1 - tauG) + rHg * tauG; rTB = rMg * (1 - tauG) + rBg * tauG   # 맞출 반지름(수건 각도는 각자 그대로)
print('RING radius (front,side,back,side) head %s body %s' % (np.round(rHg[::NR // 4], 3).tolist(), np.round(rBg[::NR // 4], 3).tolist()), flush=True)
pf = lambda arr: (lambda q: np.interp(np.asarray(q) % (2 * math.pi), TG, arr, period=2 * math.pi))
fHs, fBs, fTH, fTB, fTau = pf(rHg), pf(rBg), pf(rTH), pf(rTB), pf(tauG)
L_m = float(opt.get('morphL', 0.03))                                  # 고리 모양 맞추기: 위아래 L_m 에 걸쳐(턱 아래·어깨처럼 목 겉면에서 먼 정점은 움직이지 않음)
sv_h = n_pl.dot(P0_h); sv_b = n_pl.dot(P0_b)
if has_towel: cuM, cvM = cuH, cvH                                     # 수건이 있으면 몸 고리 가운데는 믿을 수 없음 → 새 목 가운데 기준
else: cuM, cvM = (cuH + cuB) / 2, (cvH + cvB) / 2                     # 두 고리 가운데도 평균 쪽으로(목 기울기가 띠 한곳에 몰려 턱이 지지 않게)
print('LOOP centre offset (head-body) x %.1f y %.1f mm' % ((cuH - cuB) * 1000, (cvH - cvB) * 1000), flush=True)
nmv = 0
for v in bm.verts:
    p = np.array(tuple(v.co)); h = float(p @ NP)
    if h >= sv_h - 1e-5 and h - sv_h < L_m:
        cu, cv, f_, g_, d_, keep = cuH, cvH, fHs, fTH, max(0.0, h - sv_h), False
    elif h <= sv_b + 1e-5 and sv_b - h < L_m:
        cu, cv, f_, g_, d_, keep = cuB, cvB, fBs, fTB, max(0.0, sv_b - h), True
    else: continue
    u, v2 = float(p @ E1) - cu, float(p @ E2) - cv
    th = (math.atan2(v2, u) + math.pi / 2) % (2 * math.pi); rv = math.hypot(u, v2)
    r0 = float(f_(th)); near_w = max(0.0, 1.0 - max(0.0, rv - r0) / 0.02)
    if near_w <= 0 or rv < 1e-4: continue
    x = 1 - d_ / L_m; w = x * x * (3 - 2 * x) * near_w
    if keep: w *= 1 - float(fTau(th))
    k = (rv + (float(g_(th)) - r0) * w) / rv
    v.co += Vector(tuple(E1 * (u * (k - 1) + (cuM - cu) * w) + E2 * (v2 * (k - 1) + (cvM - cv) * w))); nmv += 1
print('MORPH verts', nmv, flush=True)
# 띠 단: 맞춘 고리를 새 가운데 기준 극좌표로 다시 재서, 위(새 목)→아래(몸 고리)로 보간. 수건 각도는 둥근 윗단(원호)
def polar_about(lp):
    P = np.array([tuple(v.co) for v in lp]); u = P @ E1 - cuM; v_ = P @ E2 - cvM
    return (np.arctan2(v_, u) + math.pi / 2) % (2 * math.pi), np.hypot(u, v_)
tH2, rH2 = polar_about(LH); tB2, rB2 = polar_about(LB)
rHr = sm_circ(rfun(tH2, rH2)(TG), 1); rBr = sm_circ(rfun(tB2, rB2)(TG), 1)
gap_v = sv_h - sv_b
cc_b = cuM * E1 + cvM * E2
nrr = int(opt.get('rings', 3))
new_faces = []
rr_ = []
thg = TG - math.pi / 2
for k_ in range(1, nrr + 1):
    t = k_ / (nrr + 1)
    arc = math.sqrt(1 - (1 - t) ** 2)
    rt = rHr + (rBr - rHr) * (t * (1 - tauG) + arc * tauG)
    # 높이: 수건 각도에서는 몸 고리 쪽에서 먼저 올라감(둥근 윗단)
    ht = t * (1 - tauG) + (t * t) * tauG
    pts = np.outer(cuM + rt * np.cos(thg), E1) + np.outer(cvM + rt * np.sin(thg), E2) + np.outer(sv_h - gap_v * ht, NP)
    rr_.append([bm.verts.new(tuple(q)) for q in pts])
bm.verts.index_update(); bm.verts.ensure_lookup_table()
# 띠 UV: v2 목 피부 한 점(칠하는 쪽에서 이 둘레를 같은 색으로 칠함) — 몸 고리에서 수건이 아닌 앞쪽 면의 무게중심. 수건 칸은 수건 한 점
def face_uv(v):
    f_ = next(iter(v.link_faces)); return sum((l[uvl].uv for l in f_.loops), Vector((0, 0))) / len(f_.loops)
skin_vs = [v for v, t_ in zip(LB, towB) if t_ < 0.5 and not (np.linalg.norm(vert_col(v) - shirt_b) < 0.2)]
skin_vs.sort(key=lambda v: abs(((polar_about([v])[0][0] + math.pi) % (2 * math.pi)) - math.pi))
UVBAND = face_uv(skin_vs[0])
UVTOW = None
if has_towel:
    tv = [v for v, t_ in zip(LB, towB) if t_ > 0.5]; tv.sort(key=lambda v: -float(vert_col(v).sum())); UVTOW = face_uv(tv[len(tv) // 2])
print('BAND uv %.4f %.4f towel uv %s rings %d x %d' % (UVBAND.x, UVBAND.y, UVTOW, nrr, NR), flush=True)
def blend_w(v0, wt0, v1, wt1, vn):
    tot = {}
    for gi, w in v0[dvl].items(): tot[gi] = tot.get(gi, 0) + w * wt0
    for gi, w in v1[dvl].items(): tot[gi] = tot.get(gi, 0) + w * wt1
    for gi, w in tot.items():
        if w > 1e-4: vn[dvl][gi] = w
def nearest_idx(th, q):
    return int(np.argmin(np.abs(((th % (2 * math.pi)) - q + math.pi) % (2 * math.pi) - math.pi)))
for k_, rg in enumerate(rr_):
    t = (k_ + 1) / (nrr + 1)
    for j_, vn in enumerate(rg):
        q = float(TG[j_]); blend_w(LH[nearest_idx(thH, q)], 1 - t, LB[nearest_idx(thB, q)], t, vn)
def mkface(vs, uvp):
    c_ = sum((v.co for v in vs), Vector()) / len(vs)
    nn_ = (vs[1].co - vs[0].co).cross(vs[2].co - vs[0].co)
    pc = np.array(tuple(c_)); rad = pc - (cc_b + NP * float(pc @ NP))
    if np.dot(np.array(tuple(nn_)), rad) < 0: vs = vs[::-1]
    f = bm.faces.new(vs); f.smooth = True
    for l in f.loops: l[uvl].uv = uvp
    new_faces.append(f)
def uv_at(jj, lower):                                                 # 수건 각도의 아래쪽 단은 수건 색
    return UVTOW if (lower and UVTOW is not None and tauRaw[jj % NR] > 0.5) else UVBAND
def zip_strip(loopA, thA, ringB, lower):
    """고리(정점 수 다름)와 띠 단을 각도 순서로 삼각형으로 이음 — T자 이음 없음"""
    na, nb = len(loopA), len(ringB); i_ = j_ = 0
    ua = np.unwrap(thA % (2 * math.pi)); ua = ua - ua[0]; ub = TG
    while i_ < na or j_ < nb:
        an = ua[i_ + 1] if i_ + 1 < na else 2 * math.pi; bn = ub[j_ + 1] if j_ + 1 < nb else 2 * math.pi
        if (j_ >= nb) or (i_ < na and an <= bn):
            mkface([loopA[i_ % na], loopA[(i_ + 1) % na], ringB[j_ % nb]], uv_at(j_, lower)); i_ += 1
        else:
            mkface([loopA[i_ % na], ringB[(j_ + 1) % nb], ringB[j_ % nb]], uv_at(j_, lower)); j_ += 1
zip_strip(LH, thH, rr_[0], True)                                      # 수건 각도는 새 목 고리부터 수건 색(목·수건 경계 = 매끈한 평면 고리)
for k_ in range(nrr - 1):
    for j_ in range(NR):
        mkface([rr_[k_][j_], rr_[k_][(j_ + 1) % NR], rr_[k_ + 1][(j_ + 1) % NR], rr_[k_ + 1][j_]], uv_at(j_, True))
zip_strip(LB, thB, rr_[-1], True)
bm.normal_update()
b_left = len([e for e in bm.edges if len(e.link_faces) == 1]); non = len([e for e in bm.edges if len(e.link_faces) > 2])
print('BRIDGE faces', len(new_faces), 'open edges left', b_left, 'non-manifold', non, flush=True)
# 띠 둘레 다듬기: 띠와 위아래 1.5cm 정점을 이웃 평균으로 몇 번(법선 평균과 함께 턱·단차를 없앰)
bm.verts.index_update(); bm.verts.ensure_lookup_table()
Pv = np.array([tuple(v.co) for v in bm.verts]); hv = Pv @ NP
Wv = np.zeros(len(Pv)); Rng = float(opt.get('smoothR', 0.015))
Wv[(hv <= sv_h) & (hv >= sv_b)] = 1.0
up = (hv > sv_h) & (hv < sv_h + Rng); Wv[up] = 1 - (hv[up] - sv_h) / Rng
dn = (hv < sv_b) & (hv > sv_b - Rng); Wv[dn] = 1 - (sv_b - hv[dn]) / Rng
RCv = np.hypot(Pv @ E1 - cuM, Pv @ E2 - cvM)
Wv[RCv > rTH.max() + 0.025] = 0.0                                     # 목 둘레만
idx = np.nonzero(Wv > 0)[0]
adj = {int(i): [e.other_vert(bm.verts[int(i)]).index for e in bm.verts[int(i)].link_edges] for i in idx}
for it in range(int(opt.get('smoothIter', 12))):
    Q = Pv.copy()
    for i in idx:
        i = int(i); nb_ = adj[i]
        if nb_: Q[i] = Pv[i] + 0.5 * Wv[i] * (Pv[nb_].mean(0) - Pv[i])
    Pv = Q
for i in idx: bm.verts[int(i)].co = Vector(Pv[int(i)])
# 가중치: 띠 위아래 3cm 에 걸쳐 Head 를 매끈하게(이음 띠에 회전이 몰려 고개 돌릴 때 띠가 접히던 것) — 위 = Head, 아래 = v2 그대로(neck 등), 사이 보간
vg_i = {g.name: g.index for g in body.vertex_groups}
HS_ = {vg_i[n_] for n_ in ('Head', 'head_end', 'headfront') if n_ in vg_i}; gi_h, gi_n = vg_i['Head'], vg_i['neck']
h_lo, h_up = sv_b - 0.03, sv_h + 0.03; rlim_ = float(rTH.max()) + 0.015; nw_ = 0
for v in bm.verts:
    h = float(np.array(tuple(v.co)) @ NP)
    if h <= h_lo or h >= h_up: continue
    t = (h - h_lo) / (h_up - h_lo); sw = t * t * (3 - 2 * t)
    r_ = math.hypot(float(np.array(tuple(v.co)) @ E1) - cuM, float(np.array(tuple(v.co)) @ E2) - cvM)
    w_r = 1.0 if h > sv_b else max(0.0, min(1.0, 1 - (r_ - rlim_) / 0.03))   # 몸 쪽은 목 둘레만(옷깃·수건·어깨는 v2 그대로)
    dv = v[dvl]; other = {g_: w_ for g_, w_ in dv.items() if g_ not in HS_}; wc = sum(w_ for g_, w_ in dv.items() if g_ in HS_)
    tot = sum(other.values())
    if tot < 1e-4: other = {gi_n: 1.0}; tot = 1.0
    wH = sw * w_r + wc * (1 - w_r)
    dv.clear()
    for g_, w_ in other.items():
        if w_ / tot * (1 - wH) > 1e-4: dv[g_] = w_ / tot * (1 - wH)
    if wH > 1e-4: dv[gi_h] = wH
    nw_ += 1
print('WEIGHTS blended verts', nw_, flush=True)
for f in bm.faces: f.smooth = True
bm.normal_update()
bm.transform(mwi); bm.to_mesh(body.data); bm.free(); body.data.update()
# 가져온 법선을 버리고 형상에서 다시 계산(UV 이음매로 나뉘던 정점을 붙였으므로 매끈)
bpy.context.view_layer.objects.active = body
try: bpy.ops.mesh.customdata_custom_splitnormals_clear()
except Exception as ex: print('clear normals:', ex, flush=True)
body.data.shade_smooth()
print('custom normals left', body.data.has_custom_normals, flush=True)

# ── 칠하기·지도용 값(neck_paint.py)
me = body.data; me.calc_loop_triangles(); mw = body.matrix_world
P = np.array([tuple(mw @ v.co) for v in me.vertices], np.float32)
T = np.array([list(t.vertices) for t in me.loop_triangles], np.int32)
uvd = me.uv_layers.active.data
UV = np.array([[tuple(uvd[li].uv) for li in t.loops] for t in me.loop_triangles], np.float32)
ISH = UV[:, :, 0].mean(1) > 4096 / ATW                               # 새 머리 면
np.savez(out_p + '.npz', P=P, T=T, UV=UV, ISH=ISH, n=NP, s_h=sv_h, s_b=sv_b, uvb=np.array(tuple(UVBAND)), uvt=np.array(tuple(UVTOW)) if UVTOW is not None else np.array([-1.0, -1.0]), z_chin=z_chin, chin_y=float(Ln['chin'][1]),
    cc=np.array([cuM, cvM]), e1=E1, e2=E2, rm=rTH, collar_f=collar_f, collar_b=collar_b, shirt_b=shirt_b, ax=np.array(AX), r_neck=R_neck)
if act: arm.animation_data.action = act
bpy.ops.export_scene.gltf(filepath=out_p, export_format='GLB', export_animations=True, export_skins=True, export_yup=True, export_image_format='JPEG')
print('EXPORTED', out_p, 'verts', len(me.vertices), 'tris', len(T), flush=True)
