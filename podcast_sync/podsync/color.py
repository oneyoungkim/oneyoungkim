"""Match every camera's look to the main camera with an automatically built 3D LUT.

For each camera we pull ~30 frames spread over the shoot (after converting to
Rec.709 RGB; HLG/PQ footage is tone-mapped first, optional manufacturer LUTs
`pre_<camera>.cube` in the shoot folder are applied first), then in CIE Lab:
  * lightness: monotone curve that maps the camera's L percentiles onto the
    main camera's (contrast / exposure / log flatness)
  * white balance: shift so near-neutral pixels (walls, shirts, greys) match
  * saturation: chroma scale so colourful pixels have the same median chroma
Everything is blended with a strength < 1 so framing differences between
cameras cannot push the result too far. The LUT is written as a .cube file,
which ffmpeg, Premiere, DaVinci Resolve and CapCut can all load.
"""
import os
import shutil

import numpy as np

from . import events, ff
from .events import log

LUT_N = 33
SAMPLE_W = 320




# ------------------------------------------------------------ transforms ---

def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0, None)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


_M = np.array([[0.4124564, 0.3575761, 0.1804375],
               [0.2126729, 0.7151522, 0.0721750],
               [0.0193339, 0.1191920, 0.9503041]])
_MI = np.linalg.inv(_M)
_WHITE = np.array([0.95047, 1.0, 1.08883])


def rgb_to_lab(rgb):
    xyz = srgb_to_linear(rgb) @ _M.T / _WHITE
    d = 6 / 29
    f = np.where(xyz > d ** 3, np.cbrt(xyz), xyz / (3 * d * d) + 4 / 29)
    L = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], -1)


def lab_to_rgb(lab):
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    fy = (L + 16) / 116
    fx = fy + a / 500
    fz = fy - b / 200
    d = 6 / 29
    f = np.stack([fx, fy, fz], -1)
    xyz = np.where(f > d, f ** 3, 3 * d * d * (f - 4 / 29)) * _WHITE
    return np.clip(linear_to_srgb(xyz @ _MI.T), 0, 1)


# ------------------------------------------------------------- ffmpeg io ---

def to_rgb_chain(info, pre_lut=None):
    """ffmpeg filter chain: decoded camera frames -> Rec.709 RGB (gbrp16le)."""
    matrix, rng, transfer = info.color
    if transfer in ("arib-std-b67", "smpte2084"):
        chain = ["zscale=t=linear:npl=100", "format=gbrpf32le", "zscale=p=bt709",
                 "tonemap=tonemap=mobius:param=0.3:desat=0", "zscale=t=bt709:m=bt709:r=pc", "format=gbrp16le"]
    else:
        chain = ["scale=in_color_matrix=%s:in_range=%s" % (matrix, rng), "format=gbrp16le"]
    if pre_lut:
        chain.append("lut3d=file=%s:interp=tetrahedral" % pre_lut)
    return chain


def grab_frames(clip, times, chain, cwd, width=SAMPLE_W):
    frames = []
    vf = ",".join(chain + ["scale=%d:-2:flags=area" % width, "format=rgb48le"])
    for t in times:
        data = ff.communicate_raw(["-ss", "%.3f" % t, "-i", clip.path, "-frames:v", "1", "-an", "-vf", vf,
                                   "-f", "rawvideo", "-"], cwd=cwd)
        w = width
        n = len(data) // 6
        if n < w * 10:
            continue
        h = n // w
        img = np.frombuffer(data[: w * h * 6], dtype=np.uint16).reshape(h, w, 3) / 65535.0
        frames.append(img)
    return frames


# ------------------------------------------------------------ statistics ---

PCTS = np.array([0.5, 1, 2, 5, 10, 15, 20, 30, 40, 50, 60, 70, 80, 85, 90, 95, 98, 99, 99.5])


def lab_stats(frames):
    px = np.concatenate([f.reshape(-1, 3) for f in frames])
    lab = rgb_to_lab(px)
    L, a, b = lab[:, 0], lab[:, 1], lab[:, 2]
    C = np.hypot(a, b)
    ok = (px.max(axis=1) < 0.985) & (px.max(axis=1) > 0.02)
    neutral = ok & (C < 10) & (L > 20) & (L < 95)
    if neutral.mean() < 0.02:
        neutral = ok & (C < np.percentile(C[ok], 15))
    colourful = ok & (L > 15) & (L < 92) & (C > 6)
    if not colourful.any():
        colourful = ok
    return dict(
        Lp=np.percentile(L, PCTS),
        n_a=float(np.median(a[neutral])), n_b=float(np.median(b[neutral])),
        c_med=float(np.median(C[colourful])),
        lab=lab, neutral=neutral, colourful=colourful,
    )


def tone_curve(src_p, tgt_p, strength):
    """Lightness mapping L' = offset + gain * L^gamma, least-squares fitted to the
    5..95 percentile pairs. Three bounded parameters cannot fold or band, and they
    cover exposure, contrast (incl. flat log footage) and midtone differences."""
    x = np.clip(src_p[3:-3] / 100.0, 1e-4, 1.0)
    y = tgt_p[3:-3] / 100.0
    best = None
    for g in np.arange(0.6, 1.601, 0.02):
        xg = x ** g
        A = np.stack([np.ones_like(xg), xg], 1)
        (off, gain), *_ = np.linalg.lstsq(A, y, rcond=None)
        gain = float(np.clip(gain, 0.5, 2.2))
        off = float(np.clip(np.mean(y - gain * xg), -0.35, 0.35))
        err = float(np.sum((off + gain * xg - y) ** 2))
        if best is None or err < best[0]:
            best = (err, off, gain, g)
    _, off, gain, g = best

    def f(L):
        mapped = (off + gain * np.clip(L / 100.0, 0, 1) ** g) * 100.0
        return L + strength * (mapped - L)
    f.params = dict(offset=off * 100, gain=gain, gamma=g)
    return f


def build_transform(src, tgt, strength=1.0):
    curve = tone_curve(src["Lp"], tgt["Lp"], strength)
    da = (tgt["n_a"] - src["n_a"]) * strength
    db = (tgt["n_b"] - src["n_b"]) * strength
    k = float(np.clip(tgt["c_med"] / max(src["c_med"], 1e-3), 0.6, 2.6))
    k = 1 + 0.7 * strength * (k - 1)          # chroma statistics depend on framing: go 70% of the way

    def apply(lab):
        out = lab.copy()
        out[..., 0] = np.clip(curve(lab[..., 0]), 0, 100)
        out[..., 1] = (lab[..., 1] - src["n_a"]) * k + src["n_a"] + da
        out[..., 2] = (lab[..., 2] - src["n_b"]) * k + src["n_b"] + db
        return out
    return apply, dict(sat=k, wb_a=da, wb_b=db, **curve.params)


def write_cube(path, fn_rgb, title):
    """fn_rgb maps (N, 3) RGB in [0, 1] to (N, 3)."""
    g = np.linspace(0, 1, LUT_N)
    b, gg, r = np.meshgrid(g, g, g, indexing="ij")          # red fastest
    rgb = np.stack([r, gg, b], -1).reshape(-1, 3)
    out = np.clip(fn_rgb(rgb), 0, 1)
    with open(path, "w", encoding="ascii", newline="\n") as fh:
        fh.write('TITLE "%s"\nLUT_3D_SIZE %d\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 1 1 1\n' % (title, LUT_N))
        for v in out:
            fh.write("%.6f %.6f %.6f\n" % tuple(v))


def lab_fn(apply):
    return lambda rgb: lab_to_rgb(apply(rgb_to_lab(rgb)))


def apply_img(fn_rgb, img):
    h, w, _ = img.shape
    return np.clip(fn_rgb(img.reshape(-1, 3)), 0, 1).reshape(h, w, 3)


# ------------------------------------------- matching the same objects ---

MATCH_W = 960


def _gray(img):
    import cv2
    g = np.clip(img @ np.array([0.2126, 0.7152, 0.0722]), 0, 1)
    return cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply((g * 255 + 0.5).astype(np.uint8))


def _patch(img, pt, size):
    """Mean colour of the keypoint's own neighbourhood (scale-normalised, so the same
    physical area in both views) and how uniform it is."""
    h, w, _ = img.shape
    r = max(1.5, 0.35 * size)
    x, y = pt
    x0, x1 = int(round(x - r)), int(round(x + r)) + 1
    y0, y1 = int(round(y - r)), int(round(y + r)) + 1
    if x0 < 0 or y0 < 0 or x1 > w or y1 > h:
        return None
    p = img[y0:y1, x0:x1].reshape(-1, 3)
    return p.mean(axis=0), float(p.std(axis=0).mean())


def correspondences(pairs):
    """Colour pairs (camera -> main camera) of the same scene points, found with SIFT
    matches that survive a two-view geometry check. Frames are from the same moment."""
    import cv2
    sift = cv2.SIFT_create(nfeatures=4000)
    bf = cv2.BFMatcher(cv2.NORM_L2)
    src, tgt, wts = [], [], []
    for main_img, cam_img in pairs:
        ka, da = sift.detectAndCompute(_gray(cam_img), None)
        kb, db = sift.detectAndCompute(_gray(main_img), None)
        if da is None or db is None or len(ka) < 8 or len(kb) < 8:
            continue
        good = [m[0] for m in bf.knnMatch(da, db, k=2) if len(m) == 2 and m[0].distance < 0.75 * m[1].distance]
        if len(good) < 12:
            continue
        pa = np.float32([ka[m.queryIdx].pt for m in good])
        pb = np.float32([kb[m.trainIdx].pt for m in good])
        _, mask = cv2.findFundamentalMat(pa, pb, cv2.FM_RANSAC, 3.0, 0.999)
        if mask is None:
            continue
        for m, ok in zip(good, mask.ravel()):
            if not ok:
                continue
            a = _patch(cam_img, ka[m.queryIdx].pt, ka[m.queryIdx].size)
            b = _patch(main_img, kb[m.trainIdx].pt, kb[m.trainIdx].size)
            if a is None or b is None or max(a[1], b[1]) > 0.15:
                continue
            if a[0].max() > 0.97 or b[0].max() > 0.97:
                continue
            src.append(a[0])
            tgt.append(b[0])
            wts.append(1.0 / (0.02 + a[1] + b[1]))
    return np.array(src).reshape(-1, 3), np.array(tgt).reshape(-1, 3), np.array(wts)


def fit_colour_model(src, tgt, w):
    """RGB' = M * RGB^gamma + offset (per-channel gamma, 3x3 matrix), robust least squares,
    gently pulled toward 'no change'. Covers exposure, contrast/log, white balance, saturation."""
    from scipy.optimize import least_squares
    p0 = np.r_[np.ones(3), np.eye(3).ravel(), np.zeros(3)]
    lam = 0.01 * np.sqrt(len(src))
    sw = np.sqrt(w / w.mean())[:, None]

    def model(p, x):
        return (np.clip(x, 1e-4, 1) ** p[:3]) @ p[3:12].reshape(3, 3).T + p[12:15]

    def resid(p):
        return np.r_[((model(p, src) - tgt) * sw).ravel(), lam * (p - p0)]

    lb = np.r_[np.full(3, 0.35), np.full(9, -1.5), np.full(3, -0.5)]
    ub = np.r_[np.full(3, 2.8), np.full(9, 2.5), np.full(3, 0.5)]
    p = least_squares(resid, p0, bounds=(lb, ub), loss="soft_l1", f_scale=0.03).x
    return lambda rgb: model(p, rgb)


def delta_e(a, b):
    return np.linalg.norm(rgb_to_lab(np.clip(a, 0, 1)) - rgb_to_lab(np.clip(b, 0, 1)), axis=-1)


# ------------------------------------------------------------------ main ---

def _covering(clips, m, margin=1.0):
    for c in clips:
        s, e = c.video_span()
        if s + margin <= m <= e - margin:
            return c
    return None


def moment_pairs(cameras, main_kind, kind, pres, work, n=14):
    """Frames of the main camera and `kind` taken at the same moments."""
    cam, main = cameras[kind], cameras[main_kind]
    lo = min(c.video_span()[0] for c in cam)
    hi = max(c.video_span()[1] for c in cam)
    ok = [m for m in np.linspace(lo, hi, 300) if _covering(cam, m) and _covering(main, m)]
    if not ok:
        return []
    pairs = []
    for i in np.linspace(0, len(ok) - 1, min(n, len(ok))):
        m = ok[int(round(i))]
        cm, cc = _covering(main, m), _covering(cam, m)
        a = grab_frames(cm, [cm.master_to_local(m)], to_rgb_chain(cm.info, pres.get(main_kind)), work, MATCH_W)
        b = grab_frames(cc, [cc.master_to_local(m)], to_rgb_chain(cc.info, pres.get(kind)), work, MATCH_W)
        if a and b:
            pairs.append((a[0], b[0]))
    return pairs


def build_luts(cameras, main_kind, src_root, work, lut_dir, strength=1.0):
    """For each non-main camera build a LUT towards the main camera.
    Two candidate corrections are scored on held-out matched scene points:
      A) colour model fitted to matched objects (needs overlapping views)
      B) whole-frame statistics (always available)
    and the one that brings the same objects closer is used."""
    log("[2/5] 카메라 색감 맞추는 중 (기준: %s)..." % main_kind)
    os.makedirs(lut_dir, exist_ok=True)
    pres = {}
    for kind in cameras:
        user_lut = os.path.join(src_root, "pre_%s.cube" % kind)
        if os.path.isfile(user_lut):
            pres[kind] = "pre_%s.cube" % kind
            shutil.copyfile(user_lut, os.path.join(work, pres[kind]))
            log("  %s: 사용자 LUT 먼저 적용 (%s)" % (kind, os.path.basename(user_lut)))
    result = {k: dict(pre=pres.get(k), match=None) for k in cameras}
    views, notes = {}, {}
    others = [k for k in cameras if k != main_kind]
    for i, kind in enumerate(others):
        events.progress(i / max(1, len(others)))
        pairs = moment_pairs(cameras, main_kind, kind, pres, work)
        if not pairs:
            log("  %s: 메인 카메라와 같은 시간대 화면이 없어 색 맞춤을 건너뜁니다" % kind)
            continue
        small = [(a[::3, ::3], b[::3, ::3]) for a, b in pairs]
        glob_fn = lab_fn(build_transform(lab_stats([b for _, b in small]), lab_stats([a for a, _ in small]), strength)[0])
        try:
            src, tgt, wts = correspondences(pairs)
        except ImportError:
            src = np.zeros((0, 3))
        method, fn = "화면 통계", glob_fn
        before = after = None
        if len(src) >= 60:
            rng = np.random.default_rng(0)
            idx = rng.permutation(len(src))
            test, train = idx[: len(idx) // 5], idx[len(idx) // 5:]
            corr_fn = fit_colour_model(src[train], tgt[train], wts[train])
            before = float(np.median(delta_e(src[test], tgt[test])))
            e_corr = float(np.median(delta_e(corr_fn(src[test]), tgt[test])))
            e_glob = float(np.median(delta_e(glob_fn(src[test]), tgt[test])))
            if e_corr <= e_glob:
                method, fn = "같은 물체 %d곳 비교" % len(src), fit_colour_model(src, tgt, wts)
                after = e_corr
            else:
                after = e_glob
        if strength < 1.0:
            base = fn
            fn = lambda rgb, base=base: rgb + strength * (base(rgb) - rgb)
        name = "match_%s.cube" % kind
        write_cube(os.path.join(work, name), fn, "%s to %s" % (kind, main_kind))
        shutil.copyfile(os.path.join(work, name), os.path.join(lut_dir, name))
        result[kind].update(match=name, fn=fn)
        views[kind] = pairs[len(pairs) // 2]
        notes[kind] = dict(method=method, points=len(src), before=before, after=after)
        log("  %s: %s%s" % (kind, method, "" if before is None else " · 같은 물체 색차이 %.1f → %.1f" % (before, after)))
    return result, views, notes


def contact_sheet(views, luts, main_kind, out_path):
    """One row per camera: main camera | camera before | camera after, same moment."""
    from PIL import Image, ImageDraw
    if not views:
        return
    w = 426
    rows = []
    for kind, (main_img, cam_img) in views.items():
        fn = luts.get(kind, {}).get("fn")
        after = apply_img(fn, cam_img) if fn else cam_img
        rows.append((kind, [main_img, cam_img, after]))
    h = int(round(rows[0][1][0].shape[0] * w / rows[0][1][0].shape[1]))
    sheet = Image.new("RGB", (w * 3 + 40, (h + 26) * len(rows) + 30), (24, 24, 24))
    d = ImageDraw.Draw(sheet)
    for i, t in enumerate(("MAIN: %s" % main_kind, "BEFORE", "AFTER (matched)")):
        d.text((10 + i * (w + 10), 8), t, fill=(230, 230, 230))
    y = 30
    for kind, imgs in rows:
        for i, img in enumerate(imgs):
            arr = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
            sheet.paste(Image.fromarray(arr).resize((w, h)), (10 + i * (w + 10), y))
        d.text((12 + w + 10, y + h + 5), kind, fill=(255, 210, 90))
        y += h + 26
    sheet.save(out_path, quality=90)
