# 캐릭터 텍스처 '단순화' 시험(docs/14 5장) — Tripo 사실 질감(구운 주름 명암·얼룩·잡음)을 설정화처럼 평면 색 + 주름 선으로
# 사용: python tools/lookdev/tex_flatten.py <몸 텍스처 Tex.jpg> <그늘 텍스처 Shade.jpg> <출력 폴더> [색 수 k=16] [합칠 ΔE=10.5]
# 하는 일(크레딧 0):
#   ① 2048 로 줄여 양방향 필터 3번(가장자리 지키며 잡음·얼룩 지움)
#   ② Lab k-평균(k 색) → ΔE2000 10.5 안쪽 색끼리 합침(같은 옷감 한 색) → 화소마다 그 색으로(평면 칠) → 중앙값 5 로 부스러기 정리
#   ③ 원래 텍스처의 깊은 주름(블랙햇 9px ÷ 주변 밝기 > 0.22)만 '주름 선'으로 남김(평면 색 × 0.70, 설정화의 옷 주름 선 역할)
#   ④ 그늘 텍스처 = 평면 색 × (원래 Shade ÷ 원래 Tex 곱셈값, 부위별 상수라 중앙값 9 로 고름) — face_tex.py 의 부위별 그늘 곱셈 유지
#   결과: <출력>/Tex_flat.jpg · Shade_flat.jpg (4096) · tex_flatten.json(색 수·지운 질감 양)
import os, sys, json
import numpy as np, cv2
from PIL import Image
from skimage import color as skc
from sklearn.cluster import MiniBatchKMeans

src, shade_src, out = sys.argv[1], sys.argv[2], sys.argv[3]
K = int(sys.argv[4]) if len(sys.argv) > 4 else 16
MERGE = float(sys.argv[5]) if len(sys.argv) > 5 else 10.5
os.makedirs(out, exist_ok=True)
Image.MAX_IMAGE_PIXELS = None
tex = np.asarray(Image.open(src).convert('RGB'))
shd = np.asarray(Image.open(shade_src).convert('RGB'))
N0 = tex.shape[0]
t2 = cv2.resize(tex, (2048, 2048), interpolation=cv2.INTER_AREA)
s2 = cv2.resize(shd, (2048, 2048), interpolation=cv2.INTER_AREA)

# ① 잡음·얼룩 지우기
sm = t2.copy()
for _ in range(3):
    sm = cv2.bilateralFilter(sm, 9, 22, 7)
lab_s = skc.rgb2lab(sm / 255.0).astype(np.float32)
lab_o = skc.rgb2lab(t2 / 255.0).astype(np.float32)

# ② 평면 칠
f = lab_s.reshape(-1, 3)
idx = np.random.default_rng(0).choice(len(f), 250000, replace=False)
km = MiniBatchKMeans(K, random_state=0, n_init=4, batch_size=8192).fit(f[idx])
lbl = km.predict(f).reshape(2048, 2048)
# 비슷한 색 묶음 합치기(ΔE2000 < MERGE — 같은 옷감이 2~3 톤으로 갈라져 얼룩무늬가 되는 것 방지)
cen = km.cluster_centers_.astype(np.float64); wts = np.bincount(lbl.ravel(), minlength=K).astype(np.float64)
grp = list(range(K))
while True:
    act = sorted(set(grp))
    best = None
    for i in act:
        for j in act:
            if j <= i:
                continue
            d = float(skc.deltaE_ciede2000(cen[i], cen[j]))
            if d < MERGE and (best is None or d < best[0]):
                best = (d, i, j)
    if best is None:
        break
    _, i, j = best
    cen[i] = (cen[i] * wts[i] + cen[j] * wts[j]) / (wts[i] + wts[j]); wts[i] += wts[j]; wts[j] = 0
    grp = [i if g == j else g for g in grp]
lbl = np.array(grp)[lbl]
flat_lab = cen[lbl]
flat = (np.clip(skc.lab2rgb(flat_lab), 0, 1) * 255).astype(np.uint8)
flat = cv2.medianBlur(flat, 5)

# ③ 주름 선
L = lab_o[..., 0]
bh = cv2.morphologyEx(L, cv2.MORPH_BLACKHAT, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)))
Lc = cv2.morphologyEx(L, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)))
fold = np.clip((bh / np.maximum(Lc, 10) - 0.22) / 0.15, 0, 1) * (bh > 4)   # 상대 깊이(짙은 감색 옷의 얕은 주름도)
fold = cv2.GaussianBlur(fold.astype(np.float32), (0, 0), 0.7)
flat_f = (flat.astype(np.float32) * (1 - 0.30 * fold[..., None])).astype(np.uint8)

# ④ 그늘 곱셈값 유지
ratio = s2.astype(np.float32) / np.maximum(t2.astype(np.float32), 8)
ratio = np.clip(ratio, 0.3, 1.2)
ratio = cv2.medianBlur((ratio * 200).astype(np.uint8), 9).astype(np.float32) / 200
shade_f = np.clip(flat_f.astype(np.float32) * ratio, 0, 255).astype(np.uint8)

up = lambda a: cv2.resize(a, (N0, N0), interpolation=cv2.INTER_LINEAR)
Image.fromarray(up(flat_f)).save(os.path.join(out, 'Tex_flat.jpg'), quality=93)
Image.fromarray(up(shade_f)).save(os.path.join(out, 'Shade_flat.jpg'), quality=93)

# 지운 질감 양: 고주파 잔차 표준편차(L*) 전·후
def hf(a):
    l = skc.rgb2lab(a / 255.0)[..., 0].astype(np.float32)
    return float((l - cv2.GaussianBlur(l, (0, 0), 2)).std())
info = {'k': K, 'merged_colors': int(len(set(grp))), 'hf_before': hf(t2), 'hf_after': hf(flat_f), 'fold_px_frac': float((fold > 0.5).mean()),
        'palette_lab': km.cluster_centers_.round(1).tolist(), 'palette_share': (np.bincount(lbl.ravel(), minlength=K) / lbl.size).round(4).tolist()}
with open(os.path.join(out, 'tex_flatten.json'), 'w', encoding='utf-8') as fo:
    json.dump(info, fo, ensure_ascii=False, indent=1)
print('FLAT', json.dumps({k: v for k, v in info.items() if k not in ('palette_lab', 'palette_share')}))
