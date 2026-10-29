# Blender batch: 얼굴 텍스처 '정면 투영 휘기' 대응표 — 형상은 그대로 두고 그린 눈·눈썹만 옮긴다(2026-10-29 이윤준 닮음 고치기)
#   얼굴 앞면 삼각형의 텍셀마다 3D 점 p → 정면 투영 (x, z) 에서 휘기(눈 두 곳을 가운데 쪽으로·조금 위로·조금 크게)의 역사상 s
#   → s 에서 앞으로 쏜 광선이 맞은 겉면의 UV = 그 텍셀이 가져올 원래 색 자리. 결과는 npz(텍셀 x,y · 원본 u,v · 가중치)
#   색 칠하기는 face_warp_apply.py(Pillow/numpy) 가 한다. 뼈·정점·UV 는 바꾸지 않으므로 FBX 는 텍스처만 바뀐다.
# 사용: blender -b --factory-startup --python face_warp_map.py -- <rigged.glb> <out.npz> <eyeLx> <eyeRx> <eyeZ> [안쪽 m] [올림 m] [키움] [R m] [텍스처 px]
import bpy, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]
src, dst = a[0], a[1]
exL, exR, ez = float(a[2]), float(a[3]), float(a[4])
shift = float(a[5]) if len(a) > 5 else 0.0045
lift = float(a[6]) if len(a) > 6 else 0.0020
grow = float(a[7]) if len(a) > 7 else 1.10
R = float(a[8]) if len(a) > 8 else 0.032
TEX = int(a[9]) if len(a) > 9 else 4096
R2 = R * 0.55
mid = (exL + exR) / 2
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data.action = None
    for t in list(arm.animation_data.nla_tracks): arm.animation_data.nla_tracks.remove(t)
bpy.context.view_layer.update()
body = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
me = body.data; mw = body.matrix_world; n3 = mw.to_3x3()
me.calc_loop_triangles()
P = np.array([tuple(mw @ v.co) for v in me.vertices])
uvl = me.uv_layers.active.data
tris = me.loop_triangles
bvh = BVHTree.FromPolygons([Vector(p) for p in P], [tuple(t.vertices) for t in tris])
head_y = (arm.matrix_world @ arm.pose.bones['Head'].head).y

def disp(x, z):
    """앞 휘기 d(x,z) (numpy 배열)"""
    dx = np.zeros_like(x); dz = np.zeros_like(z)
    for ex in (exL, exR):
        rx, rz = x - ex, z - ez
        r = np.sqrt(rx * rx + rz * rz)
        w = np.where(r < R, (1 - (r / R) ** 2) ** 2, 0.0)
        toward = 1.0 if ex < mid else -1.0
        dx += toward * shift * w; dz += lift * w
        w2 = np.where(r < R2, (1 - (r / R2) ** 2) ** 2, 0.0)
        dx += rx * (grow - 1) * w2; dz += rz * (grow - 1) * w2
    return dx, dz

out_px, out_py, out_u, out_v, out_w = [], [], [], [], []
for t in tris:
    vi = t.vertices
    c = P[list(vi)].mean(0)
    nrm = (n3 @ t.normal).normalized()
    if c[1] > head_y or nrm.y > 0.1: continue
    if min(abs(c[0] - exL), abs(c[0] - exR)) > R + 0.01 or abs(c[2] - ez) > R + 0.01: continue
    uv = np.array([tuple(uvl[li].uv) for li in t.loops])           # Blender UV (v 위가 1)
    px = uv[:, 0] * TEX; py = (1 - uv[:, 1]) * TEX                 # 그림 화소(위가 0)
    x0, x1 = int(np.floor(px.min())), int(np.ceil(px.max())); y0, y1 = int(np.floor(py.min())), int(np.ceil(py.max()))
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    gx = gx.ravel(); gy = gy.ravel()
    T = np.array([[px[0] - px[2], px[1] - px[2]], [py[0] - py[2], py[1] - py[2]]])
    if abs(np.linalg.det(T)) < 1e-9: continue
    l = np.linalg.solve(T, np.vstack([gx - px[2], gy - py[2]]))
    l0, l1 = l[0], l[1]; l2 = 1 - l0 - l1
    ins = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)             # 경계 반 화소 여유(이음매 틈 막기)
    if not ins.any(): continue
    l0, l1, l2, gx, gy = l0[ins], l1[ins], l2[ins], gx[ins], gy[ins]
    p3 = l0[:, None] * P[vi[0]] + l1[:, None] * P[vi[1]] + l2[:, None] * P[vi[2]]
    x, z = p3[:, 0], p3[:, 2]
    dx, dz = disp(x, z)
    if not (np.abs(dx) + np.abs(dz) > 1e-5).any(): continue
    # 역사상: s = p - d(p - d(p))
    ddx, ddz = disp(x - dx, z - dz)
    sx, sz = x - ddx, z - ddz
    w = np.clip(np.sqrt(dx * dx + dz * dz) / 1e-3, 0, 1)
    for k in range(len(sx)):
        if w[k] <= 0: continue
        hit, hn, fi, dist = bvh.ray_cast(Vector((sx[k], -2.0, sz[k])), Vector((0, 1, 0)))
        if hit is None or abs(hit.y - p3[k, 1]) > 0.015: continue       # 앞머리 같은 다른 겉면이면 건너뜀
        tt = tris[fi]; tv = [P[j] for j in tt.vertices]
        # 맞은 삼각형의 무게중심 좌표 → UV
        v0, v1, v2 = (Vector(q) for q in tv)
        e0, e1, e2 = v1 - v0, v2 - v0, hit - v0
        d00, d01, d11 = e0.dot(e0), e0.dot(e1), e1.dot(e1); d20, d21 = e2.dot(e0), e2.dot(e1)
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14: continue
        bv = (d11 * d20 - d01 * d21) / den; bw = (d00 * d21 - d01 * d20) / den; bu = 1 - bv - bw
        tuv = [uvl[li].uv for li in tt.loops]
        u = bu * tuv[0].x + bv * tuv[1].x + bw * tuv[2].x
        v = bu * tuv[0].y + bv * tuv[1].y + bw * tuv[2].y
        out_px.append(gx[k]); out_py.append(gy[k]); out_u.append(u * TEX); out_v.append((1 - v) * TEX); out_w.append(w[k])
np.savez(dst, px=np.array(out_px, np.float32), py=np.array(out_py, np.float32), su=np.array(out_u, np.float32), sv=np.array(out_v, np.float32), w=np.array(out_w, np.float32))
print('WARPMAP texels', len(out_px), 'IPD', round((exR - exL) * 100, 2), '->', round((exR - exL - 2 * shift) * 100, 2), 'cm', flush=True)
