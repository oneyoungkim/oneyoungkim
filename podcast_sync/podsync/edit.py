"""Multicam cut list: main camera most of the time, the others by weight,
cutting preferably where the talking person changes."""
import random
from fractions import Fraction

import numpy as np
from scipy.ndimage import uniform_filter1d

SHOT_LEN = {"main": (5.0, 13.0), "other": (3.0, 7.5)}
MIN_SHOT = 1.5


def speaker_changes(mic_clips, P0, P1, rate=10):
    """Master times where the dominant microphone changes (candidate cut points)."""
    n = int((P1 - P0) * rate)
    if n <= 0 or not mic_clips:
        return np.array([])
    grid = P0 + np.arange(n) / rate
    lv = []
    for c in mic_clips:
        sm = uniform_filter1d(c.le.astype(float), 30, mode="nearest")
        idx = np.round(((grid - c.a) / c.r - c.audio_offset) * 100).astype(int)
        ok = (idx >= 0) & (idx < len(sm))
        v = np.full(n, -200.0)
        v[ok] = sm[idx[ok]] - c.speech_level
        lv.append(v)
    lv = np.array(lv)
    dom = np.argmax(lv, axis=0)
    dom[lv.max(axis=0) < -12] = -1
    # mode filter (1 s) by majority over a sliding window
    out = dom.copy()
    k = rate // 2
    for i in range(n):
        w = dom[max(0, i - k): i + k + 1]
        w = w[w >= 0]
        out[i] = np.bincount(w).argmax() if len(w) else -1
    changes = []
    last = None
    for i, d in enumerate(out):
        if d < 0:
            continue
        if last is not None and d != last:
            changes.append(grid[i])
        last = d
    return np.array(changes)


def camera_spans(cam_clips, margin=0.25):
    spans = []
    for c in cam_clips:
        s, e = c.video_span()
        if e - s > 2 * margin + MIN_SHOT:
            spans.append((s + margin, e - margin, c))
    return sorted(spans, key=lambda x: x[0])


def plan_shots(cameras, weights, main_kind, mic_clips, fps, seed=7):
    """cameras: kind -> [clips]. Returns (P0, P1, nframes, shots)
    shots: list of dict(kind, clip, f0, f1) with frame indices on the output timeline."""
    spans = {k: camera_spans(v) for k, v in cameras.items() if v}
    all_video = [s for v in spans.values() for s in v]
    mic_spans = [c.audio_span() for c in mic_clips]
    if not all_video:
        raise RuntimeError("영상 파일이 없습니다")
    v0 = min(s[0] for s in all_video)
    v1 = max(s[1] for s in all_video)
    if mic_spans:
        P0 = max(v0, min(s for s, _ in mic_spans))
        P1 = min(v1, max(e for _, e in mic_spans))
    else:
        P0, P1 = v0, v1
    fps = Fraction(fps)
    nframes = int((P1 - P0) * fps)
    P1 = P0 + nframes / fps
    changes = speaker_changes(mic_clips, P0, P1)
    rng = random.Random(seed)
    wsum = sum(weights.get(k, 0.1) for k in spans)
    w = {k: weights.get(k, 0.1) / wsum for k in spans}

    def frame_of(t):
        return int(round((t - P0) * fps))

    def span_at(kind, t, need):
        for s, e, c in spans[kind]:
            if s <= t + 1e-6 and e >= t + need:
                return s, e, c
        return None

    shots = []
    used = {k: 0.0 for k in spans}
    cur = None
    f = 0
    while f < nframes:
        t = P0 + f / fps
        avail = [k for k in spans if span_at(k, t, MIN_SHOT) or span_at(k, t, (nframes - f) / fps)]
        if not avail:
            nxt = min([s for v in spans.values() for s, e, c in v if s > t] + [P1])
            f1 = max(f + 1, min(nframes, frame_of(nxt) + 1))
            shots.append(dict(kind=None, clip=None, f0=f, f1=f1))
            f = f1
            cur = None
            continue
        cand = [k for k in avail if k != cur] or avail
        elapsed = t - P0 + 5.0
        kind = max(cand, key=lambda k: (w[k] * elapsed - used[k], w[k]))
        lo, hi = SHOT_LEN["main" if kind == main_kind else "other"]
        want = rng.uniform(lo, hi)
        if kind == main_kind and w[kind] * elapsed - used[kind] > hi:
            want *= 1.4
        end = t + want
        near = changes[(changes > t + lo * 0.7) & (changes < t + hi * 1.3)]
        if len(near):
            end = float(near[np.argmin(np.abs(near - end))])
        s, e, clip = span_at(kind, t, MIN_SHOT) or span_at(kind, t, (nframes - f) / fps)
        end = min(end, e, P1)
        f1 = min(nframes, max(f + 1, frame_of(end)))
        if nframes - f1 < int(MIN_SHOT * fps):
            if e >= P1 - 1e-6:
                f1 = nframes
        shots.append(dict(kind=kind, clip=clip, f0=f, f1=f1))
        used[kind] += (f1 - f) / fps
        cur = kind
        f = f1
    # merge neighbours that ended up on the same clip
    merged = []
    for s in shots:
        if merged and merged[-1]["clip"] is s["clip"] and merged[-1]["kind"] == s["kind"]:
            merged[-1]["f1"] = s["f1"]
        else:
            merged.append(s)
    return P0, P1, nframes, merged


def share(shots, fps):
    tot = {}
    for s in shots:
        tot[s["kind"]] = tot.get(s["kind"], 0) + (s["f1"] - s["f0"])
    n = sum(tot.values()) or 1
    return {k: v / n for k, v in tot.items()}
