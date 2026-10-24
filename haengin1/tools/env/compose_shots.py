# -*- coding: utf-8 -*-
"""preview_env.py 가 렌더한 color/normal/depth 로 화면 공간 외곽선 + 안개를 얹고, 지금/키트를 나란히 붙인다.

실행: python tools/env/compose_shots.py <렌더 폴더> [--scale 2]
  (시스템 파이썬, numpy + Pillow)
외곽선 규칙(09 문서 3-5 흉내): 깊이 차(상대 3% 또는 6cm)·법선 각 35° 넘는 곳·하늘 경계, 앞쪽 화소에만.
굵기: 12m 안 1.5px, 30m 까지 1px, 그 너머 0.5px 이 흐려져 80m 에서 0. 색 #1A1417.
안개: Zone1Builder 와 같은 선형 40~380m, 색 #D4CEC3. 하늘 #EEE3D1.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

INK = np.array([0x1A, 0x14, 0x17], np.float32) / 255
FOG = np.array([0xD4, 0xCE, 0xC3], np.float32) / 255
FONT = "C:/Windows/Fonts/malgunbd.ttf"
LABEL = {
    "1_후문상가거리": "1. 성대 후문 상가거리", "2_뒷골목주차장": "2. 후문 뒷골목 주차장", "3_골목계단": "3. 시우네 앞 → 꼭대기 계단",
    "4_와룡오르막": "4. 와룡공원 오르막", "5_성곽길": "5. 와룡공원 성곽길(암문 앞)", "6_한옥골목": "6. 명륜3가 한옥 골목",
    "7_명륜3가골목": "7. 명륜3가 골목(시우네 골목)", "8_성곽아래빈집": "8. 성곽 아래 숲길·빈집",
}


def load(stem):
    c = np.asarray(Image.open(stem + "_color.png").convert("RGB"), np.float32) / 255
    nimg = Image.open(stem + "_normal.png").convert("RGBA")
    na = np.asarray(nimg, np.float32) / 255
    n = na[..., :3] * 2 - 1
    mask = na[..., 3] > 0.5
    d = np.asarray(Image.open(stem + "_depth.png"), np.float32)
    d = d / (65535.0 if d.max() > 255 else 255.0) * 600.0
    d[~mask] = 1e5
    return c, n, d, mask


def edges(n, d, mask):
    H, W = d.shape
    e = np.zeros((H, W), bool)
    for dy, dx in ((0, 1), (1, 0), (1, 1)):
        a_d = d[: H - dy, : W - dx]
        b_d = d[dy:, dx:]
        a_n = n[: H - dy, : W - dx]
        b_n = n[dy:, dx:]
        a_m = mask[: H - dy, : W - dx]
        b_m = mask[dy:, dx:]
        near = np.minimum(a_d, b_d)
        dd = np.abs(a_d - b_d) > np.maximum(0.06, 0.035 * near)
        nn = (a_n * b_n).sum(-1) < 0.82
        sky = a_m != b_m
        hit = dd | sky | (nn & a_m & b_m)
        a_front = a_d <= b_d
        e[: H - dy, : W - dx] |= hit & a_front & a_m
        e[dy:, dx:] |= hit & ~a_front & b_m
    return e


def dilate(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dy == 0 and dx == 0:
                continue
            sh = np.zeros_like(m)
            ys, yd = (slice(dy, None), slice(None, -dy)) if dy > 0 else ((slice(None, dy), slice(-dy, None)) if dy < 0 else (slice(None), slice(None)))
            xs, xd = (slice(dx, None), slice(None, -dx)) if dx > 0 else ((slice(None, dx), slice(-dx, None)) if dx < 0 else (slice(None), slice(None)))
            sh[ys, xs] = m[yd, xd]
            out |= sh
    return out


def compose(stem, scale, night=False):
    c, n, d, mask = load(stem)
    if not night:
        f = np.clip((d - 40.0) / 340.0, 0, 1)[..., None]
        f[~mask] = 0
        c = c * (1 - f) + FOG * f
    e = edges(n, d, mask)
    # 굵기: 거리별 (2배 해상도 기준 px)
    near = e & (d < 12.0)
    mid = e & (d >= 12.0) & (d < 30.0)
    thick = dilate(near, 1) | dilate(mid, 0)
    if scale >= 2:
        mid2 = np.zeros_like(mid)
        mid2[:, 1:] |= mid[:, :-1]
        mid2[1:, :] |= mid[:-1, :]
        thick |= mid2
    # 선이 놓이는 화소의 거리(번진 화소는 이웃 거리 쓰기 대신 단순히 가까운 쪽으로 가정)
    w = np.clip((80.0 - d) / 50.0, 0, 1)
    w[d < 30] = 1.0
    alpha = np.where(thick, 1.0, 0.0)
    alpha = np.maximum(alpha, e * w)
    a = alpha[..., None] * (0.9 if night else 1.0)
    ink = INK if not night else np.array([0.06, 0.05, 0.07], np.float32)
    img = c * (1 - a) + ink * a
    im = Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8))
    if scale > 1:
        im = im.resize((im.width // scale, im.height // scale), Image.LANCZOS)
    return im


def label(im, text, sub=None):
    dr = ImageDraw.Draw(im)
    try:
        f = ImageFont.truetype(FONT, max(18, im.height // 28))
        fs = ImageFont.truetype(FONT, max(14, im.height // 40))
    except OSError:
        f = fs = ImageFont.load_default()
    pad = 12
    tw = dr.textlength(text, font=f)
    h = f.size + 12
    dr.rectangle([0, 0, tw + pad * 2, h + (fs.size + 8 if sub else 0)], fill=(26, 20, 23))
    dr.text((pad, 4), text, fill=(238, 227, 209), font=f)
    if sub:
        dr.text((pad, h), sub, fill=(200, 190, 175), font=fs)
    return im


def main():
    folder = sys.argv[1]
    scale = 2
    if "--scale" in sys.argv:
        scale = int(sys.argv[sys.argv.index("--scale") + 1])
    names = sorted({f.rsplit("_", 2)[0] for f in os.listdir(folder) if f.endswith("_color.png")})
    names = sorted({n.rsplit("_", 1)[0] if n.endswith(("_before", "_after", "_night")) else n for n in names})
    pairs = []
    for nm in names:
        out = {}
        for st in ("before", "after", "night"):
            stem = os.path.join(folder, "%s_%s" % (nm, st))
            if os.path.exists(stem + "_color.png"):
                im = compose(stem, scale, night=(st == "night"))
                im.save(stem + ".png")
                out[st] = im
        if "before" in out and "after" in out:
            b, a = out["before"], out["after"]
            W, H = a.size
            cmp_ = Image.new("RGB", (W * 2 + 8, H), (26, 20, 23))
            cmp_.paste(label(b.copy(), "지금 — 회색 상자", LABEL.get(nm, nm)), (0, 0))
            cmp_.paste(label(a.copy(), "건물 키트(평면 색 + 외곽선)", LABEL.get(nm, nm)), (W + 8, 0))
            cmp_.save(os.path.join(folder, "%s_compare.png" % nm))
            pairs.append(cmp_)
        if "night" in out:
            label(out["night"], "건물 키트 — 밤(창문 불 40%)", LABEL.get(nm, nm)).save(os.path.join(folder, "%s_night.png" % nm))
    if pairs:
        tw = 1600
        rows = [p.resize((tw, int(p.height * tw / p.width)), Image.LANCZOS) for p in pairs]
        sheet = Image.new("RGB", (tw, sum(r.height + 6 for r in rows)), (26, 20, 23))
        y = 0
        for r in rows:
            sheet.paste(r, (0, y))
            y += r.height + 6
        sheet.save(os.path.join(folder, "compare_sheet.png"))
    print("compose done", len(pairs), "pairs")


if __name__ == "__main__":
    main()
