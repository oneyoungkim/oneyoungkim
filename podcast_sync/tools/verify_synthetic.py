"""Score a podsync run on the synthetic shoot against the known truth.

usage: python verify_synthetic.py SYNTH_DIR      (the folder holding truth.json and 딥마카이/)
"""
import json
import os
import subprocess
import sys

import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from podsync import color  # noqa: E402

SR = 48000
BITS = 20


def load(root):
    with open(os.path.join(root, "truth.json"), encoding="utf-8") as fh:
        truth = json.load(fh)
    work = os.path.join(root, "딥마카이", "_편집결과", "_작업파일")
    with open(os.path.join(work, "sync.json"), encoding="utf-8") as fh:
        est = json.load(fh)["clips"]
    with open(os.path.join(work, "last_render.json"), encoding="utf-8") as fh:
        rend = json.load(fh)
    return truth, est, rend


def timeline_fit(truth, est):
    """est_master = A + B * true_master, fitted on microphone-type recordings."""
    xs, ys = [], []
    for rel, t in truth["files"].items():
        if rel not in est or t["device"] not in ("tx1", "tx2", "pocket"):
            continue
        for l in np.linspace(0, 250, 11):
            xs.append(t["start"] + l / (1 + t["eps"]))
            ys.append(est[rel]["a"] + est[rel]["r"] * l)
    B, A = np.polyfit(xs, ys, 1)
    return A, B


def sync_errors(truth, est, A, B):
    print("\n[1] 싱크 정확도 (파일별, 실제 대비 오차)")
    worst = {}
    for rel, t in sorted(truth["files"].items()):
        if rel not in est:
            print("  %-45s 싱크 안 됨!" % rel)
            continue
        errs = []
        for l in np.linspace(0, 140, 15):
            true_m = t["start"] + l / (1 + t["eps"])
            e = est[rel]["a"] + est[rel]["r"] * l - (A + B * true_m)
            errs.append(e * 1000)
        errs = np.array(errs)
        ppm_true = (1 / (1 + t["eps"]) - 1) * 1e6
        ppm_est = (est[rel]["r"] / B - 1) * 1e6
        print("  %-45s 오차 %+7.3f ~ %+7.3f ms   드리프트 실제 %+6.1f / 추정 %+6.1f ppm" % (
            rel, errs.min(), errs.max(), ppm_true, ppm_est))
        worst[rel] = float(np.max(np.abs(errs)))
    return worst


def decode_barcodes(mp4, W, H):
    y0 = int(640 * H / 720)
    strip_h = int(80 * H / 720)
    p = subprocess.run(["ffmpeg", "-v", "error", "-i", mp4, "-vf", "crop=%d:%d:0:%d,format=gray" % (W, strip_h, y0),
                        "-f", "rawvideo", "-"], stdout=subprocess.PIPE, check=True)
    fr = np.frombuffer(p.stdout, dtype=np.uint8).reshape(-1, strip_h, W).astype(float)
    sx = W / 1280.0
    cy = int(40 * H / 720)
    white = fr[:, cy - 5:cy + 5, int(26 * sx):int(46 * sx)].mean(axis=(1, 2))
    black = fr[:, cy - 5:cy + 5, int(1215 * sx):int(1260 * sx)].mean(axis=(1, 2))
    thr = (white + black) / 2
    ms = np.zeros(len(fr), dtype=np.int64)
    for b in range(BITS):
        x = int((80 + b * 56 + 20) * sx)
        v = fr[:, cy - 5:cy + 5, x - 8:x + 8].mean(axis=(1, 2))
        ms += (v > thr).astype(np.int64) << b
    return ms / 1000.0, white - black


def av_sync(rend, A, B):
    print("\n[2] 최종 영상 프레임 싱크 (바코드 판독, 영상이 보여주는 실제 시각 - 소리 시각)")
    fps = eval(rend["fps"])
    t_video, contrast = decode_barcodes(rend["out"], rend["W"], rend["H"])
    n = len(t_video)
    k = np.arange(n)
    est_m = rend["R0"] + k / fps
    true_expected = (est_m - A) / B
    err = (t_video - true_expected) * 1000
    kinds = np.empty(n, dtype=object)
    for s in rend["shots"]:
        kinds[s["f0"]:s["f1"]] = s["kind"]
    ok = contrast > 40
    for kind in sorted(set(kinds[ok])):
        m = ok & (kinds == kind)
        e = err[m]
        print("  %-7s 프레임 %5d개  오차 평균 %+6.1f ms, 범위 %+6.1f ~ %+6.1f ms" % (kind, m.sum(), e.mean(), e.min(), e.max()))
    bad = ok & (np.abs(err) > 25)
    print("  프레임 수 %d (계획 %d), 25ms 넘게 어긋난 프레임 %d개" % (n, sum(s["f1"] - s["f0"] for s in rend["shots"]), bad.sum()))
    if bad.any():
        idx = np.where(bad)[0][:10]
        print("  예:", [(int(i), kinds[i], round(float(err[i]), 1)) for i in idx])
    return err[ok]


def gcc(a, b):
    n = 1 << int(np.ceil(np.log2(len(a) + len(b))))
    R = np.fft.rfft(b, n) * np.conj(np.fft.rfft(a * np.hanning(len(a)), n))
    R /= np.abs(R) + 1e-12
    c = np.fft.irfft(R, n)[: len(b) - len(a) + 1]
    k = int(np.argmax(c))
    if 0 < k < len(c) - 1:
        y0, y1, y2 = c[k - 1:k + 2]
        k = k + 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2)
    return k


def audio_sync(root, rend, A, B, path):
    ref, _ = sf.read(os.path.join(root, "truth_speech.wav"), dtype="float64")
    out, sr = sf.read(path, dtype="float64", always_2d=True)
    out = out.mean(axis=1)
    errs = []
    for t in np.linspace(5, len(out) / SR - 8, 12):
        a = out[int(t * SR): int((t + 3) * SR)]
        if np.sqrt(np.mean(a ** 2)) < 1e-3:
            continue
        true_m = ((rend["R0"] + t) - A) / B
        s = 0.05
        i0 = int((true_m - s) * SR)
        b = ref[i0: i0 + len(a) + int(2 * s * SR)]
        k = gcc(a, b)
        errs.append(((i0 + k) / SR - true_m) * 1000)
    errs = np.array(errs)
    return errs


def colour(root, rend):
    print("\n[4] 색 맞춤 (같은 물체 색 차이 ΔE, 작을수록 비슷 / 3 이하면 눈으로 거의 구분 안 됨)")
    # object positions (x, y) in each camera's 1280x720 frame
    pos = {"leica": {"skin": (250, 265), "wall": (620, 290), "shirt_white": (500, 390), "shirt_blue": (1000, 410),
                     "shirt_black": (750, 410)},
           "pocket": {"skin": (400, 380), "wall": (1140, 430), "shirt_white": (900, 600)},
           "fuji": {"skin": (320, 350), "wall": (60, 400), "shirt_blue": (820, 620), "shirt_black": (320, 620)}}
    src = {"leica": "라이카/L1001230.MP4", "pocket": "포켓/DJI_20260928140002_0001_D.MP4", "fuji": "DSCF0456.MOV"}

    def frame_at(path, t):
        p = subprocess.run(["ffmpeg", "-v", "error", "-ss", str(t), "-i", path, "-frames:v", "1",
                            "-vf", "scale=in_color_matrix=bt709:in_range=tv,format=rgb24", "-f", "rawvideo", "-"],
                           stdout=subprocess.PIPE, check=True)
        return np.frombuffer(p.stdout, np.uint8).reshape(720, 1280, 3) / 255.0

    def lab_at(img, xy):
        x, y = xy
        return color.rgb_to_lab(img[y - 4:y + 5, x - 4:x + 5].reshape(-1, 3)).mean(axis=0)

    fps = eval(rend["fps"])
    before = {k: frame_at(os.path.join(root, "딥마카이", v), 70) for k, v in src.items()}
    after = {}
    for s in rend["shots"]:
        if s["kind"] and s["kind"] not in after and s["f1"] - s["f0"] > 10:
            after[s["kind"]] = frame_at(rend["out"], (s["f0"] + 5) / fps)
    for obj in ("skin", "wall", "shirt_white", "shirt_blue", "shirt_black"):
        ref_b = lab_at(before["leica"], pos["leica"][obj])
        ref_a = lab_at(after["leica"], pos["leica"][obj]) if "leica" in after else ref_b
        line = "  %-11s" % obj
        for k in ("pocket", "fuji"):
            if k not in after or obj not in pos[k]:
                continue
            db = np.linalg.norm(lab_at(before[k], pos[k][obj]) - ref_b)
            da = np.linalg.norm(lab_at(after[k], pos[k][obj]) - ref_a)
            line += "   %s: %5.1f → %5.1f" % (k, db, da)
        print(line)


def main():
    root = sys.argv[1]
    truth, est, rend = load(root)
    A, B = timeline_fit(truth, est)
    sync_errors(truth, est, A, B)
    av_sync(rend, A, B)
    print("\n[3] 오디오 싱크 (최종 오디오 vs 실제 목소리, ms)")
    e = audio_sync(root, rend, A, B, rend["wav"])
    print("  WAV: " + " ".join("%+.2f" % x for x in e))
    tmp = rend["out"] + ".check.wav"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", rend["out"], "-vn", "-ac", "1", tmp], check=True)
    e = audio_sync(root, rend, A, B, tmp)
    os.remove(tmp)
    print("  MP4: " + " ".join("%+.2f" % x for x in e))
    colour(root, rend)


if __name__ == "__main__":
    main()
