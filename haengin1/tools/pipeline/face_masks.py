# Blender batch: Tripo 리깅 모델(한 재질)의 얼굴 앞면 가중치·머리카락 가중치를 UV 공간 1024² 로 굽는다(2026-10-29 이윤준 닮음 고치기)
#   정점마다 wf = 얼굴 타원(정면 투영) × 정면을 보는 정도, wh = 머리 높이에서 텍스처가 짙은 곳(머리카락) → 삼각형 안은 무게중심 보간
#   face_shade_outw.py 가 이것으로 <이름>Shade.jpg(얼굴 그늘 밝게)·<이름>OutW.png(얼굴 앞면 외곽선 0, 머리카락 .45)를 만든다 — face_tex.py 규칙과 같음
# 사용: blender -b --factory-startup --python face_masks.py -- <rigged.glb> <tex.jpg> <out.npz> <cx> <cz> <rx> <rz>
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]
src, texp, dst = a[0], a[1], a[2]
cx, cz, rx, rz = (float(x) for x in a[3:7])
HAIR = np.array([float(x) / 255 for x in a[7].split(',')]) if len(a) > 7 else None   # 밝은 머리카락 색 r,g,b(0~255)
N = 1024
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
NY = np.array([(n3 @ v.normal).normalized().y for v in me.vertices])
top = P[:, 2].max()
img = bpy.data.images.load(texp); TW, TH = img.size
pix = np.empty(TW * TH * 4, np.float32); img.pixels.foreach_get(pix); pix = pix.reshape(TH, TW, 4)[::-1]   # 위가 0 인 행
lum = pix[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
uvl = me.uv_layers.active.data
e = ((P[:, 0] - cx) / rx) ** 2 + ((P[:, 2] - cz) / rz) ** 2
wf_v = np.clip((1 - e) / 0.35, 0, 1) * np.clip((0.35 - NY) / 0.45, 0, 1)   # 코 옆면(n.y≈0)까지 넣고, 옆을 보는 턱 실루엣은 뺌
wf = np.zeros((N, N), np.float32); wh = np.zeros((N, N), np.float32); cov = np.zeros((N, N), bool)
for t in me.loop_triangles:
    vi = list(t.vertices)
    if P[vi, 2].max() < top - 0.32: continue                     # 머리·목 위쪽만
    uv = np.array([tuple(uvl[li].uv) for li in t.loops])
    px = uv[:, 0] * N; py = (1 - uv[:, 1]) * N
    # 정점 머리카락 = 그 정점 UV 의 텍스처가 짙음(피부보다 훨씬 어두움)
    tx = np.clip((uv[:, 0] * TW).astype(int), 0, TW - 1); ty = np.clip(((1 - uv[:, 1]) * TH).astype(int), 0, TH - 1)
    whv = (lum[ty, tx] < 0.22).astype(np.float32) * (P[vi, 2] > top - 0.17)
    if HAIR is not None:                                            # 밝은 머리카락은 뒷머리 끝(머리 꼭대기 26cm 아래)까지
        whv = np.maximum(whv, (np.linalg.norm(pix[ty, tx, :3] - HAIR, axis=1) < 0.24).astype(np.float32) * (P[vi, 2] > top - 0.26))
    wfv = wf_v[vi]
    x0, x1 = int(np.floor(px.min())), int(np.ceil(px.max())); y0, y1 = int(np.floor(py.min())), int(np.ceil(py.max()))
    gx, gy = np.meshgrid(np.arange(max(0, x0), min(N - 1, x1) + 1) + 0.5, np.arange(max(0, y0), min(N - 1, y1) + 1) + 0.5)
    gx = gx.ravel(); gy = gy.ravel()
    T = np.array([[px[0] - px[2], px[1] - px[2]], [py[0] - py[2], py[1] - py[2]]])
    if abs(np.linalg.det(T)) < 1e-12: continue
    l = np.linalg.solve(T, np.vstack([gx - px[2], gy - py[2]])); l0, l1 = l; l2 = 1 - l0 - l1
    ins = (l0 >= -0.05) & (l1 >= -0.05) & (l2 >= -0.05)
    if not ins.any(): continue
    ix = gx[ins].astype(int); iy = gy[ins].astype(int)
    f = np.clip(l0[ins] * wfv[0] + l1[ins] * wfv[1] + l2[ins] * wfv[2], 0, 1)
    h = np.clip(l0[ins] * whv[0] + l1[ins] * whv[1] + l2[ins] * whv[2], 0, 1)
    wf[iy, ix] = np.maximum(wf[iy, ix], f); wh[iy, ix] = np.maximum(wh[iy, ix], h); cov[iy, ix] = True
np.savez(dst, wf=wf, wh=wh, cov=cov)
print('FACEMASK face texels', int((wf > 0.5).sum()), 'hair texels', int((wh > 0.5).sum()), 'of', N * N, flush=True)
