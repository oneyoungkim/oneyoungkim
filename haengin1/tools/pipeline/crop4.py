# Split a 4-view turnaround sheet into four square, equally scaled view images (front, left, back, right).
import sys
from PIL import Image
import numpy as np
src, outprefix = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(int)
# figure mask = anything not near-white. The old "dark" mask (rgb sum < 690) missed pale skin on thin forearms,
# split the arms off the body and then dropped them as narrow runs (냉장고 front/back lost both forearms).
dark = a.min(axis=2) < 235
# drop thin full-width guide/ground lines so they do not join the figures
line_rows = dark.mean(axis=1) > 0.9  # 0.5 also caught the rows where four A-posed figures span half the sheet
dark[line_rows, :] = False
col = dark.sum(axis=0) > 2
runs, inr = [], False
for x, v in enumerate(col):
    if v and not inr: s, inr = x, True
    if not v and inr:
        if x - s > 2: runs.append((s, x))
        inr = False
if inr: runs.append((s, len(col)))
# merge the closest neighbouring runs until exactly four figures remain (narrow runs = hands/fingers, keep them)
merged = [list(r) for r in runs]
while len(merged) > 4:
    gaps = [merged[i + 1][0] - merged[i][1] for i in range(len(merged) - 1)]
    i = gaps.index(min(gaps))
    merged[i] = [merged[i][0], merged[i + 1][1]]; del merged[i + 1]
merged = [tuple(r) for r in merged]
print('figures', merged)
assert len(merged) == 4, 'expected 4 figures'
boxes = []
for x0, x1 in merged:
    rows = np.where(dark[:, x0:x1].sum(axis=1) > 0)[0]
    boxes.append((x0, x1, rows[0], rows[-1]))
H = max(b[3] - b[2] for b in boxes); W = max(b[1] - b[0] for b in boxes)
side = int(max(H, W) * 1.10)
for (x0, x1, y0, y1), n in zip(boxes, ['front', 'left', 'back', 'right']):
    canvas = Image.new('RGB', (side, side), (255, 255, 255))
    crop = im.crop((x0, y0, x1, y1 + 1))
    # same scale for all views: align feet on one baseline and center horizontally
    canvas.paste(crop, (side // 2 - (x1 - x0) // 2, side - int(side * 0.05) - (y1 - y0)))
    canvas.resize((1536, 1536), Image.LANCZOS).save(f'{outprefix}_{n}.png')
    print(n, x0, x1, y0, y1)
