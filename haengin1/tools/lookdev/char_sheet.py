# 캐릭터 텍스처 단순화 비교 시트(docs/14 5장) — 설정화 · 지금(Tripo 텍스처) · 단순화 렌더를 나란히 + 수치
# 사용: python tools/lookdev/char_sheet.py <설정화 png> <before.png> <after.png> <출력 jpg>
# 수치(캐릭터 영역 안):
#   잡음 = 경계 아닌 곳의 L* 고주파(σ1.5 잔차) 표준편차 — 사실 질감·얼룩이 많을수록 큼
#   90% 색 수 = Lab 6단위 칸으로 나눴을 때 화소 90% 를 덮는 칸 수 — 평면 칠일수록 작음
#   팔레트 ΔE = 설정화와 12색 팔레트 양방향 ΔE2000
import sys, os, json
import numpy as np, cv2
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ld_core as ld

concept_p, before_p, after_p, out_p = sys.argv[1:5]
H = 1200


def figure_rgba(p):
    im = Image.open(p)
    if im.mode == 'RGBA':
        a = np.asarray(im).astype(np.float32) / 255
        return a[..., :3], a[..., 3] > 0.5
    rgb = np.asarray(im.convert('RGB')).astype(np.float32) / 255
    lb = ld.lab(rgb)
    bg = np.median(lb[:40, :40].reshape(-1, 3), 0)
    d = np.linalg.norm(lb - bg, axis=-1)
    m = (d > 12).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))       # 가는 안내선 지움
    n, lbl, st, _ = cv2.connectedComponentsWithStats(m, 8)
    big = 1 + np.argmax(st[1:, cv2.CC_STAT_AREA])
    m = (lbl == big).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)).astype(bool)
    ys, xs = np.where(m)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    s = (H * 0.86) / (y1 - y0)
    rgb = cv2.resize(rgb, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    m = cv2.resize(m.astype(np.uint8), None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST).astype(bool)
    return rgb, m


def stats(rgb, m):
    lb = ld.lab(rgb); L = lb[..., 0]
    g = np.hypot(cv2.Sobel(cv2.GaussianBlur(L, (0, 0), 1.5), cv2.CV_32F, 1, 0), cv2.Sobel(cv2.GaussianBlur(L, (0, 0), 1.5), cv2.CV_32F, 0, 1)) / 8
    inner = cv2.erode(m.astype(np.uint8), np.ones((7, 7), np.uint8)).astype(bool) & (g < 2.0)
    res = L - cv2.GaussianBlur(L, (0, 0), 1.5)
    q = np.floor(lb[m] / 6).astype(np.int32)
    _, cnt = np.unique(q, axis=0, return_counts=True)
    cnt = np.sort(cnt)[::-1]; c90 = int(np.searchsorted(np.cumsum(cnt) / cnt.sum(), 0.9) + 1)
    pal = ld.palette(lb[m].reshape(-1, 1, 3), k=12)
    return {'noise': float(res[inner].std()), 'colors90': c90}, pal


figs = [figure_rgba(p) for p in (concept_p, before_p, after_p)]
res = []
for rgb, m in figs:
    st, pal = stats(rgb, m)
    res.append((st, pal))
for i in (1, 2):
    res[i][0]['palette_de_vs_concept'] = ld.palette_de(res[i][1], res[0][1])
FONT = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 22); FB = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 26)
W = 3 * 800 + 40
sheet = Image.new('RGB', (W, H + 150), (240, 235, 222))
d = ImageDraw.Draw(sheet)
d.text((20, 12), '캐릭터 질감 — 설정화(목표) · 지금 Tripo 텍스처 · 단순화(색 군집 평탄화 + 주름 선) — 같은 셀 규칙·외곽선으로 렌더', font=FB, fill=(30, 26, 28))
names = ['설정화 siwoo_color_v2', '지금 SiwooTex', '단순화 Tex_flat']
for i, (rgb, m) in enumerate(figs):
    h, w = rgb.shape[:2]
    canvas = np.ones((H, 800, 3), np.float32) * np.array([240, 235, 222]) / 255
    ys, xs = np.where(m)
    cy, cx = int((ys.min() + ys.max()) / 2), int((xs.min() + xs.max()) / 2)
    y0 = max(0, cy - H // 2); x0 = max(0, cx - 400)
    crop = rgb[y0:y0 + H, x0:x0 + 800]; mk = m[y0:y0 + H, x0:x0 + 800]
    canvas[:crop.shape[0], :crop.shape[1]][mk] = crop[mk]
    sheet.paste(Image.fromarray((canvas * 255).astype(np.uint8)), (20 + i * 800, 56))
    st = res[i][0]
    txt = f"{names[i]}  ·  잡음 {st['noise']:.2f}  ·  90% 색 수 {st['colors90']}"
    if 'palette_de_vs_concept' in st:
        txt += f"  ·  설정화와 ΔE {st['palette_de_vs_concept']:.1f}"
    d.text((20 + i * 800, H + 70), txt, font=FONT, fill=(30, 26, 28))
sheet.save(out_p, quality=90)
with open(os.path.splitext(out_p)[0] + '.json', 'w', encoding='utf-8') as fo:
    json.dump([r[0] for r in res], fo, ensure_ascii=False, indent=1)
print('CHAR', json.dumps([r[0] for r in res], ensure_ascii=False))
