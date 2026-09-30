"""Command line / double-click entry point."""
import argparse
import csv
import json
import os
import shutil
import sys
import time
import traceback
from fractions import Fraction

from . import audio, color, discover, edit, ff, render, sync

DEFAULT_FOLDER = r"G:\딥마카이"
NAMES = {"leica": "라이카 Q2", "pocket": "DJI 포켓", "fuji": "후지 X-T4", "mic": "DJI Mic 3"}


def log(msg=""):
    print(msg, flush=True)


def tc(sec):
    sign = "-" if sec < 0 else ""
    sec = abs(sec)
    h = int(sec // 3600)
    m = int(sec % 3600 // 60)
    s = sec % 60
    return "%s%d:%02d:%06.3f" % (sign, h, m, s)


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
    ap = argparse.ArgumentParser(prog="podsync", description="팟캐스트 멀티캠 자동 싱크/믹스/색맞춤/컷편집")
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


def print_scan(items):
    log("")
    log("찾은 파일:")
    for kind in ("leica", "pocket", "fuji", "mic", None, "skip"):
        rows = [it for it in items if it["kind"] == kind]
        if not rows:
            continue
        title = NAMES.get(kind, "제외" if kind == "skip" else "알 수 없음 ← 확인 필요")
        log("  ■ %s (%d개)" % (title, len(rows)))
        for it in rows:
            dur = it["info"].duration if it["info"] else 0
            note = ""
            if it["info"] and it["info"].has_video and it["info"].rotation in (90, 270):
                note = "  ← 세로 영상: 가로 화면에 맞추느라 위아래가 많이 잘립니다"
            log("      %-48s %8s  (%s)%s" % (it["rel"], tc(dur)[:-4], it["reason"], note))


def sync_report_lines(clips, P0):
    lines = []
    lines.append("%-46s %-10s %14s %11s %9s %7s %8s  %s" % ("파일", "종류", "타임라인 시작", "길이", "시계오차", "비교구간",
                                                           "오차(ms)", "상태"))
    for c in sorted(clips, key=lambda c: (c.kind, c.rel)):
        d = c.diag or {}
        if c.a is None:
            lines.append("%-46s %-10s %14s %11s %9s %7s %8s  %s" % (c.rel, c.kind, "-", "-", "-", "-", "-",
                                                                   d.get("status", "싱크 실패")))
            continue
        start = (c.video_span()[0] if c.has_video else c.audio_span()[0]) - P0
        length = c.info.duration
        ppm = d.get("drift_ppm", (c.r - 1) * 1e6)
        resid = d.get("resid_ms")
        lines.append("%-46s %-10s %14s %11s %+8.1fppm %7s %8s  %s" % (
            c.rel, c.kind, tc(start), tc(length)[:-4], ppm, d.get("used", d.get("windows", "-")),
            "%.3f" % resid if resid is not None else "-", d.get("status", "")))
    return lines


def main(argv=None):
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
    folder = os.path.abspath(folder)
    if not os.path.isdir(folder):
        log("폴더를 찾을 수 없습니다: %s" % folder)
        return 2
    out_dir = os.path.join(folder, "_편집결과")
    work = os.path.join(out_dir, "_작업파일")
    os.makedirs(work, exist_ok=True)

    log("\n파일 살펴보는 중: %s" % folder)
    items = discover.scan(folder, out_dir)
    print_scan(items)
    unknown = [it for it in items if it["kind"] is None]
    if unknown:
        log("\n어느 카메라 파일인지 모르는 파일이 있습니다.")
        log("  %s 파일을 엑셀/메모장으로 열어 '종류' 칸에 leica / pocket / fuji / mic / skip 중 하나를 적고" %
            os.path.join(out_dir, discover.CSV_NAME))
        log("  다시 실행해 주세요.")
        return 3
    use = [it for it in items if it["kind"] in ("leica", "pocket", "fuji", "mic")]
    no_audio = [it for it in use if not it["info"].has_audio]
    for it in no_audio:
        log("  ! 소리가 없는 파일이라 싱크를 맞출 수 없어 제외합니다: %s" % it["rel"])
    use = [it for it in use if it["info"].has_audio]
    if not any(it["kind"] in ("mic", "pocket") for it in use):
        log("마이크 녹음(DJI Mic 3 파일 또는 포켓 영상)이 없습니다.")
        return 3
    if not args.yes and ask("\n이대로 진행할까요? [Enter = 예 / n = 아니오]: ", "y").lower().startswith("n"):
        log("파일분류.csv 를 고친 뒤 다시 실행하세요: %s" % os.path.join(out_dir, discover.CSV_NAME))
        return 0

    t_start = time.time()
    clips = [sync.Clip(it) for it in use]
    sig = sync.signature(use)
    cache = os.path.join(work, "sync.json")
    if sync.load(clips, cache, sig):
        log("\n[1/5] 이전에 맞춘 싱크 결과를 다시 사용합니다.")
        sync.load_analysis_audio(clips, work)
    else:
        log("")
        sync.synchronise(clips, work)
        sync.save([c for c in clips if c.a is not None], cache, sig)
    placed = [c for c in clips if c.a is not None]
    failed = [c for c in clips if c.a is None]
    mic_clips = [c for c in placed if c.is_mic]
    cameras = {}
    for c in placed:
        if c.has_video:
            cameras.setdefault(c.kind, []).append(c)
    if not cameras:
        log("싱크된 영상이 없습니다.")
        return 3
    weights = parse_weights(args.weights)
    main_kind = args.main if args.main in cameras else max(cameras, key=lambda k: weights.get(k, 0))
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
        log("\n! 싱크에 실패한 파일은 빼고 진행합니다: " + ", ".join(c.rel for c in failed))

    mode = args.mode
    if not mode:
        log("\n무엇을 만들까요?")
        log("  1) 테스트: 중간 %.0f분만 빠르게 (싱크·색·소리 확인용)" % args.test_minutes)
        log("  2) 전체 편집본")
        mode = "full" if ask("선택 [Enter = 1]: ", "1").strip() == "2" else "test"
    if mode == "test":
        span = int(args.test_minutes * 60 * fps)
        F0 = max(0, nframes // 2 - span // 2)
        F1 = min(nframes, F0 + span)
        sub = []
        for s in shots:
            a, b = max(s["f0"], F0), min(s["f1"], F1)
            if b > a:
                sub.append(dict(s, f0=a - F0, f1=b - F0))
        shots_r, R0, R1 = sub, P0 + F0 / fps, P0 + F1 / fps
        dest = os.path.join(out_dir, "테스트")
        base = "테스트_%d분" % round(args.test_minutes)
    else:
        shots_r, R0, R1 = shots, P0, P1
        dest = out_dir
        base = "최종_멀티캠"
    os.makedirs(dest, exist_ok=True)
    height = args.height
    if not height and H0 > 1080:
        if mode == "test":
            height = 1080
        else:
            log("\n해상도를 고르세요 (메인 카메라 %dx%d)" % (W0, H0))
            log("  1) 원본 해상도 그대로 (화질 최고, 오래 걸림)")
            log("  2) 1080p (빠름)")
            height = 1080 if ask("선택 [Enter = 1]: ", "1").strip() == "2" else 0
    W, H = W0, H0
    if height and height < H0:
        W, H = int(round(height * W0 / H0 / 2)) * 2, height
    W, H = W // 2 * 2, H // 2 * 2

    luts = {}
    notes = {}
    if not args.no_color:
        luts, views, notes = color.build_luts(cameras, main_kind, folder, work, os.path.join(out_dir, "LUT"),
                                              strength=args.color_strength)
        color.contact_sheet(views, luts, main_kind, os.path.join(out_dir, "색보정_비교.jpg"))

    need_gb = (R1 - R0) / 3600.0 * (W * H / (1920 * 1080.0)) * 9.0 * 2 + (R1 - R0) / 3600.0 * 3.0
    free_gb = shutil.disk_usage(out_dir).free / 1e9
    if free_gb < need_gb:
        log("\n! 저장 공간이 부족할 수 있습니다: 남은 공간 %.0fGB, 필요 예상 %.0fGB (%s)" % (free_gb, need_gb, out_dir))
        if ask("  그래도 계속할까요? [Enter = 예 / n = 아니오]: ", "y").lower().startswith("n"):
            return 0

    wav = os.path.join(dest, base + "_오디오.wav")
    stems = os.path.join(dest, base + "_마이크별")
    info = audio.build_programme_audio(mic_clips, R0, R1, work, wav, stems, args.denoise, args.lufs,
                                       use_automix=not args.no_automix)

    out_mp4 = os.path.join(dest, base + ".mp4")
    with open(os.path.join(work, "last_render.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(R0=R0, R1=R1, fps=str(fps), W=W, H=H, mode=mode, out=out_mp4, wav=wav,
                       shots=[dict(kind=s["kind"], clip=s["clip"].rel if s["clip"] else None,
                                   f0=s["f0"], f1=s["f1"]) for s in shots_r]), fh, ensure_ascii=False)
    render.render(shots_r, R0, fps, W, H, luts, work, out_mp4, wav, args.encoder, args.workers)
    shutil.rmtree(os.path.join(work, "segments"), ignore_errors=True)

    share = edit.share(shots_r, fps)
    summary_at = len(report)
    report += ["", "[카메라 비중] 목표: %s" % args.weights]
    for k, v in sorted(share.items(), key=lambda kv: -kv[1]):
        report.append("  %-10s %5.1f%%" % (NAMES.get(k, "검은 화면(영상 없음)"), v * 100))
    report += ["", "[마이크 음량 맞춤]"]
    for lane, rel, chan, g, how in info["levels"]:
        report.append("  %-8s %-40s %-4s %+6.1f dB  (%s)" % (lane, rel, chan, g, how))
    li = info["loudness_in"]
    report.append("  최종 음량 %.0f LUFS 로 맞춤 (원래 %.1f LUFS)" % (args.lufs, float(li["input_i"])))
    if notes:
        report += ["", "[색 맞춤] 기준 카메라: %s · 색차이(ΔE)는 같은 물체 기준, 3 이하면 눈으로 거의 구분 안 됨"
                   % NAMES.get(main_kind, main_kind)]
        for k, n in notes.items():
            diff = "" if n["before"] is None else " · 색차이 %.1f → %.1f" % (n["before"], n["after"])
            report.append("  %-10s 방식: %s%s" % (NAMES.get(k, k), n["method"], diff))
    with open(os.path.join(dest, base + "_리포트.txt"), "w", encoding="utf-8-sig") as fh:
        fh.write("\n".join(report) + "\n")
    with open(os.path.join(dest, base + "_컷리스트.csv"), "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["순서", "시작", "끝", "카메라", "원본 파일", "원본 위치(초)"])
        for i, s in enumerate(shots_r, 1):
            m0 = R0 + s["f0"] / fps
            src = s["clip"].master_to_local(m0) if s["clip"] else ""
            w.writerow([i, tc(s["f0"] / fps), tc(s["f1"] / fps), NAMES.get(s["kind"], "검은 화면"),
                        s["clip"].rel if s["clip"] else "", "%.3f" % src if src != "" else ""])
    for line in report[summary_at:]:
        log(line)
    log("\n완료! (%d분 걸림)" % round((time.time() - t_start) / 60))
    log("  영상: %s" % out_mp4)
    log("  오디오: %s" % wav)
    if mode == "test":
        log("\n테스트 영상을 보고 괜찮으면 다시 실행해서 2) 전체 편집본을 고르세요. (싱크 분석은 다시 안 합니다)")
    if os.name == "nt":
        try:
            os.startfile(dest)
        except OSError:
            pass
    return 0


def run():
    try:
        code = main()
    except KeyboardInterrupt:
        log("\n중단했습니다. 다시 실행하면 이어서 합니다.")
        code = 130
    except Exception:
        log("\n오류가 났습니다. 아래 내용을 캡처해서 보내주세요:\n")
        traceback.print_exc()
        code = 1
    sys.exit(code)
