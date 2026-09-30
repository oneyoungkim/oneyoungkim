"""Render the cut list: one ffmpeg call per shot (frame exact), then join + mux audio."""
import concurrent.futures as cf
import hashlib
import json
import os
import time
from fractions import Fraction

from . import color, ff


def log(msg):
    print(msg, flush=True)


def pick_encoder(prefer="auto", height=1080):
    """Return (key, codec, args). GPU encoders when present, else x264."""
    options = {
        "nvenc": ("h264_nvenc", ["-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", "18", "-b:v", "0",
                                 "-spatial-aq", "1", "-profile:v", "high"]),
        "qsv": ("h264_qsv", ["-preset", "slow", "-global_quality", "20"]),
        "amf": ("h264_amf", ["-quality", "quality", "-rc", "cqp", "-qp_i", "18", "-qp_p", "20", "-qp_b", "22"]),
        "x264": ("libx264", ["-preset", "faster" if height > 1080 else "medium", "-crf", "17", "-profile:v", "high"]),
    }
    order = [prefer] if prefer in options else ["nvenc", "qsv", "amf", "x264"]
    for key in order:
        codec, args = options[key]
        if key == "x264" or ff.encoder_works(codec):
            return key, codec, args
    return ("x264",) + options["x264"]


def shot_filter(clip, lut, fps, W, H):
    # restart timestamps at 0 so the first (nearest) source frame is output frame 0, not a duplicate
    chain = ["setpts=PTS-STARTPTS", "fps=%s" % fps,
             "scale=%d:%d:force_original_aspect_ratio=increase:flags=lanczos" % (W, H),
             "crop=%d:%d" % (W, H), "setsar=1"]
    matrix, rng, transfer = clip.info.color
    pre = lut.get("pre") if lut else None
    match = lut.get("match") if lut else None
    if pre or match or transfer in ("arib-std-b67", "smpte2084"):
        chain += color.to_rgb_chain(clip.info, pre)
        if match:
            chain.append("lut3d=file=%s:interp=tetrahedral" % match)
        chain.append("scale=out_color_matrix=bt709:out_range=tv")
    else:
        chain.append("scale=in_color_matrix=%s:in_range=%s:out_color_matrix=bt709:out_range=tv" % (matrix, rng))
    chain += ["format=yuv420p", "tpad=stop=-1:stop_mode=clone"]
    return ",".join(chain)


COLOR_TAGS = ["-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv"]


def render_shot(job):
    out = job["out"]
    if os.path.isfile(out) and os.path.getsize(out) > 0 and os.path.isfile(out + ".ok"):
        return job["i"], True
    n = job["n"]
    fps = job["fps"]
    if job["clip"] is None:
        args = ["-f", "lavfi", "-i", "color=black:s=%dx%d:r=%s" % (job["W"], job["H"], fps)]
        vf = "format=yuv420p"
    else:
        args = ["-ss", "%.6f" % job["ss"], "-i", job["clip"].path]
        vf = shot_filter(job["clip"], job["lut"], fps, job["W"], job["H"])
    args += ["-frames:v", str(n), "-an", "-sn", "-dn", "-vf", vf, "-r", str(fps),
             "-c:v", job["codec"]] + job["enc_args"] + ["-g", str(max(12, int(float(fps) * 2))),
                                                       "-pix_fmt", "yuv420p"] + COLOR_TAGS + ["-f", "mpegts", out]
    ff.run(args, cwd=job["cwd"])
    with open(out + ".ok", "w") as fh:
        fh.write(str(n))
    return job["i"], False


def render(shots, P0, fps, W, H, luts, work, out_mp4, audio_wav, encoder="auto", workers=2):
    enc_key, codec, enc_args = pick_encoder(encoder, H)
    log("[4/5] 영상 조각 만드는 중... (인코더: %s, 동시 작업 %d개)" % (enc_key, workers))
    seg_dir = os.path.join(work, "segments")
    os.makedirs(seg_dir, exist_ok=True)
    fps = Fraction(fps)
    tag = hashlib.sha1(json.dumps([W, H, str(fps), codec, enc_args], sort_keys=True).encode()).hexdigest()[:8]
    jobs = []
    for i, s in enumerate(shots):
        n = s["f1"] - s["f0"]
        clip = s["clip"]
        ss = 0.0
        if clip is not None:
            m0 = P0 + s["f0"] / fps
            local = clip.master_to_local(m0)
            half = 0.5 / float(clip.info.fps or fps)
            ss = max(0.0, local - half)
        key = "%s|%s|%.6f|%d" % (tag, clip.rel if clip else "black", ss, n)
        name = "seg_%05d_%s.ts" % (i, hashlib.sha1(key.encode("utf-8")).hexdigest()[:8])
        jobs.append(dict(i=i, n=n, fps=fps, W=W, H=H, clip=clip, ss=ss,
                         lut=luts.get(s["kind"]) if s["kind"] else None,
                         codec=codec, enc_args=enc_args, out=os.path.join(seg_dir, name), cwd=work))
    t0 = time.time()
    done = 0
    total_frames = sum(j["n"] for j in jobs)
    frames_done = 0
    with cf.ThreadPoolExecutor(max_workers=max(1, workers)) as ex:
        futs = {ex.submit(render_shot, j): j for j in jobs}
        for fut in cf.as_completed(futs):
            fut.result()
            done += 1
            frames_done += futs[fut]["n"]
            if done % max(1, len(jobs) // 40) == 0 or done == len(jobs):
                el = time.time() - t0
                rate = frames_done / el if el > 0 else 0
                left = (total_frames - frames_done) / rate if rate > 0 else 0
                log("  %d/%d 조각 (%.0f%%) · 남은 시간 약 %d분" % (done, len(jobs), 100.0 * frames_done / total_frames,
                                                           int(left / 60 + 0.5)))
    list_path = os.path.join(seg_dir, "list.txt")
    with open(list_path, "w", encoding="utf-8", newline="\n") as fh:
        for j in jobs:
            fh.write("file '%s'\n" % os.path.basename(j["out"]))
    log("[5/5] 조각 이어붙이고 오디오 합치는 중...")
    tmp = out_mp4 + ".part.mp4"
    ff.run(["-f", "concat", "-safe", "0", "-i", "list.txt", "-i", os.path.abspath(audio_wav),
            "-map", "0:v:0", "-map", "1:a:0", "-c:v", "copy", "-c:a", "aac", "-b:a", "320k", "-ar", "48000",
            "-movflags", "+faststart", os.path.abspath(tmp)], cwd=seg_dir)
    os.replace(tmp, out_mp4)
    return jobs, enc_key
