"""Audio-based synchronisation of every clip onto one master timeline.

Each clip gets a linear map   master = a + r * local
where `local` is seconds from the start of the file (the same convention as
ffmpeg's input -ss) and `r` absorbs the clock drift of the recording device.

Pipeline
  1. decode every clip's audio to 16 kHz mono (cached on disk, memory-mapped)
  2. coarse: normalised cross-correlation of speech-onset envelopes (10 ms)
     between all pairs, then a maximum spanning tree gives rough positions
  3. fine: GCC-PHAT on 4 s windows spread over each clip against a reference
     built from already placed microphones; a robust line fit gives offset +
     drift. Lavalier pairs show two peaks (+d / -d, from voice bleed between
     mics); a two-cluster fit takes the midpoint, i.e. true clock alignment.
"""
import hashlib
import json
import os

import numpy as np
from scipy.ndimage import uniform_filter1d
from scipy.signal import butter, sosfilt

from . import ff

ANA_SR = 16000
HOP = 160                      # 100 Hz envelope
FEAT_RATE = ANA_SR // HOP
CACHE_VERSION = 3
CAMERA_MIC_DELAY = 0.006       # sound travel time to an on-camera mic at ~2 m


class Clip:
    def __init__(self, item):
        info = item["info"]
        self.rel = item["rel"]
        self.path = item["path"]
        self.kind = item["kind"]
        self.info = info
        self.is_mic = self.kind in ("mic", "pocket")
        self.has_video = self.kind in ("leica", "pocket", "fuji") and info.has_video
        self.audio_offset = info.audio_offset
        self.sig = None
        self.le = None
        self.feat = None
        self.speech_level = 0.0
        self.norm = 1.0
        self.coarse = None       # master time of analysis sample 0 (r = 1)
        self.a = None
        self.r = 1.0
        self.diag = {}

    # analysis sample 0 sits at local time audio_offset
    def ana_start_master(self):
        return self.a + self.r * self.audio_offset

    def ana_len_s(self):
        return len(self.sig) / ANA_SR

    def master_to_local(self, m):
        return (m - self.a) / self.r

    def audio_span(self):
        s = self.ana_start_master()
        return s, s + self.r * self.ana_len_s()

    def video_span(self):
        vo, vd = self.info.video_offset, self.info.video_duration
        return self.a + self.r * vo, self.a + self.r * (vo + vd)


def log(msg):
    print(msg, flush=True)


# ---------------------------------------------------------------- decoding ---

def _sig_key(clip):
    st = os.stat(clip.path)
    return hashlib.sha1(("%s|%d|%d" % (clip.rel, st.st_size, int(st.st_mtime))).encode("utf-8")).hexdigest()[:16]


def load_analysis_audio(clips, work):
    os.makedirs(os.path.join(work, "ana"), exist_ok=True)
    for i, c in enumerate(clips, 1):
        out = os.path.join(work, "ana", _sig_key(c) + ".f32")
        if not (os.path.isfile(out) and os.path.getsize(out) > 0):
            log("  [%d/%d] 소리 추출: %s" % (i, len(clips), c.rel))
            tmp = out + ".part"
            ff.run(["-v", "error", "-i", c.path, "-map", "0:a:0", "-vn", "-sn", "-dn",
                    "-ac", "1", "-ar", str(ANA_SR), "-f", "f32le", tmp])
            os.replace(tmp, out)
        c.sig = np.memmap(out, dtype=np.float32, mode="r")
        c.le = envelope_db(c.sig)
        c.feat = onset_feature(c.le)
        floor = np.percentile(c.le, 20)
        act = c.le > floor + 10
        if act.sum() < 50:
            act = c.le >= np.percentile(c.le, 80)
        c.speech_level = float(np.median(c.le[act]))
        c.norm = 1.0 / max(10 ** (c.speech_level / 20.0), 1e-6)


def envelope_db(sig):
    """Band-passed (250-3500 Hz) log energy at 100 Hz."""
    sos = butter(4, [250, 3500], btype="band", fs=ANA_SR, output="sos")
    zi = np.zeros((sos.shape[0], 2))
    n_frames = len(sig) // HOP
    out = np.empty(n_frames, np.float32)
    chunk = HOP * 6000
    pos, fi = 0, 0
    total = n_frames * HOP
    while pos < total:
        end = min(pos + chunk, total)
        y, zi = sosfilt(sos, np.asarray(sig[pos:end], dtype=np.float64), zi=zi)
        e = (y.reshape(-1, HOP) ** 2).mean(axis=1)
        out[fi:fi + len(e)] = 10 * np.log10(e + 1e-12)
        fi += len(e)
        pos = end
    return out


def onset_feature(le):
    le = le.astype(np.float64)
    floor = np.percentile(le, 20)
    f = le - uniform_filter1d(le, 101, mode="nearest")
    w = np.clip((le - floor - 3.0) / 12.0, 0.0, 1.0)
    f = np.clip(f, -15, 15) * w
    f -= f.mean()
    return f / (f.std() + 1e-9)


# ------------------------------------------------------------------ coarse ---

def ncc(x, y, min_overlap):
    """Find lag L (frames) with y[n] ~ x[n + L]. Returns (L, peak, z, ratio) or None."""
    nx, ny = len(x), len(y)
    if nx < min_overlap or ny < min_overlap:
        return None
    n = 1 << int(np.ceil(np.log2(nx + ny)))
    c = np.fft.irfft(np.fft.rfft(x, n) * np.conj(np.fft.rfft(y, n)), n)
    lags = np.arange(-(ny - 1), nx)
    cc = np.concatenate([c[n - (ny - 1):], c[:nx]])
    cx = np.concatenate([[0.0], np.cumsum(x * x)])
    cy = np.concatenate([[0.0], np.cumsum(y * y)])
    x0, x1 = np.maximum(lags, 0), np.minimum(nx, ny + lags)
    y0, y1 = np.maximum(-lags, 0), np.minimum(ny, nx - lags)
    valid = (x1 - x0) >= min_overlap
    if not valid.any():
        return None
    ex = cx[x1] - cx[x0]
    ey = cy[y1] - cy[y0]
    nc = np.full(len(cc), -1.0)
    nc[valid] = cc[valid] / np.sqrt(ex[valid] * ey[valid] + 1e-9)
    k = int(np.argmax(nc))
    peak = nc[k]
    vals = nc[valid]
    med = np.median(vals)
    mad = np.median(np.abs(vals - med)) * 1.4826 + 1e-9
    mask = valid.copy()
    mask[max(0, k - 100):k + 101] = False
    second = nc[mask].max() if mask.any() else 0.0
    return int(lags[k]), float(peak), float((peak - med) / mad), float(peak / max(second, 1e-3))


def coarse_place(clips, anchor):
    """Rough master position (seconds) of analysis sample 0 for every reachable clip."""
    edges = {}
    n = len(clips)
    for i in range(n):
        for j in range(i + 1, n):
            xi, xj = clips[i].feat, clips[j].feat
            shorter = min(len(xi), len(xj))
            min_ov = min(3000, int(shorter * 0.6))
            res = ncc(xi, xj, max(min_ov, 500))
            if res is None:
                continue
            lag, peak, z, ratio = res
            if z >= 10 and ratio >= 1.2 and peak >= 0.08:
                edges[(i, j)] = (lag / FEAT_RATE, peak)
                edges[(j, i)] = (-lag / FEAT_RATE, peak)
    placed = {anchor: 0.0}
    while True:
        best = None
        for (i, j), (lag, peak) in edges.items():
            if i in placed and j not in placed and (best is None or peak > best[3]):
                best = (i, j, lag, peak)
        if best is None:
            break
        i, j, lag, _ = best
        placed[j] = placed[i] + lag
    for idx, c in enumerate(clips):
        c.coarse = placed.get(idx)
    return edges


# -------------------------------------------------------------------- fine ---

WIN_S = 4.0


def _read(sig, i0, n):
    out = np.zeros(n)
    a, b = max(i0, 0), min(i0 + n, len(sig))
    if b > a:
        out[a - i0:b - i0] = sig[a:b]
    return out


def ref_window(placed, m0, n):
    """Sum of placed clips' analysis audio (drift-corrected) for master [m0, m0 + n/sr)."""
    out = np.zeros(n)
    t = m0 + np.arange(n) / ANA_SR
    for c in placed:
        s = ((t - c.a) / c.r - c.audio_offset) * ANA_SR
        valid = (s >= 0) & (s < len(c.sig) - 2)
        if not valid.any():
            continue
        sv = s[valid]
        i0 = int(np.floor(sv[0]))
        i1 = int(np.ceil(sv[-1])) + 2
        seg = np.asarray(c.sig[i0:i1], dtype=np.float64)
        out[valid] += np.interp(sv, np.arange(i0, i0 + len(seg)), seg) * c.norm
    return out


def covered(placed, m0, m1):
    """True if [m0, m1] is fully inside the union of placed clips' audio spans."""
    spans = sorted(c.audio_span() for c in placed)
    cur = m0
    for s, e in spans:
        if s <= cur + 1e-6 and e > cur:
            cur = e
            if cur >= m1:
                return True
    return cur >= m1


_BAND = {}


def _band(n):
    if n not in _BAND:
        f = np.fft.rfftfreq(n, 1.0 / ANA_SR)
        w = np.ones_like(f)
        lo, hi = 250.0, 4500.0
        w[f < lo * 0.5] = 0
        m = (f >= lo * 0.5) & (f < lo)
        w[m] = 0.5 - 0.5 * np.cos(np.pi * (f[m] - lo * 0.5) / (lo * 0.5))
        m = (f > hi) & (f < hi * 1.4)
        w[m] = 0.5 + 0.5 * np.cos(np.pi * (f[m] - hi) / (hi * 0.4))
        w[f >= hi * 1.4] = 0
        _BAND[n] = w
    return _BAND[n]


def gcc_phat(a, b):
    """Return (k, psr): a[j] best matches b[j + k], 0 <= k <= len(b) - len(a)."""
    na, nb = len(a), len(b)
    n = 1 << int(np.ceil(np.log2(na + nb)))
    win = np.hanning(na)
    A = np.fft.rfft(a * win, n)
    B = np.fft.rfft(b, n)
    R = B * np.conj(A)
    mag = np.abs(R)
    R = R / (mag + mag.max() * 1e-6 + 1e-20) * _band(n)
    c = np.fft.irfft(R, n)[:nb - na + 1]
    k = int(np.argmax(c))
    kf = float(k)
    if 0 < k < len(c) - 1:
        y0, y1, y2 = c[k - 1], c[k], c[k + 1]
        den = y0 - 2 * y1 + y2
        if den < 0:
            kf += 0.5 * (y0 - y2) / den
    rest = np.concatenate([c[:max(0, k - 16)], c[k + 17:]])
    psr = (c[k] - rest.mean()) / (rest.std() + 1e-12) if len(rest) > 10 else 0.0
    return kf, float(psr)


def measure(clip, placed, predict, search_s, step_s, max_windows=400):
    """Measure (local_t, master_t, psr) pairs at speech windows along the clip."""
    n_win = int(WIN_S * ANA_SR)
    n_search = int(search_s * ANA_SR)
    dur = clip.ana_len_s()
    frames = clip.le
    thr = np.percentile(frames, 20) + 10
    starts = np.arange(1.0, dur - WIN_S - 1.0, step_s)
    if len(starts) > max_windows:
        starts = np.linspace(1.0, dur - WIN_S - 1.0, max_windows)
    out = []
    for s in starts:
        f0, f1 = int(s * FEAT_RATE), int((s + WIN_S) * FEAT_RATE)
        if np.mean(frames[f0:f1] > thr) < 0.25:
            continue
        i0 = int(round(s * ANA_SR))
        a = _read(clip.sig, i0, n_win)
        t_local = clip.audio_offset + i0 / ANA_SR          # local time of a[0]
        m_pred = predict(t_local)
        m0 = m_pred - search_s
        m1 = m_pred + WIN_S + search_s
        if not covered(placed, m0, m1):
            continue
        b = ref_window(placed, m0, n_win + 2 * n_search)
        if np.sqrt(np.mean(b * b)) < 1e-4:
            continue
        k, psr = gcc_phat(a, b)
        out.append((t_local, m0 + k / ANA_SR, psr))
    return out


def _wls(X, y, w):
    sw = np.sqrt(w)
    coef, *_ = np.linalg.lstsq(X * sw[:, None], y * sw, rcond=None)
    return coef


def robust_line(t, m, w, tol):
    """Fit m = alpha + beta*(t - tc). Returns (alpha, beta, tc, inlier_mask)."""
    tc = float(np.median(t))
    x = t - tc
    off = m - t
    keep = np.abs(off - np.median(off)) < max(tol * 5, 0.05)
    for _ in range(8):
        if keep.sum() < 3:
            break
        X = np.stack([np.ones(keep.sum()), x[keep]], 1)
        coef = _wls(X, m[keep], w[keep])
        res = m - (coef[0] + coef[1] * x)
        mad = np.median(np.abs(res[keep])) * 1.4826
        new = np.abs(res) < max(tol, 3 * mad)
        if (new == keep).all():
            break
        keep = new
    if keep.sum() < 3:
        return float(np.median(m - x)), 1.0, tc, keep
    X = np.stack([np.ones(keep.sum()), x[keep]], 1)
    coef = _wls(X, m[keep], w[keep])
    return float(coef[0]), float(coef[1]), tc, keep


def two_cluster_line(t, m, w):
    """Lavalier vs lavalier: points sit at +d or -d around the true line (voice bleed).
    Returns (alpha, beta, tc, keep, d)."""
    alpha, beta, tc, keep = robust_line(t, m, w, tol=0.012)
    x = t - tc
    res = m - (alpha + beta * x)
    r = res[keep]
    if len(r) < 12:
        return alpha, beta, tc, keep, 0.0
    c1, c2 = np.percentile(r, 20), np.percentile(r, 80)
    for _ in range(30):
        lab = np.abs(r - c1) > np.abs(r - c2)
        if lab.all() or (~lab).all():
            break
        n1, n2 = r[~lab].mean(), r[lab].mean()
        if abs(n1 - c1) < 1e-7 and abs(n2 - c2) < 1e-7:
            break
        c1, c2 = n1, n2
    lab = np.abs(res - c1) > np.abs(res - c2)
    frac = lab[keep].mean()
    if abs(c2 - c1) < 0.0003 or frac < 0.12 or frac > 0.88:
        a2, b2, tc2, keep2 = robust_line(t, m, w, tol=0.0008)
        return a2, b2, tc2, keep2, 0.0
    s = np.where(lab, 1.0, -1.0)
    keep2 = keep.copy()
    coef = None
    for _ in range(8):
        X = np.stack([np.ones(keep2.sum()), x[keep2], s[keep2]], 1)
        coef = _wls(X, m[keep2], w[keep2])
        pred = coef[0] + coef[1] * x + coef[2] * s
        res2 = m - pred
        # re-label each point to its nearer cluster
        alt = m - (coef[0] + coef[1] * x - coef[2] * s)
        flip = np.abs(alt) < np.abs(res2)
        s = np.where(flip, -s, s)
        res2 = np.minimum(np.abs(res2), np.abs(alt))
        mad = np.median(res2[keep2]) * 1.4826
        new = res2 < max(0.0006, 3 * mad)
        if (new == keep2).all() and not flip.any():
            break
        keep2 = new
        if keep2.sum() < 8:
            return alpha, beta, tc, keep, 0.0
    return float(coef[0]), float(coef[1]), tc, keep2, float(abs(coef[2]))


def fine_fit(clip, placed, two_cluster):
    """Refine clip.a / clip.r against the placed reference. Returns diagnostics dict."""
    base = clip.coarse - clip.audio_offset          # master = base + local (r = 1)

    def pred0(t):
        return base + t

    pts = measure(clip, placed, pred0, search_s=0.45, step_s=max(20.0, clip.ana_len_s() / 40.0), max_windows=40)
    pts = [p for p in pts if p[2] > 6]
    if len(pts) < 3:
        clip.a, clip.r = base, 1.0
        return dict(status="대략만 맞춤(정밀 비교 구간 부족)", windows=len(pts))
    t = np.array([p[0] for p in pts])
    m = np.array([p[1] for p in pts])
    w = np.array([min(p[2], 40.0) for p in pts])
    alpha, beta, tc, keep = robust_line(t, m, w, tol=0.02)
    if abs(beta - 1) > 5e-4 or np.ptp(t) < 60:
        beta = 1.0
        alpha = float(np.median((m - (t - tc))[keep])) if keep.any() else float(np.median(m - (t - tc)))

    def pred1(tt):
        return alpha + beta * (tt - tc)

    pts = measure(clip, placed, pred1, search_s=0.03, step_s=8.0)
    pts = [p for p in pts if p[2] > 5]
    if len(pts) < 5:
        clip.a = alpha - beta * tc
        clip.r = beta
        return dict(status="주의(정밀 비교 구간 적음)", windows=len(pts))
    t = np.array([p[0] for p in pts])
    m = np.array([p[1] for p in pts])
    w = np.array([min(p[2], 40.0) for p in pts])
    d = 0.0
    if two_cluster:
        alpha, beta, tc, keep, d = two_cluster_line(t, m, w)
    else:
        alpha, beta, tc, keep = robust_line(t, m, w, tol=0.004)
    if abs(beta - 1) > 5e-4 or np.ptp(t[keep]) < 60:
        beta = 1.0
        alpha = float(np.median((m - (t - tc))[keep])) if keep.any() else alpha
    clip.a = alpha - beta * tc
    clip.r = beta
    x = t - tc
    res = m - (alpha + beta * x)
    if d:
        res = np.minimum(np.abs(res - d), np.abs(res + d))
    rms = float(np.sqrt(np.mean(res[keep] ** 2))) if keep.any() else float("nan")
    ok = keep.sum() >= 5 and rms < (0.0015 if two_cluster else 0.006)
    return dict(status="정상" if ok else "주의", windows=int(len(pts)), used=int(keep.sum()),
                resid_ms=rms * 1000, bleed_ms=d * 1000, drift_ppm=(beta - 1) * 1e6)


def _overlap(c, placed):
    s0 = c.coarse
    e0 = s0 + c.ana_len_s()
    tot = 0.0
    for p in placed:
        s1, e1 = p.audio_span()
        tot += max(0.0, min(e0, e1) - max(s0, s1))
    return tot


def synchronise(clips, work):
    log("[1/5] 소리로 싱크 맞추는 중...")
    load_analysis_audio(clips, work)
    mics = [c for c in clips if c.is_mic]
    pool = mics if mics else clips
    anchor = max(pool, key=lambda c: len(c.sig))
    idx = clips.index(anchor)
    log("  대략 위치 찾는 중 (파일 %d개 서로 비교)..." % len(clips))
    coarse_place(clips, idx)
    for c in clips:
        if c.coarse is None:
            c.diag = dict(status="싱크 실패(다른 파일과 겹치는 소리를 못 찾음)")
    anchor.a, anchor.r = -anchor.audio_offset + anchor.coarse, 1.0
    anchor.diag = dict(status="기준", windows=0, drift_ppm=0.0)
    placed = [anchor]

    group_a = [c for c in pool if c is not anchor and c.coarse is not None]
    while group_a:
        nxt = max(group_a, key=lambda c: _overlap(c, placed))
        group_a.remove(nxt)
        log("  정밀 정렬: %s" % nxt.rel)
        nxt.diag = fine_fit(nxt, placed, two_cluster=bool(mics))
        placed.append(nxt)
    ref = list(placed)
    for c in clips:
        if c in ref or c.coarse is None:
            continue
        log("  정밀 정렬: %s" % c.rel)
        c.diag = fine_fit(c, ref, two_cluster=False)
        if mics and not c.is_mic:
            # the camera's own mic hears voices ~2 m away (~6 ms late); the picture has no such delay
            c.a += CAMERA_MIC_DELAY
    return [c for c in clips if c.a is not None]


# ------------------------------------------------------------------ cache ---

def signature(items):
    parts = []
    for it in sorted(items, key=lambda x: x["rel"]):
        st = os.stat(it["path"])
        parts.append("%s|%s|%d|%d" % (it["rel"], it["kind"], st.st_size, int(st.st_mtime)))
    return hashlib.sha1(("v%d\n" % CACHE_VERSION + "\n".join(parts)).encode("utf-8")).hexdigest()


def save(clips, path, sig):
    data = dict(signature=sig, clips={c.rel: dict(a=c.a, r=c.r, diag=c.diag) for c in clips})
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)


def load(clips, path, sig):
    if not os.path.isfile(path):
        return False
    with open(path, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    if data.get("signature") != sig:
        return False
    for c in clips:
        d = data["clips"].get(c.rel)
        if d:
            c.a, c.r, c.diag = d["a"], d["r"], d.get("diag", {})
    return True
