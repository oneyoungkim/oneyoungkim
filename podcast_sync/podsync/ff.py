"""FFmpeg / FFprobe discovery and thin helpers."""
import glob
import json
import os
import shutil
import subprocess
from fractions import Fraction

EXE = ".exe" if os.name == "nt" else ""

FFMPEG = None
FFPROBE = None


class ToolMissing(RuntimeError):
    pass


class FFError(RuntimeError):
    pass


def _candidates():
    found = shutil.which("ffmpeg")
    if found:
        yield found
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    yield os.path.join(here, "ffmpeg", "bin", "ffmpeg" + EXE)
    if os.name == "nt":
        la = os.environ.get("LOCALAPPDATA", "")
        if la:
            yield os.path.join(la, "Microsoft", "WinGet", "Links", "ffmpeg.exe")
            pattern = os.path.join(la, "Microsoft", "WinGet", "Packages", "Gyan.FFmpeg*", "*", "bin", "ffmpeg.exe")
            for p in sorted(glob.glob(pattern), reverse=True):
                yield p
        for base in (r"C:\ffmpeg\bin", r"C:\Program Files\ffmpeg\bin", r"C:\ProgramData\chocolatey\bin"):
            yield os.path.join(base, "ffmpeg.exe")


def find_tools():
    """Locate ffmpeg and ffprobe (PATH, winget install folders, ./ffmpeg/bin)."""
    global FFMPEG, FFPROBE
    for cand in _candidates():
        if not cand or not os.path.isfile(cand):
            continue
        probe = os.path.join(os.path.dirname(cand), "ffprobe" + EXE)
        if not os.path.isfile(probe):
            probe = shutil.which("ffprobe")
        if probe and os.path.isfile(probe):
            FFMPEG, FFPROBE = cand, probe
            return FFMPEG, FFPROBE
    raise ToolMissing("ffmpeg / ffprobe 를 찾지 못했습니다.")


def run(args, cwd=None, capture=False):
    """Run ffmpeg with args (list, without the executable). Raises FFError with stderr tail."""
    cmd = [FFMPEG, "-hide_banner", "-nostdin", "-y"] + list(args)
    proc = subprocess.run(cmd, cwd=cwd, stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
                          stderr=subprocess.PIPE)
    if proc.returncode != 0:
        tail = proc.stderr.decode("utf-8", "replace").strip().splitlines()[-15:]
        raise FFError("ffmpeg 실패:\n  " + " ".join(cmd[:12]) + " ...\n" + "\n".join(tail))
    return proc


def run_stderr(args, cwd=None):
    """Run ffmpeg and return stderr text (for filters that report via log, e.g. loudnorm)."""
    cmd = [FFMPEG, "-hide_banner", "-nostdin", "-y"] + list(args)
    proc = subprocess.run(cmd, cwd=cwd, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    text = proc.stderr.decode("utf-8", "replace")
    if proc.returncode != 0:
        raise FFError("ffmpeg 실패:\n" + "\n".join(text.strip().splitlines()[-15:]))
    return text


def popen_raw(args, cwd=None):
    """Start ffmpeg writing raw data to stdout."""
    cmd = [FFMPEG, "-hide_banner", "-nostdin", "-v", "error"] + list(args)
    # stderr is discarded: an undrained pipe could fill up and stall ffmpeg while we read stdout
    return subprocess.Popen(cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)


def probe(path):
    cmd = [FFPROBE, "-v", "error", "-print_format", "json", "-show_format", "-show_streams", path]
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0:
        raise FFError("ffprobe 실패: " + path + "\n" + proc.stderr.decode("utf-8", "replace")[-500:])
    return json.loads(proc.stdout.decode("utf-8", "replace"))


def _f(x, default=0.0):
    try:
        return float(x)
    except (TypeError, ValueError):
        return default


def parse_rate(s):
    try:
        fr = Fraction(s)
        return fr if fr > 0 else None
    except (TypeError, ValueError, ZeroDivisionError):
        return None


class MediaInfo:
    """Subset of ffprobe output that the pipeline needs."""

    def __init__(self, path, data):
        self.path = path
        self.raw = data
        fmt = data.get("format", {})
        self.format_start = _f(fmt.get("start_time"), 0.0)
        self.duration = _f(fmt.get("duration"), 0.0)
        self.tags_text = json.dumps(fmt.get("tags", {}), ensure_ascii=False).lower()
        self.video = None
        self.audio = None
        for st in data.get("streams", []):
            kind = st.get("codec_type")
            disp = st.get("disposition", {})
            if kind == "video" and self.video is None and not disp.get("attached_pic"):
                self.video = st
            elif kind == "audio" and self.audio is None:
                self.audio = st
            self.tags_text += " " + json.dumps(st.get("tags", {}), ensure_ascii=False).lower()

    # ---- audio ----
    @property
    def has_audio(self):
        return self.audio is not None

    @property
    def sample_rate(self):
        return int(_f(self.audio.get("sample_rate"), 48000)) if self.audio else None

    @property
    def channels(self):
        return int(_f(self.audio.get("channels"), 1)) if self.audio else 0

    @property
    def audio_offset(self):
        if not self.audio:
            return 0.0
        return _f(self.audio.get("start_time"), self.format_start) - self.format_start

    @property
    def audio_duration(self):
        if not self.audio:
            return 0.0
        d = _f(self.audio.get("duration"), 0.0)
        return d if d > 0 else self.duration - self.audio_offset

    # ---- video ----
    @property
    def has_video(self):
        return self.video is not None

    @property
    def fps(self):
        if not self.video:
            return None
        for key in ("avg_frame_rate", "r_frame_rate"):
            fr = parse_rate(self.video.get(key))
            if fr and fr < 1000:
                return fr.limit_denominator(1001)
        return Fraction(30000, 1001)

    @property
    def video_offset(self):
        if not self.video:
            return 0.0
        return _f(self.video.get("start_time"), self.format_start) - self.format_start

    @property
    def video_duration(self):
        if not self.video:
            return 0.0
        d = _f(self.video.get("duration"), 0.0)
        return d if d > 0 else self.duration - self.video_offset

    @property
    def rotation(self):
        if not self.video:
            return 0
        rot = self.video.get("tags", {}).get("rotate")
        for sd in self.video.get("side_data_list", []) or []:
            if "rotation" in sd:
                rot = sd["rotation"]
        try:
            return int(float(rot)) % 360
        except (TypeError, ValueError):
            return 0

    @property
    def size(self):
        if not self.video:
            return None
        w, h = int(self.video.get("width", 0)), int(self.video.get("height", 0))
        if self.rotation in (90, 270):
            w, h = h, w
        return w, h

    @property
    def color(self):
        """(matrix, range, transfer) with safe defaults for HD/UHD camera footage."""
        v = self.video or {}
        space = (v.get("color_space") or "").lower()
        if space in ("bt2020nc", "bt2020c", "bt2020"):
            matrix = "bt2020"
        elif space in ("smpte170m", "bt470bg"):
            matrix = "bt601" if space == "smpte170m" else "bt470"
        else:
            matrix = "bt709"
        rng = "pc" if (v.get("color_range") or "").lower() in ("pc", "jpeg", "full") else "tv"
        pix = (v.get("pix_fmt") or "").lower()
        if pix.startswith("yuvj"):
            rng = "pc"
        transfer = (v.get("color_transfer") or "").lower()
        return matrix, rng, transfer

    @property
    def is_hdr(self):
        return self.color[2] in ("arib-std-b67", "smpte2084")


def has_filter(name):
    try:
        out = subprocess.run([FFMPEG, "-hide_banner", "-filters"], stdout=subprocess.PIPE,
                             stderr=subprocess.DEVNULL).stdout.decode("utf-8", "replace")
    except OSError:
        return False
    return any(line.split()[1:2] == [name] for line in out.splitlines() if len(line.split()) > 1)


def encoder_works(codec, extra=()):
    cmd = [FFMPEG, "-hide_banner", "-nostdin", "-v", "error", "-f", "lavfi", "-i",
           "color=black:s=320x240:r=30:d=0.2", "-c:v", codec] + list(extra) + ["-f", "null", "-"]
    try:
        return subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              timeout=60).returncode == 0
    except (OSError, subprocess.TimeoutExpired):
        return False
