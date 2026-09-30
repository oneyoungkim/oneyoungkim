"""Find media files in the shoot folder and decide which device each came from."""
import csv
import os
import re

from . import ff

VIDEO_EXT = {".mp4", ".mov", ".m4v", ".mxf", ".mts", ".avi"}
AUDIO_EXT = {".wav", ".bwf", ".mp3", ".m4a", ".aac", ".flac", ".aif", ".aiff"}

# kind -> (display name, role)
KINDS = {
    "leica": ("라이카 Q2", "camera"),
    "pocket": ("DJI 포켓 (미니2 마이크 소리 포함)", "camera+mic"),
    "fuji": ("후지 X-T4", "camera"),
    "mic": ("DJI Mic 3 자체녹음", "mic"),
    "skip": ("제외", "none"),
}

CSV_NAME = "파일분류.csv"

_FOLDER_KEYS = [
    ("leica", ("라이카", "leica", "q2")),
    ("fuji", ("후지", "fuji", "xt4", "x-t4", "fujifilm")),
    ("pocket", ("포켓", "pocket", "osmo")),
    ("mic", ("마이크", "mic", "audio", "오디오", "녹음")),
]


def _folder_guess(rel_dir):
    low = rel_dir.lower()
    for kind, keys in _FOLDER_KEYS:
        if any(k in low for k in keys):
            return kind
    return None


def _name_guess(name):
    up = name.upper()
    if re.match(r"^L\d{7}\.", up):
        return "leica"
    if up.startswith("DSCF") or up.startswith("_DSF"):
        return "fuji"
    if up.startswith("DJI_"):
        return "pocket"
    return None


def _tag_guess(info):
    t = info.tags_text
    if "leica" in t:
        return "leica"
    if "fujifilm" in t or "fuji" in t:
        return "fuji"
    if "dji" in t or "osmo" in t:
        return "pocket"
    return None


def classify(path, rel, info):
    ext = os.path.splitext(path)[1].lower()
    if not info.has_audio and not info.has_video:
        return "skip", "소리/영상 없음"
    if not info.has_video:
        return "mic", "오디오 파일"
    if ext not in VIDEO_EXT:
        return "skip", "지원하지 않는 형식"
    guesses = ((_folder_guess(os.path.dirname(rel)), "폴더 이름"),
               (_tag_guess(info), "파일 정보"),
               (_name_guess(os.path.basename(path)), "파일 이름"))
    for guess, why in guesses:
        # a video file inside a "mic" folder is still a camera clip
        if guess and guess != "mic":
            return guess, why
    return None, "알 수 없음"


def _walk(root, skip_dirs):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and os.path.join(dirpath, d) not in skip_dirs]
        for name in sorted(filenames):
            if name.startswith(".") or name.startswith("._"):
                continue
            ext = os.path.splitext(name)[1].lower()
            if ext in VIDEO_EXT or ext in AUDIO_EXT:
                yield os.path.join(dirpath, name)


def read_overrides(csv_path):
    out = {}
    if not os.path.isfile(csv_path):
        return out
    with open(csv_path, "r", encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            rel = (row.get("파일") or "").strip()
            kind = (row.get("종류") or "").strip().lower()
            if rel and kind in KINDS:
                out[rel.replace("\\", "/")] = kind
    return out


def scan(root, out_dir):
    """Return list of dicts: rel, path, kind, reason, info."""
    csv_path = os.path.join(out_dir, CSV_NAME)
    overrides = read_overrides(csv_path)
    items = []
    for path in _walk(root, {os.path.abspath(out_dir)}):
        rel = os.path.relpath(path, root).replace("\\", "/")
        try:
            info = ff.MediaInfo(path, ff.probe(path))
        except ff.FFError as exc:
            items.append(dict(rel=rel, path=path, kind="skip", reason="읽기 실패: %s" % exc, info=None))
            continue
        if rel in overrides:
            kind, reason = overrides[rel], "파일분류.csv"
        else:
            kind, reason = classify(path, rel, info)
        if kind != "skip" and info.duration < 3:
            kind, reason = "skip", "3초 미만"
        items.append(dict(rel=rel, path=path, kind=kind, reason=reason, info=info))
    write_csv(csv_path, items)
    return items


def write_csv(csv_path, items):
    os.makedirs(os.path.dirname(csv_path), exist_ok=True)
    with open(csv_path, "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["파일", "종류", "판단 근거", "길이(초)", "안내: 종류는 leica / pocket / fuji / mic / skip 중 하나"])
        for it in items:
            dur = "%.1f" % it["info"].duration if it["info"] else ""
            w.writerow([it["rel"], it["kind"] or "", it["reason"], dur, ""])
