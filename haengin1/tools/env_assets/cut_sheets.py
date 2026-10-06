"""Cut generated sheets into single pieces and pack the sign / decal atlases.

usage: python cut_sheets.py
in : concept/art/env/signs/src/{sign_bands,sign_vertical,shop_emblems,posters_flyers}.png
     concept/art/env/decals/src/{decals_grime,decals_graffiti}.png, concept/art/env/sky/src/sky_clouds.png
out: signs/pieces/<sheet>_<nn>.png (RGBA, background removed), textures/M_Sign_Atlas.png (2048, RGBA) + signs/sign_atlas.json (UV rects),
     textures/M_Decal_Atlas.png + decals/decal_atlas.json, sky/clouds/cloud_<nn>.png
Pieces are found as connected components of 'not background' (alpha > 0, or not near-white), grown a little so
splatter dots stay with their piece. Order = rows top to bottom, then left to right.
"""
import json, pathlib
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
ENV = ROOT / 'concept/art/env'


def pieces(path, grow=12, min_area=0.002, white=238):
    im = Image.open(path).convert('RGBA'); a = np.asarray(im).astype(np.float32)
    if (a[..., 3] < 250).mean() > 0.2:                 # transparent sheet
        fg = a[..., 3] > 24
        rgba = a
    else:                                              # white sheet -> alpha from distance to white
        d = 255 - a[..., :3].min(axis=2)
        fg = d > (255 - white)
        alpha = np.clip((d - 6) / 18, 0, 1) * 255
        rgba = a.copy(); rgba[..., 3] = alpha
    lab, n = ndi.label(ndi.binary_dilation(fg, iterations=grow))
    H, W = fg.shape
    out = []
    for li, s in enumerate(ndi.find_objects(lab), start=1):
        h, w = s[0].stop - s[0].start, s[1].stop - s[1].start
        if h * w < min_area * H * W:
            continue
        out.append((s[0].start, s[1].start, s, li))
    # row clustering: pieces whose vertical centers are within 15 % of the sheet height share a row
    out.sort(key=lambda t: (t[0], t[1]))
    rows = []
    for t in out:
        cy = (t[2][0].start + t[2][0].stop) / 2
        for r in rows:
            if abs(r[0] - cy) < 0.15 * H:
                r[1].append(t); break
        else:
            rows.append([cy, [t]])
    rows.sort(key=lambda r: r[0])
    res = []
    for _, ts in rows:
        for t in sorted(ts, key=lambda t: t[1]):
            s = t[2]
            crop = rgba[s[0], s[1]].copy()
            m = (lab[s[0], s[1]] == t[3])
            crop[..., 3] *= m
            res.append(Image.fromarray(crop.astype(np.uint8), 'RGBA'))
    return res


def band_only(img):
    """sign_bands pieces include the shop front under the sign: keep rows down to the sign's bottom edge.
    The sign face is the top block of rows whose median color stays close to the face color."""
    a = np.asarray(img).astype(np.float32)[..., :3]
    H = a.shape[0]
    med = np.median(a[int(H * 0.12):int(H * 0.22)].reshape(-1, 3), axis=0)
    rowdiff = np.array([np.median(np.abs(a[y] - med).sum(axis=1)) for y in range(H)])
    start = int(H * 0.22)
    for y in range(start, H):
        if rowdiff[y] > 60:
            # include the trim line just below the face
            return img.crop((0, 0, img.width, min(H, y + 6)))
    return img


def fit(img, w, h):
    im = img.copy(); im.thumbnail((w, h), Image.LANCZOS)
    c = Image.new('RGBA', (w, h), (0, 0, 0, 0)); c.alpha_composite(im, ((w - im.width) // 2, (h - im.height) // 2))
    return c


def main():
    sig = ENV / 'signs'; (sig / 'pieces').mkdir(parents=True, exist_ok=True)
    got = {}
    for sheet in ('sign_bands', 'sign_vertical', 'shop_emblems', 'posters_flyers'):
        ps = pieces(sig / 'src' / f'{sheet}.png')
        got[sheet] = ps
        for i, p in enumerate(ps):
            p.save(sig / 'pieces' / f'{sheet}_{i:02d}.png')
        print(sheet, len(ps))
    # ---- M_Sign_Atlas 2048 x 2048 ----
    A = Image.new('RGBA', (2048, 2048), (0, 0, 0, 0)); rects = {}
    names_band = ['band_navy_alu', 'band_cream_lightbox', 'band_wood_gukbap', 'band_haru_conv', 'band_mustard_bungae', 'band_white_laundry', 'band_redbrown_bunsik', 'band_rusty_hardware']
    for i, p in enumerate(got['sign_bands'][:8]):
        b = band_only(p)
        x, y = (i % 2) * 1024, (i // 2) * 192
        A.alpha_composite(fit(b, 1016, 184), (x + 4, y + 4)); rects[names_band[i]] = [x + 4, y + 4, 1016, 184]
        b.save(sig / 'pieces' / f'{names_band[i]}.png')
    names_v = ['vert_navy', 'vert_cream', 'vert_slate', 'vert_redbrown', 'vert_white', 'vert_green', 'round_white', 'hanging_wood']
    for i, p in enumerate(got['sign_vertical'][:8]):
        x, y = i * 256, 768
        A.alpha_composite(fit(p, 248, 504), (x + 4, y + 4)); rects[names_v[i]] = [x + 4, y + 4, 248, 504]
    names_e = ['emb_gukbap', 'emb_haru', 'emb_bungae', 'emb_bunsik', 'emb_laundry', 'emb_hardware', 'emb_pc', 'emb_stationery']
    for i, p in enumerate(got['shop_emblems'][:8]):
        x, y = i * 256, 1280
        A.alpha_composite(fit(p, 248, 248), (x + 4, y + 4)); rects[names_e[i]] = [x + 4, y + 4, 248, 248]
    names_p = ['poster_room', 'poster_lostcat', 'poster_boxing', 'poster_theater', 'poster_piano', 'poster_moving', 'poster_notice', 'poster_concert']
    for i, p in enumerate(got['posters_flyers'][:8]):
        x, y = i * 256, 1536
        A.alpha_composite(fit(p, 248, 504), (x + 4, y + 4)); rects[names_p[i]] = [x + 4, y + 4, 248, 504]
    A.save(ENV / 'textures/M_Sign_Atlas.png')
    json.dump({'size': [2048, 2048], 'origin': 'top-left, pixels [x, y, w, h]; Unity UV: u = x / 2048, v = 1 - (y + h) / 2048',
               'note': 'all faces are blank: shop names are TMP text on top (fonts: Black Han Sans / Noto Sans KR / Gaegu)',
               'rects': rects}, open(sig / 'sign_atlas.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('sign atlas', len(rects))
    # ---- M_Decal_Atlas ----
    dec = ENV / 'decals'; (dec / 'pieces').mkdir(parents=True, exist_ok=True)
    D = Image.new('RGBA', (2048, 2048), (0, 0, 0, 0)); drects = {}
    slots = [(x * 341, y * 341) for y in range(3) for x in range(6)]   # 18 slots of 341 px in the top 1023 px
    k = 0
    for sheet in ('decals_grime', 'decals_graffiti'):
        ps = pieces(dec / 'src' / f'{sheet}.png', grow=18)
        print(sheet, len(ps))
        for i, p in enumerate(ps):
            nm = f'{sheet}_{i:02d}'; p.save(dec / 'pieces' / f'{nm}.png')
            if sheet == 'decals_graffiti':
                x, y = (i % 3) * 682, 1024 + (i // 3) * 512           # graffiti get bigger slots in the lower half
                if y + 512 > 2048:
                    continue
                D.alpha_composite(fit(p, 674, 504), (x + 4, y + 4)); drects[nm] = [x + 4, y + 4, 674, 504]
            elif k < len(slots):
                x, y = slots[k]; k += 1
                D.alpha_composite(fit(p, 333, 333), (x + 4, y + 4)); drects[nm] = [x + 4, y + 4, 333, 333]
    D.save(ENV / 'textures/M_Decal_Atlas.png')
    json.dump({'size': [2048, 2048], 'origin': 'top-left, pixels [x, y, w, h]', 'rects': drects}, open(dec / 'decal_atlas.json', 'w', encoding='utf-8'), indent=1)
    # ---- clouds ----
    cl = ENV / 'sky/clouds'; cl.mkdir(parents=True, exist_ok=True)
    ps = pieces(ENV / 'sky/src/sky_clouds.png', grow=6)
    for i, p in enumerate(ps):
        p.save(cl / f'cloud_{i:02d}.png')
    print('clouds', len(ps))


if __name__ == '__main__':
    main()
