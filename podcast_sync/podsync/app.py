"""The editing job itself (shared by the web UI and the command line)."""
import argparse
import csv
import json
import os
import shutil
import sys
import time
import traceback
from fractions import Fraction

from . import audio, color, discover, edit, events, ff, render, sync
from .events import log

DEFAULT_FOLDER = r"G:\딥마카이"
NAMES = {"leica": "라이카 Q2", "pocket": "DJI 포켓", "fuji": "후지 X-T4", "mic": "DJI Mic 3", "skip": "제외"}
OUT_NAME = "_편집결과"

DEFAULT_OPTIONS = dict(
    weights={"leica": 60, "pocket": 25, "fuji": 15},
    main="leica",
    height=0,              # 0 = main camera's resolution
    lufs=-14.0,
    denoise=10.0,
    color_strength=1.0,
    color=True,
    automix=True,
    encoder="auto",
    workers=2,
    test_minutes=2.0,
)


class JobError(RuntimeError):
    """A problem the user can fix (shown without a traceback)."""


def tc(sec):
    sign = "-" if sec < 0 else ""
    sec = abs(sec)
    return "%s%d:%02d:%06.3f" % (sign, int(sec // 3600), int(sec % 3600 // 60), sec % 60)


def paths(folder):
    out_dir = os.path.join(folder, OUT_NAME)
    return out_dir, os.path.join(out_dir, "_작업파일")


def scan_folder(folder, overrides=None):
    """Classify the shoot folder. Returns (items, summary dict for display)."""
    folder = os.path.abspath(folder)
    if not os.path.isdir(folder):
        raise JobError("폴더를 찾을 수 없습니다: %s" % folder)
    out_dir, work = paths(folder)
    os.makedirs(work, exist_ok=True)
    items = discover.scan(folder, out_dir, overrides)
    rows = []
    for it in items:
        info = it["info"]
        note = ""
        if info and info.has_video and info.rotation in (90, 270):
            note = "세로 영상: 가로 화면에 맞추느라 위아래가 많이 잘립니다"
        if it["kind"] in ("leica", "pocket", "fuji", "mic") and info and not info.has_audio:
            note = "소리가 없어 싱크를 맞출 수 없습니다 (제외됨)"
        video = ""
        if info and info.has_video:
            w, h = info.size
            video = "%dx%d · %.2f fps" % (w, h, float(info.fps))
        rows.append(dict(rel=it["rel"], kind=it["kind"], reason=it["reason"],
                         duration=info.duration if info else 0.0, video=video, note=note))
    return items, dict(folder=folder, out_dir=out_dir, files=rows,
                       unknown=sum(1 for it in items if it["kind"] is None))


def run_job(folder, mode="test", options=None, overrides=None):
    """Sync, mix, colour-match and cut. Returns a result dict (paths + report)."""
    opt = dict(DEFAULT_OPTIONS)
    opt.update({k: v for k, v in (options or {}).items() if v is not None})
    t_start = time.time()
    events.stage("scan")
    folder = os.path.abspath(folder)
    items, _ = scan_folder(folder, overrides)
    out_dir, work = paths(folder)
    unknown = [it["rel"] for it in items if it["kind"] is None]
    if unknown:
        raise JobError("어느 장비 파일인지 정하지 않은 파일이 있습니다: " + ", ".join(unknown[:5]))
    use = [it for it in items if it["kind"] in ("leica", "pocket", "fuji", "mic") and it["info"].has_audio]
    if not any(it["kind"] in ("mic", "pocket") for it in use):
        raise JobError("마이크 녹음(DJI Mic 3 파일 또는 포켓 영상)이 없습니다.")
    if not any(it["kind"] in ("leica", "pocket", "fuji") for it in use):
        raise JobError("영상 파일이 없습니다.")

    # 1. sync ------------------------------------------------------------------
    events.stage("sync")
    clips = [sync.Clip(it) for it in use]
    sig = sync.signature(use)
    cache = os.path.join(work, "sync.json")
    if sync.load(clips, cache, sig):
        log("[1/5] 이전에 맞춘 싱크 결과를 다시 사용합니다.")
        sync.load_analysis_audio(clips, work)
    else:
        sync.synchronise(clips, work)
        sync.save([c for c in clips if c.a is not None], cache, sig)
    placed = [c for c in clips if c.a is not None]
    failed = [c for c in clips if c.a is None]
    mic_clips = [c for c in placed if c.is_mic]
    cameras = {}
    for c in placed:
        if c.has_video:
            cameras.setdefault(c.kind, []).append(c)
    if not cameras or not mic_clips:
        raise JobError("싱크를 맞출 수 있는 영상/마이크가 부족합니다.")
    weights = {k: float(v) for k, v in opt["weights"].items()}
    main_kind = opt["main"] if opt["main"] in cameras else max(cameras, key=lambda k: weights.get(k, 0))
    main_clip = max(cameras[main_kind], key=lambda c: c.info.video_duration)
    fps = main_clip.info.fps or Fraction(30000, 1001)
    W0, H0 = main_clip.info.size

    P0, P1, nframes, shots = edit.plan_shots(cameras, weights, main_kind, mic_clips, fps)
    report = ["팟캐스트 자동 편집 리포트", "촬영 폴더: %s" % folder, "",
              "타임라인: %s 길이 (%d 프레임, %.3f fps)" % (tc(P1 - P0), nframes, float(fps)), "",
              "[싱크 결과] 타임라인 시작 = 편집본 0초 기준, 시계오차 = 기기 시계가 빠르거나 느린 정도"]
    report += sync_report_lines(clips, P0)
    for line in report[3:]:
        log(line)
    if failed:
        log("! 싱크에 실패한 파일은 빼고 진행합니다: " + ", ".join(c.rel for c in failed))

    if mode == "test":
        span = int(float(opt["test_minutes"]) * 60 * fps)
        F0 = max(0, nframes // 2 - span // 2)
        F1 = min(nframes, F0 + span)
        shots_r = []
        for s in shots:
            a, b = max(s["f0"], F0), min(s["f1"], F1)
            if b > a:
                shots_r.append(dict(s, f0=a - F0, f1=b - F0))
        R0, R1 = P0 + F0 / fps, P0 + F1 / fps
        dest = os.path.join(out_dir, "테스트")
        base = "테스트_%g분" % float(opt["test_minutes"])
    else:
        shots_r, R0, R1 = shots, P0, P1
        dest = out_dir
        base = "최종_멀티캠"
    os.makedirs(dest, exist_ok=True)
    height = int(opt["height"] or 0)
    if not height and mode == "test" and H0 > 1080:
        height = 1080
    W, H = W0, H0
    if height and height < H0:
        W, H = int(round(height * W0 / H0 / 2)) * 2, height
    W, H = W // 2 * 2, H // 2 * 2
    need_gb = (R1 - R0) / 3600.0 * (W * H / (1920 * 1080.0)) * 18.0 + (R1 - R0) / 3600.0 * 3.0
    free_gb = shutil.disk_usage(out_dir).free / 1e9
    if free_gb < need_gb:
        log("! 저장 공간이 부족할 수 있습니다: 남은 공간 %.0fGB, 필요 예상 %.0fGB" % (free_gb, need_gb))

    # 2. colour ----------------------------------------------------------------
    events.stage("color")
    luts, notes = {}, {}
    sheet = None
    if opt["color"]:
        luts, views, notes = color.build_luts(cameras, main_kind, folder, work, os.path.join(out_dir, "LUT"),
                                              strength=float(opt["color_strength"]))
        sheet = os.path.join(out_dir, "색보정_비교.jpg")
        color.contact_sheet(views, luts, main_kind, sheet)

    # 3. audio -----------------------------------------------------------------
    events.stage("audio")
    wav = os.path.join(dest, base + "_오디오.wav")
    stems = os.path.join(dest, base + "_마이크별")
    info = audio.build_programme_audio(mic_clips, R0, R1, work, wav, stems, float(opt["denoise"]),
                                       float(opt["lufs"]), use_automix=bool(opt["automix"]))

    # 4. video -----------------------------------------------------------------
    events.stage("render")
    out_mp4 = os.path.join(dest, base + ".mp4")
    with open(os.path.join(work, "last_render.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(R0=R0, R1=R1, fps=str(fps), W=W, H=H, mode=mode, out=out_mp4, wav=wav,
                       shots=[dict(kind=s["kind"], clip=s["clip"].rel if s["clip"] else None,
                                   f0=s["f0"], f1=s["f1"]) for s in shots_r]), fh, ensure_ascii=False)
    render.render(shots_r, R0, fps, W, H, luts, work, out_mp4, wav, opt["encoder"], int(opt["workers"]))
    shutil.rmtree(os.path.join(work, "segments"), ignore_errors=True)

    # 5. reports ---------------------------------------------------------------
    share = edit.share(shots_r, fps)
    summary_at = len(report)
    report += ["", "[카메라 비중] 목표: " + ", ".join("%s %g" % (NAMES.get(k, k), v) for k, v in weights.items())]
    for k, v in sorted(share.items(), key=lambda kv: -kv[1]):
        report.append("  %-10s %5.1f%%" % (NAMES.get(k, "검은 화면(영상 없음)"), v * 100))
    report += ["", "[마이크 음량 맞춤]"]
    for lane, rel, chan, g, how in info["levels"]:
        report.append("  %-8s %-40s %-4s %+6.1f dB  (%s)" % (lane, rel, chan, g, how))
    report.append("  최종 음량 %.0f LUFS 로 맞춤 (원래 %.1f LUFS)" % (float(opt["lufs"]),
                                                            float(info["loudness_in"]["input_i"])))
    if notes:
        report += ["", "[색 맞춤] 기준 카메라: %s · 색차이(ΔE)는 같은 물체 기준, 3 이하면 눈으로 거의 구분 안 됨"
                   % NAMES.get(main_kind, main_kind)]
        for k, n in notes.items():
            diff = "" if n["before"] is None else " · 색차이 %.1f → %.1f" % (n["before"], n["after"])
            report.append("  %-10s 방식: %s%s" % (NAMES.get(k, k), n["method"], diff))
    report_path = os.path.join(dest, base + "_리포트.txt")
    with open(report_path, "w", encoding="utf-8-sig") as fh:
        fh.write("\n".join(report) + "\n")
    with open(os.path.join(dest, base + "_컷리스트.csv"), "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["순서", "시작", "끝", "카메라", "원본 파일", "원본 위치(초)"])
        for i, s in enumerate(shots_r, 1):
            src = s["clip"].master_to_local(R0 + s["f0"] / fps) if s["clip"] else None
            w.writerow([i, tc(s["f0"] / fps), tc(s["f1"] / fps), NAMES.get(s["kind"], "검은 화면"),
                        s["clip"].rel if s["clip"] else "", "%.3f" % src if src is not None else ""])
    for line in report[summary_at:]:
        log(line)
    minutes = (time.time() - t_start) / 60
    log("")
    log("완료! (%.0f분 걸림)" % minutes)
    log("  영상: %s" % out_mp4)
    return dict(mode=mode, folder=folder, out_dir=out_dir, dest=dest, video=out_mp4, audio=wav, report=report_path,
                report_text="\n".join(report), sheet=sheet if sheet and os.path.isfile(sheet) else None,
                minutes=minutes, size="%dx%d" % (W, H))


def sync_report_lines(clips, P0):
    lines = ["%-46s %-8s %14s %11s %11s %6s %8s  %s" % ("파일", "종류", "타임라인 시작", "길이", "시계오차", "비교구간",
                                                         "오차(ms)", "상태")]
    for c in sorted(clips, key=lambda c: (c.kind, c.rel)):
        d = c.diag or {}
        if c.a is None:
            lines.append("%-46s %-8s %14s %11s %11s %6s %8s  %s" % (c.rel, c.kind, "-", "-", "-", "-", "-",
                                                                  d.get("status", "싱크 실패")))
            continue
        start = (c.video_span()[0] if c.has_video else c.audio_span()[0]) - P0
        resid = d.get("resid_ms")
        lines.append("%-46s %-8s %14s %11s %+8.1fppm %6s %8s  %s" % (
            c.rel, c.kind, tc(start), tc(c.info.duration)[:-4], d.get("drift_ppm", (c.r - 1) * 1e6),
            d.get("used", d.get("windows", "-")), "%.3f" % resid if resid is not None else "-", d.get("status", "")))
    return lines


# --------------------------------------------------------- command line ---

def ask(prompt, default=""):
    if not sys.stdin or not sys.stdin.isatty():
        return default
    try:
        ans = input(prompt).strip().strip('"')
    except EOFError:
        return default
    return ans or default


def parse_weights(text):
    out = {}
    for part in text.split(","):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k.strip().lower()] = float(v)
    return out


def build_parser():
    ap = argparse.ArgumentParser(prog="podsync --cli", description="팟캐스트 멀티캠 자동 싱크/믹스/색맞춤/컷편집")
    ap.add_argument("folder", nargs="?", help="촬영 파일이 있는 폴더 (기본 %s)" % DEFAULT_FOLDER)
    ap.add_argument("--mode", choices=["test", "full"], help="test: 중간 2분만 / full: 전체")
    ap.add_argument("--test-minutes", type=float, default=2.0)
    ap.add_argument("--weights", default="leica=60,pocket=25,fuji=15", help="카메라 비중")
    ap.add_argument("--main", default="leica", help="메인 카메라 (색 기준)")
    ap.add_argument("--height", type=int, default=0, help="출력 세로 해상도 (0 = 메인 카메라 그대로)")
    ap.add_argument("--lufs", type=float, default=-14.0, help="최종 음량 (유튜브 -14)")
    ap.add_argument("--denoise", type=float, default=10.0, help="잡음 제거 세기 dB (0 = 끔)")
    ap.add_argument("--color-strength", type=float, default=1.0, help="색 맞춤 세기 0~1")
    ap.add_argument("--no-color", action="store_true", help="색 맞춤 끄기")
    ap.add_argument("--no-automix", action="store_true", help="오토믹서 끄기 (마이크 전부 항상 열림)")
    ap.add_argument("--encoder", default="auto", help="auto / nvenc / qsv / amf / x264")
    ap.add_argument("--workers", type=int, default=2)
    ap.add_argument("--yes", action="store_true", help="확인 질문 없이 진행")
    return ap


def cli(argv=None):
    args = build_parser().parse_args(argv)
    log("=" * 64)
    log(" 팟캐스트 자동 편집기 — 싱크 · 마이크 믹스 · 색 맞춤 · 멀티캠 컷")
    log("=" * 64)
    try:
        ff.find_tools()
    except ff.ToolMissing:
        log("ffmpeg 가 설치되어 있지 않습니다. run_windows.bat 으로 실행하면 자동 설치됩니다.")
        return 2
    folder = args.folder or ask("촬영 폴더 경로 [Enter = %s]: " % DEFAULT_FOLDER, DEFAULT_FOLDER)
    try:
        _, summary = scan_folder(folder)
    except JobError as exc:
        log(str(exc))
        return 2
    log("")
    log("찾은 파일:")
    for row in summary["files"]:
        log("  %-10s %-48s %s" % (NAMES.get(row["kind"], "알 수 없음!"), row["rel"], row["note"]))
    if summary["unknown"]:
        log("")
        log("어느 장비 파일인지 모르는 파일이 있습니다. %s 의 '종류' 칸을 고친 뒤 다시 실행하세요." %
            os.path.join(summary["out_dir"], discover.CSV_NAME))
        return 3
    if not args.yes and ask("\n이대로 진행할까요? [Enter = 예 / n = 아니오]: ", "y").lower().startswith("n"):
        return 0
    mode = args.mode
    if not mode:
        log("")
        log("  1) 테스트: 중간 %g분만 빠르게   2) 전체 편집본" % args.test_minutes)
        mode = "full" if ask("선택 [Enter = 1]: ", "1").strip() == "2" else "test"
    options = dict(weights=parse_weights(args.weights), main=args.main, height=args.height, lufs=args.lufs,
                   denoise=args.denoise, color_strength=args.color_strength, color=not args.no_color,
                   automix=not args.no_automix, encoder=args.encoder, workers=args.workers,
                   test_minutes=args.test_minutes)
    try:
        result = run_job(folder, mode, options)
    except JobError as exc:
        log("")
        log(str(exc))
        return 3
    if os.name == "nt":
        try:
            os.startfile(result["dest"])
        except OSError:
            pass
    return 0


def run_cli(argv=None):
    try:
        code = cli(argv)
    except (KeyboardInterrupt, events.Cancelled):
        log("")
        log("중단했습니다. 다시 실행하면 이어서 합니다.")
        code = 130
    except Exception:
        log("")
        log("오류가 났습니다. 아래 내용을 캡처해서 보내주세요:")
        traceback.print_exc()
        code = 1
    sys.exit(code)
