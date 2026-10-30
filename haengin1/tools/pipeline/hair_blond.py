# 서진수 v3 머리카락 → 밝은 노란 탈색 금발(2026-10-30)
#   머리 마스크 = 메시 기하(머리 꼭대기에서 잰 깊이 한계: 앞 7cm·옆 14cm·뒤 25cm — 이마·눈썹·귀 아래·수염 제외)를 UV 로 구움
#                × 화소 색(피부 = 평면 (201,137,101) 색상 21.6° / 머리 27~29° → 색상 23.5° 위만 머리)
#   다시 칠하기 = 머리 밝기 비율 → 금발 그라디언트(결 유지), Shade.jpg 는 화소별 원래 곱셈값 유지(머리는 1.0), OutW 머리 외곽선 ×0.4
# 사용: blender -b --factory-startup --python mesh_dump.py -- <리깅 glb> <mesh.npz>
#       python hair_blond.py <mesh.npz> <원본 폴더(Tex·Shade)> <출력 폴더> <미리보기 폴더>   (서진수 v3 값 — 다른 사람은 깊이 한계·색상 기준을 다시 잴 것)
import sys
import numpy as np
from PIL import Image, ImageFilter
mp, src, dst, prev = sys.argv[1:5]
NAME = "JinsooSeo_v3"
m = np.load(mp); P, T, UV = m['P'], m['T'], m['UV']
top = P[:, 2].max(); zr = top - P[:, 2]
hv = zr < 0.20
cx, cy = P[hv, 0].mean(), P[hv, 1].mean()
phi = np.degrees(np.abs(np.arctan2(P[:, 0] - cx, -(P[:, 1] - cy))))      # 0 = 정면(-Y), 180 = 뒤
lim = np.interp(phi, [0, 35, 70, 105, 140, 180], [0.068, 0.075, 0.135, 0.165, 0.235, 0.25])
wv = np.clip((lim - zr) / 0.012 + 0.5, 0, 1).astype(np.float32)
tex = np.asarray(Image.open(f"{src}/{NAME}Tex.jpg").convert('RGB')).astype(np.float32) / 255
sh = np.asarray(Image.open(f"{src}/{NAME}Shade.jpg").convert('RGB')).astype(np.float32) / 255
H, W = tex.shape[:2]
RW, RH = W // 3, H // 3
reg = np.zeros((RH, RW), np.float32); cov = np.zeros((RH, RW), bool)
for ti in np.nonzero((zr[T] < 0.30).any(1))[0]:
    vi = T[ti]; uv = UV[ti]
    px = uv[:, 0] * RW; py = (1 - uv[:, 1]) * RH
    x0, x1 = int(np.floor(px.min())), int(np.ceil(px.max())); y0, y1 = int(np.floor(py.min())), int(np.ceil(py.max()))
    gx, gy = np.meshgrid(np.arange(max(0, x0), min(RW - 1, x1) + 1) + 0.5, np.arange(max(0, y0), min(RH - 1, y1) + 1) + 0.5)
    gx = gx.ravel(); gy = gy.ravel()
    Mx = np.array([[px[0] - px[2], px[1] - px[2]], [py[0] - py[2], py[1] - py[2]]])
    if abs(np.linalg.det(Mx)) < 1e-12: continue
    l0, l1 = np.linalg.solve(Mx, np.vstack([gx - px[2], gy - py[2]])); l2 = 1 - l0 - l1
    ins = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
    if not ins.any(): continue
    ix = gx[ins].astype(int); iy = gy[ins].astype(int)
    w = np.clip(l0[ins] * wv[vi[0]] + l1[ins] * wv[vi[1]] + l2[ins] * wv[vi[2]], 0, 1)
    reg[iy, ix] = np.maximum(reg[iy, ix], w); cov[iy, ix] = True
for _ in range(4):                                                        # 섬 가장자리 덧칠
    pr = np.pad(reg, 1, mode='edge'); pc = np.pad(cov, 1, mode='edge'); nr = np.zeros_like(reg); nc = np.zeros_like(cov)
    for dy in (0, 1, 2):
        for dx in (0, 1, 2):
            c = pc[dy:dy + RH, dx:dx + RW]; nr = np.maximum(nr, np.where(c, pr[dy:dy + RH, dx:dx + RW], 0)); nc |= c
    g = ~cov & nc; reg[g] = nr[g]; cov |= g
reg = np.asarray(Image.fromarray((reg * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32) / 255
# 화소 색상
mx = tex.max(2); mn = tex.min(2); d = mx - mn + 1e-6
r, g_, b = tex[..., 0], tex[..., 1], tex[..., 2]
hue = np.where(mx == r, ((g_ - b) / d) % 6, np.where(mx == g_, (b - r) / d + 2, (r - g_) / d + 4)) * 60
lum = tex @ np.array([0.299, 0.587, 0.114], np.float32)
hh = np.clip((hue - 23.0) / 2.5, 0, 1)
hh = np.where(lum < 0.30, np.clip((hue - 20.0) / 3.0, 0, 1), hh)          # 어두운 결은 색상 기준을 낮춤(뿌리·결 그늘)
hh = np.where(d < 0.04, 0, hh)                                            # 무채색(회색 천) 빼기
a = reg * hh
a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
med = float(np.median(lum[a > 0.8])); print('hair px', int((a > 0.8).sum()), 'lum median', round(med, 3))
x = np.clip(lum / med, 0, 1.7)
stops = np.array([0.0, 0.4, 0.75, 1.0, 1.3, 1.7])
cols = np.array([[0.80, 0.56, 0.16], [0.90, 0.68, 0.20], [0.98, 0.80, 0.27], [1.0, 0.87, 0.34], [1.0, 0.92, 0.48], [1.0, 0.96, 0.66]], np.float32)   # 게임 조명(체육관 낮 ≈ ×0.5·약간 차가움)에서 노란 금발로 읽히게 — 어두운 결도 금색으로 눌러 둠
new = np.stack([np.interp(x, stops, cols[:, c]) for c in range(3)], -1)
A = a[..., None]
tex2 = tex * (1 - A) + new * A
ratio = np.clip((sh + 0.01) / (tex + 0.01), 0.6, 1.0)
sh2 = np.clip(sh + (tex2 - tex) * ratio, 0, 1)
sh2 = sh2 * (1 - A) + tex2 * A                                            # 머리 그늘 곱셈값 1.0(2026-10-30 체육관 낮 조명에서 금발이 올리브빛으로 보이던 것, 발광은 안 씀)
print('new hair median', np.round(np.median(tex2[a > 0.8], 0) * 255).astype(int).tolist())
Image.fromarray((np.clip(tex2, 0, 1) * 255 + 0.5).astype(np.uint8)).save(f"{dst}/{NAME}Tex.jpg", quality=95)
Image.fromarray((sh2 * 255 + 0.5).astype(np.uint8)).save(f"{dst}/{NAME}Shade.jpg", quality=92)
# 머리카락 외곽선 굵기 ×0.4(OutW — 짙은 외곽선 껍질이 정수리 결 사이로 비쳐 금발이 검게 보이던 것)
from PIL import ImageFilter as _F
ow = np.asarray(Image.open(f"{src}/{NAME}OutW.png").convert('RGB')).astype(np.float32) / 255
mk = Image.fromarray((a * 255).astype(np.uint8)).filter(_F.MaxFilter(9)).resize(ow.shape[1::-1], Image.BILINEAR).filter(_F.MaxFilter(3))
mk = np.asarray(mk).astype(np.float32)[..., None] / 255
Image.fromarray((np.clip(ow * (1 - mk * 0.6), 0, 1) * 255 + 0.5).astype(np.uint8)).save(f"{dst}/{NAME}OutW.png")
c = (4062, 0, 6138, 2068)
k = Image.fromarray((a * 255).astype(np.uint8)).crop(c).resize((519, 517)).convert('RGB')
o = Image.fromarray((tex2 * 255).astype(np.uint8)).crop(c).resize((519, 517))
s = Image.new('RGB', (1038, 517)); s.paste(k, (0, 0)); s.paste(o, (519, 0)); s.save(f"{prev}/blond_atlas.jpg", quality=88)
