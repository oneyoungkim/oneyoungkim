"""Make a generated texture swatch tile seamlessly (match-crop + edge blend).

usage: python seamless.py <src.png> <out.png> [--kind grid|free] [--size 1024] [--preview tile.jpg]

Why not plain offset blending: hand-drawn generated patterns (brick, tile, roof tile) are only roughly periodic, so a
half-offset cross-fade leaves blurry ghost bricks. Instead:
1. even out low-frequency brightness (vignette, lighting drift) so the repeat shows no blotch
2. search the crop (x0, width) whose left edge strip best matches the strip just past its right edge
   (column-to-column distance on a 512 px copy, gray + edge features), same for rows (y0, height)
3. crop there, then blend only a narrow band at the left/top edge toward the real continuation of the right/bottom
   edge, so column 0 continues column width-1 exactly; grid uses a narrow band (no ghosting), free a wide one
4. resize to size x size (the crop is kept near square)
"""
import argparse
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, sobel


def flatten(a, sigma):
    """divide out the low-frequency brightness (blur done on a 128 px copy, then scaled back up)"""
    H, W = a.shape[:2]
    s = 128 / max(H, W)
    sm = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize((max(8, int(W * s)), max(8, int(H * s))), Image.BILINEAR)).astype(np.float32) / 255
    out = np.empty_like(a)
    for c in range(3):
        low = gaussian_filter(sm[..., c], sigma * s, mode='reflect')
        low = np.asarray(Image.fromarray(low.astype(np.float32), mode='F').resize((W, H), Image.BILINEAR))
        out[..., c] = a[..., c] / np.maximum(low, 1e-3) * low.mean()
    return np.clip(out, 0, 1)


def features(a):
    g = a.mean(axis=2)
    e = np.hypot(sobel(g, 0), sobel(g, 1))
    return np.stack([g, a[..., 0], a[..., 2], e * 0.5], axis=2)


def best_cut(F, k, wmin, wmax):
    """F: (rows, cols, ch). returns (x0, w, cost) minimizing |F[:, x0:x0+k] - F[:, x0+w:x0+w+k]|"""
    n = F.shape[1]
    cols = F.transpose(1, 0, 2).reshape(n, -1)              # one vector per column
    sq = (cols ** 2).sum(1)
    D = np.sqrt(np.maximum(sq[:, None] + sq[None, :] - 2 * cols @ cols.T, 0)) / np.sqrt(cols.shape[1])
    best = (None, None, 1e9)
    for w in range(wmin, min(wmax, n - k) + 1):
        diag = D[np.arange(0, n - w), np.arange(w, n)]      # D[x, x + w]
        if len(diag) < k:
            continue
        c = np.convolve(diag, np.ones(k) / k, 'valid')       # cost for x0 = 0 .. n - w - k
        i = int(np.argmin(c))
        cost = c[i] * (1 + 0.25 * (1 - w / n))               # mild preference for bigger crops
        if cost < best[2]:
            best = (i, w, cost)
    return best


def blend_edge(C, cont, K, feather=3):
    """C: crop (axis 1 = x). cont: the K columns right after the crop. Column 0 becomes the continuation.
    Inside the K band the switch from 'cont' to C follows a minimum-error path (image quilting), so mismatching
    content is cut along mortar lines/gaps instead of cross-faded into ghosts; a few px feather softens the cut."""
    band = C[:, :K]
    err = np.abs(cont - band).sum(axis=2)
    err[:, 0] += 1e3                         # column 0 must stay the continuation
    rows = err.shape[0]
    cost = err.copy(); back = np.zeros_like(err, dtype=np.int64)
    for y in range(1, rows):
        prev = cost[y - 1]
        l = np.concatenate([[np.inf], prev[:-1]]); r = np.concatenate([prev[1:], [np.inf]])
        stack = np.stack([l, prev, r]); i = np.argmin(stack, axis=0)
        cost[y] += stack[i, np.arange(K)]; back[y] = np.arange(K) + i - 1
    path = np.zeros(rows, dtype=np.int64); path[-1] = int(np.argmin(cost[-1]))
    for y in range(rows - 1, 0, -1):
        path[y - 1] = back[y, path[y]]
    xs = np.arange(K)[None, :]
    al = np.clip((path[:, None] - xs) / feather + 0.5, 0, 1).astype(np.float32)   # 1 left of the path -> cont
    C = C.copy()
    C[:, :K] = al[..., None] * cont + (1 - al[..., None]) * band
    return C


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('out')
    ap.add_argument('--kind', default='free'); ap.add_argument('--size', type=int, default=1024)
    ap.add_argument('--preview')
    o = ap.parse_args()
    a = np.asarray(Image.open(o.src).convert('RGB')).astype(np.float32) / 255
    H, W = a.shape[:2]
    a = flatten(a, max(H, W) / 5)
    S = 512
    f = W / S
    small = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize((S, int(round(H / f))), Image.BILINEAR)).astype(np.float32) / 255
    k = 10 if o.kind == 'grid' else 24          # search band (512 scale)
    Kb = 0.06 if o.kind == 'grid' else 0.10    # blend band as share of the crop
    x0, w, cx = best_cut(features(small), k, int(0.70 * S), int(0.96 * S) - k)
    X0, Wc = int(round(x0 * f)), int(round(w * f))
    KX = max(6, int(Kb * Wc)); KX = min(KX, W - X0 - Wc)
    Cx = blend_edge(a[:, X0:X0 + Wc], a[:, X0 + Wc:X0 + Wc + KX], KX)
    # rows on the column-fixed strip
    smallc = small[:, x0:x0 + w]
    h_target = w                                # keep near square
    hmin = max(int(0.70 * smallc.shape[0]), int(0.85 * h_target)); hmax = min(smallc.shape[0] - k, int(1.15 * h_target))
    if hmin > hmax:
        hmin, hmax = int(0.6 * smallc.shape[0]), smallc.shape[0] - k
    y0, h, cy = best_cut(features(smallc).transpose(1, 0, 2), k, hmin, hmax)
    Y0, Hc = int(round(y0 * f)), int(round(h * f))
    KY = max(6, int(Kb * Hc)); KY = min(KY, H - Y0 - Hc)
    Ct = Cx.transpose(1, 0, 2)
    Cy = blend_edge(Ct[:, Y0:Y0 + Hc], Ct[:, Y0 + Hc:Y0 + Hc + KY], KY).transpose(1, 0, 2)
    img = Image.fromarray((np.clip(Cy, 0, 1) * 255 + 0.5).astype(np.uint8)).resize((o.size, o.size), Image.LANCZOS)
    img.save(o.out)
    if o.preview:
        t = np.tile(np.asarray(img), (2, 2, 1))
        im = Image.fromarray(t); im.thumbnail((900, 900)); im.save(o.preview, quality=88)
    print('SEAM', o.out, {'kind': o.kind, 'crop_x': [X0, Wc], 'crop_y': [Y0, Hc], 'cost': [round(float(cx), 4), round(float(cy), 4)], 'band': [KX, KY]})


if __name__ == '__main__':
    main()
