"""체육관 텍스처 만들기 → concept/art/env/gym/textures/<M_name>.png (1024², 이음매 없음) + M_Poster_Atlas.png (2048²) + textures.json + _sheet.jpg

usage: python tools/env/gym/make_textures.py [--only M_A,M_B]
- 생성 결: textures/src/<name>.png → tools/env_assets/seamless.py (kind 는 specs/textures_meta.json). src 의 JPEG 사본을 남기고 PNG 원본은 커밋하지 않음
- 포스터 아틀라스: src/M_Poster_Atlas.png(4×2 패널, 흰 사이) → 패널을 흰 여백으로 찾아 각 칸(512×1024)에 꽉 채움 → poster_atlas.json
- 절차: M_Fluorescent · M_Mirror · M_Cage_Mesh(알파) · M_Paint_Gym · M_Glass_Clear(알파) · M_Steel_Dark · M_Timer_Red
"""
import json, subprocess, sys, pathlib
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter

HERE = pathlib.Path(__file__).parent
ROOT = HERE.parent.parent.parent
TEX = ROOT / 'concept/art/env/gym/textures'
SRC = TEX / 'src'
SEAMLESS = ROOT / 'tools/env_assets/seamless.py'
meta = json.load(open(HERE / 'specs/textures_meta.json', encoding='utf-8'))
only = sys.argv[sys.argv.index('--only') + 1].split(',') if '--only' in sys.argv else None
S = 1024
rng = np.random.default_rng(12)
TEX.mkdir(parents=True, exist_ok=True)


def hexc(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def grain(amp=0.018, sigma=1.2):
    n = gaussian_filter(rng.standard_normal((S, S)).astype(np.float32), sigma, mode='wrap')
    return n / n.std() * amp


def flat(color, amp=0.018):
    base = np.ones((S, S, 3), np.float32) * hexc(color)
    base += grain(amp)[..., None]
    return np.clip(base, 0, 1)


def save(name, arr, kind, source, meters):
    im = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), 'RGBA' if arr.shape[2] == 4 else 'RGB')
    im.save(TEX / f'{name}.png', optimize=True)
    info[name] = dict(meters=meters, kind=kind, source=source)
    print('OK', name, im.size, im.mode)


info = {}
jpath = TEX / 'textures.json'
if jpath.exists():
    info = json.load(open(jpath, encoding='utf-8'))

# ---- 생성 결 5
for name, m in meta.items():
    if only and name not in only:
        continue
    src = SRC / f'{name}.png'
    if not src.exists():
        print('NOSRC', name); continue
    Image.open(src).convert('RGB').save(SRC / f'{name}.jpg', quality=90)
    out = TEX / f'{name}.png'
    r = subprocess.run([sys.executable, str(SEAMLESS), str(src), str(out), '--kind', m['kind'], '--size', str(S)], capture_output=True, text=True)
    if out.exists():
        info[name] = dict(meters=m['meters'], kind=m['kind'], source=f'generated (GPT Image 2.5), src/{name}.jpg')
        print('OK', name, 'seamless', m['kind'])
    else:
        print('FAIL', name, r.stdout[-300:], r.stderr[-300:])

# ---- 포스터 아틀라스
CELLS = {"cage_seoul_a": (0, 0), "cage_seoul_b": (1, 0), "rules_board": (2, 0), "weight_chart": (3, 0),
         "old_fight_poster": (0, 1), "belt_plaque": (1, 1), "blank_sign": (2, 1), "gym_banner": (3, 1)}
if not only or 'M_Poster_Atlas' in only:
    src = SRC / 'M_Poster_Atlas.png'
    if src.exists():
        im = Image.open(src).convert('RGB')
        Image.open(src).convert('RGB').save(SRC / 'M_Poster_Atlas.jpg', quality=90)
        a = np.asarray(im).astype(int)
        nonwhite = a.min(axis=2) < 235

        def runs(v, minlen):
            out, s = [], None
            for i, f in enumerate(v):
                if f and s is None: s = i
                if not f and s is not None:
                    if i - s >= minlen: out.append((s, i))
                    s = None
            if s is not None and len(v) - s >= minlen: out.append((s, len(v)))
            return out
        rows = runs(nonwhite.mean(axis=1) > 0.02, 60)
        while len(rows) > 2:   # 가장 가까운 두 줄 합치기
            g = [rows[i + 1][0] - rows[i][1] for i in range(len(rows) - 1)]; i = g.index(min(g)); rows[i] = (rows[i][0], rows[i + 1][1]); del rows[i + 1]
        atlas = Image.new('RGB', (2048, 2048), (239, 233, 218))
        cells = {}
        ok = len(rows) == 2
        if ok:
            for r, (y0, y1) in enumerate(rows):
                cols = runs(nonwhite[y0:y1].mean(axis=0) > 0.02, 40)
                while len(cols) > 4:
                    g = [cols[i + 1][0] - cols[i][1] for i in range(len(cols) - 1)]; i = g.index(min(g)); cols[i] = (cols[i][0], cols[i + 1][1]); del cols[i + 1]
                if len(cols) != 4:
                    ok = False; print('ATLAS cols', r, cols); break
                for c, (x0, x1) in enumerate(cols):
                    sub = nonwhite[y0:y1, x0:x1]
                    ys = np.where(sub.mean(axis=1) > 0.02)[0]; yy0, yy1 = y0 + ys[0], y0 + ys[-1] + 1
                    panel = im.crop((x0 + 2, yy0 + 2, x1 - 2, yy1 - 2)).resize((512, 1024), Image.LANCZOS)
                    atlas.paste(panel, (c * 512, r * 1024))
        if not ok:   # 못 찾으면 단순 4×2 등분
            print('ATLAS fallback: equal grid')
            for name, (c, r) in CELLS.items():
                atlas.paste(im.crop((c * im.width // 4, r * im.height // 2, (c + 1) * im.width // 4, (r + 1) * im.height // 2)).resize((512, 1024), Image.LANCZOS), (c * 512, r * 1024))
        for name, (c, r) in CELLS.items():
            cells[name] = dict(col=c, row=r, uv=[c / 4, 1 - (r + 1) / 2, (c + 1) / 4, 1 - r / 2], px=[c * 512, r * 1024, 512, 1024])
        atlas.save(TEX / 'M_Poster_Atlas.png', optimize=True)
        json.dump(dict(size=2048, cell_px=[512, 1024], note='uv = [u0, v0, u1, v1], v 는 위가 1(Unity·Blender)', cells=cells), open(TEX / 'poster_atlas.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        info['M_Poster_Atlas'] = dict(meters=1.0, kind='atlas', source='generated (GPT Image 2.5) 4x2 panels, src/M_Poster_Atlas.jpg', detected=ok)
        print('OK M_Poster_Atlas', 'detected' if ok else 'fallback')

# ---- 절차 7
def proc(name):
    return not only or name in only

if proc('M_Fluorescent'):
    a = flat('#EAF0EC', 0.008)
    ys = np.arange(S)
    for cy in (S * 0.33, S * 0.67):   # 관 2개의 밝은 띠
        band = np.exp(-((ys - cy) / (S * 0.07)) ** 2)[:, None]
        a[..., :3] = np.clip(a[..., :3] + band[..., None] * 0.05, 0, 1)
    save('M_Fluorescent', a, 'procedural', 'make_textures.py: 발광 확산판(unit UV)', 1.0)
if proc('M_Mirror'):
    a = flat('#B8BEC2', 0.006)
    d = ImageDraw.Draw(im := Image.fromarray((a * 255).astype(np.uint8)))
    for off, wdt, col in ((-120, 26, (214, 220, 224)), (60, 12, (206, 212, 216))):
        d.line([(0 + off, S), (S + off, 0)], fill=col, width=wdt)
        d.line([(0 + off - S, S), (S + off - S, 0)], fill=col, width=wdt)
        d.line([(0 + off + S, S), (S + off + S, 0)], fill=col, width=wdt)
    save('M_Mirror', np.asarray(im).astype(np.float32) / 255, 'procedural', 'make_textures.py: 거울 판 + 사선 2', 1.0)
if proc('M_Cage_Mesh'):
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cell, w = S // 20, 4                            # 마름모 5 cm, 철사 ~4 mm
    ink = (26, 20, 23, 255)
    for k in range(-21, 42):
        d.line([(k * cell - S, 0), (k * cell + S, 2 * S)], fill=ink, width=w)   # 45°
        d.line([(k * cell + S, 0), (k * cell - S, 2 * S)], fill=ink, width=w)   # -45°
    a = np.asarray(im).astype(np.float32) / 255
    save('M_Cage_Mesh', a, 'procedural', 'make_textures.py: 체인링크 마름모 5cm, 알파 컷아웃·양면', 1.0)
if proc('M_Paint_Gym'):
    save('M_Paint_Gym', flat('#4F5B55', 0.014), 'procedural', 'make_textures.py: 유성 페인트 띠', 2.0)
if proc('M_Steel_Dark'):
    save('M_Steel_Dark', flat('#2A2A2C', 0.012), 'procedural', 'make_textures.py: 검은 철', 1.0)
if proc('M_Timer_Red'):
    save('M_Timer_Red', flat('#C83C32', 0.004), 'procedural', 'make_textures.py: 발광 빨강(라운드 타이머·응급함 띠)', 1.0)
if proc('M_Glass_Clear'):
    a = flat('#DCE4E8', 0.004)
    alpha = np.full((S, S, 1), 0.15, np.float32)
    im = Image.fromarray((np.concatenate([a, alpha], 2) * 255).astype(np.uint8), 'RGBA'); d = ImageDraw.Draw(im)
    d.line([(-100, S), (S - 100, 0)], fill=(245, 248, 250, 90), width=18)
    d.line([(S // 2 + 80, S), (S + S // 2 + 80, 0)], fill=(245, 248, 250, 70), width=10)
    save('M_Glass_Clear', np.asarray(im).astype(np.float32) / 255, 'procedural', 'make_textures.py: 투명 유리(알파 0.15)', 1.0)

json.dump(dict(sorted(info.items())), open(jpath, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
# ---- 시트(2×2 타일 미리 보기)
names = sorted(info)
sheet = Image.new('RGB', (len(names) * 260, 280), 'white'); d = ImageDraw.Draw(sheet)
for i, n in enumerate(names):
    im = Image.open(TEX / f'{n}.png').convert('RGBA')
    bg = Image.new('RGBA', im.size, (200, 196, 190, 255)); im = Image.alpha_composite(bg, im).convert('RGB')
    if info[n]['kind'] != 'atlas':
        t = im.resize((128, 128)); tile = Image.new('RGB', (256, 256))
        for x in (0, 128):
            for y in (0, 128): tile.paste(t, (x, y))
    else:
        tile = im.resize((256, 256))
    sheet.paste(tile, (i * 260, 0)); d.text((i * 260 + 2, 260), n, fill='black')
sheet.save(TEX / '_sheet.jpg', quality=85)
print('sheet', len(names))
