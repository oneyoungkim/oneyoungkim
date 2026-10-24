# -*- coding: utf-8 -*-
"""스타일 프레임(왼쪽) | 키트+소품 미리 보기 렌더(오른쪽) 를 6행으로 붙여 concept/art/env/gym/_preview_compare.jpg 로.

실행: python tools/env/gym/compose_compare.py [<렌더 폴더>]   (기본 concept/art/env/gym/_preview)
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GYM = os.path.join(ROOT, "concept", "art", "env", "gym")
rend = sys.argv[1] if len(sys.argv) > 1 else os.path.join(GYM, "_preview")
LABEL = {"g01": "g01 입구 계단 전경(낮)", "g02": "g02 케이지 안 스파링(저녁)", "g03": "g03 매트·샌드백 줄(저녁 피크)",
         "g04": "g04 웨이트 코너(아침)", "g05": "g05 유리창 사무실(낮)", "g06": "g06 밤 개인 지도"}
NAMES = {"g01": "g01_stairs_overview", "g02": "g02_cage_sparring", "g03": "g03_mats_bags", "g04": "g04_weight_corner",
         "g05": "g05_office_window", "g06": "g06_night_coaching"}
W, H, HEAD = 896, 504, 26
try:
    font = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 17)
except OSError:
    font = ImageFont.load_default()
rows = []
for k in ("g01", "g02", "g03", "g04", "g05", "g06"):
    f = os.path.join(GYM, "frames", NAMES[k] + ".png")
    r = os.path.join(rend, k + ".png")
    if not os.path.exists(f):
        continue
    a = Image.open(f).convert("RGB").resize((W, H), Image.LANCZOS)
    b = Image.open(r).convert("RGB").resize((W, H), Image.LANCZOS) if os.path.exists(r) else Image.new("RGB", (W, H), (200, 196, 190))
    rows.append((k, a, b))
sheet = Image.new("RGB", (W * 2 + 12, len(rows) * (H + HEAD) + 4), (245, 240, 230))
d = ImageDraw.Draw(sheet)
for i, (k, a, b) in enumerate(rows):
    y = i * (H + HEAD)
    d.text((6, y + 4), LABEL[k] + "  —  스타일 프레임", fill=(26, 20, 23), font=font)
    d.text((W + 18, y + 4), "키트 + 소품 미리 보기(EEVEE 2톤 + Freestyle, 사람은 대역)", fill=(26, 20, 23), font=font)
    sheet.paste(a, (0, y + HEAD))
    sheet.paste(b, (W + 12, y + HEAD))
out = os.path.join(GYM, "_preview_compare.jpg")
sheet.save(out, quality=86)
print(out, sheet.size, len(rows))
