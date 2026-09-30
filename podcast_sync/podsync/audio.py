"""Build the programme audio from the microphone recordings only.

  lanes    one track per microphone channel, drift-corrected (soxr) and placed on
           the master timeline at 48 kHz, sample accurate
  clean    high-pass + FFT denoise (ffmpeg afftdn) per lane
  level    per recording, speech level measured where that mic dominates
  automix  Dugan-style gain sharing: the mic of whoever talks opens, the others
           duck, so bleed / room noise does not pile up (4 open lavs -> 1)
  master   gentle bus compression + two-pass EBU R128 loudness (YouTube -14 LUFS)
"""
import json
import os
import re

import numpy as np
import soundfile as sf
import soxr
from scipy.ndimage import maximum_filter1d
from scipy.signal import filtfilt, lfilter

from . import events, ff
from .events import log

SR = 48000
CTRL = 48                      # 1 kHz control rate for the automixer
TARGET_SPEECH_DB = -23.0




# ------------------------------------------------------------- channels ---

def _snippet(clip, seconds=60.0):
    dur = clip.info.audio_duration
    start = max(0.0, dur / 2 - seconds / 2)
    data = ff.communicate_raw(["-ss", "%.3f" % start, "-t", "%.3f" % seconds, "-i", clip.path, "-map", "0:a:0",
                               "-vn", "-ar", "16000", "-f", "f32le", "-"])
    ch = max(1, clip.info.channels)
    x = np.frombuffer(data, dtype=np.float32)
    return x[: len(x) // ch * ch].reshape(-1, ch)


def channel_plan(clip):
    """Which channel(s) of this recording to use: list of 'c0' / 'c1' / 'mix'."""
    ch = clip.info.channels
    if ch <= 1:
        return ["c0"]
    x = _snippet(clip)
    if len(x) < 1000:
        return ["mix"]
    l, r = x[:, 0].astype(np.float64), x[:, 1].astype(np.float64)
    rl, rr = np.sqrt(np.mean(l * l)), np.sqrt(np.mean(r * r))
    if min(rl, rr) < 1e-5:                      # one side silent
        return ["c0"] if rl >= rr else ["c1"]
    corr = float(np.dot(l, r) / (np.sqrt(np.dot(l, l) * np.dot(r, r)) + 1e-12))
    if clip.kind == "pocket":
        return ["mix"] if corr > 0.95 else ["c0", "c1"]
    if corr > 0.9:
        # main + safety (-6 dB) track: take the louder unless it clips
        clip_l = np.mean(np.abs(l) > 0.999)
        clip_r = np.mean(np.abs(r) > 0.999)
        if rl >= rr:
            return ["c0"] if clip_l < 1e-4 else ["c1"]
        return ["c1"] if clip_r < 1e-4 else ["c0"]
    return ["c0", "c1"]


def assign_lanes(mic_clips):
    """Return list of sources: dict(clip, chan, lane)."""
    sources = []
    lanes_end = {}
    lanes_dir = {}
    counter = [0]
    mic_sources = []
    for c in mic_clips:
        plan = channel_plan(c)
        c.diag["channels"] = plan
        for chan in plan:
            if c.kind == "pocket":
                sources.append(dict(clip=c, chan=chan, lane="pocket_" + chan))
            else:
                mic_sources.append(dict(clip=c, chan=chan))
    mic_sources.sort(key=lambda s: s["clip"].audio_span()[0])
    for s in mic_sources:
        start, end = s["clip"].audio_span()
        folder = os.path.dirname(s["clip"].rel) + "|" + s["chan"]
        free = [ln for ln, e in lanes_end.items() if e <= start + 0.5]
        if free:
            same = [ln for ln in free if lanes_dir.get(ln) == folder]
            lane = (same or sorted(free, key=lambda ln: lanes_end[ln]))[0]
        else:
            counter[0] += 1
            lane = "mic%d" % counter[0]
        lanes_end[lane] = end
        lanes_dir[lane] = folder
        s["lane"] = lane
        sources.append(s)
    return sources


# ---------------------------------------------------------------- lanes ---

def _decode_into(src, lane_mm, R0, n_total):
    c, chan = src["clip"], src["chan"]
    sr_in = c.info.sample_rate
    nch = max(1, c.info.channels)
    local_from = (R0 - c.a) / c.r - 2.0
    args = []
    lt0 = c.audio_offset
    if local_from > c.audio_offset + 1.0:
        lt0 = local_from
        args += ["-ss", "%.6f" % lt0]
    need = (n_total / SR) / c.r + 6.0
    args += ["-t", "%.3f" % need, "-i", c.path, "-map", "0:a:0", "-vn", "-f", "f32le", "-acodec", "pcm_f32le", "-"]
    p = ff.popen_raw(args)
    rs = soxr.ResampleStream(float(sr_in), SR * c.r, 1, dtype="float32", quality="VHQ")
    k = int(round((c.a + c.r * lt0 - R0) * SR))
    block = sr_in * nch * 4 * 2
    placed = 0
    while True:
        if events.cancelled():
            break
        buf = p.stdout.read(block)
        last = len(buf) < block
        x = np.frombuffer(buf, dtype=np.float32)
        x = x[: len(x) // nch * nch].reshape(-1, nch)
        if chan == "mix":
            mono = x.mean(axis=1)
        else:
            mono = x[:, min(int(chan[1:]), nch - 1)]
        y = rs.resample_chunk(np.ascontiguousarray(mono, dtype=np.float32), last=last)
        if len(y):
            a, b = max(k, 0), min(k + len(y), n_total)
            if b > a:
                lane_mm[a:b] = y[a - k:b - k]
                placed += b - a
            k += len(y)
        if last or k >= n_total:
            break
    p.stdout.close()
    p.kill()
    p.wait()
    ff.release(p)
    events.check()
    return placed


def build_lanes(sources, R0, R1, work):
    n_total = int(round((R1 - R0) * SR))
    lane_names = sorted({s["lane"] for s in sources})
    raw = {}
    n_src = max(1, len(sources))
    done = 0
    for ln in lane_names:
        path = os.path.join(work, "lane_%s.raw" % ln)
        mm = np.memmap(path, dtype=np.float32, mode="w+", shape=(n_total,))
        mm[:] = 0
        for s in [s for s in sources if s["lane"] == ln]:
            s0, s1 = s["clip"].audio_span()
            if s1 <= R0 or s0 >= R1:
                continue
            log("  마이크 트랙 배치: %s (%s)" % (s["clip"].rel, s["chan"]))
            done += 1
            events.progress(0.45 * done / n_src)
            if _decode_into(s, mm, R0, n_total) == 0:
                log("  ! 이 파일의 소리를 읽지 못했습니다: %s" % s["clip"].rel)
        mm.flush()
        del mm
        raw[ln] = path
    return raw, n_total


_LATENCY = {}


def filter_latency(chain, work):
    """Samples of delay a filter chain adds (afftdn delays by its FFT window, ~25 ms)."""
    if chain in _LATENCY:
        return _LATENCY[chain]
    x = np.zeros(SR * 4, np.float32)
    x[SR * 2] = 0.5
    src = os.path.join(work, "impulse.raw")
    dst = os.path.join(work, "impulse_out.raw")
    x.tofile(src)
    ff.run(["-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", src, "-af", chain, "-f", "f32le", dst])
    y = np.fromfile(dst, np.float32)
    lat = int(np.argmax(np.abs(y))) - SR * 2
    _LATENCY[chain] = max(0, lat) if abs(lat) < SR else 0
    for p in (src, dst):
        os.remove(p)
    return _LATENCY[chain]


def clean_lane(raw_path, out_path, denoise_db, work):
    chain = "highpass=f=80:poles=2"
    if denoise_db > 0:
        chain += ",afftdn=nr=%g:nf=-55:tn=1" % denoise_db
    lat = filter_latency(chain, work)
    if lat:
        # pad the end, cut the same amount from the start: output stays sample aligned
        chain = "apad=pad_len=%d,%s,atrim=start_sample=%d" % (lat, chain, lat)
    ff.run(["-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", raw_path, "-af", chain,
            "-f", "f32le", "-acodec", "pcm_f32le", out_path])


# -------------------------------------------------------------- automix ---

def _ctrl_power(mm):
    n = len(mm) // CTRL
    out = np.empty(n, np.float64)
    step = CTRL * 48000
    for i in range(0, n * CTRL, step):
        seg = np.asarray(mm[i:min(i + step, n * CTRL)], dtype=np.float64)
        out[i // CTRL: i // CTRL + len(seg) // CTRL] = (seg.reshape(-1, CTRL) ** 2).mean(axis=1)
    return out


def _segments(sources, lane, R0, n_ctrl):
    segs = []
    for s in sources:
        if s["lane"] != lane:
            continue
        a, b = s["clip"].audio_span()
        i0 = max(0, int((a - R0) * 1000))
        i1 = min(n_ctrl, int((b - R0) * 1000))
        if i1 > i0:
            segs.append((i0, i1, s))
    return segs


def automix(clean, sources, R0, n_total, mix_path, stem_dir, enabled=True):
    lanes = sorted(clean)
    mms = {ln: np.memmap(clean[ln], dtype=np.float32, mode="r") for ln in lanes}
    log("  사람별 목소리 크기 측정...")
    P = {ln: _ctrl_power(mms[ln]) for ln in lanes}
    n_ctrl = min(len(v) for v in P.values())
    for ln in lanes:
        P[ln] = P[ln][:n_ctrl]
    n20 = n_ctrl // 20
    L = {ln: 10 * np.log10(P[ln][: n20 * 20].reshape(-1, 20).mean(axis=1) + 1e-12) for ln in lanes}
    gains = {ln: np.zeros(n_ctrl) for ln in lanes}
    report = []
    for ln in lanes:
        others = [L[o] for o in lanes if o != ln]
        other_max = np.max(others, axis=0) if others else np.full(n20, -120.0)
        for i0, i1, s in _segments(sources, ln, R0, n_ctrl):
            f0, f1 = i0 // 20, max(i0 // 20 + 1, i1 // 20)
            li = L[ln][f0:f1]
            if len(li) == 0:
                continue
            floor = np.percentile(li, 10)
            dom = (li - other_max[f0:f1] >= 6) & (li > floor + 15)
            if dom.sum() >= 150:
                level = float(np.median(li[dom]))
                g_db = float(np.clip(TARGET_SPEECH_DB - level, -20, 36))
                how = "주 화자 구간 %.0f초" % (dom.sum() * 0.02)
            else:
                act = li[li > floor + 10]
                level = float(np.percentile(act, 90)) if len(act) else TARGET_SPEECH_DB
                g_db = float(np.clip(TARGET_SPEECH_DB - level, -12, 12))
                how = "말한 구간이 짧아 보수적으로 맞춤"
            gains[ln][i0:i1] = 10 ** (g_db / 20)
            report.append((ln, s["clip"].rel, s["chan"], g_db, how))
    log("  오토믹싱(말하는 사람 마이크 위주로 열기)...")
    alpha = 1 - np.exp(-1.0 / 10.0)                    # 10 ms detector
    D = {}
    for ln in lanes:
        D[ln] = lfilter([alpha], [1, alpha - 1], P[ln] * gains[ln] ** 2)
    total = sum(D.values()) + 1e-11
    beta = 1 - np.exp(-1.0 / 20.0)
    A = {}
    for ln in lanes:
        share = np.sqrt(D[ln] / total)
        share = np.maximum(share, 10 ** (-30 / 20))
        share = maximum_filter1d(share, size=121, mode="nearest")      # +-60 ms look-ahead / hold
        share = filtfilt([beta], [1, beta - 1], share)
        A[ln] = (np.clip(share, 0, 1) if enabled else 1.0) * gains[ln]
    os.makedirs(stem_dir, exist_ok=True)
    mix = np.memmap(mix_path, dtype=np.float32, mode="w+", shape=(n_total,))
    stems = {ln: sf.SoundFile(os.path.join(stem_dir, "%s.wav" % ln), "w", SR, 1, "PCM_24",
                              format="RF64" if n_total > 2 ** 31 // 3 else "WAV") for ln in lanes}
    block = SR * 10
    ctrl_t = (np.arange(n_ctrl) + 0.5) * CTRL
    for i in range(0, n_total, block):
        j = min(i + block, n_total)
        t = np.arange(i, j)
        acc = np.zeros(j - i)
        for ln in lanes:
            x = np.asarray(mms[ln][i:j], dtype=np.float64)
            if len(x) < j - i:
                x = np.pad(x, (0, j - i - len(x)))
            g = np.interp(t, ctrl_t, A[ln])
            y = x * g
            stems[ln].write(np.clip(y, -1, 1).astype(np.float32))
            acc += y
        mix[i:j] = acc
    mix.flush()
    for f in stems.values():
        f.close()
    del mix
    return report


# --------------------------------------------------------------- master ---

def _loudnorm_json(text):
    blocks = re.findall(r"\{[^{}]*\"input_i\"[^{}]*\}", text, re.S)
    if not blocks:
        raise ff.FFError("loudnorm 측정값을 읽지 못했습니다")
    return json.loads(blocks[-1])


def master(mix_path, out_wav, lufs, true_peak=-1.5):
    comp = "acompressor=threshold=-26dB:ratio=2:attack=10:release=250:makeup=1"
    base = ["-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", mix_path]
    text = ff.run_stderr(base + ["-af", comp + ",loudnorm=I=%g:TP=%g:LRA=11:print_format=json" % (lufs, true_peak),
                                 "-f", "null", "-"])
    m = _loudnorm_json(text)
    ln = ("loudnorm=I=%g:TP=%g:LRA=11:measured_I=%s:measured_TP=%s:measured_LRA=%s:"
          "measured_thresh=%s:offset=%s:linear=true" %
          (lufs, true_peak, m["input_i"], m["input_tp"], m["input_lra"], m["input_thresh"], m["target_offset"]))
    ff.run(base + ["-af", comp + "," + ln + ",aresample=%d" % SR, "-ac", "2", "-c:a", "pcm_s24le", out_wav])
    return m


def build_programme_audio(mic_clips, R0, R1, work, out_wav, stem_dir, denoise_db=10.0, lufs=-14.0, use_automix=True):
    log("[3/5] 마이크 소리만으로 오디오 만드는 중...")
    sources = assign_lanes(mic_clips)
    raw, n_total = build_lanes(sources, R0, R1, work)
    clean = {}
    for i, (ln, path) in enumerate(raw.items(), 1):
        events.progress(0.45 + 0.3 * i / max(1, len(raw)))
        log("  잡음 정리: %s" % ln)
        out = path.replace(".raw", "_clean.raw")
        clean_lane(path, out, denoise_db, work)
        clean[ln] = out
        os.remove(path)
    mix_path = os.path.join(work, "mix.raw")
    events.progress(0.8)
    report = automix(clean, sources, R0, n_total, mix_path, stem_dir, enabled=use_automix)
    events.progress(0.9)
    log("  음량 마감 (%.0f LUFS)..." % lufs)
    meas = master(mix_path, out_wav, lufs)
    for p in list(clean.values()) + [mix_path]:
        try:
            os.remove(p)
        except OSError:
            pass
    return dict(levels=report, loudness_in=meas, lanes=sorted(clean), n_samples=n_total, sources=sources)
