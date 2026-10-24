# 정식 모델 텍스처 작업(docs/09_M3_버티컬슬라이스_설계.md 4-2 ④⑤⑥, 4-3 4~6) — tools/formal_model.py 다음 단계(PIL·numpy, 크레딧 0)
# 사용: python tools/face_tex.py <작업 폴더> <출력 폴더> <이름> <인물: siwoo|taeo>
#   작업 폴더 = formal_model.py 가 남긴 tex.jpg · face.npz · info.json
# 만드는 것(출력 폴더):
#   <이름>Tex.jpg    몸 텍스처에서 눈·눈썹·입을 지운 것(얼굴 앞면을 정면에서 본 그림으로 펴서 '피부 안의 구멍' = 이목구비를 찾아 둘레 피부로 메움)
#   <이름>Face.png   표정 아틀라스 2048² RGBA, 5 × 5 칸(줄 = 표정 normal·hurt·ko·win·endure, 칸 = 뜸·반쯤 감음·감음·말하기 A·말하기 B).
#                    칸 하나 = 얼굴 데칼 UV 0~1(앞에서 본 얼굴 상자). 눈·입 자리는 원래 텍스처에서 찾은 자리(못 찾으면 FUJIMOTO 3-2 비율)
#   <이름>Shade.jpg  1단 그림자 텍스처 = 몸 텍스처 × 부위별 곱셈값(FUJIMOTO 6-3: 피부 .80·.72·.72 · 얼굴 .93·.88·.88 · 흰 옷 .78·.80·.88 · 회색 .74·.74·.80 · 짙은 옷·머리 .70·.70·.78 · 강조색 .80·.74·.74)
#   <이름>Mask.png   부위 마스크 1024²(R 피부 · G 머리 · B 옷 · A 강조색) — 기록·다음 작업용
#   <이름>OutW.png   외곽선 굵기 1024²(UTS _Outline_Sampler): 실루엣 1.0 · 머리 덩어리 .45 · 얼굴 앞면 0, 저주파 노이즈 ±25%(7-4)
#   <이름>Face.json  아틀라스 칸 수·칸 화소·윗눈꺼풀 굵기(월드 mm·칸 화소)·눈 자리·두개골 기준 머리 높이(눈선 .53 규칙)
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

work, out, name, who = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
os.makedirs(out, exist_ok=True)
info = json.load(open(os.path.join(work, 'info.json'), encoding='utf-8'))
D = np.load(os.path.join(work, 'face.npz'))
tex = np.asarray(Image.open(os.path.join(work, 'tex.jpg')).convert('RGB')).astype(np.float32) / 255.0
TH, TW = tex.shape[:2]
INK = np.array([0x1A, 0x14, 0x17]) / 255.0
rng = np.random.default_rng(7)


def raster(tri, W, H):
    """tri (3,2) 화소 좌표 → (ys, xs, 무게중심 좌표 (n,3))"""
    x0, y0 = np.floor(tri.min(0)).astype(int)
    x1, y1 = np.ceil(tri.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0)
    x1, y1 = min(x1, W - 1), min(y1, H - 1)
    if x1 < x0 or y1 < y0:
        return None
    ys, xs = np.mgrid[y0:y1 + 1, x0:x1 + 1]
    px = np.stack([xs.ravel() + 0.5, ys.ravel() + 0.5], 1)
    a, b, c = tri
    v0, v1 = b - a, c - a
    den = v0[0] * v1[1] - v1[0] * v0[1]
    if abs(den) < 1e-12:
        return None
    d = px - a
    l1 = (d[:, 0] * v1[1] - v1[0] * d[:, 1]) / den
    l2 = (v0[0] * d[:, 1] - d[:, 0] * v0[1]) / den
    l0 = 1 - l1 - l2
    m = (l0 >= -1e-4) & (l1 >= -1e-4) & (l2 >= -1e-4)
    if not m.any():
        return None
    return ys.ravel()[m], xs.ravel()[m], np.stack([l0[m], l1[m], l2[m]], 1)


def uv2tex(uv, W=TW, H=TH):
    return np.stack([uv[..., 0] * W, (1 - uv[..., 1]) * H], -1)


# ───────── ① 앞에서 본 얼굴(데칼 공간 512²) — 픽셀마다 '가장 뒤 겹'(피부 면) 삼각형만. 앞머리 카드는 피부 앞에 떠 있는 따로 된 면이라
#   맨 앞 겹으로 그리면 눈 위를 지나 구멍이 테두리와 이어진다(첫 시험). Tripo 텍스처는 조각이 잘게 흩어져 있어(얼굴 삼각형 UV 가 텍스처 전체에 퍼짐)
#   텍스처 공간에서 '피부에 둘러싸인 구멍'을 찾을 수도 없다(둘째 시험) → 앞에서 본 뒤 겹 그림에서 찾고, 텍셀은 그 뒤 겹 삼각형만 칠한다.
FV = 512
orig, decal, pos = D['orig'], D['decal'], D['pos']
fdir = np.array(info['front'])
front = np.zeros((FV, FV, 3), np.float32)
depth = np.full((FV, FV), np.inf, np.float32)
tid = np.full((FV, FV), -1, np.int32)
for t in range(len(orig)):
    r = raster(uv2tex(decal[t], FV, FV), FV, FV)
    if r is None:
        continue
    ys, xs, bc = r
    z = bc @ (pos[t] @ fdir)
    m = z < depth[ys, xs]
    if not m.any():
        continue
    ys, xs, bc, z = ys[m], xs[m], bc[m], z[m]
    ouv = bc @ orig[t]
    tx = np.clip((ouv[:, 0] * TW).astype(int), 0, TW - 1)
    ty = np.clip(((1 - ouv[:, 1]) * TH).astype(int), 0, TH - 1)
    front[ys, xs] = tex[ty, tx]
    depth[ys, xs] = z
    tid[ys, xs] = t
fmask = tid >= 0
Image.fromarray((np.clip(front, 0, 1) * 255).astype(np.uint8)).save(os.path.join(work, 'front.png'))   # 배치값 고를 때 보는 그림
lum = front @ np.array([0.299, 0.587, 0.114])
band = np.zeros_like(fmask)
band[int(FV * 0.55):int(FV * 0.80), int(FV * 0.30):int(FV * 0.70)] = True       # 행 = 위에서부터(볼·코)
band &= fmask
cand = band & (lum > np.quantile(lum[band], 0.45))
skin = np.median(front[cand], 0)
dist = np.linalg.norm(front - skin, axis=2)
skin_l = float(skin @ np.array([0.299, 0.587, 0.114]))
skinm = fmask & (dist < 0.17)
# 이목구비 찾기(색으로): 둘러싸임으로는 못 찾는다 — 눈꺼풀 선이 텍스처에 그려진 앞머리와 이어져 있다(세 번째 시험).
#   머리털 색 = 맨 위 15% 행의 중앙값. 눈 = 피부도 머리털도 아닌 밝은 색(흰자·홍채), 입·코 = 피부보다 확 어두운 선(아래 가운데)
top = fmask.copy()
top[int(FV * 0.15):] = False
hair_col = np.median(front[top], 0) if top.any() else np.array([0.1, 0.1, 0.12])
dh = np.linalg.norm(front - hair_col, axis=2)
rows = np.arange(FV)[:, None] / FV          # 위에서부터 0~1
cols = np.arange(FV)[None, :] / FV
eye_px = fmask & (dist > 0.20) & (dh > 0.20) & (lum > 0.22) & (rows > 0.30) & (rows < 0.70) & (np.abs(cols - 0.5) < 0.40)
bright = fmask & (lum > 0.55) & (dist > 0.15)                 # 흰자·홍채 반사 — 눈 덩어리 점수
eye_px = np.asarray(Image.fromarray((eye_px * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
dark = fmask & (lum < skin_l * 0.78)
mouth_px = dark & (rows > 0.62) & (rows < 0.88) & (np.abs(cols - 0.5) < 0.16)


def components(m):
    H_, W_ = m.shape
    lab = np.zeros(m.shape, np.int32)
    comps = []
    k = 0
    for y0 in range(H_):
        for x0 in range(W_):
            if m[y0, x0] and lab[y0, x0] == 0:
                k += 1
                st = [(y0, x0)]
                lab[y0, x0] = k
                pts = []
                while st:
                    y, x = st.pop()
                    pts.append((y, x))
                    for dy_, dx_ in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        yy, xx = y + dy_, x + dx_
                        if 0 <= yy < H_ and 0 <= xx < W_ and m[yy, xx] and lab[yy, xx] == 0:
                            lab[yy, xx] = k
                            st.append((yy, xx))
                p = np.array(pts)
                comps.append(dict(n=len(p), cy=p[:, 0].mean() / H_, cx=p[:, 1].mean() / W_, w=(p[:, 1].max() - p[:, 1].min() + 1) / W_, h=(p[:, 0].max() - p[:, 0].min() + 1) / H_,
                                  r0=int(p[:, 0].min()), r1=int(p[:, 0].max()), c0=int(p[:, 1].min()), c1=int(p[:, 1].max())))
    return comps


mcomp = sorted([c for c in components(np.asarray(Image.fromarray((mouth_px * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 0) if c['n'] >= 20], key=lambda c: -c['n'])[:1]
mid = mcomp[0]['cx'] if mcomp else 0.5                       # 얼굴 가운데선 = 입
# 눈 짝 고르기: 머리털 하이라이트·옷깃 조각도 '피부·머리털 아닌 밝은 색'이라 섞인다(배달 시우 — 처음엔 양옆 머리털을 눈으로 잡음).
#   후보 = 너무 크지 않은 덩어리, 점수 = 상자 안 밝은 화소 수. 왼쪽·오른쪽 한 개씩, 높이가 비슷하고 가운데선(입)에서 거리가 비슷한 짝.
eyes = [c for c in components(eye_px) if c['n'] >= 25 and c['w'] < 0.30 and c['h'] < 0.20]
for c in eyes:
    c['score'] = float(bright[c['r0']:c['r1'] + 1, c['c0']:c['c1'] + 1].sum()) + 0.25 * c['n']
Ls = [c for c in eyes if 0.06 < mid - c['cx'] < 0.30]
Rs = [c for c in eyes if 0.06 < c['cx'] - mid < 0.30]
best, L_, R_ = -1.0, [], []
for a_ in Ls:
    for b_ in Rs:
        sc_ = min(a_['score'], b_['score']) * math.exp(-abs(a_['cy'] - b_['cy']) / 0.04) * math.exp(-abs((mid - a_['cx']) - (b_['cx'] - mid)) / 0.05)
        if sc_ > best:
            best, L_, R_ = sc_, [a_], [b_]
if not (L_ and R_):
    L_ = sorted(Ls, key=lambda c: -c['score'])[:1]
    R_ = sorted(Rs, key=lambda c: -c['score'])[:1]
    if L_ and R_:
        L_, R_ = (L_, []) if L_[0]['score'] >= R_[0]['score'] else ([], R_)
mouth = mcomp
box = info['face_box']
bw, bh = box['width'], box['height']
found = 'both' if (L_ and R_) else 'one' if (L_ or R_) else 'none'
if L_ and R_:
    off = 0.5 * ((mid - L_[0]['cx']) + (R_[0]['cx'] - mid))
    ev_ = 1 - 0.5 * (L_[0]['cy'] + R_[0]['cy'])
elif L_ or R_:
    c = (L_ or R_)[0]
    off = abs(c['cx'] - mid)
    ev_ = 1 - c['cy']
else:
    off, ev_ = 0.14, 0.48
eyeL, eyeR = (mid - off, ev_), (mid + off, ev_)               # 좌우 대칭(가운데선 = 입)
eye_v = ev_
gap = 2 * off
ew = 0.5 * gap                                                # 눈 폭 ≈ 눈 가운데 사이의 반(FUJIMOTO 3-3)
mouth_v = 1 - mouth[0]['cy'] if mouth else eye_v - 0.31 / 0.47 * (eye_v - (info['chin'] - box['z0']) / bh)
mouth_u = mid
# 인물별 얼굴 배치값(tools/face_layout.json — 4-3 '인물마다 얼굴 배치값만 다름'): 있으면 자동 찾기 대신 쓴다.
#   Tripo 텍스처는 한쪽 눈이 번져 있거나(시우·배달 시우) 눈썹이 눈꺼풀에 붙어 있어(태오) 색으로 찾기가 인물마다 흔들렸다 → 앞에서 본 그림(front.png)에
#   격자를 대고 눈 가운데·반지름, 입 자리를 한 번 적어 둔다. 지우는 자리 = 그 타원 안의 피부 아닌 화소.
LAY = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'face_layout.json'), encoding='utf-8')).get(name)
if LAY:
    mid, off, ev_ = LAY['mid'], LAY['eye_off'], LAY['eye_v']
    eyeL, eyeR = (mid - off, ev_), (mid + off, ev_)
    eye_v, gap = ev_, 2 * off
    ew = LAY.get('eye_w', 0.5 * gap)
    mouth_u, mouth_v = LAY['mouth']
    found = 'layout'
eye_z = box['z0'] + eye_v * bh
# 턱끝: 얼굴 앞면 상자 맨 아래에는 목 앞쪽 뾰족한 조각이 섞인다 → 피부 화소 너비가 가장 넓은 줄의 25% 이상인 가장 아래 줄
skin_rows = (skinm & fmask).sum(1)
wmax = skin_rows.max() if skin_rows.size else 1
chin_row = int(np.where(skin_rows >= 0.25 * wmax)[0].max()) if (skin_rows >= 0.25 * wmax).any() else FV - 1
chin_z = box['z0'] + (1 - chin_row / FV) * bh
# 두개골 기준 머리 높이(FUJIMOTO 2-1: 정수리 피부~턱끝, 머리털 볼륨 = 머리 높이의 6~10% → 8%)
head_h = (info['head_top'] - chin_z) / 1.08
heads = info['height'] / head_h
head_h_eye = (eye_z - chin_z) / 0.47            # 참고: 눈선 .53 규칙으로 잰 값(애니풍 원래 눈이 낮으면 크게 나옴)
# 지울 자리(눈): 밝은 눈(흰자·홍채) 덩어리 상자 안의 피부 아닌 화소 + 상자 위아래로 '피부가 나올 때까지' 이어진 선(눈꺼풀·속눈썹)만.
#   위로는 4mm, 아래로는 2.5mm 까지만 올라간다 — 눈썹이 눈꺼풀에 붙어 그려진 경우(태오)에도 눈썹은 남는다.
#   (타원으로 지웠더니 태오 눈썹이 함께 지워졌고, 사각형으로 지웠더니 시우 앞머리가 띠처럼 잘렸다 → 눈썹·앞머리는 원래 텍스처 것을 쓴다)
ppm_v = FV / (bh * 1000.0)                 # 데칼 그림의 세로 화소/mm (얼굴 상자 가로세로가 달라 축마다 다름)
ppm_u = FV / (bw * 1000.0)
notskin = fmask & (dist > 0.10)
hole = np.zeros_like(fmask)
up_max, dn_max = max(3, int(round(4.0 * ppm_v))), max(2, int(round(2.5 * ppm_v)))
for E in ([] if LAY else (L_ + R_)):
    a, b, c, d = E['r0'] + 2, E['r1'] - 2, E['c0'] + 2, E['c1'] - 2   # 덩어리는 5칸 최대 필터로 부풀린 것 → 2칸 줄임
    if b < a or d < c:
        continue
    hole[a:b + 1, c:d + 1] |= notskin[a:b + 1, c:d + 1]
    ext = max(2, int(round(0.25 * (d - c + 1))))                       # 눈꼬리 속눈썹(밝은 덩어리 밖 가로 25%)
    cy_ = int(round(0.5 * (a + b)))
    for x in range(max(c - ext, 0), min(d + ext, FV - 1) + 1):
        inside = c <= x <= d
        top0, bot0 = (a - 1, b + 1) if inside else (cy_, cy_ + 1)
        lim_up = up_max + (0 if inside else (cy_ - a))
        lim_dn = dn_max + (0 if inside else (b - cy_))
        y, k = top0, 0
        while y >= 0 and k < lim_up and notskin[y, x]:
            hole[y, x] = True
            y -= 1
            k += 1
        y, k = bot0, 0
        while y < FV and k < lim_dn and notskin[y, x]:
            hole[y, x] = True
            y += 1
            k += 1
if LAY:
    for (eu, ev) in (eyeL, eyeR):
        hole |= (((cols - eu) / LAY['eye_rx']) ** 2 + ((rows - (1 - ev)) / LAY['eye_ry']) ** 2 <= 1.0) & fmask      # 타원 안 전부(번진 흰 자국·옅은 속눈썹까지)
    mu, mv = LAY['mouth']
    hole |= (((cols - mu) / LAY['mouth_rx']) ** 2 + ((rows - (1 - mv)) / LAY['mouth_ry']) ** 2 <= 1.0) & fmask
nose = np.zeros_like(fmask)
nose[int((1 - (eye_v - 0.06)) * FV):int((1 - (mouth_v + 0.05)) * FV), int((mid - 0.07) * FV):int((mid + 0.07) * FV)] = True
mreg = np.zeros_like(fmask)
mreg[int((1 - mouth_v - 0.05) * FV):int((1 - mouth_v + 0.06) * FV), int((mid - 0.6 * gap) * FV):int((mid + 0.6 * gap) * FV)] = True
hole |= (nose | mreg) & fmask & (lum < skin_l - 0.06)
hole = (np.asarray(Image.fromarray((hole * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0) & fmask

# ───────── ② 구멍 메우기(뒤 겹 그림에서 둘레 피부를 퍼뜨림) → 그 뒤 겹 삼각형의 텍셀에만 되돌림(앞머리 카드는 그대로)
fill = front.copy()
fill[hole] = skin
for _ in range(220):
    acc = np.zeros_like(fill)
    cnt = np.zeros(fill.shape[:2], np.float32)
    for dy_, dx_ in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        acc += np.roll(np.roll(fill * fmask[..., None], dy_, 0), dx_, 1)
        cnt += np.roll(np.roll(fmask, dy_, 0), dx_, 1)
    avg = acc / np.maximum(cnt, 1)[..., None]
    fill[hole] = avg[hole]
out_tex = tex.copy()
painted = 0
for t in np.unique(tid[hole]):
    if t < 0:
        continue
    r = raster(uv2tex(orig[t]), TW, TH)
    if r is None:
        continue
    ys, xs, bc = r
    duv = bc @ decal[t]
    fx = np.clip((duv[:, 0] * FV).astype(int), 0, FV - 1)
    fy = np.clip(((1 - duv[:, 1]) * FV).astype(int), 0, FV - 1)
    m = hole[fy, fx] & (tid[fy, fx] == t)
    out_tex[ys[m], xs[m]] = fill[fy[m], fx[m]]
    painted += int(m.sum())
Image.fromarray((np.clip(out_tex, 0, 1) * 255).astype(np.uint8)).save(os.path.join(out, f'{name}Tex.jpg'), quality=93)

# ───────── ③ 부위 마스크 · 1단 그림자 · 외곽선 굵기(1024² 부위 래스터 → 4096 으로 키움)
MS = 1024
cls_img = np.full((MS, MS), -1, np.int8)
for t in range(len(D['all_uv'])):
    r = raster(uv2tex(D['all_uv'][t], MS, MS), MS, MS)
    if r is None:
        continue
    ys, xs, _ = r
    cls_img[ys, xs] = D['all_cls'][t]
small2 = np.asarray(Image.fromarray((np.clip(out_tex, 0, 1) * 255).astype(np.uint8)).resize((MS, MS), Image.BILINEAR)).astype(np.float32) / 255
sd = np.linalg.norm(small2 - skin, axis=2)
sl = small2 @ np.array([0.299, 0.587, 0.114])
mx, mn = small2.max(2), small2.min(2)
sat = (mx - mn) / np.maximum(mx, 1e-3)
hue_orange = (small2[..., 0] > small2[..., 1] * 1.35) & (small2[..., 1] > small2[..., 2] * 1.1) & (sat > 0.45) & (sl > 0.25)
is_skin = sd < 0.20
part = np.zeros((MS, MS), np.int8)          # 0 옷 · 1 피부 · 2 머리 · 3 강조
part[is_skin] = 1
part[(cls_img == 1) & ~is_skin] = 2
part[(cls_img == 3)] = 1
part[(cls_img == 2)] = 1
part[hue_orange & (cls_img != 3)] = 3
mask = np.zeros((MS, MS, 4), np.uint8)
mask[..., 0] = (part == 1) * 255
mask[..., 1] = (part == 2) * 255
mask[..., 2] = (part == 0) * 255
mask[..., 3] = (part == 3) * 255
Image.fromarray(mask, 'RGBA').save(os.path.join(out, f'{name}Mask.png'))
mul = np.zeros((MS, MS, 3), np.float32)
mul[:] = (0.70, 0.70, 0.78)                                     # 짙은 옷·머리
mul[(part == 0) & (sl > 0.72)] = (0.78, 0.80, 0.88)             # 흰 옷
mul[(part == 0) & (sl > 0.45) & (sl <= 0.72) & (sat < 0.25)] = (0.74, 0.74, 0.80)   # 회색
mul[part == 1] = (0.80, 0.72, 0.72)                             # 피부
mul[cls_img == 3] = (0.93, 0.88, 0.88)                          # 얼굴 앞면(거의 평면)
mul[part == 3] = (0.80, 0.74, 0.74)                             # 강조색
mulb = np.asarray(Image.fromarray((mul * 255).astype(np.uint8)).resize((TW, TH), Image.NEAREST).filter(ImageFilter.BoxBlur(2))).astype(np.float32) / 255
shade = np.clip(out_tex * mulb, 0, 1)
Image.fromarray((shade * 255).astype(np.uint8)).save(os.path.join(out, f'{name}Shade.jpg'), quality=92)
ow = np.ones((MS, MS), np.float32)
hair = part == 2
hb = np.asarray(Image.fromarray((hair * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))) > 0
ow[hb & ~hair] = 0.45            # 머리 덩어리 경계(머리털 둘레 안쪽 띠)
ow[cls_img == 3] = 0.0           # 얼굴 앞면 = 0(얼굴 선은 텍스처)
noise = np.asarray(Image.fromarray((rng.random((32, 32)) * 255).astype(np.uint8)).resize((MS, MS), Image.BICUBIC)).astype(np.float32) / 255
ow = np.clip(ow * (0.75 + 0.5 * noise), 0, 1)
ow = np.asarray(Image.fromarray((ow * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(1)))
Image.fromarray(ow).convert('RGB').save(os.path.join(out, f'{name}OutW.png'))      # RGB 로(UTS 는 .r 을 읽음 — 1채널 PNG 는 Unity 가 Alpha8 로 들일 수 있어서)

# ───────── ④ 표정 아틀라스
CELL, N = 409, 5
SS = 3                                   # 3배로 그려 줄임(매끈한 선)
EXPR = ['normal', 'hurt', 'ko', 'win', 'endure']
STATE = ['open', 'half', 'closed', 'talkA', 'talkB']
LID_MM = 6.6                             # 윗눈꺼풀 굵기(월드) — 탐색 4m(FOV 45, 1080p)에서 2px 이상(D14 · FUJIMOTO 7-2)
lid_px = LID_MM / 1000.0 / bw * CELL     # 칸 화소
shadow = np.clip(skin * np.array([0.80, 0.72, 0.72]), 0, 1)
IRIS = {'siwoo': (0x2B, 0x1E, 0x18), 'taeo': (0x3A, 0x2A, 0x20)}[who]
COVER = {'siwoo': 0.35, 'taeo': 0.25}[who]
C = lambda c, a=255: tuple(int(round(v * 255)) for v in c) + (a,)
ink, shd, white = C(INK), C(shadow, 230), (246, 241, 234, 255)


ASP = bh / bw                             # 데칼 UV 는 얼굴 상자(가로 bw · 세로 bh)를 0~1 로 편 것 → 월드에서 고른 굵기로 그리려면 세로를 ASP 배 길게 그린 뒤 정사각 칸으로 줄인다(태오 2.0)


def draw_cell(expr, state):
    S = CELL * SS
    SH = int(round(S * ASP))
    im = Image.new('RGBA', (S, SH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    X = lambda u: u * S
    Y = lambda v: (1 - v) * SH
    lw = lid_px * SS
    e_w = ew * S
    e_h = e_w / 3.2
    for side, (eu, ev) in (('L', eyeL), ('R', eyeR)):
        cx, cy = X(eu), Y(ev)
        outer = -1 if side == 'L' else 1          # 바깥쪽(관자놀이) 방향 — 그림 왼쪽 눈은 왼쪽이 바깥
        open_ = {'open': 1.0, 'half': 0.45, 'closed': 0.0, 'talkA': 1.0, 'talkB': 1.0}[state]
        cover = COVER
        if expr == 'hurt':
            if side == ('L' if who == 'siwoo' else 'X'):
                open_ = 0.0                       # 시우: 한쪽 꽉 감음
            else:
                cover = 0.05; open_ *= 1.15       # 크게 뜸
        if expr == 'ko':
            cover = 0.5
        if expr == 'win' and who == 'taeo':
            open_ = 0.0                           # 태오 실눈 웃음(위로 휜 곡선)
        if expr == 'endure':
            cover = 0.48                          # 이 악문 무표정 — 더 덮음
        h = e_h * open_
        top = cy - e_h * 0.55
        if h > 0.5:
            # 흰자 + 홍채(윗눈꺼풀이 cover 만큼 덮음) + 동공
            d.ellipse([cx - e_w / 2, cy - h / 2, cx + e_w / 2, cy + h / 2], fill=white)
            ir = e_w * 0.36 / 2
            ic_x = cx + outer * e_w * (0.08 if expr != 'ko' else 0.16)      # 시선 살짝 옆
            ic_y = cy + (ir * 0.15 if expr != 'ko' else -ir * 0.35)
            eye_clip = Image.new('L', (S, SH), 0)
            ImageDraw.Draw(eye_clip).ellipse([cx - e_w / 2, cy - h / 2, cx + e_w / 2, cy + h / 2], fill=255)
            iris = Image.new('RGBA', (S, SH), (0, 0, 0, 0))
            di = ImageDraw.Draw(iris)
            di.ellipse([ic_x - ir, ic_y - ir, ic_x + ir, ic_y + ir], fill=IRIS + (255,), outline=ink, width=max(1, int(lw * 0.25)))
            if expr != 'ko':
                pr = ir * 0.30
                di.ellipse([ic_x - pr, ic_y - pr, ic_x + pr, ic_y + pr], fill=ink)
            lid_cut = Image.new('L', (S, SH), 255)
            ImageDraw.Draw(lid_cut).rectangle([0, 0, S, cy - h / 2 + h * cover], fill=0)
            from PIL import ImageChops
            m = ImageChops.multiply(eye_clip, lid_cut)
            im.paste(iris, (0, 0), ImageChops.multiply(m, iris.split()[3]))
            # 윗눈꺼풀: 가운데~바깥 1/3 가장 굵게, 꼬리는 눈 폭 10%까지만
            ly = cy - h / 2 + h * cover
            pts = [(cx - e_w / 2 - outer * 0.0, cy), (cx - e_w * 0.25, ly), (cx + e_w * 0.25, ly), (cx + e_w / 2, cy - h * 0.05)]
            d.line(pts, fill=ink, width=int(lw), joint='curve')
            tail = (cx + outer * e_w * 0.6, ly + h * 0.1)
            d.line([(cx + outer * e_w * 0.45, ly + h * 0.02), tail], fill=ink, width=int(lw * 0.7))
            # 쌍꺼풀(가늘게) · 아래눈꺼풀(바깥 2/3)
            d.arc([cx - e_w * 0.5, ly - e_h * 0.55, cx + e_w * 0.5, ly + e_h * 0.35], 200, 340, fill=shd, width=max(1, int(lw * 0.33)))
            lo_y = cy + h / 2
            x_in, x_out = cx - outer * e_w * 0.15, cx + outer * e_w * 0.5
            d.line([(min(x_in, x_out), lo_y), (max(x_in, x_out), lo_y - h * 0.08)], fill=ink, width=max(1, int(lw * 0.33)))
        else:
            # 감은 눈: 한 획(실눈 웃음이면 위로 휜 곡선)
            if expr == 'win' and who == 'taeo':
                d.arc([cx - e_w / 2, cy - e_h * 0.6, cx + e_w / 2, cy + e_h * 0.9], 200, 340, fill=ink, width=int(lw * 0.8))
            else:
                squeeze = expr == 'hurt'
                d.line([(cx - e_w / 2, cy + (e_h * 0.15 if squeeze else 0)), (cx, cy - (e_h * 0.12 if squeeze else -e_h * 0.08)), (cx + e_w / 2, cy + (e_h * 0.15 if squeeze else 0))],
                       fill=ink, width=int(lw * 0.85), joint='curve')
        # 다크서클(시우 2줄 · 태오 1줄)
        for k in range(2 if who == 'siwoo' else 1):
            yy = cy + e_h * (0.75 + 0.3 * k)
            d.arc([cx - e_w * 0.42, yy - e_h * 0.3, cx + e_w * 0.42, yy + e_h * 0.3], 20, 160, fill=shd, width=max(1, int(lw * 0.25)))
        # 눈썹: 가는 한 획(거의 수평, 미간 쪽 살짝 내려감)
        by = cy - e_h * 2.1
        tilt = {'normal': 0.06, 'hurt': 0.22, 'ko': -0.05, 'win': 0.0, 'endure': 0.18}[expr] * e_w
        inner_x, outer_x = cx - outer * e_w * 0.45, cx + outer * e_w * 0.55
        if expr in ('hurt', 'endure'):     # 눈썹은 몸 텍스처 것을 그대로 쓴다 — 찡그린 표정만 미간 쪽 짧은 획을 더함
            d.line([(inner_x, by + tilt), (inner_x + outer * e_w * 0.3, by + tilt * 0.4)], fill=ink, width=max(1, int(lw * (0.4 if who == 'siwoo' else 0.53))))
    # 미간 주름(hurt · endure)
    mx_ = X(0.5 * (eyeL[0] + eyeR[0]))
    if expr in ('hurt', 'endure'):
        for k in (-1, 1):
            d.line([(mx_ + k * e_w * 0.08, Y(eye_v) - e_h * 2.2), (mx_ + k * e_w * 0.05, Y(eye_v) - e_h * 1.5)], fill=shd, width=max(1, int(lw * 0.3)))
    # 코: 그림자 쪽 짧은 세로선 + 콧구멍 틱
    nv = eye_v - (0.74 - 0.53) / (0.84 - 0.53) * (eye_v - mouth_v)
    nx = X(mouth_u) + e_w * 0.12
    d.line([(nx, Y(nv) - e_h * 0.9), (nx, Y(nv))], fill=shd, width=max(1, int(lw * 0.33)))
    d.line([(nx - e_w * 0.12, Y(nv) + e_h * 0.1), (nx - e_w * 0.04, Y(nv) + e_h * 0.12)], fill=shd, width=max(1, int(lw * 0.33)))
    # 입
    mx2, my = X(mouth_u), Y(mouth_v)
    mw = gap * S * 0.55
    talk = {'talkA': 0.5, 'talkB': 0.25}.get(state, 0.0)
    if expr == 'ko':
        d.ellipse([mx2 - mw * 0.35, my - mw * 0.12, mx2 + mw * 0.35, my + mw * 0.22], fill=(58, 36, 36, 255), outline=ink, width=int(lw * 0.5))
        d.line([(mx2 - mw * 0.25, my - mw * 0.06), (mx2 + mw * 0.25, my - mw * 0.06)], fill=white, width=max(1, int(lw * 0.5)))
    elif expr in ('hurt', 'endure'):
        # 이 악물기: 흰 띠 + 세로 구분선
        hh = mw * (0.16 if expr == 'hurt' else 0.08)
        d.rectangle([mx2 - mw * 0.45, my - hh, mx2 + mw * 0.45, my + hh], fill=white, outline=ink, width=int(lw * 0.5))
        for k in range(5):
            xx = mx2 - mw * 0.3 + k * mw * 0.15
            d.line([(xx, my - hh), (xx, my + hh)], fill=ink, width=max(1, int(lw * 0.25)))
    elif expr == 'win':
        if who == 'siwoo':     # 한쪽 입꼬리만 올라간 짧은 비웃음
            d.line([(mx2 - mw * 0.4, my + mw * 0.02), (mx2 + mw * 0.1, my), (mx2 + mw * 0.45, my - mw * 0.16)], fill=ink, width=int(lw * 0.5), joint='curve')
            d.polygon([(mx2 + mw * 0.05, my + 1), (mx2 + mw * 0.38, my - mw * 0.12), (mx2 + mw * 0.3, my - mw * 0.03)], fill=white)
        else:                  # 크게 벌린 웃음 + 윗니 줄
            d.chord([mx2 - mw * 0.5, my - mw * 0.35, mx2 + mw * 0.5, my + mw * 0.35], 0, 180, fill=(70, 40, 40, 255), outline=ink, width=int(lw * 0.5))
            d.rectangle([mx2 - mw * 0.42, my, mx2 + mw * 0.42, my + mw * 0.08], fill=white)
    else:
        if talk > 0:
            d.ellipse([mx2 - mw * 0.25, my - mw * 0.05, mx2 + mw * 0.25, my + mw * talk * 0.4], fill=(70, 40, 40, 255), outline=ink, width=int(lw * 0.45))
        elif who == 'taeo':    # 태오 기본: 한쪽만 살짝 올린 반미소
            d.line([(mx2 - mw * 0.4, my), (mx2 + mw * 0.2, my), (mx2 + mw * 0.42, my - mw * 0.08)], fill=ink, width=int(lw * 0.5), joint='curve')
        else:
            d.line([(mx2 - mw * 0.4, my), (mx2 + mw * 0.4, my)], fill=ink, width=int(lw * 0.5))
        d.line([(mx2 - mw * 0.15, my + mw * 0.14), (mx2 + mw * 0.15, my + mw * 0.14)], fill=shd, width=max(1, int(lw * 0.3)))      # 아랫입술 그림자 대시
    return im.resize((CELL, CELL), Image.LANCZOS)


atlas = Image.new('RGBA', (2048, 2048), (0, 0, 0, 0))
for r_, ex in enumerate(EXPR):
    for c_, st in enumerate(STATE):
        atlas.paste(draw_cell(ex, st), (c_ * CELL, r_ * CELL))
atlas.save(os.path.join(out, f'{name}Face.png'))

meta = dict(name=name, who=who, cells=N, cell_px=CELL, atlas_px=2048, expr=EXPR, state=STATE,
            lid_mm=LID_MM, lid_px=round(lid_px, 2), face_box_m=[bw, bh],
            eye_found=found, eyeL=eyeL, eyeR=eyeR, eye_w=ew, mouth=[mouth_u, mouth_v],
            skin=[round(float(x), 3) for x in skin], painted_texels=painted, holes=int(hole.sum()),
            head_h_skull=round(head_h, 4), heads=round(heads, 3), head_h_eye=round(head_h_eye, 4), chin_z=round(chin_z, 4), height=info['height'],
            head_top=info['head_top'], head_scale=info.get('head_scale', 1.0), body_rescale=info.get('body_rescale', 1.0), heads_target=info.get('heads_target', 0.0),
            tris_lod0=info.get('tris_lod0'), tris_lod1=info.get('tris_lod1'), tris_lod2=info.get('tris_lod2'), tris_face=info.get('tris_face'), face_overlay_polys=info.get('face_overlay_polys', 0))
json.dump(meta, open(os.path.join(out, f'{name}Face.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
# 점검 그림(작업 폴더): 정면 · 구멍 · 메운 정면
chk = np.concatenate([front, np.repeat(hole[..., None], 3, 2).astype(np.float32), fill], 1)
Image.fromarray((np.clip(chk, 0, 1) * 255).astype(np.uint8)).save(os.path.join(work, 'face_check.png'))
print('FACE', json.dumps(meta, ensure_ascii=False))
