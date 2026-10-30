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

# 고개 돌림(미트 자세, 좌우 40°): 사람마다 v2 왼·오른, v3 왼·오른 — 전체 칸 + 턱~옷깃 ×2
rows = []
for k, nm in names.items():
    if (k, "v3", "turnL") not in pts:
        continue
    sheet = Image.new("RGB", (480 * 4, 270 + 360), (15, 15, 15))
    dr = ImageDraw.Draw(sheet)
    c = 0
    for ver in ("v2", "v3"):
        for w, wn in (("turnL", "왼쪽 봄"), ("turnR", "오른쪽 봄")):
            im = Image.open(os.path.join(src, f"{k}_{ver}_{w}.png")).convert("RGB")
            sheet.paste(im.resize((480, 270), Image.LANCZOS), (480 * c, 0))
            x, y = pts[(k, ver, w)]
            sheet.paste(im.crop((x - CW // 2, y - CH // 2, x + CW // 2, y + CH // 2)).resize((CW * 2, CH * 2), Image.NEAREST), (480 * c, 270))
            label(dr, 480 * c + 4, 4, f"{nm} {ver} · {wn}")
            label(dr, 480 * c + 4, 274, "턱~옷깃 원본 ×2")
            c += 1
    sheet.save(os.path.join(out, f"neck_turn_{k}.jpg"), quality=92)
    rows.append(sheet)
if rows:
    t = Image.new("RGB", (1920, sum(r.height for r in rows)))
    y = 0
    for r in rows:
        t.paste(r, (0, y)); y += r.height
    t.save(os.path.join(out, "neck_turn.jpg"), quality=90)
    print("neck_turn.jpg", t.size)

# 얼굴 대칭: 사람마다 v2 · v3 · v3 격자(2cm 눈금 — 1.5m·FOV30 에서 1cm ≈ 13.5화소) 원본 해상도 가운데 잘라 그대로
if os.path.exists(os.path.join(src, f"yunjun_v3_face.png")):
    FW, FH = 480, 560
    ref = sys.argv[3] if len(sys.argv) > 3 else None
    made = []
    for k, nm in names.items():
        sheet = Image.new("RGB", (FW * 4, FH), (15, 15, 15))
        dr = ImageDraw.Draw(sheet)
        for c, ver in enumerate(("v2", "v3")):
            im = Image.open(os.path.join(src, f"{k}_{ver}_face.png")).convert("RGB")
            cx, cy = im.width // 2, im.height // 2
            crop = im.crop((cx - FW // 2, cy - FH // 2, cx + FW // 2, cy + FH // 2))
            sheet.paste(crop, (FW * c, 0))
            label(dr, FW * c + 4, 4, f"{nm} {ver} 정면 1.5m")
            if ver == "v3":
                g = crop.copy(); gd = ImageDraw.Draw(g)
                for yy in range(0, FH, 27):
                    gd.line((0, yy, FW, yy), fill=(0, 255, 255), width=1)
                gd.line((FW // 2, 0, FW // 2, FH), fill=(255, 255, 0), width=1)
                sheet.paste(g, (FW * 2, 0))
                label(dr, FW * 2 + 4, 4, "v3 수평 눈금 2cm·가운데선")
        if k == "yunjun" and ref and os.path.exists(ref):
            r = Image.open(ref).convert("RGB")
            r.thumbnail((FW, FH))
            sheet.paste(r, (FW * 3 + (FW - r.width) // 2, (FH - r.height) // 2))
            label(dr, FW * 3 + 4, 4, "설정화 yunjun_v3_face_b")
        else:
            g = Image.open(os.path.join(src, f"{k}_v2_face.png")).convert("RGB")
            cx, cy = g.width // 2, g.height // 2
            g = g.crop((cx - FW // 2, cy - FH // 2, cx + FW // 2, cy + FH // 2)); gd = ImageDraw.Draw(g)
            for yy in range(0, FH, 27):
                gd.line((0, yy, FW, yy), fill=(0, 255, 255), width=1)
            gd.line((FW // 2, 0, FW // 2, FH), fill=(255, 255, 0), width=1)
            sheet.paste(g, (FW * 3, 0))
            label(dr, FW * 3 + 4, 4, "v2 수평 눈금 2cm·가운데선")
        sheet.save(os.path.join(out, f"face_sym_{k}.jpg"), quality=92)
        made.append(sheet)
    t = Image.new("RGB", (FW * 4, FH * len(made)))
    for i, r in enumerate(made):
        t.paste(r, (0, FH * i))
    t.save(os.path.join(out, "face_sym.jpg"), quality=90)
    print("face_sym.jpg", t.size)
