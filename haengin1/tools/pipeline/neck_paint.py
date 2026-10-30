# head_bridge.py 결과(.npz)로 아틀라스의 이음 띠 ±3cm 를 같은 피부색 그라데이션으로 칠하고, 목 전체 마스크(1024²)를 만든다(2026-10-30)
#   옷깃(셔츠·수건 색)은 칠하지 않음. 띠 UV 한 점 둘레는 완전히 같은 색. 덮이지 않은 텍셀로도 조금 번지게 함(밉맵)
# 사용: python neck_paint.py <atlas.jpg> <out.glb.npz> <atlas_out.jpg> <neck_mask.npz> [번짐 거리 m]
import sys
import numpy as np, cv2
from PIL import Image
ap, dp, op, mp = sys.argv[1:5]
d = np.load(dp); P, T, UV, ISH = d['P'], d['T'], d['UV'], d['ISH']
n, s_h, s_b, uvb, z_chin = d['n'], float(d['s_h']), float(d['s_b']), d['uvb'], float(d['z_chin'])
cc, e1, e2, rm, shirt_b = d['cc'], d['e1'], d['e2'], d['rm'], d['shirt_b']
img = np.asarray(Image.open(ap).convert('RGB')).astype(np.float32) / 255; H, W = img.shape[:2]
hv = P @ n; dhv = hv - s_h; dbv = s_b - hv
rv = np.hypot(P @ e1 - cc[0], P @ e2 - cc[1]); rlim = float(rm.max()) + 0.03
FADE = float(sys.argv[5]) if len(sys.argv) > 5 else 0.03             # 새 머리 쪽 번짐 거리
FADE_B = float(sys.argv[6]) if len(sys.argv) > 6 else 0.065          # 몸 쪽은 옷깃까지(옛 목에 그려진 주름·선도 덮음, 옷 화소는 뺌)
def fade(x, L):
    t = np.clip((x - 0.004) / (L - 0.004), 0, 1); return 1 - t * t * (3 - 2 * t)
wh_v = fade(np.maximum(dhv, 0), FADE) * (rv < rlim); wb_v = fade(np.maximum(dbv, 0), FADE_B) * (rv < rlim)
def raster(tris_uv, tris_w, W_, H_, grow=0.03):
    out = np.zeros((H_, W_), np.float32)
    for uv, w in zip(tris_uv, tris_w):
        px = uv[:, 0] * W_; py = (1 - uv[:, 1]) * H_
        x0, x1 = int(np.floor(px.min())) - 1, int(np.ceil(px.max())) + 1; y0, y1 = int(np.floor(py.min())) - 1, int(np.ceil(py.max())) + 1
        gx, gy = np.meshgrid(np.arange(max(0, x0), min(W_ - 1, x1) + 1) + 0.5, np.arange(max(0, y0), min(H_ - 1, y1) + 1) + 0.5)
        gx = gx.ravel(); gy = gy.ravel()
        M = np.array([[px[0] - px[2], px[1] - px[2]], [py[0] - py[2], py[1] - py[2]]])
        if abs(np.linalg.det(M)) < 1e-9: continue
        l0, l1 = np.linalg.solve(M, np.vstack([gx - px[2], gy - py[2]])); l2 = 1 - l0 - l1
        ins = (l0 >= -grow) & (l1 >= -grow) & (l2 >= -grow)
        if not ins.any(): continue
        ix = gx[ins].astype(int); iy = gy[ins].astype(int)
        v = np.clip(l0[ins] * w[0] + l1[ins] * w[1] + l2[ins] * w[2], 0, 1)
        out[iy, ix] = np.maximum(out[iy, ix], v)
    return out
uvt = d['uvt']
is_band = (np.abs(UV[:, :, 0] - uvb[0]).max(1) + np.abs(UV[:, :, 1] - uvb[1]).max(1) < 1e-5) | (np.abs(UV[:, :, 0] - uvt[0]).max(1) + np.abs(UV[:, :, 1] - uvt[1]).max(1) < 1e-5)
# 색: 칠할 곳 바로 바깥의 깨끗한 피부(양쪽) 중앙값의 평균
cuv = UV.mean(1); cx = np.clip((cuv[:, 0] * W).astype(int), 0, W - 1); cy = np.clip(((1 - cuv[:, 1]) * H).astype(int), 0, H - 1); cc_col = img[cy, cx]
dh_t = dhv[T].mean(1); db_t = dbv[T].mean(1); r_t = rv[T].mean(1)
def clean(col):
    l = col @ np.array([0.299, 0.587, 0.114]); return col[l > np.median(l) * 0.85]
is_cloth_c = (np.linalg.norm(cc_col - shirt_b, axis=1) < 0.2) | ((cc_col.min(1) > 0.55) & (cc_col.max(1) - cc_col.min(1) < 0.15))
sb = clean(cc_col[~ISH & ~is_band & ~is_cloth_c & (db_t > 0.004) & (db_t < FADE) & (r_t < rlim)])
C_b = np.median(sb, 0)
sa = cc_col[ISH & (dh_t < FADE + 0.03) & (r_t < rlim)]               # 새 목: 수염·머리카락(짙음)을 빼고 몸 피부에 가까운 쪽
sa = sa[np.linalg.norm(sa - C_b, axis=1) < 0.40]; sa = clean(sa)
C_h = np.median(sa, 0) if len(sa) > 20 else C_b; C_mid = (C_h + C_b) / 2
print('COLOR head', np.round(C_h * 255).astype(int).tolist(), 'body', np.round(C_b * 255).astype(int).tolist(), 'mid', np.round(C_mid * 255).astype(int).tolist(), flush=True)
sel_h = ISH & (wh_v[T].max(1) > 0); sel_b = ~ISH & ~is_band & (wb_v[T].max(1) > 0)
wm_h = raster(UV[sel_h], wh_v[T[sel_h]], W, H); wm_b = raster(UV[sel_b], wb_v[T[sel_b]], W, H)
print('PAINT tris head', int(sel_h.sum()), 'body', int(sel_b.sum()), flush=True)
# 옷깃(셔츠·수건) 화소는 칠하지 않음 — 몸 쪽만
dist = np.linalg.norm(img - shirt_b, axis=2); m_sh = np.clip((dist - 0.12) / 0.10, 0, 1)
m_tw = 1 - np.clip((img.min(2) - 0.50) / 0.06, 0, 1) * np.clip((0.17 - (img.max(2) - img.min(2))) / 0.04, 0, 1)
wm = np.maximum(wm_h, wm_b * m_sh * m_tw)
# 띠 UV 한 점 둘레는 완전히 같은 색
ux, uy = int(uvb[0] * W), int((1 - uvb[1]) * H)
cv2.circle(wm, (ux, uy), 8, 1.0, -1)
# 덮이지 않은 텍셀로 번지기(밉맵에서 옛 색이 비치지 않게)
cov = np.zeros((H, W), np.uint8)
pts = np.stack([UV[:, :, 0] * W, (1 - UV[:, :, 1]) * H], -1).astype(np.int32)
for tri in pts: cv2.fillConvexPoly(cov, tri, 1)
wd = cv2.dilate(wm, np.ones((13, 13), np.uint8)); wm = np.where(cov > 0, wm, wd)
out = img * (1 - wm[..., None]) + C_mid * wm[..., None]
Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(op, quality=95)
# 목 전체 마스크(1024²): 턱 밑~옷깃. 머리 쪽 = 목 둘레의 턱 끝 높이 아래, 몸 쪽 = 고리 아래 옷깃까지(옷은 화소에서 뺌)
N = 1024
wn_v = np.where((rv < rlim) & (P[:, 2] < z_chin + 0.005) & (hv >= s_b - 0.12), 1.0, 0.0).astype(np.float32)
sel = (wn_v[T].max(1) > 0) & ~is_band
wn = raster(UV[sel], wn_v[T[sel]], N, N, grow=0.0)
ms = cv2.resize(m_sh * m_tw, (N, N), interpolation=cv2.INTER_AREA)
wn = wn * np.where(np.arange(N)[None, :] < N * 4096 / 6144, ms, 1.0)      # 몸 구역은 옷 빼기
np.savez(mp, wn=wn)
print('NECKMASK texels', int((wn > 0.5).sum()), flush=True)
