# -*- coding: utf-8 -*-
"""무대 4곳 결 텍스처 → concept/art/env/stages/textures/M_*.png (1024², 이음매 없음 — 평면 색 + 드문 먹 자국, 체육관·맵 결과 같은 규칙)

usage: python tools/env/stages/make_textures.py
절차로 만드는 것: 타일(벽·바닥)·테라조·장판·천장 판·나무·칠판·코르크·밤거리·새벽 간유리
GPT 그림을 다듬는 것(specs/textures.json → textures/src/): T_Products → M_Products(그대로), T_FortressView → M_View_Fortress(그대로),
  T_Wallpaper → M_Wallpaper(가장자리 섞어 이음매 없앰)
"""
import os, json, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = os.path.join(ROOT, "concept", "art", "env", "stages", "textures")
SRC = os.path.join(OUT, "src")
os.makedirs(OUT, exist_ok=True)
N = 1024
INK = (26, 20, 23)


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def grain(img, amt=6, seed=0):
    rng = np.random.default_rng(seed)
    a = np.asarray(img).astype(np.int16)
    n = rng.normal(0, amt, (a.shape[0], a.shape[1], 1)).astype(np.int16)
    n = np.asarray(Image.fromarray(np.clip(n + 128, 0, 255).astype(np.uint8)[:, :, 0]).filter(ImageFilter.GaussianBlur(0.8))).astype(np.int16)[:, :, None] - 128
    return Image.fromarray(np.clip(a + n, 0, 255).astype(np.uint8))


def wrap_draw(draw_fn, img):
    """이음매 없게: 3×3 으로 그린 뒤 가운데."""
    big = Image.new("RGB", (N * 3, N * 3))
    for i in range(3):
        for j in range(3):
            big.paste(img, (i * N, j * N))
    d = ImageDraw.Draw(big)
    draw_fn(d, big)
    return big.crop((N, N, 2 * N, 2 * N))


def tiles(base, grout, cells, gw=3, marks=0.03, seed=1, jitter=0.03):
    rng = random.Random(seed)
    img = Image.new("RGB", (N, N), base)
    d = ImageDraw.Draw(img)
    s = N / cells
    for i in range(cells):
        for j in range(cells):
            k = 1.0 + rng.uniform(-jitter, jitter)
            d.rectangle([i * s + gw, j * s + gw, (i + 1) * s - gw, (j + 1) * s - gw], fill=shade(base, k))
    for i in range(cells + 1):
        d.rectangle([i * s - gw, 0, i * s + gw - 1, N], fill=grout)
        d.rectangle([0, i * s - gw, N, i * s + gw - 1], fill=grout)
    for _ in range(int(cells * cells * marks * 10)):
        x, y = rng.uniform(0, N), rng.uniform(0, N)
        L = rng.uniform(6, 18)
        d.line([x, y, x + L, y + rng.uniform(-3, 3)], fill=shade(grout, 0.85), width=2)
    return grain(img, 4, seed)


def speckle(base, dots, n, rmax, seed=2):
    rng = random.Random(seed)
    img = Image.new("RGB", (N, N), base)

    def fn(d, big):
        for _ in range(n):
            x, y = rng.uniform(0, N) + N, rng.uniform(0, N) + N
            r = rng.uniform(1.5, rmax)
            c = rng.choice(dots)
            d.ellipse([x - r, y - r, x + r, y + r], fill=c)
    return grain(wrap_draw(fn, img), 5, seed)


def wood(base, lines, seed=3, plank=None):
    rng = random.Random(seed)
    img = Image.new("RGB", (N, N), base)

    def fn(d, big):
        if plank:
            for k in range(plank):
                y = N + k * N / plank
                d.rectangle([N, y, 2 * N, y + 3], fill=shade(base, 0.72))
                kk = 1.0 + rng.uniform(-0.04, 0.04)
                d.rectangle([N, y + 4, 2 * N, y + N / plank - 1], fill=shade(base, kk))
        for _ in range(lines):
            y = rng.uniform(0, N) + N
            x = rng.uniform(0, N) + N
            L = rng.uniform(80, 260)
            pts = [(x + t, y + 3 * math.sin(t / 37.0 + x)) for t in range(0, int(L), 8)]
            d.line(pts, fill=shade(base, 0.8), width=2)
    return grain(wrap_draw(fn, img), 4, seed)


def chalkboard():
    base = hexc("#2F4A3E")
    rng = random.Random(7)
    img = Image.new("RGB", (N, N), base)

    def fn(d, big):
        for _ in range(26):     # 지운 자국(옅은 넓은 띠)
            x, y = rng.uniform(0, N) + N, rng.uniform(0, N) + N
            w, h = rng.uniform(120, 360), rng.uniform(30, 90)
            d.rounded_rectangle([x, y, x + w, y + h], 20, fill=shade(base, 1.09))
        for _ in range(14):     # 알아볼 수 없는 분필 끄적임
            x, y = rng.uniform(0, N) + N, rng.uniform(0, N) + N
            pts = [(x + 9 * t, y + 6 * math.sin(t * 1.7 + rng.random()) + rng.uniform(-3, 3)) for t in range(rng.randint(6, 18))]
            d.line(pts, fill=(196, 204, 196), width=2)
    return grain(wrap_draw(fn, img), 5, 7)


def jangpan():
    base = hexc("#D9B46A")
    img = tiles(base, shade(base, 0.9), 3, gw=1, marks=0.0, seed=11, jitter=0.0)
    rng = random.Random(12)
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = rng.uniform(0, N), rng.uniform(0, N)
        d.arc([x, y, x + 40, y + 24], 200, 320, fill=shade(base, 0.86), width=2)
    return img


def night_view():
    w, h = 2048, 1024
    img = Image.new("RGB", (w, h), hexc("#232B3D"))
    d = ImageDraw.Draw(img)
    rng = random.Random(21)
    # 건너편 건물 덩어리 + 창 불 몇 + 가로등 주황 웅덩이 + 길
    for k in range(9):
        x = k * 240 + rng.uniform(-30, 30)
        top = rng.uniform(120, 420)
        d.rectangle([x, top, x + rng.uniform(170, 240), h * 0.68], fill=hexc("#2C3448"))
        for _ in range(rng.randint(2, 6)):
            wx, wy = x + rng.uniform(20, 150), rng.uniform(top + 30, h * 0.6)
            d.rectangle([wx, wy, wx + 36, wy + 26], fill=hexc("#E8C987") if rng.random() < 0.6 else hexc("#3B4560"))
    d.rectangle([0, h * 0.68, w, h], fill=hexc("#3A3A40"))
    for cx in (520, 1500):
        for r in range(260, 0, -20):
            a = 1 - r / 260
            c = tuple(int(hexc("#3A3A40")[i] * (1 - a * 0.7) + hexc("#E39A4A")[i] * a * 0.7) for i in range(3))
            d.ellipse([cx - r * 1.6, h * 0.8 - r * 0.35, cx + r * 1.6, h * 0.8 + r * 0.35], fill=c)
        d.rectangle([cx - 6, h * 0.25, cx + 6, h * 0.8], fill=hexc("#1A1417"))
        d.ellipse([cx - 30, h * 0.22, cx + 30, h * 0.28], fill=hexc("#F2C07A"))
    return grain(img, 4, 21)


def dawn_glass():
    img = Image.new("RGB", (512, 512))
    a = np.zeros((512, 512, 3), np.float32)
    top, bot = np.array(hexc("#B9CBE0")), np.array(hexc("#7F9BBB"))
    for y in range(512):
        a[y] = top * (1 - y / 511) + bot * (y / 511)
    img = Image.fromarray(a.astype(np.uint8))
    return grain(img, 6, 31).filter(ImageFilter.GaussianBlur(1.2))


def seamless_blend(img, edge=0.12):
    a = np.asarray(img.convert("RGB").resize((N, N), Image.LANCZOS)).astype(np.float32)
    rolled = np.roll(np.roll(a, N // 2, 0), N // 2, 1)
    m = int(N * edge)
    w = np.ones(N, np.float32)
    ramp = np.linspace(0, 1, m)
    w[:m] = ramp; w[-m:] = ramp[::-1]
    W = np.minimum(w[:, None], w[None, :])[:, :, None]
    out = a * W + rolled * (1 - W)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def main():
    made = {}
    T = {
        "M_Tile_WhiteWall": lambda: tiles(hexc("#E9E6DE"), hexc("#B9B7B0"), 4, 3, 0.02, 1, 0.015),
        "M_Terrazzo": lambda: speckle(hexc("#A9A8A2"), [hexc("#8A8983"), hexc("#C8C7C0"), hexc("#7A746C"), hexc("#B9A88E")], 2600, 4.5, 2),
        "M_Tile_Floor": lambda: tiles(hexc("#B98E6A"), hexc("#7F604A"), 2, 3, 0.03, 4, 0.04),
        "M_Tile_Store": lambda: tiles(hexc("#E4E2DC"), hexc("#C2C0BA"), 2, 2, 0.01, 5, 0.01),
        "M_Floor_School": lambda: tiles(hexc("#D8D1C2"), hexc("#B7AE9C"), 4, 2, 0.02, 6, 0.03),
        "M_Jangpan": jangpan,
        "M_Ceiling": lambda: speckle(hexc("#EDE7D8"), [hexc("#D9D2C1")], 900, 2.0, 8),
        "M_Wainscot": lambda: wood(hexc("#8E6444"), 30, 9, plank=6),
        "M_Wood_Light": lambda: wood(hexc("#C9A77A"), 36, 10),
        "M_Wood_Counter": lambda: wood(hexc("#9A6B45"), 34, 13, plank=4),
        "M_Chalkboard": chalkboard,
        "M_Cork": lambda: speckle(hexc("#B98A5A"), [hexc("#9C6F45"), hexc("#CDA172"), hexc("#8A5F3A")], 5200, 2.6, 14),
    }
    for k, fn in T.items():
        im = fn()
        if k == "M_Ceiling":    # 60cm 판 줄눈
            d = ImageDraw.Draw(im)
            d.rectangle([0, 0, N, 3], fill=hexc("#C9C2B0")); d.rectangle([0, 0, 3, N], fill=hexc("#C9C2B0"))
        im.save(os.path.join(OUT, k + ".png"))
        made[k] = dict(meters=None, src="procedural")
    night_view().save(os.path.join(OUT, "M_View_Night.png")); made["M_View_Night"] = dict(src="procedural")
    dawn_glass().save(os.path.join(OUT, "M_View_Dawn.png")); made["M_View_Dawn"] = dict(src="procedural")
    for src, dst, fn in (("T_Products", "M_Products", None), ("T_FortressView", "M_View_Fortress", None), ("T_Wallpaper", "M_Wallpaper", seamless_blend)):
        p = os.path.join(SRC, src + ".png")
        if not os.path.exists(p):
            print("없음:", p)
            continue
        im = Image.open(p).convert("RGB")
        if fn:
            im = fn(im)
        elif max(im.size) > 2048:
            im.thumbnail((2048, 2048), Image.LANCZOS)
        im.save(os.path.join(OUT, dst + ".png"))
        made[dst] = dict(src=src)
    with open(os.path.join(OUT, "textures.json"), "w", encoding="utf-8") as f:
        json.dump(made, f, ensure_ascii=False, indent=1)
    # 확인용 시트
    names = list(made)
    th = 160
    sheet = Image.new("RGB", (th * 6, th * ((len(names) + 5) // 6)), "white")
    for i, n in enumerate(names):
        im = Image.open(os.path.join(OUT, n + ".png")).convert("RGB").resize((th, th))
        sheet.paste(im, ((i % 6) * th, (i // 6) * th))
    sheet.save(os.path.join(OUT, "_sheet.jpg"), quality=85)
    print("텍스처", len(made), "→", OUT)


if __name__ == "__main__":
    main()
