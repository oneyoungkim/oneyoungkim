# 검수 사진 합치기(2026-10-31 손가락·미트·인물별 자세)
#   hands <사진 폴더> <결과.jpg> 키 키 …            : 사람마다 한 줄 — 왼손 정면 전/후 · 왼손 옆 전/후 · 오른손 정면 전/후 · 오른손 옆 전/후 (Z_Hands_Shots)
#   mitt <사진 폴더> <결과.jpg> 접미사,접미사 키 키 …: 코치마다 한 줄 — {키}_{접미사}.png (Z_Mitt_Shots)
#   var <전 폴더> <후 폴더> <결과.jpg> 키:이름 …     : 사람마다 정면 전/후 · 옆 전/후 (Z_Stand_Shots, 한 줄 8칸)
import os, sys
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding='utf-8')
F = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 20)
FS = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 15)


def label(d, x, y, s, font=F):
    w = d.textlength(s, font=font)
    d.rectangle((x, y, x + w + 10, y + font.size + 8), fill=(0, 0, 0))
    d.text((x + 5, y + 2), s, font=font, fill=(255, 255, 255))


def tile(path, w, h):
    if os.path.exists(path):
        im = Image.open(path).convert('RGB')
        r = min(w / im.width, h / im.height)
        im = im.resize((round(im.width * r), round(im.height * r)), Image.LANCZOS)
        c = Image.new('RGB', (w, h), (30, 30, 30))
        c.paste(im, ((w - im.width) // 2, (h - im.height) // 2))
        return c
    c = Image.new('RGB', (w, h), (70, 20, 20))
    ImageDraw.Draw(c).text((10, h // 2), '없음', font=F, fill=(255, 255, 255))
    return c


mode = sys.argv[1]
if mode == 'hands':
    src, out, keys = sys.argv[2], sys.argv[3], sys.argv[4:]
    T = 240
    cols = [(h, v, ver) for h in 'LR' for v in ('front', 'side') for ver in ('before', 'after')]
    im = Image.new('RGB', (T * len(cols), T * len(keys) + 30), (20, 20, 20))
    d = ImageDraw.Draw(im)
    head = {'L': '왼손', 'R': '오른손', 'front': '정면', 'side': '옆', 'before': '전', 'after': '후'}
    for c, (h, v, ver) in enumerate(cols):
        d.text((c * T + 6, 4), f"{head[h]} {head[v]} {head[ver]}", font=FS, fill=(255, 255, 255))
    for r, k in enumerate(keys):
        for c, (h, v, ver) in enumerate(cols):
            im.paste(tile(os.path.join(src, f"{k}_{h}_{v}_{ver}.png"), T, T), (c * T, r * T + 30))
        label(d, 0, r * T + 30, k, FS)
    im.save(out, quality=88)
elif mode == 'mitt':
    src, out, sfx, keys = sys.argv[2], sys.argv[3], sys.argv[4].split(','), sys.argv[5:]
    W, H = 480, 270
    im = Image.new('RGB', (W * len(sfx), H * len(keys)), (20, 20, 20))
    d = ImageDraw.Draw(im)
    for r, k in enumerate(keys):
        for c, s in enumerate(sfx):
            im.paste(tile(os.path.join(src, f"{k}_{s}.png"), W, H), (c * W, r * H))
            label(d, c * W, r * H, f"{k} {s}", FS)
    im.save(out, quality=88)
elif mode == 'var':
    a, b, out, keys = sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5:]
    W, H = 240, 360
    per = 2       # 한 줄에 사람 둘
    rows = (len(keys) + per - 1) // per
    im = Image.new('RGB', (W * 4 * per, H * rows), (20, 20, 20))
    d = ImageDraw.Draw(im)
    for i, kn in enumerate(keys):
        k, n = kn.split(':')
        x0, y0 = (i % per) * W * 4, (i // per) * H
        for c, (fold, ver, view, tag) in enumerate(((a, 'after', 'front', '정면 전'), (b, 'after', 'front', '정면 후'), (a, 'after', 'side', '옆 전'), (b, 'after', 'side', '옆 후'))):
            im.paste(tile(os.path.join(fold, f"{k}_{ver}_{view}.png"), W, H), (x0 + c * W, y0))
            label(d, x0 + c * W, y0 + H - 26, tag, FS)
        label(d, x0, y0, n)
    im.save(out, quality=90)
if mode == 'row':
    # row <전 폴더> <후 폴더> <결과.jpg> front|side 키:이름 … : 전/후 두 줄(전신 그대로 나란히)
    a, b, out, view, keys = sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6:]
    W, H = 110, 330
    im = Image.new('RGB', (W * len(keys), H * 2 + 44), (20, 20, 20))
    d = ImageDraw.Draw(im)
    for r, fold in enumerate((a, b)):
        for c, kn in enumerate(keys):
            k, n = kn.split(':')
            src = Image.open(os.path.join(fold, f"{k}_after_{view}.png")).convert('RGB')
            cw = src.width * 0.5
            crop = src.crop((int(src.width / 2 - cw / 2), 0, int(src.width / 2 + cw / 2), src.height)).resize((W, H), Image.LANCZOS)
            im.paste(crop, (c * W, 22 + r * (H + 22)))
    for c, kn in enumerate(keys):
        d.text((c * W + 2, 3), kn.split(':')[1], font=FS, fill=(255, 255, 255))
        d.text((c * W + 2, H + 25), '후' if False else kn.split(':')[1], font=FS, fill=(255, 220, 120))
    d.text((2, 22), '전', font=F, fill=(255, 255, 255)); d.text((2, H + 44), '후', font=F, fill=(255, 220, 120))
    im.save(out, quality=92)
print('저장', out)
