# face_warp_map.py 대응표로 텍스처를 다시 칠한다(쌍선형 샘플). 사용: python face_warp_apply.py <in.jpg> <map.npz> <out.jpg>
import sys
import numpy as np
from PIL import Image
src, mp, dst = sys.argv[1], sys.argv[2], sys.argv[3]
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
H, W = im.shape[:2]
m = np.load(mp)
px = np.clip(m['px'].astype(int), 0, W - 1); py = np.clip(m['py'].astype(int), 0, H - 1)
su = np.clip(m['su'] - 0.5, 0, W - 1.001); sv = np.clip(m['sv'] - 0.5, 0, H - 1.001)
x0 = np.floor(su).astype(int); y0 = np.floor(sv).astype(int); fx = (su - x0)[:, None]; fy = (sv - y0)[:, None]
c = (im[y0, x0] * (1 - fx) * (1 - fy) + im[y0, x0 + 1] * fx * (1 - fy) + im[y0 + 1, x0] * (1 - fx) * fy + im[y0 + 1, x0 + 1] * fx * fy)
out = im.copy()
out[py, px] = c
Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst, quality=95)
print('WARPED', len(px), 'texels ->', dst)
