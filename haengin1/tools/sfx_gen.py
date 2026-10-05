# 전투 효과음 자리(합성음) — docs/08_M2_전투_설계.md 5-7 (2026-10-06)
# 3D 시안 페이지 Sound(concept/src/05_fx.js)의 WebAudio 레시피를 오프라인으로 굽는다: 사인(지수 주파수 미끄러짐) + 잡음 → 쌍2차 필터(RBJ) → 지수 감쇠.
# 잡음은 시드 고정(같은 파일이 나온다). 44.1kHz 16bit 모노 WAV.
# 사용: python tools/sfx_gen.py <출력 폴더>
import math, os, sys, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(2024)


def env(n, gain, dur):
    # WebAudio exponentialRampToValueAtTime(.001, dur): gain → 0.001
    t = np.arange(n) / SR
    k = math.log(0.001 / max(gain, 1e-6)) / max(dur, 1e-4)
    return gain * np.exp(k * t)


def tone(dur, f0, f1, gain, kind='sine'):
    n = int((dur + 0.02) * SR)
    t = np.arange(n) / SR
    tt = np.minimum(t, dur)
    # 지수 주파수: f(t) = f0 * (f1/f0)^(t/dur) → 위상 = 적분
    r = math.log(f1 / f0) / dur
    ph = 2 * math.pi * f0 * (np.exp(r * tt) - 1) / r
    ph = ph + 2 * math.pi * f1 * np.maximum(t - dur, 0)
    if kind == 'sine':
        w = np.sin(ph)
    elif kind == 'sawtooth':
        w = 2 * ((ph / (2 * math.pi)) % 1.0) - 1
    else:   # triangle
        w = 2 * np.abs(2 * ((ph / (2 * math.pi)) % 1.0) - 1) - 1
    e = env(n, gain, dur)
    e[t > dur] = 0.0
    return w * e


def biquad(x, f, q, kind, f2=None, dur=None):
    """RBJ 쌍2차. f2 가 있으면 주파수를 dur 동안 지수로 바꾼다(조각마다 계수 다시)"""
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    blk = 64
    for s in range(0, len(x), blk):
        t = s / SR
        fc = f if f2 is None else f * (f2 / f) ** min(1.0, t / dur)
        w0 = 2 * math.pi * min(fc, SR * 0.45) / SR
        alpha = math.sin(w0) / (2 * q)
        cw = math.cos(w0)
        if kind == 'bandpass':     # 일정 피크 이득 0dB(WebAudio bandpass)
            b0, b1, b2 = alpha, 0.0, -alpha
        else:                      # lowpass
            b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
        b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
        for i in range(s, min(s + blk, len(x))):
            xi = x[i]
            yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
            x2, x1, y2, y1 = x1, xi, y1, yi
            y[i] = yi
    return y


def noise(dur, f, q, gain, kind='bandpass', f2=None):
    n = int((dur + 0.02) * SR)
    x = rng.uniform(-1, 1, n)
    y = biquad(x, f, q, kind, f2, dur)
    e = env(n, gain, dur)
    e[np.arange(n) / SR > dur] = 0.0
    return y * e


def mix(*parts, offsets=None):
    offsets = offsets or [0.0] * len(parts)
    n = max(int(o * SR) + len(p) for p, o in zip(parts, offsets))
    out = np.zeros(n)
    for p, o in zip(parts, offsets):
        i = int(o * SR)
        out[i:i + len(p)] += p
    return out


def save(path, x, peak=0.89):
    m = np.max(np.abs(x)) if len(x) else 1.0
    if m > peak: x = x * (peak / m)          # 넘치면 줄임(작으면 그대로 — 소리 사이 크기 비율 유지)
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main(out):
    os.makedirs(out, exist_ok=True)
    made = []
    for p in (1, 2, 3, 4):     # 4 = 기세(강보다 한 단계 — 5-1)
        k = .45 + p * .2
        x = mix(tone(.16 + p * .04, 170, 42, .9 * k),          # 쿵(저음 사인)
                noise(.06, 1900, .9, .7 * k),                  # 탁(고음 띠 잡음)
                noise(.12, 400, .7, .5 * k, 'lowpass'))        # 저역 잡음
        name = f'hit_p{p}.wav'; save(os.path.join(out, name), x); made.append(name)
    save(os.path.join(out, 'whoosh.wav'), noise(.14, 700, 1.2, .18, 'bandpass', 2600)); made.append('whoosh.wav')
    save(os.path.join(out, 'slam.wav'), mix(tone(.55, 95, 28, 1.1), noise(.4, 380, .6, .8, 'lowpass'), noise(.08, 2400, .8, .5))); made.append('slam.wav')
    save(os.path.join(out, 'heat.wav'), mix(tone(.35, 220, 880, .12, 'sawtooth'), noise(.3, 1200, .8, .12, 'bandpass', 5000))); made.append('heat.wav')
    save(os.path.join(out, 'block.wav'), mix(noise(.05, 900, 1.0, .5), tone(.06, 300, 200, .4))); made.append('block.wav')
    save(os.path.join(out, 'dodge.wav'), noise(.14, 700, 1.2, .18 * .6, 'bandpass', 2600)); made.append('dodge.wav')
    print('ok', made)


if __name__ == '__main__':
    main(sys.argv[1])
