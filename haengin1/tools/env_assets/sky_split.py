"""Split the grey skyline cards into tintable layers, and preview the four times of day.

usage: python sky_split.py
in : concept/art/env/sky/src/sky_<dir>.png  (white sky + 3 flat grey value layers: far light, mid medium, near dark)
out: sky/sky_<dir>_layers.png  RGBA 2688 x 1152: R = far mask, G = mid mask, B = near mask, A = any silhouette
     sky/sky_<dir>_detail.png  L: hatching / window lines as a multiply map (1 = no line)
     sky/sky_palette.json      sky + layer colors per time of day (dawn, day, sunset, night)
     sky/_sky_preview.jpg      every card in every time of day
Unity: color = lerp(lerp(lerp(sky, far, R), mid, G), near, B) * detail, alpha = A (unlit, no fog, drawn behind everything).
Layers are nested (near is inside mid is inside far) so a layer never shows holes where a nearer one sits.
"""
import json, pathlib
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ENV = pathlib.Path(__file__).resolve().parent.parent.parent / 'concept/art/env/sky'
CARDS = ['sky_north_bugaksan', 'sky_east_naksan', 'sky_south_city']
# per time of day: sky, far, mid, near (REFERENCES 2: far mountains grey-brown, the city in front lighter; paper sky)
PAL = {
    'dawn':   {'sky': '#CBD1D6', 'far': '#A3AAB4', 'mid': '#B7BCC3', 'near': '#5B616C', 'lines': 0.6},
    'day':    {'sky': '#E9E0CC', 'far': '#A39C90', 'mid': '#C9C1B2', 'near': '#6E6A64', 'lines': 0.7},
    'sunset': {'sky': '#EBC9A6', 'far': '#B48F86', 'mid': '#C9A493', 'near': '#5A4A4C', 'lines': 0.7},
    'night':  {'sky': '#222A44', 'far': '#323A57', 'mid': '#2A3150', 'near': '#151A2B', 'lines': 0.5},
}


def hexc(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def clean(m, close=3, min_px=60):
    m = ndi.binary_closing(m, iterations=close)
    m = ndi.binary_fill_holes(m)
    lab, n = ndi.label(m)
    if n:
        sizes = ndi.sum(m, lab, range(1, n + 1))
        m = np.isin(lab, 1 + np.where(sizes >= min_px)[0])
    return m


def split(path):
    g = np.asarray(Image.open(path).convert('L')).astype(np.float32) / 255
    gs = ndi.median_filter(g, size=5)                     # drop hatching / window lines when deciding layers
    any_ = clean(gs < 0.93, 2)
    mid = clean(gs < 0.66, 3) & any_
    near = clean(gs < 0.40, 3) & mid
    # line detail: darker than the local layer base
    base = ndi.median_filter(g, size=15)
    detail = np.clip(1 - np.maximum(base - g, 0) * 3.0, 0, 1)
    detail[~any_] = 1
    return any_, mid, near, detail


def main():
    pal = PAL
    json.dump({'formula': 'color = lerp(lerp(lerp(sky, far, R), mid, G), near, B) * lerp(1, detail, lines); alpha = A', 'times': pal},
              open(ENV / 'sky_palette.json', 'w', encoding='utf-8'), indent=1)
    rows = []
    for c in CARDS:
        src = ENV / 'src' / f'{c}.png'
        any_, mid, near, detail = split(src)
        H, W = any_.shape
        L = np.zeros((H, W, 4), np.uint8)
        L[..., 0] = any_ * 255; L[..., 1] = mid * 255; L[..., 2] = near * 255; L[..., 3] = any_ * 255
        Image.fromarray(L, 'RGBA').save(ENV / f'{c}_layers.png')
        Image.fromarray((detail * 255).astype(np.uint8), 'L').save(ENV / f'{c}_detail.png')
        tiles = []
        for t, p in PAL.items():
            sky, far, md, nr = (hexc(p[k]) for k in ('sky', 'far', 'mid', 'near'))
            col = np.ones((H, W, 3), np.float32) * sky
            col = np.where(any_[..., None], far, col); col = np.where(mid[..., None], md, col); col = np.where(near[..., None], nr, col)
            col *= 1 + (detail[..., None] - 1) * p['lines']
            im = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)); im.thumbnail((700, 700))
            ImageDraw.Draw(im).text((6, 4), f'{c} / {t}', fill=(30, 30, 30) if t != 'night' else (220, 220, 220))
            tiles.append(im)
        rows.append(tiles)
        print(c, 'far %.2f mid %.2f near %.2f' % (any_.mean(), mid.mean(), near.mean()))
    w, h = rows[0][0].size
    out = Image.new('RGB', (w * 4, h * len(rows)), 'white')
    for r, tiles in enumerate(rows):
        for i, t in enumerate(tiles):
            out.paste(t, (i * w, r * h))
    out.save(ENV / '_sky_preview.jpg', quality=85)


if __name__ == '__main__':
    main()
