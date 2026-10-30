# 목 검수 묶음: 사람마다 v2 줄·v3 줄 — 전체 칸 4(정면·3/4·옆·2.6m) + 턱~옷깃 원본 해상도 240×180 잘라 2배(가장 가까운 화소) 칸 4
# 사용: python neck_sheet.py <촬영 폴더> <출력 폴더>
import os, sys
from PIL import Image, ImageDraw, ImageFont
src, out = sys.argv[1], sys.argv[2]
f = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 22)
pts = {}
for l in open(os.path.join(src, "neck_points.txt"), encoding="utf-8"):
    k, v, w, x, y = l.strip().split("\t")
    pts[(k, v, w)] = (int(x), int(y))
views = (("front", "정면 1.5m"), ("q34", "3/4 1.5m"), ("side", "옆 1.5m"), ("waist", "정면 2.6m"))
names = dict(yunjun="이윤준", kim="김승연", son="손진수", seo="서진수")
CW, CH = 240, 180
def label(dr, x, y, t):
    w = dr.textlength(t, font=f)
    dr.rectangle((x, y, x + w + 12, y + 30), fill=(0, 0, 0))
    dr.text((x + 6, y + 2), t, font=f, fill=(255, 255, 255))
people = []
for k, nm in names.items():
    sheet = Image.new("RGB", (480 * 4, (270 + 360) * 2), (15, 15, 15))
    dr = ImageDraw.Draw(sheet)
    for r, ver in enumerate(("v2", "v3")):
        y0 = r * (270 + 360)
        for c, (w, wn) in enumerate(views):
            im = Image.open(os.path.join(src, f"{k}_{ver}_{w}.png")).convert("RGB")
            sheet.paste(im.resize((480, 270), Image.LANCZOS), (480 * c, y0))
            x, y = pts[(k, ver, w)]
            box = (x - CW // 2, y - CH // 2, x + CW // 2, y + CH // 2)
            sheet.paste(im.crop(box).resize((CW * 2, CH * 2), Image.NEAREST), (480 * c, y0 + 270))
            label(dr, 480 * c + 4, y0 + 4, f"{nm} {ver} · {wn}")
            label(dr, 480 * c + 4, y0 + 274, "턱~옷깃 원본 ×2")
    p = os.path.join(out, f"neck_check_{k}.jpg")
    sheet.save(p, quality=92)
    people.append(sheet)
    print(p)
all_ = Image.new("RGB", (1920, sum(s.height for s in people)))
y = 0
for s in people:
    all_.paste(s, (0, y)); y += s.height
all_.save(os.path.join(out, "neck_check.jpg"), quality=90)
print("neck_check.jpg", all_.size)
