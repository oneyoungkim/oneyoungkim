# 정면 얼굴 사진에서 눈선 기울기 재기(게임 화면 1920×1080, 정면 1.5m, 수평 카메라)
# 왼눈·오른눈 칸의 줄별 어두움(눈꺼풀·눈동자) 분포를 서로 밀어 맞춰 높이 차를 구함 — 눈 모양이 조금 달라도 됨, 1/8 화소까지. 각도 = atan(높이 차 / 두 칸 가운데 거리)
# + 부호 = 화면 오른쪽 눈이 아래. 칸은 사람마다 고정(새 모델이면 눈 자리를 사진으로 확인하고 고칠 것).
# 사용: python eye_line.py <촬영 폴더> <출력.jpg> [판(v3)]
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
src, out = sys.argv[1], sys.argv[2]
ver = sys.argv[3] if len(sys.argv) > 3 else "v3"
f = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 22)
names = dict(yunjun="이윤준", kim="김승연", son="손진수", seo="서진수")
# (왼눈 x0, x1, 오른눈 x0, x1, y0, y1)
BOX = dict(yunjun=(865, 930, 970, 1025, 415, 470), kim=(875, 925, 970, 1010, 406, 450), son=(855, 920, 975, 1045, 350, 386), seo=(850, 915, 970, 1035, 404, 440))
W, H = 480, 240
sheet = Image.new("RGB", (W * 2, H * 2), (15, 15, 15))
dr = ImageDraw.Draw(sheet)

def profile(lum, x0, x1, y0, y1):
    b = lum[y0:y1, x0:x1]
    skin = float(np.median(b))
    p = np.clip(skin - b, 0, None).sum(1)
    return p / max(p.max(), 1e-3)

for c, (k, nm) in enumerate(names.items()):
    im = Image.open(os.path.join(src, f"{k}_{ver}_face.png")).convert("RGB")
    lum = np.asarray(im).astype(np.float32) @ np.array([0.299, 0.587, 0.114], np.float32)
    lx0, lx1, rx0, rx1, y0, y1 = BOX[k]
    pl, pr = profile(lum, lx0, lx1, y0, y1), profile(lum, rx0, rx1, y0, y1)
    n = len(pl); K = 8
    xs = np.arange(n * K) / K
    ul, ur = np.interp(xs, np.arange(n), pl), np.interp(xs, np.arange(n), pr)
    best, bs = -1e9, 0
    for s in range(-12 * K, 12 * K + 1):         # 오른눈을 s/K 화소 위로 밀어 왼눈에 맞춤
        a = ul[max(0, -s):len(ul) - max(0, s)]
        b = ur[max(0, s):len(ur) - max(0, -s)]
        v = float(np.dot(a - a.mean(), b - b.mean()) / (a.std() * b.std() * len(a) + 1e-6))
        if v > best:
            best, bs = v, s
    dy = bs / K
    dx = (rx0 + rx1) / 2 - (lx0 + lx1) / 2
    ang = np.degrees(np.arctan2(dy, dx))
    print(f"{k}\t{ver}\t눈선 {ang:+.2f}°\t높이 차 {dy:+.2f}px / 거리 {dx:.0f}px\t맞춤 {best:.2f}")
    bx = (lx0 - 20, (y0 + y1) // 2 - 60, lx0 + 220, (y0 + y1) // 2 + 60)
    crop = im.crop(bx).resize((W, H), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    ly = ((y0 + y1) / 2 - bx[1]) * 2
    d.line((0, ly, W, ly), fill=(255, 255, 0), width=1)
    x, y = (c % 2) * W, (c // 2) * H
    sheet.paste(crop, (x, y))
    t = f"{nm} {ver} 눈선 {ang:+.1f}°"
    w = dr.textlength(t, font=f)
    dr.rectangle((x, y, x + w + 12, y + 30), fill=(0, 0, 0))
    dr.text((x + 6, y + 2), t, font=f, fill=(255, 255, 255))
sheet.save(out, quality=92)
print(out, sheet.size)
