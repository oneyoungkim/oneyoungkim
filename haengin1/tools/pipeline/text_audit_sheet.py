# 전/후 묶음: python audit_sheet.py <전 shots> <후 shots> <결과.jpg> 정규식 정규식 … (쌍마다 왼쪽 전·오른쪽 후, 최대 6쌍 = 12칸)
import os, re, sys
from PIL import Image, ImageDraw, ImageFont
sys.stdout.reconfigure(encoding='utf-8')
a, b, out, pats = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4:]
F = ImageFont.truetype(r"C:\Windows\Fonts\malgunbd.ttf", 18)
W, H = 800, 450
la, lb = sorted(os.listdir(a)) if os.path.isdir(a) else [], sorted(os.listdir(b))
def tail(n): return re.sub(r'^\d+_', '', n)
rows = []
for p in pats:
    fb = next((n for n in lb if re.search(p, n)), None)
    if fb is None: print('후 없음', p); continue
    fa = next((n for n in la if tail(n) == tail(fb)), None) or next((n for n in la if re.search(p, n)), None)
    rows.append((p, fa, fb))
im = Image.new('RGB', (W * 2, H * len(rows)), (20, 20, 20))
d = ImageDraw.Draw(im)
for r, (p, fa, fb) in enumerate(rows):
    for c, (fold, f, tag) in enumerate(((a, fa, '전'), (b, fb, '후'))):
        if f:
            im.paste(Image.open(os.path.join(fold, f)).convert('RGB').resize((W, H), Image.LANCZOS), (c * W, r * H))
        s = tag + ' · ' + (tail(f)[:46] if f else '없음(이번에 처음 잰 구간)')
        w = d.textlength(s, font=F)
        d.rectangle((c * W, r * H, c * W + w + 10, r * H + 26), fill=(0, 0, 0)); d.text((c * W + 5, r * H + 2), s, font=F, fill=(255, 255, 255))
im.save(out, quality=90)
print('저장', len(rows))
