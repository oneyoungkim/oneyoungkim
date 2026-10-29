# Blender batch: 한쪽 눈 그림이 깨졌을 때(Tripo 텍스처의 흰 점 박힌 눈동자) 반대쪽 눈을 얼굴 가운데 선으로 거울 복사하는 대응표
#   face_warp_map.py 와 같은 npz(텍셀 x,y · 원본 u,v · 가중치)를 만든다 → face_warp_apply.py 로 칠한다(휘기를 먼저 했으면 그 결과 텍스처에)
# 사용: blender -b --factory-startup --python face_mirror_map.py -- <rigged.glb> <out.npz> <대상 눈 x> <눈 z> <가운데 x> [반지름 m] [텍스처 px]
import bpy, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]
src, dst = a[0], a[1]
tx, tz, mid = float(a[2]), float(a[3]), float(a[4])
R = float(a[5]) if len(a) > 5 else 0.017
TEX = int(a[6]) if len(a) > 6 else 4096
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
out = {k: [] for k in ('px', 'py', 'su', 'sv', 'w')}
for t in tris:
    vi = t.vertices
    c = P[list(vi)].mean(0)
    if c[1] > head_y or (n3 @ t.normal).normalized().y > 0.1: continue
    if abs(c[0] - tx) > R + 0.008 or abs(c[2] - tz) > R + 0.008: continue
    uv = np.array([tuple(uvl[li].uv) for li in t.loops])
    px = uv[:, 0] * TEX; py = (1 - uv[:, 1]) * TEX
    x0, x1 = int(np.floor(px.min())), int(np.ceil(px.max())); y0, y1 = int(np.floor(py.min())), int(np.ceil(py.max()))
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5); gx = gx.ravel(); gy = gy.ravel()
    T = np.array([[px[0] - px[2], px[1] - px[2]], [py[0] - py[2], py[1] - py[2]]])
    if abs(np.linalg.det(T)) < 1e-9: continue
    l = np.linalg.solve(T, np.vstack([gx - px[2], gy - py[2]])); l0, l1 = l; l2 = 1 - l0 - l1
    ins = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
    if not ins.any(): continue
    l0, l1, l2, gx, gy = l0[ins], l1[ins], l2[ins], gx[ins], gy[ins]
    p3 = l0[:, None] * P[vi[0]] + l1[:, None] * P[vi[1]] + l2[:, None] * P[vi[2]]
    r = np.sqrt((p3[:, 0] - tx) ** 2 + (p3[:, 2] - tz) ** 2)
    w = np.clip((R - r) / (0.35 * R), 0, 1)                      # 가장자리 35% 는 섞어서 이음매 없음
    for k in np.nonzero(w > 0)[0]:
        sx = 2 * mid - p3[k, 0]
        hit, hn, fi, dist = bvh.ray_cast(Vector((sx, -2.0, p3[k, 2])), Vector((0, 1, 0)))
        if hit is None or abs(hit.y - p3[k, 1]) > 0.015: continue
        tt = tris[fi]; v0, v1, v2 = (Vector(P[j]) for j in tt.vertices)
        e0, e1, e2 = v1 - v0, v2 - v0, hit - v0
        d00, d01, d11 = e0.dot(e0), e0.dot(e1), e1.dot(e1); d20, d21 = e2.dot(e0), e2.dot(e1)
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14: continue
        bv = (d11 * d20 - d01 * d21) / den; bw = (d00 * d21 - d01 * d20) / den; bu = 1 - bv - bw
        tuv = [uvl[li].uv for li in tt.loops]
        u = bu * tuv[0].x + bv * tuv[1].x + bw * tuv[2].x; v = bu * tuv[0].y + bv * tuv[1].y + bw * tuv[2].y
        out['px'].append(gx[k]); out['py'].append(gy[k]); out['su'].append(u * TEX); out['sv'].append((1 - v) * TEX); out['w'].append(w[k])
np.savez(dst, **{k: np.array(v, np.float32) for k, v in out.items()})
print('MIRRORMAP texels', len(out['px']), flush=True)
