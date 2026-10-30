# 서 있는 자세 검수 묶음(ZYunjunShots.Z_Stand_Shots 결과): 인물마다 한 줄 — 전 정면·전 옆면·후 정면·후 옆면. 옆면에 발목 세로 기준선과 귀·어깨·엉덩이·무릎·발목 점
# 사용: python stand_sheet.py <촬영 폴더> <출력 폴더>   → stand_fix.jpg(전체) + stand_fix_<무리>.jpg(5명씩, 원본 비율 확인용)
import os, sys
from PIL import Image, ImageDraw, ImageFont
src, out = sys.argv[1], sys.argv[2]
f = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 24)
names = {"Yunjun_v2": "이윤준", "Seungyeon_v2": "김승연", "JinsooSon_v2": "손진수", "JinsooSeo_v2": "서진수", "SeungminBaek": "백승민", "Gojun": "고준", "Siwoo": "시우", "Taeo": "태오",
         "Kkanjok": "관원1(깐족이)", "Seokdal": "관원2(석달)", "Naengjanggo": "관원3(냉장고)", "Scrum": "관원4(스크럼)", "Mom": "엄마", "Haneul": "하늘", "Dokyungmin": "도경민", "Minjae": "민재", "Geonwoo": "건우",
         "StudentM": "남학생", "StudentF": "여학생", "Gwak": "곽반장", "Halmeoni": "사장 할머니", "BakeryStaff": "직원 누나", "Dad": "아빠"}
pts = {}
for l in open(os.path.join(src, "stand_points.txt"), encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    pts[(c[0], c[1], c[2])] = [tuple(int(v) for v in x.split(",")) for x in c[3:]]
W, H = 480, 720
sx = W / 720
def tile(key, ver, view):
    p = os.path.join(src, f"{key}_{ver}_{view}.png")
    if not os.path.exists(p):
        return None
    a = Image.open(p).convert("RGB").resize((W, H), Image.LANCZOS)
    if view == "side" and (key, ver, view) in pts:
        d = ImageDraw.Draw(a)
        q = [(x * sx, y * sx) for x, y in pts[(key, ver, view)]]
        ax = q[4][0]
        d.line((ax, 0, ax, H), fill=(0, 255, 255), width=1)
        for (x, y), c in zip(q, ((255, 255, 0), (255, 120, 0), (255, 0, 0), (0, 255, 0), (0, 255, 255))):
            d.ellipse((x - 4, y - 4, x + 4, y + 4), outline=c, width=2)
    return a
keys = [k for k in names if os.path.exists(os.path.join(src, f"{k}_after_side.png")) or os.path.exists(os.path.join(src, f"{k}_before_side.png"))]
rows = []
for k in keys:
    im = Image.new("RGB", (W * 4, H), (15, 15, 15))
    dr = ImageDraw.Draw(im)
    for c, (ver, view, lab) in enumerate((("before", "front", "전 정면"), ("before", "side", "전 옆면"), ("after", "front", "후 정면"), ("after", "side", "후 옆면"))):
        t = tile(k, ver, view)
        if t is not None:
            im.paste(t, (W * c, 0))
        txt = f"{names[k]} {lab}"
        w = dr.textlength(txt, font=f)
        dr.rectangle((W * c, 0, W * c + w + 12, 34), fill=(0, 0, 0))
        dr.text((W * c + 6, 2), txt, font=f, fill=(255, 255, 255))
    rows.append(im)
for i in range(0, len(rows), 3):
    part = rows[i:i + 3]
    sheet = Image.new("RGB", (W * 4, H * len(part)))
    for j, r in enumerate(part):
        sheet.paste(r, (0, H * j))
    sheet.save(os.path.join(out, f"stand_fix_{i // 3 + 1}.jpg"), quality=90)
all_ = Image.new("RGB", (W * 4, H * len(rows)))
for j, r in enumerate(rows):
    all_.paste(r, (0, H * j))
all_.save(os.path.join(out, "stand_fix.jpg"), quality=88)
print("stand_fix.jpg", all_.size, "인물", len(rows))
