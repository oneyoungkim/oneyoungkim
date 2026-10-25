# 룩데브 시안 실행(docs/14_룩데브_계획.md 2·3장) — 프레임 ↔ Unity 같은 장면 짝을 재고, Unity 화면에 후처리 6단계를 얹어 다시 잰다.
# 사용: python tools/lookdev/run_lookdev.py <Unity 사진 루트(scratchpad)> <출력 폴더> [--lut-out concept/art/env/lut]
#   Unity 사진 루트 아래: env_unity/shots_final7/*.png(카메라 B, 7차 패스) · gym_unity/compare/g0N.png · m3_shots/stages/stage_s0N.png
# 결과: <출력>/lookdev_sheet.jpg(단계별 전·후 + 수치) · diag_sheet.jpg(진단: 팔레트·그늘·선 색 견본) · detail_sheet.jpg(100% 확대) · results.json
#       --lut-out 에 시간대별 3D LUT(.cube 33³) + URP Color Lookup 띠 PNG(32² × 32)
# 단계: ① 시간대 LUT(프레임 팔레트로 IDT 색 전이, 33³, 세기 0.85) ② 그늘 색조 나누기 ③ 먹선 지우고 Kuwahara + 붓 결
#       ④ 굵기 변하는 색 먹선 + 색 경계 잔선 ⑤ 대기 원근(소실점 가우스 — 깊이 없음) ⑥ 종이 결
# 크레딧 0. 1920×1080 한 장에 단계 전부 약 20초.
import os, sys, json, time, glob
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ld_core as ld

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
SRC = sys.argv[1]
OUT = sys.argv[2]
LUT_OUT = sys.argv[sys.argv.index('--lut-out') + 1] if '--lut-out' in sys.argv else None
os.makedirs(OUT, exist_ok=True)
FR = os.path.join(REPO, 'concept', 'art', 'env')


def U(rel):
    return os.path.join(SRC, rel)


def F(pat):
    return glob.glob(os.path.join(FR, pat))[0]


Z = 'env_unity/shots_final7/'
# (이름, Unity, 프레임, 묶음, 실내)
PAIRS = [
    ('P1 후문 상가거리 밤', U(Z + 'shot_1_night.png'), F('frames/f01_*.png'), 'Night', False),
    ('P2 뒷골목 주차장 노을', U(Z + 'shot_2.png'), F('frames/f02_*.png'), 'Dusk', False),
    ('P3 골목 계단 새벽', U(Z + 'shot_3.png'), F('frames/f03_*.png'), 'Dawn', False),
    ('P4 와룡 오르막 새벽', U(Z + 'shot_4.png'), F('frames/f05_*.png'), 'Dawn', False),
    ('P5 성곽길 낮', U(Z + 'shot_5.png'), F('frames/f06_*.png'), 'Day', False),
    ('P6 성곽 아래 집 낮', U(Z + 'shot_8.png'), F('frames/f04_*.png'), 'Day', False),
    ('P7 체육관 계단 위 낮', U('gym_unity/compare/g01.png'), F('gym/frames/g01_*.png'), 'Gym', True),
    ('P8 국밥집 홀 저녁', U('m3_shots/stages/stage_s02.png'), F('stages/frames/s02_*.png'), 'Stage', True),
]
# LUT 맞춤에만 더 쓰는 짝(시트엔 안 나옴)
FIT_EXTRA = [
    (U('gym_unity/compare/g02.png'), F('gym/frames/g02_*.png'), 'Gym'),
    (U('m3_shots/stages/stage_s01.png'), F('stages/frames/s01_*.png'), 'Stage'),
    (U('m3_shots/stages/stage_s03.png'), F('stages/frames/s03_*.png'), 'Stage'),
    (U('m3_shots/stages/stage_s04.png'), F('stages/frames/s04_*.png'), 'Stage'),
]
# 보지 않은 화면(LUT 맞춤에 안 쓴 Unity 사진) — 일반화 확인
HOLD = [
    ('H1 명륜3가 골목 밤 ↔ f08', U(Z + 'shot_7_night.png'), F('frames/f08_*.png'), 'Night', False),
    ('H2 한옥 골목 낮 ↔ f04', U(Z + 'shot_6.png'), F('frames/f04_*.png'), 'Day', False),
    ('H3 후문 상가거리 새벽 ↔ f03', U(Z + 'tod_1_Dawn.png'), F('frames/f03_*.png'), 'Dawn', False),
    ('H4 후문 상가거리 노을 ↔ f02', U(Z + 'tod_1_Dusk.png'), F('frames/f02_*.png'), 'Dusk', False),
    ('H5 체육관 사무실 창 ↔ g05', U('gym_unity/compare/g05.png'), F('gym/frames/g05_*.png'), 'Gym', True),
    ('H6 교실 뒷문 ↔ s07', U('m3_shots/stages/stage_s07.png'), F('stages/frames/s07_*.png'), 'Stage', True),
]
FOG = {'Night': 0.22, 'Dusk': 0.30, 'Dawn': 0.36, 'Day': 0.30, 'Gym': 0.10, 'Stage': 0.06}
STEPS = ['Unity 원본', '① 시간대 LUT', '② 그늘 색조', '③ 붓 그림', '④ 색 먹선', '⑤ 대기 원근', '⑥ 종이 결']

t0 = time.time()


def size_of(p):
    with Image.open(p) as im:
        return im.size


def load_pair(u, f):
    sz = size_of(u)
    return ld.load(u, sz), ld.load(f, sz)


# ───────────── 1. 프레임 지표 → 묶음별 목표값
cache = {}


def fmet(fpath, size, indoor):
    k = (fpath, size)
    if k not in cache:
        cache[k] = ld.metrics(ld.load(fpath, size), indoor)
    return cache[k]


groups = {}
for name, u, f, g, ind in PAIRS:
    groups.setdefault(g, []).append((u, f, ind))
for u, f, g in FIT_EXTRA:
    groups.setdefault(g, []).append((u, f, g in ('Gym', 'Stage')))

target = {}
luts = {}
rng = np.random.default_rng(0)
for g, lst in groups.items():
    su, sf, mets = [], [], []
    for u, f, ind in lst:
        iu, iff = load_pair(u, f)
        lu, lf = ld.lab(iu).reshape(-1, 3), ld.lab(iff).reshape(-1, 3)
        su.append(lu[rng.choice(len(lu), 80000, replace=False)]); sf.append(lf[rng.choice(len(lf), 80000, replace=False)])
        mets.append(fmet(f, iu.shape[1::-1], ind))
    steps = ld.idt_fit(np.concatenate(su), np.concatenate(sf), n_iter=10)
    luts[g] = ld.build_lut(steps, 33, 0.85, 0.9)
    T = {}
    for key in ('sh_lab', 'lit_lab', 'line_lab', 'far_lab'):
        vals = [m[key] for m in mets if key in m]
        if vals:
            T[key] = list(np.mean(vals, 0))
    T['grain'] = float(np.mean([m['grain'] for m in mets]))
    target[g] = T
    print(f'[LUT] {g}: 짝 {len(lst)} · {time.time() - t0:.0f}s', flush=True)

if LUT_OUT:
    os.makedirs(LUT_OUT, exist_ok=True)
    meta = {}
    for g, lut in luts.items():
        nm = f'LUT_Z1_{g}' if g in ('Dawn', 'Day', 'Dusk', 'Night') else f'LUT_{g}'
        ld.write_cube(os.path.join(LUT_OUT, nm + '.cube'), lut, f'Haengin1 {g} lookdev v1')
        # URP 띠 32: 33³ 를 32 격자로 다시 뽑기
        t = np.linspace(0, 1, 32)
        b, gg, r = np.meshgrid(t, t, t, indexing='ij')
        l32 = ld.apply_lut(np.stack([r, gg, b], -1).astype(np.float32).reshape(-1, 1, 3), lut).reshape(32, 32, 32, 3)
        ld.write_strip(os.path.join(LUT_OUT, nm + '_URP32.png'), l32)
        meta[nm] = {'group': g, 'fit_pairs': [[os.path.basename(u), os.path.basename(f)] for u, f, _ in groups[g]],
                    'size': 33, 'strength': 0.85, 'grid_smooth_sigma': 0.9, 'space': 'sRGB(display) → sRGB, 톤매핑 뒤',
                    'shadow_target_lab': target[g].get('sh_lab'), 'lit_target_lab': target[g].get('lit_lab'),
                    'line_color_lab': target[g].get('line_lab')}
    with open(os.path.join(LUT_OUT, 'lut_meta.json'), 'w', encoding='utf-8') as fo:
        json.dump(meta, fo, ensure_ascii=False, indent=1)
    print('[LUT] 저장', LUT_OUT, flush=True)


# ───────────── 2. 단계 적용
def run_steps(img, g, indoor, full=True):
    T = target[g]
    L0 = ld.lab(img)[..., 0]
    bh0, lm0 = ld.line_field(L0)
    out = [img]
    s1 = ld.apply_lut(img, luts[g]); out.append(s1)
    s2 = ld.shadow_split(s1, T['sh_lab'][1:], T['lit_lab'][1:], tgt_sh_L=T['sh_lab'][0]); out.append(s2)
    if not full:
        return out
    s3 = ld.brush(ld.kuwahara(ld.remove_lines(s2, lm0))); out.append(s3)
    s4 = ld.draw_lines(s3, bh0, T.get('line_lab', [20, 4, 6])); out.append(s4)
    fl = T.get('far_lab', [70, 0, -6])
    fog_lab = [min(fl[0] + 10, 92), fl[1] * 0.6, fl[2] * 0.6 - 3]
    s5 = ld.fog(s4, fog_lab, FOG[g], vp=(0.5, 0.40 if indoor else 0.42), indoor=indoor); out.append(s5)
    s6 = ld.paper(s5); out.append(s6)
    return out


results = {'pairs': [], 'hold': [], 'targets': target}
thumbs = {}
for name, u, f, g, ind in PAIRS:
    iu, iff = load_pair(u, f)
    mf = fmet(f, iu.shape[1::-1], ind)
    imgs = run_steps(iu, g, ind)
    rows = []
    for si, im in enumerate(imgs):
        mu = ld.metrics(im, ind)
        d = ld.compare(mu, mf)
        rows.append({'step': STEPS[si], 'cmp': d, 'm': {k: v for k, v in mu.items() if k != '_pal'}})
        if si == 0:
            mu0 = mu
    results['pairs'].append({'name': name, 'group': g, 'unity': os.path.relpath(u, SRC), 'frame': os.path.relpath(f, REPO),
                             'frame_m': {k: v for k, v in mf.items() if k != '_pal'}, 'steps': rows,
                             'pal_u': [mu0['_pal'][0].tolist(), mu0['_pal'][1].tolist()], 'pal_f': [mf['_pal'][0].tolist(), mf['_pal'][1].tolist()]})
    thumbs[name] = (imgs, iff)
    tag = name.split()[0]
    ld.save(os.path.join(OUT, f'{tag}_final.jpg'), imgs[-1], 92)
    print(f'[짝] {name}: ΔE {rows[0]["cmp"]["palette_de"]:.1f} → ① {rows[1]["cmp"]["palette_de"]:.1f} → ⑥ {rows[-1]["cmp"]["palette_de"]:.1f} · {time.time() - t0:.0f}s', flush=True)

for name, u, f, g, ind in HOLD:
    iu, iff = load_pair(u, f)
    mf = fmet(f, iu.shape[1::-1], ind)
    imgs = run_steps(iu, g, ind, full=False)
    rows = []
    for si, im in enumerate(imgs):
        d = ld.compare(ld.metrics(im, ind), mf)
        rows.append({'step': STEPS[si], 'cmp': d})
    results['hold'].append({'name': name, 'group': g, 'steps': rows})
    print(f'[보지 않은] {name}: ΔE {rows[0]["cmp"]["palette_de"]:.1f} → ① {rows[1]["cmp"]["palette_de"]:.1f} → ② {rows[2]["cmp"]["palette_de"]:.1f}', flush=True)

with open(os.path.join(OUT, 'results.json'), 'w', encoding='utf-8') as fo:
    json.dump(results, fo, ensure_ascii=False, indent=1, default=float)

# ───────────── 3. 시트
FONT = 'C:/Windows/Fonts/malgun.ttf'
FONTB = 'C:/Windows/Fonts/malgunbd.ttf'
f14, f16, f20, f26 = (ImageFont.truetype(FONT, s) for s in (14, 16, 20, 26))
b20, b30 = ImageFont.truetype(FONTB, 20), ImageFont.truetype(FONTB, 30)
TW, TH = 400, 225
BG = (24, 22, 26); FG = (236, 232, 224); DIM = (150, 146, 140); ACC = (232, 170, 90); GOOD = (130, 210, 140); BAD = (235, 120, 110)


def thumb(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).resize((TW, TH), Image.LANCZOS)


def ratio_col(x):
    return GOOD if 0.67 <= x <= 1.5 else BAD


SHEET_ROWS = ['P2 뒷골목 주차장 노을', 'P3 골목 계단 새벽', 'P5 성곽길 낮', 'P1 후문 상가거리 밤', 'P7 체육관 계단 위 낮', 'P8 국밥집 홀 저녁']
cols = len(STEPS) + 1
RH = TH + 70
W = 20 + cols * (TW + 8) + 12
Htab = 60 + 26 * (len(PAIRS) + len(HOLD) + 4)
H = 120 + len(SHEET_ROWS) * RH + Htab
sheet = Image.new('RGB', (W, H), BG)
d = ImageDraw.Draw(sheet)
d.text((20, 14), '행인1 룩데브 시안 — Unity 같은 장면에 후처리 6단계를 얹어 스타일 프레임과의 거리를 잼 (Unity 없이, Python 오프라인)', font=b30, fill=FG)
d.text((20, 58), 'ΔE = 16색 팔레트 양방향 ΔE2000(낮을수록 프레임과 같은 색을 씀) · 그늘ΔE = 그늘 평균색 차 · 선/결/붓 = 프레임 대비 배율(1.0 이 같음, 0.67~1.5 초록) · ⑤ 는 깊이 없이 소실점 근사', font=f16, fill=DIM)
y = 92
for ci, nm in enumerate(STEPS + ['스타일 프레임(목표)']):
    d.text((20 + ci * (TW + 8), y), nm, font=b20, fill=ACC if ci < len(STEPS) else GOOD)
y += 30
byname = {p['name']: p for p in results['pairs']}
for rn in SHEET_ROWS:
    imgs, fr = thumbs[rn]
    pr = byname[rn]
    for ci in range(cols):
        x = 20 + ci * (TW + 8)
        sheet.paste(thumb(imgs[ci] if ci < len(STEPS) else fr), (x, y))
        if ci < len(STEPS):
            c = pr['steps'][ci]['cmp']
            d.text((x + 2, y + TH + 4), f"ΔE {c['palette_de']:.1f}  그늘ΔE {c['shadow_de']:.1f}", font=f16, fill=FG)
            d.text((x + 2, y + TH + 26), f"선 ×{c['line_ratio']:.2f}", font=f14, fill=ratio_col(c['line_ratio']))
            d.text((x + 92, y + TH + 26), f"결 ×{c['grain_ratio']:.2f}", font=f14, fill=ratio_col(c['grain_ratio']))
            d.text((x + 182, y + TH + 26), f"붓 ×{c['brush_ratio']:.2f}", font=f14, fill=ratio_col(c['brush_ratio']))
            if 'far_contrast_u' in c:
                d.text((x + 272, y + TH + 26), f"먼대비 {c['far_contrast_u']:.2f}", font=f14, fill=DIM)
        else:
            m = pr['frame_m']
            d.text((x + 2, y + TH + 4), rn, font=f16, fill=GOOD)
            if 'far_contrast' in m:
                d.text((x + 272, y + TH + 26), f"먼대비 {m['far_contrast']:.2f}", font=f14, fill=DIM)
    y += RH
# 표: 단계별 팔레트 ΔE(보지 않은 화면 포함)
y += 10
d.text((20, y), '팔레트 ΔE2000 — 단계별 (P = LUT 맞춤에 쓴 짝, H = 맞춤에 안 쓴 Unity 사진: 일반화 확인)', font=b20, fill=FG)
y += 34
xs = [20, 380] + [380 + 120 * i for i in range(1, len(STEPS) + 1)]
d.text((xs[0], y), '장면', font=f16, fill=DIM)
for i, s in enumerate(STEPS):
    d.text((xs[1] + 120 * i, y), s, font=f16, fill=DIM)
d.text((xs[1] + 120 * len(STEPS), y), '개선(원본→끝)', font=f16, fill=DIM)
y += 26
for blk in (results['pairs'], results['hold']):
    for p in blk:
        d.text((xs[0], y), p['name'], font=f16, fill=FG)
        vals = [s['cmp']['palette_de'] for s in p['steps']]
        for i, v in enumerate(vals):
            d.text((xs[1] + 120 * i, y), f'{v:.1f}', font=f16, fill=FG)
        imp = (vals[0] - vals[-1]) / vals[0] * 100
        d.text((xs[1] + 120 * len(STEPS), y), '%.1f → %.1f (%s%.0f%%)' % (vals[0], vals[-1], '-' if imp > 0 else '+', abs(imp)), font=f16, fill=GOOD if imp > 0 else BAD)
        y += 26
    y += 8
avg = lambda blk, i: float(np.mean([p['steps'][i]['cmp']['palette_de'] for p in blk]))
sp_ = ' → '.join('%.1f' % avg(results['pairs'], i) for i in range(len(STEPS)))
sh_ = ' → '.join('%.1f' % avg(results['hold'], i) for i in range(3))
d.text((xs[0], y), '평균 P: ' + sp_ + '   ·   평균 H(①②까지): ' + sh_, font=b20, fill=ACC)
sheet.save(os.path.join(OUT, 'lookdev_sheet.jpg'), quality=88)
print('[시트] lookdev_sheet.jpg', sheet.size, flush=True)

# 100% 확대(결·선·붓): P3·P5·P2 한 칸씩 — 원본 / ③ / ⑥ / 프레임
crops = [('P3 골목 계단 새벽', (520, 330)), ('P5 성곽길 낮', (700, 380)), ('P2 뒷골목 주차장 노을', (300, 300))]
CW, CH = 560, 360
det = Image.new('RGB', (20 + 4 * (CW + 8), 70 + len(crops) * (CH + 40)), BG)
dd = ImageDraw.Draw(det)
dd.text((20, 14), '100% 확대 — 원본 · ③ 붓 그림 · ⑥ 전 단계 · 프레임(같은 자리 아님, 결 비교용)', font=b20, fill=FG)
yy = 56
for nm, (cx, cy) in crops:
    imgs, fr = thumbs[nm]
    for ci, a in enumerate([imgs[0], imgs[3], imgs[6], fr]):
        im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).crop((cx, cy, cx + CW, cy + CH))
        det.paste(im, (20 + ci * (CW + 8), yy))
    dd.text((20, yy + CH + 6), nm, font=f16, fill=DIM)
    yy += CH + 40
det.save(os.path.join(OUT, 'detail_sheet.jpg'), quality=90)

# 진단 시트: 짝마다 Unity·프레임 + 팔레트 띠 + 그늘/밝은 면/선 색 견본
def swatch(lab3):
    return tuple(int(x * 255) for x in ld.rgb(np.array(lab3, np.float64).reshape(1, 1, 3))[0, 0])


DW, DH = 320, 180
dg = Image.new('RGB', (20 + 2 * (DW + 8) + 760, 60 + len(PAIRS) * (DH + 24)), BG)
gd = ImageDraw.Draw(dg)
gd.text((20, 14), '진단 — 왼쪽 Unity · 가운데 프레임 · 오른쪽 위 Unity / 아래 프레임: 16색 팔레트(넓이 = 비율) · 그늘 · 밝은 면 · 먹선 색', font=b20, fill=FG)
yy = 56
for p in results['pairs']:
    imgs, fr = thumbs[p['name']]
    dg.paste(Image.fromarray((imgs[0] * 255).astype(np.uint8)).resize((DW, DH)), (20, yy))
    dg.paste(Image.fromarray((fr * 255).astype(np.uint8)).resize((DW, DH)), (20 + DW + 8, yy))
    x0 = 20 + 2 * (DW + 8) + 10
    for k, (pal, mm) in enumerate(((p['pal_u'], p['steps'][0]['m']), (p['pal_f'], p['frame_m']))):
        yb = yy + 4 + k * 88
        cs, ws = np.array(pal[0]), np.array(pal[1])
        order = np.argsort(cs[:, 0])
        x = x0
        for i in order:
            wpx = max(1, int(round(ws[i] * 480)))
            gd.rectangle([x, yb, x + wpx, yb + 40], fill=swatch(cs[i])); x += wpx
        for j, (lbl, key) in enumerate((('그늘', 'sh_lab'), ('밝은 면', 'lit_lab'), ('먹선', 'line_lab'))):
            xs0 = x0 + 500 + j * 80
            gd.rectangle([xs0, yb, xs0 + 60, yb + 40], fill=swatch(mm[key]))
            gd.text((xs0, yb + 44), lbl, font=f14, fill=DIM)
        gd.text((x0, yb + 44), ('Unity' if k == 0 else '프레임') + f"  L*{mm['L_mean']:.0f} C*{mm['C_mean']:.0f} 그늘 h{mm['sh_hue']:.0f}°", font=f14, fill=FG)
    gd.text((20, yy + DH + 2), p['name'], font=f14, fill=DIM)
    yy += DH + 24
dg.save(os.path.join(OUT, 'diag_sheet.jpg'), quality=88)
print(f'[끝] {time.time() - t0:.0f}s', flush=True)
