# 배달 자전거 소품(concept/art/env/props/delivery_bicycle.glb — 삼각형 수프, 한 메시)을 주행용으로 나눈다(docs/09 2-6, 8단계 — 크레딧 0)
# 사용: "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tools/bike_split.py -- <src.glb> <dst.fbx> [--saddle-up 0.12] [--crank 0.16]
#   --saddle-up m : 안장·안장 기둥 윗부분을 m 만큼 곧게 올린다(기둥은 죔쇠 위에서 늘어남 — 뒤로 눕히면 배달통 앞면에 엉덩이가 닿아서 수직으로)
#   --crank m     : 크랭크 팔 길이(BB → 페달 가운데)를 m 로(팔은 늘이고 페달은 통째로 옮김, 체인링·축은 그대로)
#   2026-10-26 대표 결정(docs/14 8장 3번): 1.72m 시우에 작음 → --saddle-up 0.12 --crank 0.16 (원래 안장 BB 위 0.50m·크랭크 0.10m)
# 결과 FBX(정면 = Unity +Z, 위 = +Y, 미터):
#   Bike(빈 루트) ─ Frame(차체·바구니·배달통·흙받이·포크 — 움직이지 않음)
#                 ├ WheelF / WheelR(앞·뒤 바퀴 — 원점 = 바퀴 축, 굴러감)
#                 ├ Crank(크랭크 팔·페달·체인링 — 원점 = BB 축, 페달질) ─ PedalL · PedalR(페달 가운데 빈 점, 크랭크와 같이 돎)
#                 └ 빈 점: Saddle(안장 윗면 가운데) · GripL · GripR(핸들 손잡이 가운데) · FootDown(정차 때 왼발 땅 자리)
# 나누는 규칙(삼각형 무게중심):
#   바퀴 = 바퀴살·허브(축에서 타이어 안쪽 원 − 1cm 안, |x| < 3.2cm) — 타이어·림·흙받이는 차체(돌려도 같아 보이고 흙받이와 못 가름). 굴림 반지름 R = 축 높이 − 땅 닿는 점 · |x| < 3.2cm(포크 다리·짐받이 지주·체인은 그 밖)  — 축·R 은 바퀴 아래쪽 타이어 정점으로 원 맞춤
#   크랭크 = BB 에서 19cm 안 · |x| ≥ 6cm(크랭크 팔·페달) 또는 3~6cm 이면서 11cm 안(체인링·축), 뒤쪽 체인 스테이(BB 뒤 10cm 넘고 |x| < 9cm)는 뺌
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst = argv[0], argv[1]
def _opt(k, d):
    return float(argv[argv.index(k) + 1]) if k in argv else d
SADDLE_UP = _opt('--saddle-up', 0.0)
CRANK_LEN = _opt('--crank', 0.0)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = np.array([tuple(v.co) for v in me.vertices])
tris = np.array([tuple(p.vertices) for p in me.polygons])
cen = co[tris].mean(1)


def circle_fit(p):
    """(y, z) 점들 → 중심 · 반지름(대수 최소 제곱)"""
    A = np.c_[2 * p[:, 0], 2 * p[:, 1], np.ones(len(p))]
    b = (p ** 2).sum(1)
    sol = np.linalg.lstsq(A, b, rcond=None)[0]
    c = sol[:2]
    return c, math.sqrt(sol[2] + (c ** 2).sum())


info = {}
wheels = {}
rng = np.random.default_rng(3)
for name, sel in (('WheelF', co[:, 1] < -0.15), ('WheelR', co[:, 1] > 0.15)):
    pts = co[sel & (np.abs(co[:, 0]) < 0.03) & (co[:, 2] < 0.75)][:, 1:3]
    # RANSAC: 세 점 원 중 타이어 바깥 면(정점이 가장 많은 원, 반지름 0.25~0.36m)
    best, bc, br = -1, None, 0.0
    for _ in range(4000):
        a, b, c = pts[rng.choice(len(pts), 3, replace=False)]
        d = 2 * (a[0] * (b[1] - c[1]) + b[0] * (c[1] - a[1]) + c[0] * (a[1] - b[1]))
        if abs(d) < 1e-9:
            continue
        ux = ((a @ a) * (b[1] - c[1]) + (b @ b) * (c[1] - a[1]) + (c @ c) * (a[1] - b[1])) / d
        uy = ((a @ a) * (c[0] - b[0]) + (b @ b) * (a[0] - c[0]) + (c @ c) * (b[0] - a[0])) / d
        r = math.hypot(a[0] - ux, a[1] - uy)
        if not (0.25 < r < 0.36):
            continue
        n = int((np.abs(np.hypot(pts[:, 0] - ux, pts[:, 1] - uy) - r) < 0.004).sum())
        if n > best:
            best, bc, br = n, np.array([ux, uy]), r
    inl = pts[np.abs(np.hypot(pts[:, 0] - bc[0], pts[:, 1] - bc[1]) - br) < 0.006]
    c, r = circle_fit(inl)
    r_in = r
    # 굴림 반지름 = 축 높이 − 바퀴 밑 땅 닿는 점(맞춘 원은 타이어 안쪽 면일 수 있어 바깥 면으로 다시)
    under = pts[np.abs(pts[:, 0] - c[0]) < 0.05]
    r = float(c[1] - under[:, 1].min())
    wheels[name] = (c, r, r_in)
    info[name] = dict(y=float(c[0]), z=float(c[1]), r=float(r), r_fit=float(r_in), inliers=int(len(inl)))
print('WHEELS', info)

# BB: 페달 정점(|x| > 0.12, z < 0.5, 두 바퀴 사이)의 가운데 → 크랭크 축
pm = (np.abs(co[:, 0]) > 0.12) & (co[:, 2] < 0.5) & (co[:, 1] > -0.1) & (co[:, 1] < 0.3)
pl = co[pm & (co[:, 0] < 0)].mean(0)
pr = co[pm & (co[:, 0] > 0)].mean(0)
bb = np.array([0.0, (pl[1] + pr[1]) / 2, (pl[2] + pr[2]) / 2])
crank_r = float(np.hypot(pl[1] - bb[1], pl[2] - bb[2]))
info['BB'] = dict(y=float(bb[1]), z=float(bb[2]), crank=crank_r, pedalL=pl.tolist(), pedalR=pr.tolist())
print('BB', info['BB'])

part = np.zeros(len(tris), int)   # 0 Frame · 1 WheelF · 2 WheelR · 3 Crank
for k, name in ((1, 'WheelF'), (2, 'WheelR')):
    c, r, r_in = wheels[name]
    rho = np.hypot(cen[:, 1] - c[0], cen[:, 2] - c[1])
    # 바퀴살·허브만 돈다(맞춘 원 − 1cm 안). 타이어·림은 돌려도 똑같아 보이고, 타이어에 바짝 붙은 흙받이와 갈라낼 수 없어서 차체에 둔다
    part[(rho < r_in - 0.01) & (np.abs(cen[:, 0]) < 0.032)] = k
rho_bb = np.hypot(cen[:, 1] - bb[1], cen[:, 2] - bb[2])
ax = np.abs(cen[:, 0])
crank = ((ax >= 0.06) & (rho_bb < 0.19)) | ((ax >= 0.03) & (ax < 0.06) & (rho_bb < 0.11))
stay = (cen[:, 1] > bb[1] + 0.10) & (ax < 0.09)
part[crank & ~stay & (part == 0)] = 3
info['tris'] = {n: int((part == k).sum()) for k, n in enumerate(['Frame', 'WheelF', 'WheelR', 'Crank'])}
print('PARTS', info['tris'])
if os.environ.get('BIKE_PARTS'):
    np.save(os.environ['BIKE_PARTS'], part)

# ───────────────────────── 치수 고치기(안장 높이·크랭크 길이) — 부위 판정은 위에서 원래 모양으로 끝냈다
tri_of = np.full(len(co), -1, int)
for ti, tv in enumerate(tris):
    tri_of[tv] = ti
info['resize'] = dict(saddle_up=SADDLE_UP, crank=CRANK_LEN or crank_r, crank_orig=crank_r)
if SADDLE_UP > 0:
    # 안장 + 안장 기둥: 죔쇠(z 0.672) 위, 배달통·짐받이(y ≥ 0.36) 앞, 몸통 폭 안. 기둥은 0.672 → 0.74 사이에서 서서히(늘어남), 그 위는 통째로
    vy, vz, vx = co[:, 1], co[:, 2], np.abs(co[:, 0])
    reg = (vz > 0.672) & (vy > 0.05) & (vy < 0.36) & (vx < 0.16)
    tri_reg = np.zeros(len(tris), bool)
    tri_reg[np.unique(tri_of[reg])] = True
    vreg = tri_reg[tri_of] & (vz > 0.66)
    t = np.clip((vz - 0.672) / (0.74 - 0.672), 0, 1)
    w = t * t * (3 - 2 * t)
    co[vreg, 2] += SADDLE_UP * w[vreg]
    info['resize']['saddle_verts'] = int(vreg.sum())
if CRANK_LEN > 0 and abs(CRANK_LEN - crank_r) > 1e-4:
    k = CRANK_LEN / crank_r
    vpart = part[tri_of]
    vx = co[:, 0]
    n_arm = n_ped = 0
    for side, pc in ((-1, pl), (1, pr)):
        u = np.array([pc[1] - bb[1], pc[2] - bb[2]]); u /= np.linalg.norm(u)
        sel = (vpart == 3) & (np.sign(vx) == side) & (np.abs(vx) >= 0.06)
        # 페달 = 크랭크 팔 끝 바깥(|x| > 0.12 — 삼각형 단위로) → 통째로, 팔 = 그 사이 → BB 쪽은 그대로·끝으로 갈수록 늘림
        ped_tri = np.zeros(len(tris), bool)
        ped_tri[np.unique(tri_of[sel & (np.abs(vx) > 0.12)])] = True
        ped = sel & ped_tri[tri_of]
        arm = sel & ~ped
        d = co[:, 1:3] - bb[1:3]
        tt = d @ u
        co[ped, 1:3] += np.outer(np.full(ped.sum(), CRANK_LEN - crank_r), u)
        grow = arm & (tt > 0)
        co[grow, 1:3] += np.outer(tt[grow] * (k - 1), u)
        n_arm += int(grow.sum()); n_ped += int(ped.sum())
        pc[1] += (CRANK_LEN - crank_r) * u[0]; pc[2] += (CRANK_LEN - crank_r) * u[1]
    crank_r = CRANK_LEN
    info['BB'] = dict(y=float(bb[1]), z=float(bb[2]), crank=crank_r, pedalL=pl.tolist(), pedalR=pr.tolist())
    info['resize']['crank_arm_verts'] = n_arm; info['resize']['pedal_verts'] = n_ped
if SADDLE_UP > 0 or CRANK_LEN > 0:
    for i, v in enumerate(me.vertices):
        v.co = Vector(co[i])
    me.update()
    cen = co[tris].mean(1)
    ax = np.abs(cen[:, 0])
    print('RESIZE', info['resize'], 'BB', info['BB'])

# 안장 윗면·손잡이·발 디딜 자리
sad = cen[(cen[:, 2] > 0.7 + SADDLE_UP) & (cen[:, 2] < 0.9 + SADDLE_UP) & (cen[:, 1] > 0.05) & (cen[:, 1] < 0.35) & (ax < 0.12)]
saddle = np.array([0.0, float(np.median(sad[:, 1])), float(sad[:, 2].max())])
gl = cen[(cen[:, 0] < -0.3)]
gr = cen[(cen[:, 0] > 0.3)]
grips = (gl.mean(0), gr.mean(0))
info['saddle'] = saddle.tolist(); info['grips'] = [g.tolist() for g in grips]

# 부위별 메시 만들기
bm_all = bmesh.new()
bm_all.from_mesh(me)
bm_all.faces.ensure_lookup_table()
mat = me.materials[0] if me.materials else None
coll = bpy.context.scene.collection
root = bpy.data.objects.new('Bike', None)
coll.objects.link(root)
origins = {0: Vector((0, 0, 0)), 1: Vector((0, wheels['WheelF'][0][0], wheels['WheelF'][0][1])),
           2: Vector((0, wheels['WheelR'][0][0], wheels['WheelR'][0][1])), 3: Vector((0, bb[1], bb[2]))}
objs = {}
for k, name in enumerate(['Frame', 'WheelF', 'WheelR', 'Crank']):
    bm = bm_all.copy()
    bm.faces.ensure_lookup_table()
    kill = [f for f in bm.faces if part[f.index] != k]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    o_ = origins[k]
    for v in bm.verts:
        v.co = v.co - o_
    m2 = bpy.data.meshes.new(name)
    bm.to_mesh(m2)
    bm.free()
    if mat:
        m2.materials.append(mat)
    ob = bpy.data.objects.new(name, m2)
    ob.location = o_
    ob.parent = root
    coll.objects.link(ob)
    objs[name] = ob


def empty(name, pos, parent):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.05
    coll.objects.link(e)
    e.parent = parent
    e.location = Vector(pos) - (parent.location if parent is not root else Vector((0, 0, 0)))
    return e


empty('Saddle', saddle, root)
empty('GripL', grips[0], root)      # Blender −x = 자전거 왼쪽? (정면 −Y 를 볼 때 왼쪽은 +x) — 이름은 Unity 쪽 BikeVisual 이 x 부호로 다시 고름
empty('GripR', grips[1], root)
empty('PedalL', pl, objs['Crank'])
empty('PedalR', pr, objs['Crank'])
empty('FootDown', (pl[0] - 0.12, bb[1] - 0.05, 0.0), root)
bpy.data.objects.remove(obj, do_unlink=True)

bpy.ops.object.select_all(action='SELECT')
os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'EMPTY', 'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=False,
                         mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP', embed_textures=False)
json.dump(info, open(os.path.splitext(dst)[0] + '.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('WROTE', dst)
