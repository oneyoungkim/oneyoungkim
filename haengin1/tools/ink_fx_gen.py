# 먹 이펙트·의성어·HUD 그림 만들기 — docs/08_M2_전투_설계.md 5-5·7장 (2026-10-06)
# 3D 시안 페이지(concept/src/05_fx.js inkFxTex, 02_textures.js wordTexture)의 캔버스 그리기를 그대로 옮겼다.
# 난수 = 시안과 같은 mulberry32(2024), 같은 순서(brush → splat → shard → star → lines → ring → dust) — 모양이 시안과 같다.
# 흰색으로 그린 것(brush·splat·lines·ring)은 Unity 재질 색으로 먹색을 입히고, shard·star·dust 는 흰 면 + 먹 테를 그대로 쓴다.
# 의성어는 글자가 몇 개뿐이라 TMP 대신 시안과 같은 그림(512×256, Black Han Sans 140px, 주황 그림자 + 먹 테 + 흰 글자, −0.08 rad)으로 굽는다.
# 사용: python tools/ink_fx_gen.py <출력 폴더> <BlackHanSans.ttf>
import math, os, sys
from PIL import Image, ImageDraw, ImageFont

SS = 4                     # 슈퍼샘플(가장자리 매끈하게)
INK = (0x1A, 0x14, 0x17)   # 08 문서 먹 #1A1417
WORD_INK = (0x14, 0x15, 0x1D)
ORANGE = (0xE8, 0x57, 0x2A)
PAPER = (0xF4, 0xEF, 0xE6)
TAU = math.pi * 2


def mulberry(seed):
    st = [seed & 0xFFFFFFFF]

    def imul(a, b):
        return (a * b) & 0xFFFFFFFF

    def r():
        st[0] = (st[0] + 0x6D2B79F5) & 0xFFFFFFFF
        t = st[0]
        t = imul(t ^ (t >> 15), t | 1)
        t = (t ^ ((t + imul(t ^ (t >> 7), t | 61)) & 0xFFFFFFFF)) & 0xFFFFFFFF
        return ((t ^ (t >> 14)) & 0xFFFFFFFF) / 4294967296.0
    return r


class Canvas:
    """캔버스 2D 의 필요한 것만: 경로 채우기·선(둥근 끝)·원·지우기. 좌표는 원래 크기, 내부는 SS 배"""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new('RGBA', (w * SS, h * SS), (255, 255, 255, 0))
        self.d = ImageDraw.Draw(self.img)

    def P(self, pts):
        return [(x * SS, y * SS) for x, y in pts]

    def fill(self, pts, color):
        self.d.polygon(self.P(pts), fill=color)

    def circle(self, x, y, r, color):
        self.d.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=color)

    def stroke(self, pts, width, color, closed=False):
        q = list(pts) + ([pts[0]] if closed else [])
        self.d.line(self.P(q), fill=color, width=max(1, int(round(width * SS))), joint='curve')
        for x, y in q:          # 둥근 끝·이음
            self.circle(x, y, width / 2, color)

    def erase_stroke(self, pts, width):
        # destination-out: 알파를 0 으로
        mask = Image.new('L', self.img.size, 0)
        md = ImageDraw.Draw(mask)
        md.line(self.P(pts), fill=255, width=max(1, int(round(width * SS))), joint='curve')
        for x, y in pts:
            r = width / 2
            md.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=255)
        a = self.img.getchannel('A')
        a.paste(0, mask=mask)
        self.img.putalpha(a)
        self.d = ImageDraw.Draw(self.img)

    def arc(self, cx, cy, r, a0, a1, width, color, n=72):
        pts = [(cx + math.cos(a0 + (a1 - a0) * i / n) * r, cy + math.sin(a0 + (a1 - a0) * i / n) * r) for i in range(n + 1)]
        self.stroke(pts, width, color)

    def save(self, path):
        self.img.resize((self.w, self.h), Image.LANCZOS).save(path)


W1 = (255, 255, 255, 255)


def ink_fx(out):
    R = mulberry(2024)
    ink = INK + (255,)

    # 붓 획: 초승달, 머리 굵고 꼬리 갈라짐(비백)
    W, H = 512, 256
    c = Canvas(W, H)
    r = H * 2; cx = W / 2; cy = H * .5 - 26 + r; a0 = -math.pi / 2 - .47; a1 = -math.pi / 2 + .47; N = 64
    wAt = lambda t: H * .26 * math.pow(math.sin(math.pi * min(1, t * 1.15)), .55) * (1 - t * .35)
    pts = []
    for i in range(N + 1):
        t = i / N; a = a0 + (a1 - a0) * t; rr = r + wAt(t) * .5
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    for i in range(N, -1, -1):
        t = i / N; a = a0 + (a1 - a0) * t; rr = r - wAt(t) * .5
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    c.fill(pts, W1)
    for k in range(15):
        off = (R() - .5) * .9; t0 = .3 + R() * .5; lw = 1 + R() * 2.4
        line = []
        for i in range(31):
            t = t0 + (1 - t0) * i / 30; a = a0 + (a1 - a0) * t; rr = r + wAt(t) * off
            line.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        c.erase_stroke(line, lw)
    for k in range(12):
        t = R() * .35; a = a0 + (a1 - a0) * t; rr = r + (R() - .2) * H * .38
        c.circle(cx + math.cos(a) * rr, cy + math.sin(a) * rr, 1.5 + R() * 4, W1)
    c.save(os.path.join(out, 'fx_brush.png'))

    # 먹 튀김
    W = 128
    c = Canvas(W, W)
    pts = []
    for i in range(19):
        a = i / 18 * TAU; rr = W * (.24 + R() * .12)
        pts.append((W / 2 + math.cos(a) * rr, W / 2 + math.sin(a) * rr))
    c.fill(pts, W1)
    for k in range(6):
        a = R() * TAU; rr = W * (.36 + R() * .1)
        c.circle(W / 2 + math.cos(a) * rr, W / 2 + math.sin(a) * rr, 2 + R() * 5, W1)
    c.save(os.path.join(out, 'fx_splat.png'))

    # 흰 섬광 조각(마름모 + 먹 테 7px — 채우기가 안쪽 반을 덮음)
    W, H = 64, 256
    c = Canvas(W, H)
    pts = [(W / 2, 6), (W * .78, H * .42), (W / 2, H - 6), (W * .26, H * .5)]
    c.stroke(pts, 7, ink, closed=True)
    c.fill(pts, W1)
    c.save(os.path.join(out, 'fx_shard.png'))

    # 충격 섬광 별
    W = 256
    c = Canvas(W, W)
    n = 11; pts = []
    for i in range(n * 2):
        a = i / (n * 2) * TAU + R() * .08
        rr = W * ((.13 + R() * .05) if i % 2 else (.34 + R() * .13))
        pts.append((W / 2 + math.cos(a) * rr, W / 2 + math.sin(a) * rr))
    c.stroke(pts, 9, ink, closed=True)
    c.fill(pts, W1)
    c.save(os.path.join(out, 'fx_star.png'))

    # 집중선
    W = 512
    c = Canvas(W, W)
    for i in range(70):
        a = R() * TAU; r0 = W * (.2 + R() * .1); r1 = W * (.44 + R() * .06); w = .006 + R() * .014
        c.fill([(W / 2 + math.cos(a) * r0, W / 2 + math.sin(a) * r0),
                (W / 2 + math.cos(a - w) * r1, W / 2 + math.sin(a - w) * r1),
                (W / 2 + math.cos(a + w) * r1, W / 2 + math.sin(a + w) * r1)], W1)
    c.save(os.path.join(out, 'fx_lines.png'))

    # 붓 고리(다운 착지 바닥 · 락온 발밑 원)
    W = 256
    c = Canvas(W, W)
    for k in range(4):
        a0 = R() * TAU; ln = TAU * (.55 + R() * .35); lw = 5 + R() * 9
        c.arc(W / 2, W / 2, W * (.4 + (R() - .5) * .04), a0, a0 + ln, lw, W1)
    c.save(os.path.join(out, 'fx_ring.png'))

    # 만화 먼지 구름
    W = 256
    c = Canvas(W, W)
    cs = [(x * W, y * W, rr * W * (.9 + R() * .2)) for x, y, rr in [(.5, .55, .2), (.32, .58, .15), (.68, .6, .15), (.42, .4, .15), (.6, .42, .13)]]
    for x, y, rr in cs: c.circle(x, y, rr + 6, ink)
    for x, y, rr in cs: c.circle(x, y, rr, W1)
    c.save(os.path.join(out, 'fx_dust.png'))


def hud_art(out):
    R = mulberry(77)
    # 화면 가장자리 먹 테두리('읽었다' — 08 3-5): 가운데 비고 가장자리에 붓 결
    W, H = 640, 360
    c = Canvas(W, H)
    for side in range(4):
        for k in range(26):
            u = R()
            depth = 10 + R() * 34
            lw = 6 + R() * 16
            if side == 0: pts = [(u * W - 60, depth * .3), (u * W + 60, depth)]
            elif side == 1: pts = [(u * W - 60, H - depth * .3), (u * W + 60, H - depth)]
            elif side == 2: pts = [(depth * .3, u * H - 40), (depth, u * H + 40)]
            else: pts = [(W - depth * .3, u * H - 40), (W - depth, u * H + 40)]
            c.stroke(pts, lw, W1)
    c.d.rectangle([0, 0, W * SS, 8 * SS], fill=W1); c.d.rectangle([0, (H - 8) * SS, W * SS, H * SS], fill=W1)
    c.d.rectangle([0, 0, 8 * SS, H * SS], fill=W1); c.d.rectangle([(W - 8) * SS, 0, W * SS, H * SS], fill=W1)
    c.save(os.path.join(out, 'ui_edge.png'))
    # 락온 머리 위 역삼각형(흰 + 먹 테)
    c = Canvas(64, 64)
    pts = [(8, 12), (56, 12), (32, 54)]
    c.stroke(pts, 7, INK + (255,), closed=True)
    c.fill(pts, W1)
    c.save(os.path.join(out, 'ui_tri.png'))
    # 가드 반원(흰 굵은 호 — Image Filled Radial180 로 깎음)
    c = Canvas(256, 128)
    c.arc(128, 128, 104, math.pi, TAU, 22, W1)
    c.save(os.path.join(out, 'ui_arc.png'))


def words(out, ttf):
    # 시안 wordTexture: 512×256, 140px, translate(256,132) rotate(−0.08), 그림자 (10,10) → 테 26px → 글자
    styles = {
        'A': dict(fill=(255, 255, 255), stroke=WORD_INK, shadow=ORANGE, size=140, sw=13),
        'block': dict(fill=(0xE4, 0xDF, 0xD6), stroke=WORD_INK, shadow=None, size=120, sw=12),
        'ink': dict(fill=(0x11, 0x11, 0x11), stroke=(0xF7, 0xF5, 0xF0), shadow=None, size=118, sw=12),
    }
    items = [('word_pok', '퍽!', 'A'), ('word_ppak', '빡!', 'A'), ('word_kwajik', '콰직!', 'A'), ('word_kung', '쿵!', 'A'),
             ('word_tuk', '툭', 'block'), ('word_read', '읽었다', 'ink'), ('word_heat', '기세', 'ink')]
    S2 = 2
    for name, text, sk in items:
        st = styles[sk]
        size = st['size']
        font = ImageFont.truetype(ttf, size * S2)
        layer = Image.new('RGBA', (512 * S2 * 2, 256 * S2 * 2), (255, 255, 255, 0))
        d = ImageDraw.Draw(layer)
        cx, cy = layer.size[0] / 2, layer.size[1] / 2 + 4 * S2
        if st['shadow'] is not None:
            d.text((cx + 10 * S2, cy + 10 * S2), text, font=font, fill=st['shadow'] + (255,), anchor='mm')
        d.text((cx, cy), text, font=font, fill=st['fill'] + (255,), anchor='mm', stroke_width=st['sw'] * S2, stroke_fill=st['stroke'] + (255,))
        d.text((cx, cy), text, font=font, fill=st['fill'] + (255,), anchor='mm')
        layer = layer.rotate(math.degrees(0.08), resample=Image.BICUBIC, center=(cx, cy))
        w, h = 512 * S2, 256 * S2
        layer = layer.crop((int(cx - w / 2), int(cy - h / 2), int(cx + w / 2), int(cy + h / 2))).resize((512, 256), Image.LANCZOS)
        layer.save(os.path.join(out, name + '.png'))


if __name__ == '__main__':
    out = sys.argv[1]
    ttf = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    ink_fx(out)
    hud_art(out)
    words(out, ttf)
    print('ok', sorted(f for f in os.listdir(out) if f.endswith('.png')))
