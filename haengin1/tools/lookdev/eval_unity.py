# 룩데브 Unity 적용 검사(docs/14 9장 '구현 결과') — ZLookShots 사진(변형별 폴더)을 프레임과 짝지어 재고, 시간대·장소 LUT 를 다시 맞추고, 비교 시트를 만든다(크레딧 0)
# 사용:
#   python tools/lookdev/eval_unity.py <사진 루트> <출력 폴더> [--variants now,base,ink,...] [--before <사진 루트>/<변형>]
#          [--fit-lut <변형> --lut-out concept/art/env/lut] [--sheet <변형>] [--offline-lut]
#   <사진 루트>/<변형>/{z1_*,st_s0*,gym_g0*}.png = Unity ZLookShots(-lookshots <루트> -lookvariants ...)
# 결과: <출력>/eval.json(짝별·변형별 지표) · eval.txt(묶음 평균 표) · compare.jpg(프레임 ∣ 적용 전 ∣ 적용 후, Zone1 8 + 실내 4 + 체육관 2)
# LUT 맞춤(--fit-lut): 묶음마다 그 변형 사진 → 프레임 색 분포 IDT(ld_core, 14 문서 3-1 과 같은 방법) → 33³ .cube + URP 띠 32(1024×32) + lut_meta.json
#   묶음: Z1_Dawn · Z1_Day · Z1_Dusk · Z1_Night(Zone1 시간대) · Gukbap · Home · Conv · School(실내 장소별 — 14 문서 3-4 'H6 교실' 권고) · Gym
import os, sys, json, glob, time
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ld_core as ld

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
FR = os.path.join(REPO, 'concept', 'art', 'env')
argv = sys.argv[1:]
SRC, OUT = argv[0], argv[1]


def opt(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d


os.makedirs(OUT, exist_ok=True)


def F(pat):
    return glob.glob(os.path.join(FR, pat))[0]


# (이름, Unity 파일, 프레임, 묶음, 실내, 역할) — 역할: pair(평균·LUT 맞춤) · hold(LUT 맞춤에 안 씀)
PAIRS = [
    ('Z1 후문 상가거리 밤 ↔ f01', 'z1_shot_1_night', 'frames/f01_*.png', 'Z1_Night', False, 'pair'),
    ('Z1 뒷골목 주차장 노을 ↔ f02', 'z1_shot_2', 'frames/f02_*.png', 'Z1_Dusk', False, 'pair'),
    ('Z1 골목 계단 새벽 ↔ f03', 'z1_shot_3', 'frames/f03_*.png', 'Z1_Dawn', False, 'pair'),
    ('Z1 성곽 아래 집 낮 ↔ f04', 'z1_shot_8', 'frames/f04_*.png', 'Z1_Day', False, 'pair'),
    ('Z1 와룡 오르막 새벽 ↔ f05', 'z1_shot_4', 'frames/f05_*.png', 'Z1_Dawn', False, 'pair'),
    ('Z1 성곽길 낮 ↔ f06', 'z1_shot_5', 'frames/f06_*.png', 'Z1_Day', False, 'pair'),
    ('Z1 명륜3가 골목 밤 ↔ f08', 'z1_shot_7_night', 'frames/f08_*.png', 'Z1_Night', False, 'pair'),
    ('Z1 한옥 골목 낮 ↔ f04', 'z1_shot_6', 'frames/f04_*.png', 'Z1_Day', False, 'pair'),
    ('H 후문 상가거리 새벽 ↔ f03', 'z1_tod_1_Dawn', 'frames/f03_*.png', 'Z1_Dawn', False, 'hold'),
    ('H 후문 상가거리 노을 ↔ f02', 'z1_tod_1_Dusk', 'frames/f02_*.png', 'Z1_Dusk', False, 'hold'),
    ('H 명륜3가 골목 낮 ↔ f03', 'z1_shot_7', 'frames/f03_*.png', 'Z1_Day', False, 'hold'),
    ('실내 국밥집 주방 새벽 ↔ s01', 'st_s01', 'stages/frames/s01_*.png', 'Gukbap', True, 'pair'),
    ('실내 국밥집 홀 저녁 ↔ s02', 'st_s02', 'stages/frames/s02_*.png', 'Gukbap', True, 'pair'),
    ('실내 시우네 아침 ↔ s03', 'st_s03', 'stages/frames/s03_*.png', 'Home', True, 'pair'),
    ('실내 시우네 밤 ↔ s04', 'st_s04', 'stages/frames/s04_*.png', 'Home', True, 'pair'),
    ('실내 편의점 통로 밤 ↔ s05', 'st_s05', 'stages/frames/s05_*.png', 'Conv', True, 'pair'),
    ('실내 편의점 계산대 ↔ s06', 'st_s06', 'stages/frames/s06_*.png', 'Conv', True, 'pair'),
    ('실내 교실 뒷문 ↔ s07', 'st_s07', 'stages/frames/s07_*.png', 'School', True, 'pair'),
    ('실내 교실 앞 ↔ s08', 'st_s08', 'stages/frames/s08_*.png', 'School', True, 'pair'),
    ('체육관 계단 위 낮 ↔ g01', 'gym_g01', 'gym/frames/g01_*.png', 'Gym', True, 'pair'),
    ('체육관 케이지 저녁 ↔ g02', 'gym_g02', 'gym/frames/g02_*.png', 'Gym', True, 'pair'),
    ('체육관 매트 저녁 ↔ g03', 'gym_g03', 'gym/frames/g03_*.png', 'Gym', True, 'pair'),
    ('체육관 웨이트 아침 ↔ g04', 'gym_g04', 'gym/frames/g04_*.png', 'Gym', True, 'pair'),
    ('체육관 사무실 창 ↔ g05', 'gym_g05', 'gym/frames/g05_*.png', 'Gym', True, 'hold'),
    ('체육관 밤 코칭 ↔ g06', 'gym_g06', 'gym/frames/g06_*.png', 'Gym', True, 'pair'),
]
SHEET_ROWS = ['z1_shot_1_night', 'z1_shot_2', 'z1_shot_3', 'z1_shot_8', 'z1_shot_4', 'z1_shot_5', 'z1_shot_7_night', 'z1_shot_6',
              'st_s02', 'st_s03', 'st_s05', 'st_s07', 'gym_g01', 'gym_g02']
CLASS = {'Z1': lambda p: p[1].startswith('z1') and p[5] == 'pair', 'IN': lambda p: p[1].startswith('st_'), 'GYM': lambda p: p[1].startswith('gym') and p[5] == 'pair',
         'HOLD': lambda p: p[5] == 'hold'}
t0 = time.time()
_fcache = {}


def frame_m(fpat, size, indoor):
    k = (fpat, size)
    if k not in _fcache:
        _fcache[k] = ld.metrics(ld.load(F(fpat), size), indoor)
    return _fcache[k]


def measure(img, p):
    size = (img.shape[1], img.shape[0])
    mf = frame_m(p[2], size, p[4])
    mu = ld.metrics(img, p[4])
    c = ld.compare(mu, mf)
    c['line_w_u'] = mu['line_w']; c['line_w_f'] = mf['line_w']
    c['near_range_u'] = mu['near_range']; c['near_range_f'] = mf['near_range']
    c['L_p5_u'] = mu['L_p5']; c['L_p5_f'] = mf['L_p5']
    c['C_mean_u'] = mu['C_mean']; c['C_mean_f'] = mf['C_mean']
    c['sh_lab_u'] = mu['sh_lab']; c['sh_lab_f'] = mf['sh_lab']
    c['line_lab_u'] = mu['line_lab']; c['line_lab_f'] = mf['line_lab']
    if 'far_contrast_u' in c:
        c['far_diff'] = abs(c['far_contrast_u'] - c['far_contrast_f'])
    return c, mu, mf


def avg(rows, key):
    v = [r[key] for r in rows if key in r and r[key] is not None]
    return float(np.mean(v)) if v else float('nan')


KEYS = [('palette_de', '팔레트 ΔE', '{:.2f}'), ('shadow_de', '그늘 ΔE', '{:.2f}'), ('line_ratio', '먹선 배율', '{:.2f}'), ('line_w_u', '먹선 굵기 px', '{:.2f}'),
        ('edge_ratio', '디테일 배율', '{:.2f}'), ('far_diff', '먼 대비 차', '{:.2f}'), ('L_p5_u', '어두운 5% L*', '{:.1f}'), ('C_mean_u', '채도 C*', '{:.1f}')]

variants = (opt('--variants') or ','.join(sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d))))).split(',')
before = opt('--before')
res = {'variants': {}, 'pairs': [p[:2] + (p[3], p[5]) for p in PAIRS]}
lines = []


def eval_dir(folder, tag):
    out = {}
    for p in PAIRS:
        f = os.path.join(folder, p[1] + '.png')
        if not os.path.exists(f):
            continue
        img = ld.load(f, Image.open(f).size)
        c, mu, mf = measure(img, p)
        out[p[1]] = {k: (float(v) if isinstance(v, (float, np.floating, int)) else v) for k, v in c.items()}
    res['variants'][tag] = out
    return out


def table(tag, out):
    lines.append(f'── {tag}')
    for cls, fn in CLASS.items():
        rows = [out[p[1]] for p in PAIRS if fn(p) and p[1] in out]
        if not rows:
            continue
        s = ' · '.join(f'{nm} {fmt.format(avg(rows, k))}' for k, nm, fmt in KEYS)
        mx = max(r['palette_de'] for r in rows)
        lines.append(f'  {cls:4s}({len(rows)}) {s} · 팔레트 최대 {mx:.2f}')
    for key, nm in (('z1_shot_5', '근경 밝기 폭 낮 P5(f06)'), ('z1_shot_1_night', '근경 밝기 폭 밤 P1(f01)')):
        if key in out:
            lines.append(f'  {nm}: {out[key]["near_range_u"]:.1f} (프레임 {out[key]["near_range_f"]:.1f})')


if before:
    table('적용 전 ' + before, eval_dir(before, 'before'))
for v in variants:
    d = os.path.join(SRC, v)
    if os.path.isdir(d):
        table(v, eval_dir(d, v))
        print(f'[평가] {v} · {time.time() - t0:.0f}s', flush=True)

# ───────────── LUT 다시 맞추기
fit = opt('--fit-lut')
if fit:
    lut_out = opt('--lut-out', os.path.join(REPO, 'concept', 'art', 'env', 'lut'))
    os.makedirs(lut_out, exist_ok=True)
    rng = np.random.default_rng(0)
    groups = {}
    for p in PAIRS:
        if p[5] != 'pair':
            continue
        f = os.path.join(SRC, fit, p[1] + '.png')
        if os.path.exists(f):
            groups.setdefault(p[3], []).append((f, p))
    meta = {}
    luts = {}
    for g, lst in groups.items():
        su, sf = [], []
        for f, p in lst:
            iu = ld.load(f, Image.open(f).size)
            iff = ld.load(F(p[2]), (iu.shape[1], iu.shape[0]))
            lu, lf = ld.lab(iu).reshape(-1, 3), ld.lab(iff).reshape(-1, 3)
            su.append(lu[rng.choice(len(lu), 80000, replace=False)]); sf.append(lf[rng.choice(len(lf), 80000, replace=False)])
        steps = ld.idt_fit(np.concatenate(su), np.concatenate(sf), n_iter=10)
        # 세기 고르기: 그 묶음 짝에서 (팔레트 ΔE + 0.6 × 그늘 ΔE) 평균이 가장 낮은 세기(LUT 가 그늘을 벌리는 묶음은 약하게 — 체육관처럼 ② 로 이미 가까운 곳)
        best = None
        for strength in (0.35, 0.55, 0.75, 0.9):
            lut_ = ld.build_lut(steps, 33, strength, 1.1)
            sc = []
            for f, p in lst:
                im = ld.apply_lut(ld.load(f, Image.open(f).size), lut_)
                c, _, _ = measure(im, p)
                sc.append(c['palette_de'] + 0.6 * c['shadow_de'])
            score = float(np.mean(sc))
            print(f'[LUT] {g} 세기 {strength}: 점수 {score:.2f}', flush=True)
            if best is None or score < best[0]:
                best = (score, strength, lut_)
        _, strength, lut = best
        luts[g] = lut
        nm = 'LUT_' + g
        ld.write_cube(os.path.join(lut_out, nm + '.cube'), lut, f'Haengin1 {g} lookdev v2 (Unity, 2026-10-26)')
        t = np.linspace(0, 1, 32)
        b, gg, r = np.meshgrid(t, t, t, indexing='ij')
        l32 = ld.apply_lut(np.stack([r, gg, b], -1).astype(np.float32).reshape(-1, 1, 3), lut).reshape(32, 32, 32, 3)
        ld.write_strip(os.path.join(lut_out, nm + '_URP32.png'), l32)
        meta[nm] = {'group': g, 'fit_variant': fit, 'fit_pairs': [[p[1], os.path.basename(F(p[2]))] for _, p in lst], 'size': 33, 'strength': strength,
                    'grid_smooth_sigma': 1.1, 'space': 'sRGB(display) → sRGB, URP HDR 등급 모드(내부 등급 뒤에 적용)'}
        print(f'[LUT] {g}: 짝 {len(lst)}', flush=True)
    with open(os.path.join(lut_out, 'lut_meta.json'), 'w', encoding='utf-8') as fo:
        json.dump(meta, fo, ensure_ascii=False, indent=1)
    if '--offline-lut' in argv:
        # 예상치: 그 변형 사진에 LUT 를 오프라인으로 얹어 잰다(Unity 결과가 이것과 ±0.5 안이면 색 공간·방향이 맞음 — 14 문서 4-11)
        out = {}
        for p in PAIRS:
            f = os.path.join(SRC, fit, p[1] + '.png')
            if not os.path.exists(f) or p[3] not in luts:
                continue
            img = ld.apply_lut(ld.load(f, Image.open(f).size), luts[p[3]])
            c, _, _ = measure(img, p)
            out[p[1]] = {k: (float(v) if isinstance(v, (float, np.floating, int)) else v) for k, v in c.items()}
        res['variants'][fit + '+LUT(오프라인)'] = out
        table(fit + ' + LUT(오프라인 예상)', out)

with open(os.path.join(OUT, 'eval.json'), 'w', encoding='utf-8') as fo:
    json.dump(res, fo, ensure_ascii=False, indent=1, default=float)
with open(os.path.join(OUT, 'eval.txt'), 'w', encoding='utf-8') as fo:
    fo.write('\n'.join(lines) + '\n')
print('\n'.join(lines))

# ───────────── 비교 시트: 프레임 ∣ 적용 전 ∣ 적용 후
sheet = opt('--sheet')
if sheet and before:
    W, H, G, T = 640, 360, 6, 24
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    rows = [p for k in SHEET_ROWS for p in PAIRS if p[1] == k]
    im = Image.new('RGB', (3 * W + 4 * G, len(rows) * (H + T + G) + G + 30), (34, 32, 30))
    dr = ImageDraw.Draw(im)
    dr.text((G, 6), f'룩데브 Unity 적용 — 프레임 | 적용 전 | 적용 후({sheet}) · 수치는 프레임 대비(팔레트 ΔE · 그늘 ΔE · 먹선 배율 · 디테일 배율)', fill=(240, 236, 228), font=font)
    for i, p in enumerate(rows):
        y = 30 + G + i * (H + T + G)
        fb = res['variants'].get('before', {}).get(p[1]); fa = res['variants'].get(sheet, {}).get(p[1])
        cells = [(F(p[2]), '프레임 ' + p[0].split('↔')[-1].strip()), (os.path.join(before, p[1] + '.png'), '적용 전', fb), (os.path.join(SRC, sheet, p[1] + '.png'), '적용 후', fa)]
        for j, cell in enumerate(cells):
            x = G + j * (W + G)
            path = cell[0]
            if os.path.exists(path):
                a = Image.open(path).convert('RGB')
                w0, h0 = a.size
                if abs(w0 / h0 - 16 / 9) > 0.01:
                    nh = round(w0 * 9 / 16); a = a.crop((0, (h0 - nh) // 2, w0, (h0 - nh) // 2 + nh))
                im.paste(a.resize((W, H), Image.LANCZOS), (x, y + T))
            txt = (p[0].split('↔')[0].strip() + ' — ' + cell[1]) if j == 0 else cell[1]
            if j > 0 and cell[2]:
                c = cell[2]
                txt += f'   ΔE {c["palette_de"]:.1f} · 그늘 {c["shadow_de"]:.1f} · 선 {c["line_ratio"]:.2f} · 디테일 {c["edge_ratio"]:.2f}'
            dr.text((x + 2, y + 3), txt, fill=(236, 232, 224), font=font)
    im.save(os.path.join(OUT, 'compare.jpg'), quality=86)
    print('[시트]', os.path.join(OUT, 'compare.jpg'), im.size)
