# Blender batch: v2 리깅 몸(뼈 24·Idle 동작)은 그대로 두고 머리만 Tripo 머리+상반신 메시로 바꿔 붙인다 — 2026-10-30 코치 v3
#   1) 새 메시: Tripo 정면 +X → −90° 돌려 정면 −Y(glTF +Z), 목이 가장 가는 높이의 '피부' 폭을 몸 쪽 목 폭에 맞춰 크기·자리 맞춤
#   2) 몸: 목 위쪽 피부·머리 전체를 지우고(목 둘레 반지름 밖의 옷깃·수건은 남김), 새 머리+목은 셔츠 깃 안쪽까지 내려 겹침(이음매가 깃 안에 숨음)
#   3) 새 머리 정점 가중치 = 지우기 전 몸에서 데이터 옮기기(가까운 면 보간) → 같은 뼈(Head·neck…)를 따라 움직임
#   4) 얼굴 형상 고치기(선택): 눈 둘레를 가운데로·위로, 코·입·턱을 아래로(부드러운 감쇠, 정면 −Y 쪽 얼굴만)
#   5) UV = 가로 6144×4096 한 장(왼쪽 4096² = 몸 텍스처, 오른쪽 위 2048² = 새 머리 텍스처) → 재질 하나, GLB 로 내보냄(동작 유지)
# 사용: blender -b --factory-startup --python head_swap.py -- <body_rigged.glb> <bust_tripo.glb> <head_atlas.jpg 출력> <out.glb> [json 인자]
#   json: {"eyeL":x,"eyeR":x,"eyeZ":z,"shift":m,"lift":m,"R":m, "drop":m, "dropTop":z, "dropBot":z, "inflate":m, "scale":배율 보정}
import bpy, sys, json, math
import numpy as np
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index('--') + 1:]
body_p, bust_p, atlas_p, out_p = a[:4]
opt = json.loads(a[4]) if len(a) > 4 else {}
ATW, ATH, HS = 6144, 4096, 2048
import os
DEBUG = os.environ.get('HS_DEBUG') == '1'

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
    return img, px.reshape(h, w, 4)[:, :, :3]                     # 행 0 = 아래(Blender)

def vert_colors(obj, P, pix):
    """정점마다 텍스처 색(그 정점이 처음 나오는 루프의 UV)"""
    me = obj.data; uv = me.uv_layers.active.data; h, w = pix.shape[:2]
    first = np.full(len(me.vertices), -1)
    li = np.array([l.vertex_index for l in me.loops])
    order = np.arange(len(li))[::-1]; first[li[order]] = order
    uvs = np.array([tuple(uv[i].uv) for i in first])
    x = np.clip((uvs[:, 0] * w).astype(int), 0, w - 1); y = np.clip((uvs[:, 1] * h).astype(int), 0, h - 1)
    return pix[y, x]

def neck_profile(P, col, z0, z1, cx, cy, rmax=0.11, skin=None, widest=False):
    """z0..z1 사이에서 피부 정점만 보고 높이마다 좌우 폭 → (가장 가는 z, 그 폭, 그 높이의 중심 x,y)"""
    sel = (P[:, 2] > z0) & (P[:, 2] < z1) & (np.hypot(P[:, 0] - cx, P[:, 1] - cy) < rmax)
    if skin is None: skin = np.median(col[sel & (col.sum(1) > 0.9)], 0)
    isskin = np.linalg.norm(col - skin, axis=1) < 0.16
    best = None
    for z in np.linspace(z0, z1, 48):
        m = sel & isskin & (np.abs(P[:, 2] - z) < 0.006)
        if m.sum() < 12: continue
        xs = P[m, 0]; wdt = np.percentile(xs, 97) - np.percentile(xs, 3)
        if DEBUG: print('  prof z %.3f n %d w %.3f' % (z, int(m.sum()), wdt))
        if best is None or (wdt > best[1] if widest else wdt < best[1]): best = (z, wdt, float(np.median(P[m, 0])), float(np.median(P[m, 1])))
    return best, skin

mw = body.matrix_world
PB = np.array([tuple(mw @ v.co) for v in body.data.vertices])
_, bpix = tex_np(body)
CB = vert_colors(body, PB, bpix)
def face_skin(P, col, z0, z1):
    m = (P[:, 2] > z0) & (P[:, 2] < z1) & (col.sum(1) > 1.2)                  # 수염·머리카락처럼 짙은 곳은 빼고
    idx = np.nonzero(m)[0]; idx = idx[np.argsort(P[idx, 1])][:400]           # 정면(−Y)에서 가장 앞쪽 = 뺨·코
    return np.median(col[idx], 0)
skin_b = face_skin(PB, CB, head_b.z + 0.06, head_b.z + 0.11)
print('BODY skin', np.round(skin_b * 255).astype(int).tolist(), flush=True)
nb, _ = neck_profile(PB, CB, neck_b.z - 0.01, head_b.z + 0.05, neck_b.x, neck_b.y, skin=skin_b)
wneck = nb[1]
hb_, _ = neck_profile(PB, CB, head_b.z + 0.03, head_b.z + 0.16, head_b.x, head_b.y, rmax=0.14, skin=skin_b, widest=True)
zb, wb = hb_[0], hb_[1]; bx, by = head_b.x, head_b.y
print('BODY face widest z %.3f width %.3f, neck width %.3f (neck bone %.3f, Head bone %.3f)' % (zb, wb, wneck, neck_b.z, head_b.z), flush=True)

# ── 새 머리+상반신
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
bust.data.transform(Matrix.Rotation(-math.pi / 2, 4, 'Z'))       # Tripo 정면 +X → −Y
PN = np.array([tuple(v.co) for v in bust.data.vertices])
_, npix = tex_np(bust)
CN = vert_colors(bust, PN, npix)
ztop, zbot = PN[:, 2].max(), PN[:, 2].min()
# 새 메시의 목: 아래(어깨)와 위(머리) 사이에서 피부가 가장 가는 곳 — 머리 높이의 대략 아래 45% 구간
cxn, cyn = float(np.median(PN[:, 0])), float(np.median(PN[:, 1]))
hh = ztop - zbot
skin_n = face_skin(PN, CN, zbot + 0.62 * hh, zbot + 0.75 * hh)
print('BUST skin', np.round(skin_n * 255).astype(int).tolist(), 'height', round(float(hh), 3), flush=True)
nn, _ = neck_profile(PN, CN, zbot + 0.45 * hh, zbot + 0.85 * hh, cxn, cyn, rmax=0.5 * hh, skin=skin_n, widest=True)
zn, wn, nx, _ = nn
ny = float(np.median(PN[(np.abs(PN[:, 2] - zn) < 0.02 * hh), 1]))           # 그 높이 단면의 앞뒤 가운데
s = wb / wn * float(opt.get('scale', 1.0))
T = Matrix.Translation((bx, by, zb)) @ Matrix.Scale(s, 4) @ Matrix.Translation((-nx, -ny, -zn))
bust.data.transform(T); bust.data.update()
PN = np.array([tuple(v.co) for v in bust.data.vertices])
print('BUST face widest %.4f → scale %.4f, new head top %.3f (old %.3f)' % (wn, s, PN[:, 2].max(), PB[:, 2].max()), flush=True)

# ── 지울 곳 / 남길 곳(이음매 = 목 꼭대기, 턱 그늘·뒷머리 밑): 옛 목·옷깃·수건은 그대로 두고 머리만 지움
r_tube = 0.8 * wneck
z_seam = head_b.z - 0.005                                           # 옛 몸은 이 위를 지움(머리), 새 메시는 이 위 + 아래 2cm 겹침
z_new_bot = float(opt.get('newBot', z_seam - 0.03))
rb = np.hypot(PB[:, 0] - neck_b.x, PB[:, 1] - neck_b.y)
gname = {g.index: g.name for g in body.vertex_groups}
HEADS = {'Head', 'head_end', 'headfront'}
wh = np.array([sum(g.weight for g in v.groups if gname.get(g.group) in HEADS) for v in body.data.vertices])
del_b = (wh > 0.3) & (PB[:, 2] > neck_b.z)                        # 옛 머리(Head 계열 뼈 가중치) — 목 뼈 쪽 목·옷깃은 남김
chest = (np.abs(PB[:, 2] - (neck_b.z - 0.10)) < 0.03) & (np.abs(PB[:, 0] - neck_b.x) < 0.06)
cidx = np.nonzero(chest)[0]; cidx = cidx[np.argsort(PB[cidx, 1])][:300]
shirt_b = np.median(CB[cidx], 0) if len(cidx) else np.array([9.0, 9.0, 9.0])
hairlike = (np.linalg.norm(CB - skin_b, axis=1) > 0.25) & (np.linalg.norm(CB - shirt_b, axis=1) > 0.25) & (CB.sum(1) < 2.2)
hl = hairlike & (wh > 0.2) & (PB[:, 2] > neck_b.z) & (rb < 0.16)     # 목 아래로 늘어진 옛 머리카락만
if opt.get('keepOldBelow'): hl &= PB[:, 2] > z_seam                 # 새 메시 바닥 아래의 옛 수염 목은 남겨 틈을 메움
del_b |= hl
print('BODY shirt', np.round(shirt_b * 255).astype(int).tolist(), 'old hair below seam', int(hl.sum()), flush=True)
rn = np.hypot(PN[:, 0] - neck_b.x, PN[:, 1] - neck_b.y)
lowb = PN[:, 2] < PN[:, 2].min() + 0.08 * (PN[:, 2].max() - PN[:, 2].min())
shirt_n = np.median(CN[lowb], 0)
notshirt = np.linalg.norm(CN - shirt_n, axis=1) > 0.12
keep_n = ((PN[:, 2] > z_seam + 0.005) & (notshirt | (PN[:, 2] > head_b.z + 0.04))) | ((PN[:, 2] > z_new_bot) & (rn < r_tube * float(opt.get('tubeK', 1.25))) & notshirt)
print('delete body verts', int(del_b.sum()), 'keep new verts', int(keep_n.sum()), 'of', len(PN), flush=True)

# 가중치 옮기기용 원본 몸 복사(지우기 전)
src = body.copy(); src.data = body.data.copy(); bpy.context.scene.collection.objects.link(src)
src.modifiers.clear()

import bmesh
def delete_verts(obj, mask):
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in np.nonzero(mask)[0]], context='VERTS')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
delete_verts(bust, ~keep_n)

# ── 겹침 구간: 새 목을 옛 목보다 조금 굵게(옛 목 꼭대기 테두리가 새 목 안에 숨게), 이음매 위 2cm 에서 원래대로
def ring_width(P, z, rmax):
    m = (np.abs(P[:, 2] - z) < 0.004) & (np.hypot(P[:, 0] - neck_b.x, P[:, 1] - neck_b.y) < rmax)
    return (np.percentile(P[m, 0], 97) - np.percentile(P[m, 0], 3)) if m.sum() > 8 else None
PNk = np.array([tuple(v.co) for v in bust.data.vertices])
wo = ring_width(PB[~del_b], z_seam - 0.01, r_tube * 1.1); wn2 = ring_width(PNk, z_seam - 0.01, r_tube * 1.1)
fac = max(1.0, min(1.25, (wo / wn2) * 1.04)) if (wo and wn2) else 1.04
print('NECK old %s new %s → radial %.3f' % (wo and round(wo, 3), wn2 and round(wn2, 3), fac), flush=True)
for v in bust.data.vertices:
    if v.co.z < z_seam + 0.02:
        t = min(1.0, max(0.0, (v.co.z - z_new_bot) / (z_seam + 0.02 - z_new_bot)))
        f = fac + (1.0 - fac) * t
        v.co.x = neck_b.x + (v.co.x - neck_b.x) * f; v.co.y = neck_b.y + (v.co.y - neck_b.y) * f

# ── 얼굴 형상 고치기(정면 쪽만)
def smooth(r, R): return np.where(r < R, (1 - (r / R) ** 2) ** 2, 0.0)
if 'eyeL' in opt or 'drop' in opt or 'noseDrop' in opt:
    P = np.array([tuple(v.co) for v in bust.data.vertices])
    front = np.clip((by - P[:, 1]) / 0.03, 0, 1)                  # 목 중심보다 앞(−Y)일수록 1
    d = np.zeros_like(P)
    if 'eyeL' in opt:
        exL, exR, ez = opt['eyeL'], opt['eyeR'], opt['eyeZ']; R = opt.get('R', 0.03); mid = (exL + exR) / 2
        for ex in (exL, exR):
            r = np.hypot(P[:, 0] - ex, P[:, 2] - ez); w = smooth(r, R) * front
            d[:, 0] += (1 if ex < mid else -1) * opt.get('shift', 0.0) * w
            d[:, 2] += opt.get('lift', 0.0) * w
    if 'drop' in opt:                                              # 코·입·턱을 아래로: dropTop(눈 아래) 0 → dropBot(턱) 최대 → 그 아래 4cm 동안 줄어듦
        zt, zbt, dr = opt['dropTop'], opt['dropBot'], opt['drop']
        t = np.clip((zt - P[:, 2]) / (zt - zbt), 0, 1)
        below = np.clip(1 - (zbt - P[:, 2]) / 0.04, 0, 1)
        prof = np.where(P[:, 2] > zbt, t, below)
        lat = np.clip(1 - np.abs(P[:, 0] - bx) / 0.085, 0, 1)
        d[:, 2] -= dr * prof * front * lat
    if 'noseDrop' in opt:                                          # 코끝 둘레만 아래로(코 길이)
        r = np.hypot(P[:, 0] - opt['noseX'], P[:, 2] - opt['noseZ']); w = smooth(r, opt.get('noseR', 0.022)) * front
        d[:, 2] -= opt['noseDrop'] * w
    for i, v in enumerate(bust.data.vertices): v.co += Vector(d[i])
    bust.data.update()
    print('FACEGEO max move mm', round(float(np.abs(d).max()) * 1000, 1), flush=True)

# ── 옛 몸에서 지울 곳 = 새 머리 부피 안(또는 겉면 8mm 안)에 들어간 정점 + 목 아래로 늘어진 옛 머리카락. 옷깃·수건·남는 목은 그대로
from mathutils.bvhtree import BVHTree
PNf = [Vector(v.co) for v in bust.data.vertices]
bvh = BVHTree.FromPolygons(PNf, [tuple(p.vertices) for p in bust.data.polygons])
cand = np.nonzero((PB[:, 2] > z_new_bot - 0.01) & (rb < 0.2))[0]
inside = np.zeros(len(PB), bool)
for i in cand:
    loc, nrm, fi, dist = bvh.find_nearest(Vector(PB[i]))
    if loc is None: continue
    if dist < 0.008 or (Vector(PB[i]) - loc).dot(nrm) < 0: inside[i] = True
cloth = (np.linalg.norm(CB - shirt_b, axis=1) < 0.2) | (CB.min(1) > 0.55)      # 셔츠·수건(밝은 무채색)은 새 머리 안이어도 남김
if opt.get('keepOldBelow'):                                         # 새 메시 아래 끝(+1.2cm) 밑의 옛 목은 '안쪽' 판정이 나도 남김(열린 끝 근처 오판 → 목 구멍·가로선)
    zmin_new = min(v.co.z for v in bust.data.vertices)
    inside &= PB[:, 2] > zmin_new + 0.012
    print('keepOldBelow: new bottom %.3f' % zmin_new, flush=True)
del_b = (inside & ~cloth) | hl | ((wh > 0.6) & (PB[:, 2] > z_seam + 0.03))     # 머리 위쪽의 옛 머리는 새 머리보다 커도 지움
if 'oldNeckCut' in opt:                                             # 옛 목 피부를 이 높이 위로 모두 지움(새 목이 깃 근처까지 덮을 때 — 겹침 가로선 없앰)
    del_b |= (PB[:, 2] > opt['oldNeckCut']) & (rb < r_tube * 1.3) & ~cloth & (np.linalg.norm(CB - skin_b, axis=1) < 0.25)
jaw = (wh > 0.35) & ~cloth & (PB[:, 1] < neck_b.y - 0.02) & (PB[:, 2] > neck_b.z + 0.02)   # 새 턱 밑으로 삐져나오던 옛 턱·수염
if opt.get('keepOldBelow'): jaw &= PB[:, 2] > zmin_new + 0.012
del_b |= jaw
print('delete body verts (inside new head)', int(inside.sum()), 'old jaw', int(jaw.sum()), 'total', int(del_b.sum()), flush=True)
delete_verts(body, del_b)

# ── 가중치: 원본 몸 → 새 머리
bust.parent = arm; bust.matrix_parent_inverse = arm.matrix_world.inverted()
for g in body.vertex_groups: bust.vertex_groups.new(name=g.name)
dt = bust.modifiers.new('dt', 'DATA_TRANSFER'); dt.object = src
dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.context.view_layer.objects.active = bust
bpy.ops.object.modifier_apply(modifier='dt')
am = bust.modifiers.new('Armature', 'ARMATURE'); am.object = arm

# ── UV 한 장으로: 몸 = 왼쪽 4096², 새 머리 = 오른쪽 위 2048²
for l in body.data.uv_layers.active.data: l.uv = (l.uv.x * 4096 / ATW, l.uv.y)
for l in bust.data.uv_layers.active.data: l.uv = ((4096 + l.uv.x * HS) / ATW, 1 - HS / ATH + l.uv.y * HS / ATH)
atlas = bpy.data.images.load(atlas_p)                             # 밖에서 Pillow 로 만든 6144×4096(make_atlas.py)
mat = body.data.materials[0]
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE': n.image = atlas
bust.data.materials.clear(); bust.data.materials.append(mat)
for p in bust.data.polygons: p.material_index = 0
bpy.data.objects.remove(src, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
bust.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
for i in range(len(body.data.materials) - 1, 0, -1): body.data.materials.pop(index=i)
print('JOINED verts', len(body.data.vertices), 'groups', len(body.vertex_groups), 'bones', len(arm.data.bones), flush=True)

if act: arm.animation_data.action = act
bpy.ops.export_scene.gltf(filepath=out_p, export_format='GLB', export_animations=True, export_skins=True, export_yup=True, export_image_format='JPEG')
print('EXPORTED', out_p, flush=True)
