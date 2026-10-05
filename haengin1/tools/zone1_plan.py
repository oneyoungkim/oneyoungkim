# -*- coding: utf-8 -*-
"""zone1.json → 평면도 PNG (docs/img/06_zone1_plan.png). Pillow 만 필요.

실행: python tools/zone1_plan.py [zone1.json] [출력.png]
"""
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "docs", "img", "06_zone1_plan.png")

Z = json.load(open(SRC, encoding="utf-8"))
C = Z["colors"]
S = 9.0          # px / m
LEG_W = 300      # 범례 폭(px)


def font(size, bold=False):
    for p in (("C:/Windows/Fonts/malgunbd.ttf" if bold else "C:/Windows/Fonts/malgun.ttf"),
              "/usr/share/fonts/truetype/nanum/NanumGothic.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


F, FS, FB, FT = font(11), font(10), font(15, True), font(18, True)
b = Z["bounds"]
X0, Z0 = b["min"][0] - 2, b["min"][2] - 2
X1, Z1 = b["max"][0] + 2, b["max"][2] + 2
W, H = int((X1 - X0) * S), int((Z1 - Z0) * S)
img = Image.new("RGB", (W + LEG_W, H + 40), "#F4EFE6")
d = ImageDraw.Draw(img, "RGBA")


def T(x, z):
    return ((x - X0) * S, (Z1 - z) * S + 40)


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# 지형: 높이로 명암 + 1m 등고선 점
G = [g for g in Z["ground"] if g["kind"] == "heightgrid"][0]
gx0, gz0 = G["origin"]
c = G["cell"]
Hm = G["heights"]
base = hexrgb(C["ground"])
for j in range(G["nz"] - 1):
    for i in range(G["nx"] - 1):
        h = (Hm[j][i] + Hm[j][i + 1] + Hm[j + 1][i] + Hm[j + 1][i + 1]) / 4
        k = 0.82 + 0.3 * (h - 20) / 30
        col = tuple(max(0, min(255, int(v * k))) for v in base)
        x, z = gx0 + i * c, gz0 + j * c
        d.rectangle([T(x, z + c), T(x + c, z)], fill=col)
for j in range(G["nz"] - 1):
    for i in range(G["nx"] - 1):
        a, r, u = Hm[j][i], Hm[j][i + 1], Hm[j + 1][i]
        for lv in range(20, 52, 2):
            if (a - lv) * (r - lv) < 0 or (a - lv) * (u - lv) < 0:
                x, z = gx0 + i * c, gz0 + j * c
                px, pz = T(x + c / 2, z + c / 2)
                d.ellipse([px - 1, pz - 1, px + 1, pz + 1], fill=(110, 98, 78, 160))


def rect(cx, cz, w, dd, yaw):
    r = math.radians(yaw)
    ux, uz = (math.cos(r), -math.sin(r)), (math.sin(r), math.cos(r))
    return [T(cx + ux[0] * sx * w / 2 + uz[0] * sz * dd / 2, cz + ux[1] * sx * w / 2 + uz[1] * sz * dd / 2)
            for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]


def ribbon(pts, w):
    L, R = [], []
    for i, p in enumerate(pts):
        a, bb = (pts[0], pts[1]) if i == 0 else ((pts[-2], pts[-1]) if i == len(pts) - 1 else (pts[i - 1], pts[i + 1]))
        dx, dz = bb[0] - a[0], bb[2] - a[2]
        l = math.hypot(dx, dz)
        nx, nz = -dz / l, dx / l
        L.append(T(p[0] + nx * w / 2, p[2] + nz * w / 2))
        R.append(T(p[0] - nx * w / 2, p[2] - nz * w / 2))
    return L + R[::-1]


for g in Z["ground"]:
    if g["kind"] != "pad":
        continue
    col = C["pad_" + g["surface"]]
    if g["shape"] == "circle":
        cx, cz = T(g["center"][0], g["center"][2])
        r = g["size"][0] / 2 * S
        d.ellipse([cx - r, cz - r, cx + r, cz + r], fill=col, outline="black")
    else:
        d.polygon(rect(g["center"][0], g["center"][2], g["size"][0], g["size"][1], g["yaw"]), fill=col, outline="black")
order = {"road": 0, "driveway": 1, "sidewalk": 2, "alley": 3, "path": 3, "trail": 3, "wallpath": 3}
for r in sorted(Z["roads"], key=lambda r: order[r["kind"]]):
    d.polygon(ribbon(r["points"], r["width"]), fill=C[r["kind"]], outline=(90, 90, 90))
for s in Z["stairs"]:
    poly = ribbon([s["from"], s["to"]], s["width"])
    d.polygon(poly, fill=C["stairs"], outline="black")
    a, bb = s["from"], s["to"]
    L = math.hypot(bb[0] - a[0], bb[2] - a[2])
    nx, nz = -(bb[2] - a[2]) / L, (bb[0] - a[0]) / L
    for k in range(1, s["steps"]):
        t = k / s["steps"]
        x, z = a[0] + t * (bb[0] - a[0]), a[2] + t * (bb[2] - a[2])
        d.line([T(x + nx * s["width"] / 2, z + nz * s["width"] / 2), T(x - nx * s["width"] / 2, z - nz * s["width"] / 2)],
               fill=(80, 40, 30, 120))
w = Z["walls"][0]
d.polygon(ribbon(w["points"], w["thickness"]), fill=C["wall"], outline="black")
for bl in Z["blocks"]:
    d.polygon(rect(bl["center"][0], bl["center"][2], bl["size"][0], bl["size"][2], bl["yaw"]),
              fill=C.get(bl["kind"], "#f0f"), outline=(40, 40, 40))
for bl in Z["blocks"]:
    if bl["kind"] in ("shop", "house", "hanok", "campus"):
        x, z = T(bl["center"][0], bl["center"][2])
        top = bl["center"][1] + bl["size"][1] / 2
        name = bl["name"].split("(")[0]
        d.text((x, z), "%s\n지붕 %.0f" % (name[:9], top), font=FS, fill=("white" if bl["kind"] == "hanok" else "black"),
               anchor="mm", align="center")
mk = {"door": C["door"], "lamp": "#E0A030", "pole": "#3A3434", "sign": C["sign"], "bus_stop": C["bus_stop"],
      "board": C["board"], "wall_gate": C["wall_gate"]}
for L in Z["landmarks"]:
    if L["kind"] not in mk:
        continue
    x, z = T(L["pos"][0], L["pos"][2])
    d.ellipse([x - 4, z - 4, x + 4, z + 4], fill=mk[L["kind"]], outline="white")
# 길 이름
labels = {"성균관로(후문~명륜1가 방면)": "성균관로", "와룡공원 성곽길": "와룡공원 성곽길", "성곽 안쪽 숲길(혜성고 방면)": "성곽 안쪽 숲길 → 혜성고",
          "와룡공원 안쪽 숲길(남)": "안쪽 숲길", "명륜3가 골목 동쪽(명륜1가 방면)": "골목 → 명륜1가", "와룡공원길(삼청동 방면, M1 경계)": "와룡공원길"}
for r in Z["roads"]:
    if r["name"] in labels:
        p = r["points"][len(r["points"]) // 2]
        q = r["points"][len(r["points"]) // 2 - 1]
        x, z = T((p[0] + q[0]) / 2, (p[2] + q[2]) / 2)
        d.text((x, z), labels[r["name"]], font=F, fill=(30, 30, 30), anchor="mm", stroke_width=2, stroke_fill="#F4EFE6")
x, z = T(-235, 155)
d.text((x, z), "한양도성 성곽 (성 밖 = 성북동)", font=FB, fill=(60, 55, 50), anchor="mm", stroke_width=2, stroke_fill="#F4EFE6")
# 체크포인트
for i, r in enumerate(Z["route"]):
    x, z = T(r["pos"][0], r["pos"][2])
    rr = r["radius"] * S
    d.ellipse([x - rr, z - rr, x + rr, z + rr], outline=(190, 30, 30), width=2)
    d.text((x, z - rr - 9), "%d" % (i + 1), font=FB, fill=(190, 30, 30), anchor="mm", stroke_width=2, stroke_fill="white")
    if i:
        p = T(Z["route"][i - 1]["pos"][0], Z["route"][i - 1]["pos"][2])
        d.line([p, (x, z)], fill=(190, 30, 30, 110), width=1)
sp = Z["spawn"]
ya = math.radians(sp["yaw"])
x, z = T(sp["pos"][0], sp["pos"][2])
d.line([(x, z), (x + 55 * math.sin(ya), z - 55 * math.cos(ya))], fill=(190, 30, 30), width=4)
d.text((x - 6, z + 16), "시작(성대 후문)", font=F, fill=(190, 30, 30), anchor="rm", stroke_width=2, stroke_fill="white")
# 눈금·제목·북쪽·축척
for xx in range(-270, -129, 10):
    d.text((T(xx, Z1)[0], 30), str(xx), font=FS, fill="black", anchor="mb")
for zz in range(70, 175, 10):
    d.text((3, T(X0, zz)[1]), str(zz), font=FS, fill="black", anchor="lm")
d.text((W / 2, 4), "1구역 평면도 — 성대 후문 ~ 와룡공원 (북 = 위, 단위 m, 원점 = 혜화동 로터리, 땅 밝을수록 높음, 점선 = 2m 등고선)",
       font=FT, fill="black", anchor="mt")
nx0, nz0 = 60, 110
d.polygon([(nx0, nz0 - 30), (nx0 - 9, nz0), (nx0 + 9, nz0)], fill="black")
d.text((nx0, nz0 + 4), "N", font=FB, fill="black", anchor="mt")
sx0, sz0 = 40, H + 20
d.rectangle([sx0, sz0, sx0 + 20 * S, sz0 + 6], fill="black")
d.text((sx0 + 10 * S, sz0 - 4), "20 m", font=F, fill="black", anchor="mb")
# 범례
lx, ly = W + 16, 60
d.text((lx, ly - 20), "범례 (그레이박스 색)", font=FB, fill="black")
items = [("차도", "road"), ("인도", "sidewalk"), ("골목", "alley"), ("공원 흙길", "path"), ("숲길", "trail"), ("성곽길(돌 포장)", "wallpath"),
         ("계단", "stairs"), ("흙 공터·마당", "pad_dirt"), ("아스팔트 패드", "pad_asphalt"), ("상가", "shop"), ("주택", "house"),
         ("한옥(기와)", "hanok"), ("캠퍼스", "campus"), ("성곽", "wall"), ("담·난간", "fence"), ("옹벽", "retaining"),
         ("소나무", "tree"), ("풀숲", "bush"), ("정자·게이트", "pavilion"), ("경계 차단물", "barrier"), ("소품", "prop"),
         ("표지·정류장", "sign"), ("출입문", "door")]
for i, (nm, k) in enumerate(items):
    y = ly + i * 22
    d.rectangle([lx, y, lx + 26, y + 15], fill=C[k], outline="black")
    d.text((lx + 34, y + 7), "%s  %s" % (nm, C[k]), font=F, fill="black", anchor="lm")
y = ly + len(items) * 22 + 10
d.ellipse([lx + 3, y, lx + 23, y + 20], outline=(190, 30, 30), width=2)
d.text((lx + 34, y + 10), "체크포인트(반경)", font=F, fill="black", anchor="lm")
d.ellipse([lx + 9, y + 32, lx + 17, y + 40], fill="#E0A030", outline="white")
d.text((lx + 34, y + 36), "가로등·보안등", font=F, fill="black", anchor="lm")
d.text((lx, y + 62), "체크포인트\n" + "\n".join("%d. %s (높이 %.1f)" % (i + 1, r["name"].split(". ", 1)[1], r["pos"][1])
                                           for i, r in enumerate(Z["route"])), font=FS, fill="black")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT, optimize=True)
print("wrote", OUT, img.size)
