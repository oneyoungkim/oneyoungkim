"""Log lines, stage progress and cancellation shared by the pipeline, the CLI and the web UI."""
import threading

STAGES = [
    ("scan", "파일 확인", 0.0),
    ("sync", "싱크 맞추기", 0.20),
    ("color", "색 맞추기", 0.05),
    ("audio", "오디오 만들기", 0.15),
    ("render", "영상 만들기", 0.55),
    ("mux", "마무리", 0.05),
]

_lock = threading.Lock()
_lines = []
_state = {"stage": None, "frac": 0.0}
_cancel = threading.Event()


class Cancelled(Exception):
    pass


def log(msg=""):
    print(msg, flush=True)
    with _lock:
        _lines.append(str(msg))


def reset():
    with _lock:
        del _lines[:]
        _state.update(stage=None, frac=0.0)
    _cancel.clear()


def stage(name):
    with _lock:
        _state.update(stage=name, frac=0.0)


def progress(frac):
    with _lock:
        _state["frac"] = max(0.0, min(1.0, float(frac)))


def cancel():
    _cancel.set()


def cancelled():
    return _cancel.is_set()


def check():
    if _cancel.is_set():
        raise Cancelled()


def snapshot(since=0):
    with _lock:
        name, frac = _state["stage"], _state["frac"]
        lines = _lines[since:]
        total = len(_lines)
    done = 0.0
    label = ""
    for key, text, weight in STAGES:
        if key == name:
            done += weight * frac
            label = text
            break
        done += weight
    if name is None:
        done = 0.0
    return dict(stage=name, label=label, frac=frac, overall=min(1.0, done), lines=lines, next=total)
