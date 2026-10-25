# -*- coding: utf-8 -*-
"""무대 4곳 목표 그림 | Unity 사진 비교 시트: python tools/env/stages/compose_compare.py <Unity 사진 폴더(stage_s0*.png)> <결과 jpg>"""
import os, sys, glob
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
FR = os.path.join(ROOT, "concept", "art", "env", "stages", "frames")
shots, out = sys.argv[1], sys.argv[2]
W, H = 800, 450
rows = []
for f in sorted(glob.glob(os.path.join(FR, "s0*.png"))):
    key = os.path.basename(f)[:3]
    u = os.path.join(shots, "stage_%s.png" % key)
    rows.append((f, u if os.path.exists(u) else None, os.path.splitext(os.path.basename(f))[0]))
S = Image.new("RGB", (W * 2 + 30, (H + 26) * len(rows)), "white")
d = ImageDraw.Draw(S)
for i, (f, u, name) in enumerate(rows):
    y = i * (H + 26)
    d.text((6, y + 6), name + "  (목표 그림 | Unity)", fill="black")
    S.paste(Image.open(f).convert("RGB").resize((W, H)), (0, y + 24))
    if u:
        S.paste(Image.open(u).convert("RGB").resize((W, H)), (W + 30, y + 24))
S.save(out, quality=86)
print("비교 시트", len(rows), "→", out)
