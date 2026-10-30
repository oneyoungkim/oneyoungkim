# 얼굴 밝기 전/후: 사람마다 채움 끔·켬 정면(가운데 잘라 384×432), face_luma.txt 수치 표시
# 사용: python fill_sheet.py <촬영 폴더> <출력.jpg>
import os, sys
from PIL import Image, ImageDraw, ImageFont
src, out = sys.argv[1], sys.argv[2]
f = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 24)
names = dict(yunjun="이윤준", kim="김승연", son="손진수", seo="서진수", siwoo="시우")
lum = {}
for l in open(os.path.join(src, "face_luma.txt"), encoding="utf-8"):
    c = l.strip().split("\t")
    lum[c[0]] = (c[1], c[2])
W, H = 384, 432
im = Image.new("RGB", (W * 5, H * 2), (15, 15, 15))
dr = ImageDraw.Draw(im)
for c, (k, nm) in enumerate(names.items()):
    for r in range(2):
        a = Image.open(os.path.join(src, f"{k}_fill{r}.png")).convert("RGB")
        cx, cy = a.width // 2, a.height // 2
        a = a.crop((cx - 288, cy - 324, cx + 288, cy + 324)).resize((W, H), Image.LANCZOS)
        im.paste(a, (W * c, H * r))
        t = f"{nm} {lum[k][r]}"
        w = dr.textlength(t, font=f)
        dr.rectangle((W * c, H * r, W * c + w + 12, H * r + 34), fill=(0, 0, 0))
        dr.text((W * c + 6, H * r + 2), t, font=f, fill=(255, 255, 255))
im.save(out, quality=90)
print(out)
