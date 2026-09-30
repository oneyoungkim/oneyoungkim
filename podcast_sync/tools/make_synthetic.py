"""Generate a fake 4-person podcast shoot that mimics the real one, with known truth.

  * 4 speakers (espeak-ng Korean voices) talking in turns, some overlap
  * DJI Mic 3 TX1 / TX2 internal WAVs (TX1 split into 2 files, TX2 has a -6 dB
    safety channel), Pocket with Mic Mini 2 stereo (L = speaker C, R = speaker D)
  * Leica (2 clips, 29.97p), Pocket (30p, flat/cool "log-ish" look),
    Fuji (25p, warm/dark look); camera mics hear the room from ~2.5 m
  * every device has its own clock drift; video frames carry a binary barcode of
    the true master time (ms) so the final render can be checked frame by frame

usage: python make_synthetic.py OUT_DIR [--seconds 300]
"""
import argparse
import json
import os
import random
import subprocess
import tempfile

import numpy as np
import soundfile as sf
import soxr
from PIL import Image, ImageDraw
from scipy.signal import butter, fftconvolve, sosfilt

SR = 48000
C_SOUND = 343.0
BITS = 20

SENTENCES = [
    "오늘은 요즘 가장 뜨거운 이야기를 해 보겠습니다", "그 부분은 저도 정말 궁금했어요", "자료를 찾아보니까 생각보다 복잡하더라고요",
    "잠깐만요 그건 조금 다르게 봐야 할 것 같아요", "맞아요 저도 같은 생각입니다", "구독자분들이 이 질문을 제일 많이 하셨어요",
    "처음에는 그냥 소문인 줄 알았거든요", "그런데 기록을 보면 날짜가 딱 맞아요", "이건 확인된 사실이고 나머지는 추측입니다",
    "제가 현장에 직접 가 봤는데요", "그 사람 인터뷰가 결정적이었죠", "근데 왜 아무도 그 얘기를 안 했을까요",
    "숫자로 보면 훨씬 명확해집니다", "여기서 반전이 하나 있어요", "다음 주에 이어서 더 깊게 파 보겠습니다",
    "아 그거 진짜 웃기네요", "정리하면 세 가지로 볼 수 있어요", "첫 번째는 시간 순서입니다", "두 번째는 돈의 흐름이고요",
    "세 번째가 제일 중요한데요", "댓글로 여러분 생각도 남겨 주세요", "저는 솔직히 반대 의견이에요",
]
BACKCHANNEL = ["네", "맞아요", "음", "그렇죠", "와", "아하"]
VOICES = [("ko+m3", 35, 165), ("ko+m1", 55, 175), ("ko+f2", 60, 160), ("ko+f4", 75, 170)]


def espeak(text, voice, pitch, speed, tmp):
    path = os.path.join(tmp, "e.wav")
    subprocess.run(["espeak-ng", "-v", voice, "-p", str(pitch), "-s", str(speed), "-w", path, text], check=True)
    x, sr = sf.read(path, dtype="float32")
    return soxr.resample(x, sr, SR).astype(np.float64)


def speech_tracks(seconds, seed, tmp):
    rng = random.Random(seed)
    n = int((seconds + 20) * SR)
    tracks = np.zeros((4, n))
    t = 1.0
    last = -1
    while t < seconds + 10:
        spk = rng.choice([s for s in range(4) if s != last])
        last = spk
        voice, pitch, speed = VOICES[spk]
        parts = []
        for _ in range(rng.randint(1, 3)):
            parts.append(espeak(rng.choice(SENTENCES), voice, pitch + rng.randint(-5, 5), speed + rng.randint(-10, 10), tmp))
            parts.append(np.zeros(int(SR * rng.uniform(0.1, 0.3))))
        utt = np.concatenate(parts) * rng.uniform(0.6, 1.0)
        i0 = int(t * SR)
        i1 = min(n, i0 + len(utt))
        tracks[spk, i0:i1] += utt[: i1 - i0]
        if rng.random() < 0.3:
            other = rng.choice([s for s in range(4) if s != spk])
            v, p, s_ = VOICES[other]
            bc = espeak(rng.choice(BACKCHANNEL), v, p, s_, tmp) * 0.7
            j0 = i0 + int(len(utt) * rng.uniform(0.4, 0.9))
            j1 = min(n, j0 + len(bc))
            tracks[other, j0:j1] += bc[: j1 - j0]
        t += len(utt) / SR + rng.uniform(0.15, 0.9)
    # master time 0 = sample 10 s into these buffers (so devices can start before 0)
    return tracks, 10.0


def delay(x, sec):
    d = sec * SR
    n = int(np.floor(d))
    frac = d - n
    y = np.zeros_like(x)
    if n < len(x):
        y[n:] = x[: len(x) - n]
    if frac > 1e-6:
        y2 = np.zeros_like(y)
        y2[1:] = y[:-1]
        y = (1 - frac) * y + frac * y2
    return y


def room_ir(rng, rt60=0.35):
    n = int(rt60 * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / rt60)
    ir[: int(0.002 * SR)] = 0
    return ir / np.sqrt(np.sum(ir ** 2)) * 0.35


def lowpass(x, fc):
    return sosfilt(butter(2, fc, fs=SR, output="sos"), x)


def build_signals(tracks, seed):
    rng = np.random.default_rng(seed)
    ir = room_ir(rng)
    # positions (m) of 4 people around a table and 3 cameras
    people = np.array([[0.0, 0.0], [1.2, 0.0], [1.2, 1.1], [0.0, 1.1]])
    mouths = people
    lav = people + np.array([0.0, 0.18])
    cams = {"leica": np.array([0.6, -2.3]), "pocket": np.array([-1.6, 0.4]), "fuji": np.array([2.8, 0.7])}
    reverb = [fftconvolve(tr, ir)[: tracks.shape[1]] for tr in tracks]
    lavs = []
    for i in range(4):
        acc = np.zeros(tracks.shape[1])
        for j in range(4):
            dist = np.linalg.norm(mouths[j] - lav[i])
            if i == j:
                acc += delay(tracks[j], dist / C_SOUND)
            else:
                g = 10 ** (rng.uniform(-19, -14) / 20)
                acc += g * lowpass(delay(tracks[j], dist / C_SOUND), 6000) + 0.05 * g * reverb[j]
        acc += 10 ** (-60 / 20) * rng.standard_normal(len(acc))
        lavs.append(acc)
    scratch = {}
    for name, pos in cams.items():
        acc = np.zeros(tracks.shape[1])
        for j in range(4):
            dist = np.linalg.norm(mouths[j] - pos)
            acc += (0.5 / dist) * delay(tracks[j], dist / C_SOUND) + 0.4 * reverb[j]
        acc = sosfilt(butter(2, [120, 9000], btype="band", fs=SR, output="sos"), acc)
        acc += 10 ** (-48 / 20) * rng.standard_normal(len(acc))
        scratch[name] = acc * 0.5
    return lavs, scratch


def device_audio(sig, t0_buf, start, end, eps):
    """Record master-time [start, end] with a device clock running (1+eps) fast."""
    i0 = int(round((start + t0_buf) * SR))
    i1 = int(round((end + t0_buf) * SR))
    seg = sig[i0:i1]
    return soxr.resample(seg.astype(np.float32), SR, SR * (1 + eps), quality="VHQ")


_SCENE = None


def scene_image():
    """One 2560x1440 'studio' shared by all cameras (fixed seed), with enough texture
    (books, posters, wood grain, plants, faces) for feature matching like real footage.
    Sample points used by verify_synthetic.py (face centres, shirt centres, wall at
    (1240, 580)) are kept on plain colour."""
    global _SCENE
    if _SCENE is not None:
        return _SCENE
    rng = np.random.default_rng(11)
    W0, H0 = 2560, 1440
    y = np.linspace(0, 1, H0)[:, None, None]
    wall = np.array([196.0, 178.0, 150.0]) - 20 * y + np.zeros((H0, W0, 3))
    tex = rng.standard_normal((H0 // 8, W0 // 8))
    tex = np.kron(tex, np.ones((8, 8)))[:H0, :W0]
    wall += 2.5 * tex[..., None]
    img = Image.fromarray(np.clip(wall, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.rectangle([1700, 150, 2400, 650], fill=(235, 240, 245))                     # window
    for xx in (1933, 2166):
        d.line([(xx, 150), (xx, 650)], fill=(90, 90, 95), width=10)
    d.line([(1700, 400), (2400, 400)], fill=(90, 90, 95), width=10)
    d.rectangle([1150, 110, 1640, 390], fill=(95, 70, 50))                        # bookshelf
    x = 1165
    while x < 1620:
        w = int(rng.integers(12, 34))
        h = int(rng.integers(150, 250))
        col = tuple(int(c) for c in rng.integers(30, 230, 3))
        d.rectangle([x, 380 - h, x + w, 380], fill=col)
        d.line([(x + 3, 380 - h + 20), (x + w - 3, 380 - h + 20)], fill=(240, 230, 200), width=3)
        x += w + 3
    d.rectangle([300, 200, 700, 380], fill=(200, 160, 40))                         # poster
    d.text((330, 230), "THE PODCAST", fill=(40, 30, 20))
    d.text((330, 300), "SEASON 1", fill=(120, 20, 20))
    for k in range(6):
        d.ellipse([560 + k * 20, 250 + (k % 2) * 40, 600 + k * 20, 290 + (k % 2) * 40], outline=(60, 40, 10), width=4)
    d.rectangle([780, 150, 1060, 360], fill=(250, 250, 245))                       # abstract painting
    for _ in range(14):
        x0, y0 = int(rng.integers(790, 1000)), int(rng.integers(160, 320))
        d.rectangle([x0, y0, x0 + int(rng.integers(20, 60)), y0 + int(rng.integers(15, 40))],
                    fill=tuple(int(c) for c in rng.integers(20, 240, 3)))
    d.rectangle([150, 900, 950, 1300], fill=(40, 70, 140))                         # sofa + cushions
    d.rectangle([200, 860, 380, 1000], fill=(200, 120, 60))
    d.rectangle([700, 860, 880, 1000], fill=(230, 210, 120))
    d.rectangle([900, 1000, 1900, 1150], fill=(120, 80, 45))                       # table, wood grain
    for k in range(40):
        yy = 1005 + k * 3.6
        d.line([(900, yy), (1900, yy + rng.uniform(-4, 4))], fill=(100 + int(rng.integers(-15, 15)), 66, 36), width=1)
    for _ in range(60):                                                            # plant leaves
        cx, cy = rng.uniform(2230, 2430), rng.uniform(820, 1280)
        d.ellipse([cx - 28, cy - 12, cx + 28, cy + 12], fill=(30 + int(rng.integers(0, 40)), 110 + int(rng.integers(0, 50)), 40))
    shirts = [(180, 40, 40), (230, 230, 225), (30, 30, 35), (90, 140, 200)]
    skins = [(224, 172, 140), (228, 180, 150), (218, 165, 132), (226, 176, 146)]
    hair = [(35, 25, 20), (60, 40, 25), (20, 20, 22), (150, 110, 60)]
    for k, x in enumerate([500, 1000, 1500, 2000]):
        d.ellipse([x - 90, 420, x + 90, 640], fill=skins[k])                      # face
        d.chord([x - 95, 395, x + 95, 520], 180, 360, fill=hair[k])               # hair
        d.ellipse([x - 45, 495, x - 25, 510], fill=(40, 30, 30))                  # eyes
        d.ellipse([x + 25, 495, x + 45, 510], fill=(40, 30, 30))
        d.arc([x - 35, 565, x + 35, 605], 20, 160, fill=(150, 60, 60), width=5)   # mouth
        d.rectangle([x - 160, 640, x + 160, 1000], fill=shirts[k])               # shirt
        d.rectangle([x + 60, 700, x + 120, 760], outline=(200, 190, 60), width=4) # pocket / logo
        d.rectangle([x - 20, 900, x + 20, 1000], fill=(50, 50, 55))               # desk mic
        d.ellipse([x - 32, 850, x + 32, 915], fill=(70, 70, 75))
    _SCENE = img
    return img


def make_scene(path, crop, look_seed):
    x0, y0, x1, y1 = crop
    scene_image().crop((x0, y0, x1, y1)).resize((1280, 720), Image.LANCZOS).save(path)


def barcode_filters(start, eps):
    """drawbox filters encoding floor(1000 * master_time) in BITS white boxes + 2 reference boxes."""
    m = "floor(1000*(%.6f+t/(1+%.9f)))" % (start, eps)
    fl = ["drawbox=x=0:y=640:w=1280:h=80:color=black:t=fill",
          "drawbox=x=16:y=660:w=40:h=40:color=white:t=fill"]            # reference white
    for b in range(BITS):
        x = 80 + b * 56
        fl.append("drawbox=x=%d:y=660:w=40:h=40:color=white:t=fill:enable='eq(mod(floor(%s/%d),2),1)'" % (x, m, 2 ** b))
    return fl


LOOKS = {
    "leica": "null",
    "pocket": "eq=contrast=0.70:brightness=0.07:saturation=0.55,colorbalance=rs=-0.05:bs=0.08:bm=0.06",
    "fuji": "eq=brightness=-0.05:saturation=1.2:gamma=0.9,colorbalance=rs=0.08:rm=0.07:bm=-0.07:bh=-0.05",
}


def write_video(path, scene, fps, start, eps, dur_local, audio_path, audio_codec, look):
    vf = ",".join(barcode_filters(start, eps) + [LOOKS[look], "format=yuv420p"])
    cmd = ["ffmpeg", "-hide_banner", "-v", "error", "-y", "-loop", "1", "-framerate", fps, "-i", scene,
           "-i", audio_path, "-t", "%.6f" % dur_local, "-vf", vf, "-c:v", "libx264", "-preset", "ultrafast",
           "-crf", "18", "-g", "30", "-bf", "2", "-colorspace", "bt709", "-color_primaries", "bt709",
           "-color_trc", "bt709", "-c:a", audio_codec]
    if audio_codec == "aac":
        cmd += ["-b:a", "192k"]
    cmd += ["-shortest", path]
    subprocess.run(cmd, check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--seconds", type=float, default=300)
    ap.add_argument("--seed", type=int, default=3)
    a = ap.parse_args()
    T = a.seconds
    os.makedirs(a.out, exist_ok=True)
    tmp = tempfile.mkdtemp()
    print("speech...")
    tracks, t0 = speech_tracks(T, a.seed, tmp)
    print("room...")
    lavs, scratch = build_signals(tracks, a.seed)
    truth = dict(files={}, t0_buf=t0, seconds=T)
    ref = tracks.sum(axis=0)
    sf.write(os.path.join(a.out, "..", "truth_speech.wav"),
             ref[int(t0 * SR): int((t0 + T + 5) * SR)].astype(np.float32), SR, subtype="FLOAT")

    def rec(name, rel, sig, start, end, eps, **kw):
        truth["files"][rel] = dict(start=start, eps=eps, device=name, **kw)
        return device_audio(sig, t0, start, end, eps)

    # --- DJI Mic 3 TX1 (speaker A), split in two files, +80 ppm
    os.makedirs(os.path.join(a.out, "마이크", "TX1"), exist_ok=True)
    x = device_audio(lavs[0], t0, -3.0, T + 10, 80e-6)
    cut = int(153.0 * SR)
    sf.write(os.path.join(a.out, "마이크", "TX1", "DJI_01_20260928_140001.WAV"), x[:cut].astype(np.float32), SR, subtype="FLOAT")
    sf.write(os.path.join(a.out, "마이크", "TX1", "DJI_01_20260928_143001.WAV"), x[cut:].astype(np.float32), SR, subtype="FLOAT")
    truth["files"]["마이크/TX1/DJI_01_20260928_140001.WAV"] = dict(start=-3.0, eps=80e-6, device="tx1")
    truth["files"]["마이크/TX1/DJI_01_20260928_143001.WAV"] = dict(start=-3.0 + cut / SR / (1 + 80e-6), eps=80e-6, device="tx1")
    # --- DJI Mic 3 TX2 (speaker B), stereo with -6 dB safety, -40 ppm
    os.makedirs(os.path.join(a.out, "마이크", "TX2"), exist_ok=True)
    x = rec("tx2", "마이크/TX2/DJI_02_20260928_140003.WAV", lavs[1], -1.0, T + 8, -40e-6)
    sf.write(os.path.join(a.out, "마이크", "TX2", "DJI_02_20260928_140003.WAV"),
             np.stack([x, x * 0.5], 1).astype(np.float32), SR, subtype="FLOAT")

    # --- scenes
    scenes = {}
    for name, crop in (("leica", (0, 0, 2560, 1440)), ("pocket", (100, 150, 1380, 870)), ("fuji", (1180, 180, 2460, 900))):
        scenes[name] = os.path.join(tmp, "scene_%s.png" % name)
        make_scene(scenes[name], crop, name)

    # --- Pocket (Mic Mini 2 stereo: L = C, R = D), 30p, +35 ppm
    os.makedirs(os.path.join(a.out, "포켓"), exist_ok=True)
    start, end, eps = 2.0, T - 3.0, 35e-6
    xl = device_audio(lavs[2], t0, start, end, eps)
    xr = device_audio(lavs[3], t0, start, end, eps)
    wav = os.path.join(tmp, "pocket.wav")
    sf.write(wav, np.stack([xl, xr], 1).astype(np.float32), SR, subtype="FLOAT")
    rel = "포켓/DJI_20260928140002_0001_D.MP4"
    truth["files"][rel] = dict(start=start, eps=eps, device="pocket")
    print("video pocket...")
    write_video(os.path.join(a.out, rel), scenes["pocket"], "30", start, eps, len(xl) / SR, wav, "aac", "pocket")

    # --- Leica: two clips (gap), 29.97p, +12 ppm
    os.makedirs(os.path.join(a.out, "라이카"), exist_ok=True)
    for k, (start, end) in enumerate(((5.0, 160.0), (163.0, T))):
        eps = 12e-6
        x = device_audio(scratch["leica"], t0, start, end, eps)
        wav = os.path.join(tmp, "leica%d.wav" % k)
        sf.write(wav, np.stack([x, x], 1).astype(np.float32), SR, subtype="FLOAT")
        rel = "라이카/L100%04d.MP4" % (1230 + k)
        truth["files"][rel] = dict(start=start, eps=eps, device="leica")
        print("video", rel)
        write_video(os.path.join(a.out, rel), scenes["leica"], "30000/1001", start, eps, len(x) / SR, wav, "aac", "leica")

    # --- Fuji: 25p, -60 ppm, PCM in MOV, flat folder (named by file prefix only)
    start, end, eps = 20.0, T + 5, -60e-6
    x = device_audio(scratch["fuji"], t0, start, end, eps)
    wav = os.path.join(tmp, "fuji.wav")
    sf.write(wav, np.stack([x, x], 1).astype(np.float32), SR, subtype="FLOAT")
    rel = "DSCF0456.MOV"
    truth["files"][rel] = dict(start=start, eps=eps, device="fuji")
    print("video fuji...")
    write_video(os.path.join(a.out, rel), scenes["fuji"], "25", start, eps, len(x) / SR, wav, "pcm_s16le", "fuji")

    with open(os.path.join(a.out, "..", "truth.json"), "w", encoding="utf-8") as fh:
        json.dump(truth, fh, ensure_ascii=False, indent=1)
    print("done")


if __name__ == "__main__":
    main()
