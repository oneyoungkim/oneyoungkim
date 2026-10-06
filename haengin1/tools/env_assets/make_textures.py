"""Build concept/art/env/textures/<M_name>.png (1024, seamless) from generated swatches + procedural flat ones.

usage: python make_textures.py [--only M_A,M_B]
- generated: textures/src/<name>.png -> seamless.py (kind from specs/textures_meta.json); a JPEG copy of the source is kept
  as textures/src/<name>.jpg (the PNG source is not committed)
- procedural: flat colors with a faint wrap-around paper grain (glass, pipe, AC body, school-zone paint)
- writes textures/textures.json (meters per tile, kind, source) and textures/_sheet.jpg (2x2 tiled previews)
"""
import json, subprocess, sys, pathlib
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter

HERE = pathlib.Path(__file__).parent
ROOT = HERE.parent.parent
TEX = ROOT / 'concept/art/env/textures'
SRC = TEX / 'src'
meta = json.load(open(HERE / 'specs/textures_meta.json', encoding='utf-8'))
only = sys.argv[sys.argv.index('--only') + 1].split(',') if '--only' in sys.argv else None
S = 1024
rng = np.random.default_rng(7)


def hexc(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def grain(amp=0.018, sigma=1.2):
    n = gaussian_filter(rng.standard_normal((S, S)).astype(np.float32), sigma, mode='wrap')
    return n / n.std() * amp


def save(a, name):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(TEX / f'{name}.png')


def flat(col):
    return np.ones((S, S, 3), np.float32) * hexc(col) + grain()[..., None]


def band(a, col, x0, x1, slope):
    """diagonal flat band (wraps), used for glass reflections"""
    yy, xx = np.mgrid[0:S, 0:S]
    u = (xx + yy * slope) % S
    m = (u >= x0) & (u < x1)
    a[m] = hexc(col) + grain()[m][..., None] * 0.5
    return a


PROC = {
    'M_Glass_Dark': ('dark window glass: flat blue-grey with two diagonal reflection bands', 1.0,
                     lambda: band(band(flat('#2E3A44'), '#43515D', 180, 300, 1), '#3A4753', 360, 400, 1)),
    'M_Glass_Lit': ('lit window at night: warm flat light, darker blind band at the top', 1.0,
                    lambda: _lit()),
    'M_Pipe_Yellow': ('yellow painted gas pipe', 1.0, lambda: flat('#D8B23A')),
    'M_AC_White': ('off-white AC unit / appliance body', 1.0, lambda: flat('#E6E2D8')),
    'M_Tile_Beige': ('shop facade ceramic tiles 20 x 10 cm, stack bond, 5 x 10 per meter (the generated swatch had uneven columns)', 1.0,
                     lambda: _tiles()),
    'M_Paint_SchoolZone': ('red-brown anti-slip road paint of a school safety zone', 2.0, lambda: _school()),
}


def _lit():
    a = flat('#F1D79A')
    a[: int(S * 0.18)] = hexc('#DDBB7C') + grain()[: int(S * 0.18)][..., None]
    a[int(S * 0.18): int(S * 0.20)] = hexc('#C9A56C')
    return a


def _tiles(nx=5, ny=10, grout=4):
    pal = [hexc(c) for c in ('#CDBFA6', '#C4B59B', '#D6C9B1', '#CBBDA2')]
    a = np.zeros((S, S, 3), np.float32)
    tw, th = S // nx, S // ny
    for j in range(ny):
        for i in range(nx):
            a[j * th:(j + 1) * th, i * tw:(i + 1) * tw] = pal[rng.integers(len(pal))]
    for j in range(ny):
        a[j * th: j * th + grout] = hexc('#A79C8B')
    for i in range(nx):
        a[:, i * tw: i * tw + grout] = hexc('#A79C8B')
    a += grain(0.012)[..., None]
    ink = hexc('#5A4E44')
    for _ in range(int(nx * ny * 0.35)):          # sparse hand-drawn ticks like the generated swatches
        x, y = rng.integers(0, S - 12, 2); L = rng.integers(5, 11)
        for t in range(L):
            a[(y + t) % S, (x + t // 2) % S] = a[(y + t) % S, (x + t // 2) % S] * 0.6 + ink * 0.4
    return a


def _school():
    a = flat('#A85A4C')
    d = rng.random((S, S)) < 0.004
    a[d] = hexc('#8E4A3F')
    return a


def remove_dark_patches(path, k=0.045):
    """paint over distinctive darker blobs (e.g. a repair patch on asphalt) with the same texture shifted by half,
    so a 4 m tile does not show the same patch every 4 m"""
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32) / 255
    L = gaussian_filter(a.mean(axis=2), 6, mode='wrap')
    m = (L < np.median(L) - k).astype(np.float32)
    m = np.clip(gaussian_filter(m, 10, mode='wrap') * 3, 0, 1)
    b = np.roll(a, (S // 2, S // 2), axis=(0, 1))
    mb = np.roll(m, (S // 2, S // 2), axis=(0, 1))
    if (m * mb).sum() > 0.05 * m.sum():           # shifted copy would bring a patch back: use a quarter shift
        b = np.roll(a, (S // 4, S // 3), axis=(0, 1))
    out = a * (1 - m[..., None]) + b * m[..., None]
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path)
    return float(m.mean())


CLEAN = {'M_Asphalt'}


def main():
    info = {}
    for n, m in meta.items():
        if only and n not in only:
            continue
        if n in PROC:
            continue
        src = SRC / f'{n}.png'
        if not src.exists():
            print('MISSING', n); continue
        r = subprocess.run([sys.executable, str(HERE / 'seamless.py'), str(src), str(TEX / f'{n}.png'), '--kind', m['kind'], '--size', str(S)],
                           capture_output=True, text=True)
        print(r.stdout.strip() or r.stderr[-400:])
        if n in CLEAN:
            print('  cleaned patches, area', round(remove_dark_patches(TEX / f'{n}.png'), 4))
        Image.open(src).convert('RGB').save(SRC / f'{n}.jpg', quality=90)
        info[n] = {'meters': m['meters'], 'kind': m['kind'], 'source': f'generated (GPT Image 2.5), src/{n}.jpg'}
    for n, (desc, meters, fn) in PROC.items():
        if only and n not in only:
            continue
        save(fn(), n)
        info[n] = {'meters': meters, 'kind': 'procedural', 'source': 'make_textures.py: ' + desc}
        print('PROC', n)
    jp = TEX / 'textures.json'
    old = json.load(open(jp, encoding='utf-8')) if jp.exists() else {}
    old.update(info)
    json.dump(dict(sorted(old.items())), open(jp, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    # contact sheet: every texture tiled 2x2
    names = sorted(p.stem for p in TEX.glob('M_*.png') if not p.stem.endswith('_Atlas'))
    W = 300; cols = 6; rows = (len(names) + cols - 1) // cols
    sheet = Image.new('RGB', (W * cols, (W + 18) * rows), (244, 239, 230)); d = ImageDraw.Draw(sheet)
    for i, n in enumerate(names):
        t = Image.open(TEX / f'{n}.png').convert('RGB').resize((W // 2, W // 2), Image.LANCZOS)
        r, c = divmod(i, cols)
        for dx in (0, 1):
            for dy in (0, 1):
                sheet.paste(t, (c * W + dx * W // 2, r * (W + 18) + 18 + dy * W // 2))
        d.text((c * W + 4, r * (W + 18) + 3), f"{n}  {old.get(n, {}).get('meters', '?')} m", fill=(26, 20, 23))
    sheet.save(TEX / '_sheet.jpg', quality=85)
    print('sheet', len(names))


if __name__ == '__main__':
    main()
