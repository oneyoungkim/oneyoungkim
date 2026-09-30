"""Browser UI: a small local web server (127.0.0.1 only) + native folder picker.

run_windows.bat -> python -m podsync -> this module opens the page in the browser.
The console window only shows the address; closing it ends the program.
"""
import json
import mimetypes
import os
import queue
import secrets
import socket
import threading
import time
import traceback
import urllib.parse
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from . import app, events, ff

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SETTINGS = os.path.join(HERE, "settings.json")


class State:
    token = secrets.token_urlsafe(18)
    lock = threading.Lock()
    job = dict(running=False, mode=None, result=None, error=None, started=None, ended=None)
    roots = set()               # folders whose files the page may load (results)
    dialogs = queue.Queue()
    stop = threading.Event()
    has_tk = False
    ffmpeg_error = None


def load_settings():
    try:
        with open(SETTINGS, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return {}


def save_settings(data):
    try:
        with open(SETTINGS, "w", encoding="utf-8") as fh:
            json.dump(data, fh, ensure_ascii=False, indent=1)
    except OSError:
        pass


def remember(folder=None, options=None):
    s = load_settings()
    if folder:
        recent = [f for f in s.get("recent", []) if os.path.normcase(f) != os.path.normcase(folder)]
        s["recent"] = [folder] + recent[:5]
    if options is not None:
        s["options"] = options
    save_settings(s)


# ------------------------------------------------------------------ job ---

def _job(folder, mode, options, overrides):
    events.reset()
    with State.lock:
        State.job.update(running=True, mode=mode, result=None, error=None, started=time.time(), ended=None)
    try:
        res = app.run_job(folder, mode, options, overrides)
        State.roots.add(os.path.normcase(os.path.abspath(res["out_dir"])))
        State.job["result"] = res
    except events.Cancelled:
        events.log("중지했습니다. 다시 만들면 이미 끝난 부분은 건너뛰고 이어서 합니다.")
        State.job["error"] = "중지했습니다."
    except app.JobError as exc:
        events.log(str(exc))
        State.job["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001 - shown to the user
        events.log(traceback.format_exc())
        State.job["error"] = "오류가 났습니다: %s (자세한 진행 기록을 캡처해서 보내주세요)" % exc
    finally:
        ff.kill_all()
        with State.lock:
            State.job.update(running=False, ended=time.time())


# --------------------------------------------------------------- server ---

class Handler(BaseHTTPRequestHandler):
    server_version = "podsync"

    def log_message(self, fmt, *args):
        pass

    def _send(self, code, body, ctype="application/json; charset=utf-8", extra=None):
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def _authed(self, query=None):
        tok = self.headers.get("X-Token") or (query or {}).get("t", [""])[0]
        return secrets.compare_digest(tok or "", State.token)

    def _body(self):
        n = int(self.headers.get("Content-Length") or 0)
        if not n:
            return {}
        return json.loads(self.rfile.read(n).decode("utf-8") or "{}")

    # ---- GET
    def do_GET(self):
        url = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(url.query)
        if url.path in ("/", "/index.html"):
            return self._send(200, PAGE.encode("utf-8"), "text/html; charset=utf-8")
        if not self._authed(q):
            return self._send(403, {"error": "forbidden"})
        if url.path == "/api/state":
            s = load_settings()
            return self._send(200, dict(settings=s, defaults=app.DEFAULT_OPTIONS, default_folder=app.DEFAULT_FOLDER,
                                        has_tk=State.has_tk, ffmpeg_error=State.ffmpeg_error, job=self._job_view()))
        if url.path == "/api/status":
            since = int((q.get("since") or ["0"])[0])
            snap = events.snapshot(since)
            snap["job"] = self._job_view()
            return self._send(200, snap)
        if url.path == "/file":
            return self._file((q.get("p") or [""])[0])
        return self._send(404, {"error": "not found"})

    def _job_view(self):
        with State.lock:
            j = dict(State.job)
        now = time.time()
        j["elapsed"] = ((j["ended"] or now) - j["started"]) if j["started"] else 0
        return j

    def _file(self, path):
        path = os.path.abspath(path)
        norm = os.path.normcase(path)
        if not any(norm.startswith(r + os.sep) for r in State.roots) or not os.path.isfile(path):
            return self._send(404, {"error": "not found"})
        size = os.path.getsize(path)
        ctype = mimetypes.guess_type(path)[0] or "application/octet-stream"
        start, end = 0, size - 1
        rng = self.headers.get("Range", "")
        partial = rng.startswith("bytes=")
        if partial:
            a, _, b = rng[6:].split(",")[0].partition("-")
            if a:
                start = int(a)
                end = int(b) if b else size - 1
            else:
                start = max(0, size - int(b))
            end = min(end, size - 1)
            if start > end:
                return self._send(416, b"", "text/plain", {"Content-Range": "bytes */%d" % size})
        self.send_response(206 if partial else 200)
        self.send_header("Content-Type", ctype)
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Length", str(end - start + 1))
        if partial:
            self.send_header("Content-Range", "bytes %d-%d/%d" % (start, end, size))
        self.end_headers()
        try:
            with open(path, "rb") as fh:
                fh.seek(start)
                left = end - start + 1
                while left > 0:
                    chunk = fh.read(min(1 << 20, left))
                    if not chunk:
                        break
                    self.wfile.write(chunk)
                    left -= len(chunk)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass

    # ---- POST
    def do_POST(self):
        url = urllib.parse.urlparse(self.path)
        if not self._authed():
            return self._send(403, {"error": "forbidden"})
        try:
            body = self._body()
        except ValueError:
            return self._send(400, {"error": "bad json"})
        try:
            handler = {
                "/api/pick": self._pick, "/api/scan": self._scan, "/api/start": self._start,
                "/api/cancel": self._cancel, "/api/open": self._open, "/api/quit": self._quit,
            }.get(url.path)
            if not handler:
                return self._send(404, {"error": "not found"})
            return handler(body)
        except app.JobError as exc:
            return self._send(400, {"error": str(exc)})
        except Exception as exc:  # noqa: BLE001
            traceback.print_exc()
            return self._send(500, {"error": str(exc)})

    def _pick(self, body):
        if not State.has_tk:
            return self._send(200, {"path": None, "unavailable": True})
        req = dict(initial=body.get("initial") or "", event=threading.Event(), result=None)
        State.dialogs.put(req)
        req["event"].wait(timeout=600)
        return self._send(200, {"path": req["result"]})

    def _scan(self, body):
        folder = (body.get("folder") or "").strip().strip('"')
        if not folder:
            raise app.JobError("폴더를 먼저 골라 주세요.")
        _, summary = app.scan_folder(folder, body.get("overrides") or None)
        State.roots.add(os.path.normcase(os.path.abspath(summary["out_dir"])))
        remember(folder=summary["folder"])
        return self._send(200, summary)

    def _start(self, body):
        with State.lock:
            if State.job["running"]:
                raise app.JobError("이미 만들고 있습니다.")
        folder = (body.get("folder") or "").strip().strip('"')
        mode = "full" if body.get("mode") == "full" else "test"
        options = body.get("options") or {}
        remember(folder=folder, options=options)
        threading.Thread(target=_job, args=(folder, mode, options, body.get("overrides") or None),
                         daemon=True).start()
        return self._send(200, {"ok": True})

    def _cancel(self, body):
        events.cancel()
        ff.kill_all()
        return self._send(200, {"ok": True})

    def _open(self, body):
        path = os.path.abspath(body.get("path") or "")
        norm = os.path.normcase(path)
        if not any(norm == r or norm.startswith(r + os.sep) for r in State.roots) or not os.path.exists(path):
            raise app.JobError("열 수 없는 경로입니다.")
        if os.name == "nt":
            os.startfile(path)
        return self._send(200, {"ok": True})

    def _quit(self, body):
        self._send(200, {"ok": True})
        events.cancel()
        ff.kill_all()
        State.stop.set()


def _free_port():
    for port in range(8765, 8790):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            try:
                s.bind(("127.0.0.1", port))
                return port
            except OSError:
                continue
    return 0


def _dialog_loop():
    """Folder dialogs must run on the main thread (Tk)."""
    root = None
    try:
        import tkinter as tk
        from tkinter import filedialog
        root = tk.Tk()
        root.withdraw()
        State.has_tk = True
    except Exception:  # noqa: BLE001 - no Tk: the page falls back to typing the path
        State.has_tk = False
    while not State.stop.is_set():
        try:
            req = State.dialogs.get(timeout=0.3)
        except queue.Empty:
            if root is not None:
                try:
                    root.update()
                except Exception:  # noqa: BLE001
                    pass
            continue
        path = None
        try:
            root.attributes("-topmost", True)
            root.lift()
            root.update()
            initial = req["initial"] if req["initial"] and os.path.isdir(req["initial"]) else None
            path = filedialog.askdirectory(parent=root, initialdir=initial, title="촬영 폴더를 고르세요",
                                           mustexist=True)
        except Exception:  # noqa: BLE001
            path = None
        req["result"] = os.path.normpath(path) if path else None
        req["event"].set()


def main():
    try:
        ff.find_tools()
    except ff.ToolMissing:
        State.ffmpeg_error = "FFmpeg가 설치되어 있지 않습니다. run_windows.bat 으로 다시 실행하면 자동으로 설치합니다."
    port = _free_port()
    httpd = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    httpd.daemon_threads = True
    port = httpd.server_address[1]
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    url = "http://127.0.0.1:%d/?t=%s" % (port, State.token)
    print("=" * 64)
    print(" 팟캐스트 자동 편집기")
    print("=" * 64)
    print(" 브라우저에 편집기 화면이 열립니다.")
    print(" 안 열리면 아래 주소를 브라우저 주소창에 붙여넣으세요:")
    print("   " + url)
    print("")
    print(" 이 검은 창을 닫으면 프로그램이 꺼집니다. (작업 중이면 작업도 멈춥니다)")
    print("=" * 64, flush=True)
    threading.Timer(0.8, lambda: webbrowser.open(url)).start()
    try:
        _dialog_loop()
    except KeyboardInterrupt:
        pass
    events.cancel()
    ff.kill_all()
    httpd.shutdown()
    os._exit(0)


PAGE = r"""<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>팟캐스트 자동 편집기</title>
<style>
:root {
  --bg: #f4f5f7; --card: #ffffff; --ink: #16181d; --muted: #6b7280; --line: #e3e5ea;
  --accent: #4b50e6; --accent-ink: #ffffff; --accent-soft: #eceeff; --alt: #0f766e;
  --warn: #b45309; --warn-soft: #fff4e5; --bad: #c62828; --bad-soft: #fdecec; --ok: #15803d;
  --field: #f7f8fa; --shadow: 0 1px 2px rgba(16,24,40,.06), 0 4px 16px rgba(16,24,40,.06);
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #111317; --card: #1a1d23; --ink: #eef0f4; --muted: #9aa1ad; --line: #2b3039;
    --accent: #7c80ff; --accent-ink: #0d0f14; --accent-soft: #252a4a; --alt: #2dd4bf;
    --warn: #fbbf24; --warn-soft: #3a2e12; --bad: #f87171; --bad-soft: #3b1d1d; --ok: #4ade80;
    --field: #14171c; --shadow: none;
  }
}
* { box-sizing: border-box; }
html, body { margin: 0; background: var(--bg); color: var(--ink);
  font: 15px/1.55 "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans KR", system-ui, sans-serif; }
header { position: sticky; top: 0; z-index: 5; display: flex; align-items: center; gap: 14px;
  padding: 14px 28px; background: var(--card); border-bottom: 1px solid var(--line); }
.brand { font-weight: 700; font-size: 17px; display: flex; align-items: center; gap: 10px; }
.dot { width: 12px; height: 12px; border-radius: 50%; background: var(--accent); box-shadow: 0 0 0 4px var(--accent-soft); }
header .sub { color: var(--muted); font-size: 13px; }
header .spacer { flex: 1; }
main { max-width: 980px; margin: 0 auto; padding: 24px 20px 80px; display: grid; gap: 18px; }
.card { background: var(--card); border: 1px solid var(--line); border-radius: 14px; padding: 20px 22px; box-shadow: var(--shadow); }
.card h2 { margin: 0 0 14px; font-size: 16px; display: flex; align-items: center; gap: 10px; }
.card h3 { font-size: 14px; margin: 18px 0 8px; }
.num { width: 24px; height: 24px; border-radius: 50%; background: var(--accent-soft); color: var(--accent);
  font-size: 13px; display: inline-flex; align-items: center; justify-content: center; }
.hidden { display: none !important; }
.row { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.between { justify-content: space-between; }
.muted { color: var(--muted); font-weight: 400; font-size: 13px; }
input[type=text] { flex: 1; min-width: 240px; padding: 10px 12px; border-radius: 9px; border: 1px solid var(--line);
  background: var(--field); color: var(--ink); font: inherit; }
input[type=text]:focus, select:focus { outline: 2px solid var(--accent); outline-offset: 1px; }
button { font: inherit; cursor: pointer; border-radius: 9px; border: 1px solid var(--line); background: var(--card);
  color: var(--ink); padding: 9px 14px; }
button:hover { border-color: var(--accent); }
button:disabled { opacity: .45; cursor: not-allowed; }
button.primary { background: var(--accent); border-color: var(--accent); color: var(--accent-ink); font-weight: 600; }
button.alt { background: var(--alt); border-color: var(--alt); }
button.danger { color: var(--bad); border-color: var(--bad); }
button.ghost { background: transparent; color: var(--muted); }
button.big { padding: 12px 20px; display: flex; flex-direction: column; align-items: flex-start; min-width: 200px; }
button.big small { font-weight: 400; opacity: .85; font-size: 12px; }
.chips { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px; }
.chip { font-size: 13px; padding: 5px 11px; border-radius: 999px; background: var(--field); border: 1px solid var(--line); }
.chip:hover { border-color: var(--accent); color: var(--accent); }
.note { padding: 10px 14px; border-radius: 10px; font-size: 14px; margin-top: 12px; }
.note.warn { background: var(--warn-soft); color: var(--warn); }
.note.bad { background: var(--bad-soft); color: var(--bad); }
.tablewrap { max-height: 360px; overflow: auto; border: 1px solid var(--line); border-radius: 10px; }
table { width: 100%; border-collapse: collapse; font-size: 14px; }
th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid var(--line); vertical-align: middle; }
th { position: sticky; top: 0; background: var(--field); font-weight: 600; color: var(--muted); font-size: 12px; }
tr:last-child td { border-bottom: 0; }
td.file { word-break: break-all; }
td.file small { display: block; color: var(--warn); }
tr.unknown td { background: var(--warn-soft); }
select { font: inherit; padding: 6px 8px; border-radius: 8px; border: 1px solid var(--line); background: var(--field); color: var(--ink); }
.counts { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
.count { font-size: 13px; padding: 4px 10px; border-radius: 8px; background: var(--accent-soft); color: var(--accent); }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 18px 26px; }
.field > label { display: block; font-size: 13px; color: var(--muted); margin-bottom: 7px; font-weight: 600; }
.seg { display: inline-flex; border: 1px solid var(--line); border-radius: 9px; overflow: hidden; }
.seg button { border: 0; border-radius: 0; border-right: 1px solid var(--line); padding: 7px 12px; background: var(--field); font-size: 14px; }
.seg button:last-child { border-right: 0; }
.seg button.on { background: var(--accent); color: var(--accent-ink); font-weight: 600; }
.w { display: grid; grid-template-columns: 78px 1fr 44px; align-items: center; gap: 10px; font-size: 14px; }
.w b { text-align: right; font-variant-numeric: tabular-nums; }
input[type=range] { width: 100%; accent-color: var(--accent); }
.switch { display: inline-flex; align-items: center; gap: 10px; cursor: pointer; font-size: 14px; }
.switch input { width: 18px; height: 18px; accent-color: var(--accent); }
.stages { display: flex; gap: 6px; margin: 16px 0 10px; flex-wrap: wrap; }
.stages span { font-size: 12px; padding: 4px 10px; border-radius: 999px; background: var(--field); color: var(--muted); border: 1px solid var(--line); }
.stages span.done { color: var(--ok); border-color: var(--ok); }
.stages span.now { background: var(--accent); color: var(--accent-ink); border-color: var(--accent); }
.bar { height: 12px; background: var(--field); border: 1px solid var(--line); border-radius: 999px; overflow: hidden; }
.bar > div { height: 100%; width: 0; background: var(--accent); transition: width .5s; }
#stageLabel { font-weight: 600; margin-top: 8px; }
pre { background: var(--field); border: 1px solid var(--line); border-radius: 10px; padding: 12px; font-size: 12px;
  max-height: 320px; overflow: auto; white-space: pre-wrap; word-break: break-all;
  font-family: Consolas, "D2Coding", ui-monospace, monospace; }
details { margin-top: 12px; }
summary { cursor: pointer; color: var(--muted); font-size: 13px; }
video, img.sheet { width: 100%; border-radius: 10px; background: #000; margin-bottom: 12px; display: block; }
img.sheet { background: transparent; border: 1px solid var(--line); }
.done-box { display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-radius: 10px; background: var(--accent-soft); margin-bottom: 14px; }
.overlay { position: fixed; inset: 0; background: var(--bg); display: flex; align-items: center; justify-content: center; font-size: 18px; z-index: 50; }
@media (max-width: 640px) { header { padding: 12px 16px; flex-wrap: wrap; } main { padding: 16px 12px 60px; } button.big { min-width: 0; flex: 1; } }
</style>
</head>
<body>
<header>
  <div class="brand"><span class="dot"></span>팟캐스트 자동 편집기</div>
  <div class="sub">싱크 · 마이크 믹스 · 색 맞춤 · 멀티캠 컷</div>
  <div class="spacer"></div>
  <button id="quit" class="ghost">프로그램 종료</button>
</header>
<main>
  <div id="fatal" class="note bad hidden"></div>

  <section class="card" id="s-folder">
    <h2><span class="num">1</span>촬영 폴더</h2>
    <div class="row">
      <input type="text" id="folder" placeholder="예: G:\딥마카이" spellcheck="false">
      <button id="pick">폴더 선택…</button>
      <button id="scan" class="primary">파일 확인</button>
    </div>
    <div id="recent" class="chips"></div>
    <div id="scanMsg" class="note hidden"></div>
  </section>

  <section class="card hidden" id="s-files">
    <h2><span class="num">2</span>파일 확인 <span id="fileSummary" class="muted"></span></h2>
    <div id="counts" class="counts"></div>
    <div class="tablewrap"><table>
      <thead><tr><th>파일</th><th>길이</th><th>영상</th><th>장비</th></tr></thead>
      <tbody id="files"></tbody>
    </table></div>
    <div id="unknownMsg" class="note warn hidden">노란 줄은 어느 장비인지 모르는 파일입니다. 오른쪽에서 골라 주세요.</div>
  </section>

  <section class="card hidden" id="s-opts">
    <h2><span class="num">3</span>설정</h2>
    <div class="grid">
      <div class="field">
        <label>카메라 화면 비중</label>
        <div class="w"><span>라이카 Q2</span><input type="range" min="0" max="100" id="w_leica"><b id="wv_leica"></b></div>
        <div class="w"><span>DJI 포켓</span><input type="range" min="0" max="100" id="w_pocket"><b id="wv_pocket"></b></div>
        <div class="w"><span>후지 X-T4</span><input type="range" min="0" max="100" id="w_fuji"><b id="wv_fuji"></b></div>
      </div>
      <div class="field">
        <label>메인 카메라 (색 기준)</label>
        <select id="main">
          <option value="leica">라이카 Q2</option><option value="pocket">DJI 포켓</option><option value="fuji">후지 X-T4</option>
        </select>
        <div style="height:14px"></div>
        <label style="display:block;font-size:13px;color:var(--muted);margin-bottom:7px;font-weight:600">해상도 (전체 편집본)</label>
        <div class="seg" data-name="height"><button data-v="0">원본 그대로</button><button data-v="1080">1080p (빠름)</button></div>
      </div>
      <div class="field">
        <label>최종 음량</label>
        <div class="seg" data-name="lufs"><button data-v="-14">유튜브 (-14)</button><button data-v="-16">팟캐스트 앱 (-16)</button></div>
      </div>
      <div class="field">
        <label>잡음 제거</label>
        <div class="seg" data-name="denoise"><button data-v="0">끔</button><button data-v="6">약하게</button><button data-v="10">보통</button><button data-v="15">강하게</button></div>
      </div>
      <div class="field">
        <label>소리</label>
        <label class="switch"><input type="checkbox" id="automix"> 오토믹서 (말하는 사람 마이크 위주)</label>
      </div>
      <div class="field">
        <label>색</label>
        <label class="switch"><input type="checkbox" id="color"> 메인 카메라 색에 맞추기</label>
      </div>
      <div class="field">
        <label>테스트 길이</label>
        <div class="seg" data-name="test_minutes"><button data-v="1">1분</button><button data-v="2">2분</button><button data-v="5">5분</button></div>
      </div>
    </div>
  </section>

  <section class="card hidden" id="s-run">
    <h2><span class="num">4</span>만들기</h2>
    <div class="row">
      <button id="runTest" class="primary big">테스트 만들기<small id="testHint">중간 2분만 · 싱크/색/소리 확인용</small></button>
      <button id="runFull" class="primary alt big">전체 편집본 만들기<small>오래 걸립니다</small></button>
      <button id="stop" class="danger hidden">중지</button>
    </div>
    <div id="prog" class="hidden">
      <div class="stages" id="stages">
        <span data-s="sync">싱크</span><span data-s="color">색</span><span data-s="audio">오디오</span><span data-s="render">영상</span><span data-s="mux">마무리</span>
      </div>
      <div class="bar"><div id="barfill"></div></div>
      <div class="row between"><span id="stageLabel"></span><span id="pct" class="muted"></span></div>
      <div id="jobMsg" class="note hidden"></div>
      <details id="logBox"><summary>자세한 진행 기록</summary><pre id="log"></pre></details>
    </div>
  </section>

  <section class="card hidden" id="s-result">
    <h2><span class="num">5</span>결과</h2>
    <div class="done-box"><b id="doneTitle">완성!</b><span id="doneInfo" class="muted"></span></div>
    <video id="video" controls preload="metadata"></video>
    <div class="row">
      <button id="openVideo">영상 파일 열기</button>
      <button id="openFolder">결과 폴더 열기</button>
    </div>
    <div id="sheetBox" class="hidden"><h3>색 맞춤 비교 (왼쪽 메인 카메라 · 가운데 전 · 오른쪽 후)</h3><img id="sheet" class="sheet" alt="색 맞춤 비교"></div>
    <details><summary>리포트 (싱크 결과 · 카메라 비중 · 음량)</summary><pre id="report"></pre></details>
  </section>
</main>
<script>
const T = new URLSearchParams(location.search).get("t");
const $ = (id) => document.getElementById(id);
const KINDS = [["leica","라이카 Q2"],["pocket","DJI 포켓 (+미니2 마이크)"],["fuji","후지 X-T4"],["mic","DJI Mic 3 녹음"],["skip","사용 안 함"]];
let files = [], overrides = {}, polling = null, logNext = 0, lastResult = null, defaults = {};

async function api(path, body) {
  const opt = { headers: { "X-Token": T } };
  if (body !== undefined) { opt.method = "POST"; opt.headers["Content-Type"] = "application/json"; opt.body = JSON.stringify(body); }
  const r = await fetch(path, opt);
  const data = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(data.error || ("오류 " + r.status));
  return data;
}
function fmtDur(s) { s = Math.round(s); const h = Math.floor(s/3600), m = Math.floor(s%3600/60), x = s%60;
  return (h ? h + ":" + String(m).padStart(2,"0") : m) + ":" + String(x).padStart(2,"0"); }
function show(id, on) { $(id).classList.toggle("hidden", !on); }
function msg(id, text, kind) { const el = $(id); el.textContent = text || ""; el.className = "note " + (kind || "") + (text ? "" : " hidden"); }
function fileUrl(p) { return "/file?t=" + encodeURIComponent(T) + "&p=" + encodeURIComponent(p) + "&v=" + Date.now(); }

function segSet(name, v) { document.querySelectorAll('.seg[data-name="' + name + '"] button').forEach(b => b.classList.toggle("on", String(b.dataset.v) === String(v))); }
function segGet(name) { const b = document.querySelector('.seg[data-name="' + name + '"] button.on'); return b ? Number(b.dataset.v) : null; }
document.querySelectorAll(".seg").forEach(seg => seg.addEventListener("click", e => {
  if (e.target.tagName !== "BUTTON") return; segSet(seg.dataset.name, e.target.dataset.v);
  if (seg.dataset.name === "test_minutes") $("testHint").textContent = "중간 " + e.target.dataset.v + "분만 · 싱크/색/소리 확인용";
}));
function weightsView() {
  const w = { leica: +$("w_leica").value, pocket: +$("w_pocket").value, fuji: +$("w_fuji").value };
  const present = new Set(files.filter(f => ["leica","pocket","fuji"].includes(kindOf(f))).map(kindOf));
  const tot = Object.entries(w).filter(([k]) => !present.size || present.has(k)).reduce((a, [, v]) => a + v, 0) || 1;
  for (const k of Object.keys(w)) $("wv_" + k).textContent = (!present.size || present.has(k)) ? Math.round(w[k] * 100 / tot) + "%" : "없음";
  return w;
}
["w_leica","w_pocket","w_fuji"].forEach(id => $(id).addEventListener("input", weightsView));

function applyOptions(o) {
  $("w_leica").value = o.weights.leica; $("w_pocket").value = o.weights.pocket; $("w_fuji").value = o.weights.fuji;
  $("main").value = o.main; segSet("height", o.height || 0); segSet("lufs", o.lufs); segSet("denoise", o.denoise);
  segSet("test_minutes", o.test_minutes); $("automix").checked = o.automix !== false; $("color").checked = o.color !== false;
  $("testHint").textContent = "중간 " + o.test_minutes + "분만 · 싱크/색/소리 확인용"; weightsView();
}
function collectOptions() {
  return { weights: weightsView(), main: $("main").value, height: segGet("height") || 0, lufs: segGet("lufs"),
    denoise: segGet("denoise"), test_minutes: segGet("test_minutes"), automix: $("automix").checked, color: $("color").checked };
}

function renderRecent(list) {
  const box = $("recent"); box.innerHTML = "";
  (list || []).forEach(f => { const b = document.createElement("button"); b.className = "chip"; b.textContent = f;
    b.onclick = () => { $("folder").value = f; scan(); }; box.appendChild(b); });
}

$("pick").onclick = async () => {
  $("pick").disabled = true;
  try {
    const r = await api("/api/pick", { initial: $("folder").value });
    if (r.unavailable) msg("scanMsg", "이 컴퓨터에서는 폴더 선택 창을 열 수 없습니다. 탐색기 주소창의 경로를 복사해서 붙여넣어 주세요.", "warn");
    else if (r.path) { $("folder").value = r.path; scan(); }
  } catch (e) { msg("scanMsg", e.message, "bad"); }
  $("pick").disabled = false;
};
$("scan").onclick = () => scan();
$("folder").addEventListener("keydown", e => { if (e.key === "Enter") scan(); });

function kindOf(f) { return overrides[f.rel] || f.kind; }
async function scan() {
  const folder = $("folder").value.trim();
  if (!folder) { msg("scanMsg", "폴더를 먼저 골라 주세요.", "warn"); return; }
  msg("scanMsg", "파일을 살펴보는 중…", ""); $("scan").disabled = true;
  try {
    const r = await api("/api/scan", { folder });
    $("folder").value = r.folder; files = r.files; overrides = {};
    renderFiles(); msg("scanMsg", "");
    show("s-files", true); show("s-opts", true); show("s-run", true);
    const s = await api("/api/state"); renderRecent(s.settings.recent);
  } catch (e) { msg("scanMsg", e.message, "bad"); }
  $("scan").disabled = false;
}
function renderFiles() {
  const tb = $("files"); tb.innerHTML = "";
  for (const f of files) {
    const tr = document.createElement("tr"); const k = kindOf(f);
    if (!k) tr.className = "unknown";
    const sel = document.createElement("select");
    if (!k) { const o = document.createElement("option"); o.value = ""; o.textContent = "골라 주세요"; sel.appendChild(o); }
    for (const [v, t] of KINDS) { const o = document.createElement("option"); o.value = v; o.textContent = t; sel.appendChild(o); }
    sel.value = k || "";
    sel.onchange = () => { overrides[f.rel] = sel.value; renderFiles(); };
    const td1 = document.createElement("td"); td1.className = "file"; td1.textContent = f.rel;
    if (f.note) { const sm = document.createElement("small"); sm.textContent = f.note; td1.appendChild(sm); }
    const td2 = document.createElement("td"); td2.textContent = fmtDur(f.duration);
    const td3 = document.createElement("td"); td3.textContent = f.video || "소리만"; td3.className = "muted";
    const td4 = document.createElement("td"); td4.appendChild(sel);
    tr.append(td1, td2, td3, td4); tb.appendChild(tr);
  }
  const counts = {}; let unknown = 0;
  for (const f of files) { const k = kindOf(f); if (!k) unknown++; else counts[k] = (counts[k] || 0) + 1; }
  const box = $("counts"); box.innerHTML = "";
  for (const [v, t] of KINDS) if (counts[v]) { const s = document.createElement("span"); s.className = "count"; s.textContent = t.split(" (")[0] + " " + counts[v] + "개"; box.appendChild(s); }
  $("fileSummary").textContent = "· 파일 " + files.length + "개";
  show("unknownMsg", unknown > 0);
  const hasMic = files.some(f => ["mic","pocket"].includes(kindOf(f)));
  const hasCam = files.some(f => ["leica","pocket","fuji"].includes(kindOf(f)));
  const ok = unknown === 0 && hasMic && hasCam;
  $("runTest").disabled = $("runFull").disabled = !ok || !!polling;
  weightsView();
}

async function start(mode) {
  if (mode === "full" && !confirm("전체 편집본을 만듭니다. 영상 길이와 컴퓨터 성능에 따라 몇 시간 걸릴 수 있습니다. 시작할까요?")) return;
  try {
    await api("/api/start", { folder: $("folder").value.trim(), mode, options: collectOptions(), overrides });
    $("log").textContent = ""; logNext = 0; show("s-result", false); msg("jobMsg", "");
    startPolling();
  } catch (e) { alert(e.message); }
}
$("runTest").onclick = () => start("test");
$("runFull").onclick = () => start("full");
$("stop").onclick = async () => { if (confirm("만드는 중인 작업을 멈출까요? 다시 만들면 끝난 부분은 건너뜁니다.")) await api("/api/cancel", {}); };

function startPolling() {
  show("prog", true); show("stop", true); $("runTest").disabled = $("runFull").disabled = true;
  if (polling) clearInterval(polling);
  polling = setInterval(poll, 1000); poll();
}
const ORDER = ["sync","color","audio","render","mux"];
async function poll() {
  let s;
  try { s = await api("/api/status?since=" + logNext); } catch (e) { return; }
  if (s.lines.length) { const pre = $("log"); pre.textContent += s.lines.join("\n") + "\n"; pre.scrollTop = pre.scrollHeight; }
  logNext = s.next;
  const j = s.job;
  const idx = ORDER.indexOf(s.stage);
  document.querySelectorAll("#stages span").forEach((el, i) => { el.classList.toggle("done", idx > i || (!j.running && j.result)); el.classList.toggle("now", idx === i && j.running); });
  const pct = j.result ? 100 : Math.round(s.overall * 100);
  $("barfill").style.width = pct + "%";
  $("stageLabel").textContent = j.running ? (s.label || "준비 중") + (s.stage === "render" ? " · " + Math.round(s.frac * 100) + "%" : "") : (j.result ? "완성" : (j.error ? "멈춤" : ""));
  $("pct").textContent = pct + "% · " + fmtDur(j.elapsed || 0) + " 경과";
  if (!j.running) {
    clearInterval(polling); polling = null; show("stop", false); renderFiles();
    if (j.error) msg("jobMsg", j.error, j.error.startsWith("중지") ? "warn" : "bad");
    if (j.result) showResult(j.result);
  }
}
function showResult(r) {
  lastResult = r;
  show("s-result", true);
  $("doneTitle").textContent = r.mode === "full" ? "전체 편집본 완성!" : "테스트 완성!";
  $("doneInfo").textContent = r.size + " · " + Math.max(1, Math.round(r.minutes)) + "분 걸림" + (r.mode === "test" ? " · 괜찮으면 '전체 편집본 만들기'를 누르세요" : "");
  $("video").src = fileUrl(r.video);
  if (r.sheet) { $("sheet").src = fileUrl(r.sheet); show("sheetBox", true); } else show("sheetBox", false);
  $("report").textContent = r.report_text;
  $("s-result").scrollIntoView({ behavior: "smooth", block: "start" });
}
$("openVideo").onclick = () => lastResult && api("/api/open", { path: lastResult.video }).catch(e => alert(e.message));
$("openFolder").onclick = () => lastResult && api("/api/open", { path: lastResult.dest }).catch(e => alert(e.message));
$("quit").onclick = async () => {
  if (polling && !confirm("작업 중입니다. 종료하면 작업이 멈춥니다. 종료할까요?")) return;
  try { await api("/api/quit", {}); } catch (e) {}
  document.body.innerHTML = '<div class="overlay">프로그램을 종료했습니다. 이 창을 닫으셔도 됩니다.</div>';
};

(async function init() {
  if (!T) { $("fatal").textContent = "run_windows.bat 으로 다시 열어 주세요."; show("fatal", true); return; }
  let s;
  try { s = await api("/api/state"); } catch (e) { $("fatal").textContent = "프로그램과 연결이 끊겼습니다. run_windows.bat 으로 다시 열어 주세요."; show("fatal", true); return; }
  defaults = s.defaults;
  if (s.ffmpeg_error) { $("fatal").textContent = s.ffmpeg_error; show("fatal", true); }
  const o = Object.assign({}, s.defaults, s.settings.options || {});
  o.weights = Object.assign({}, s.defaults.weights, (s.settings.options || {}).weights || {});
  applyOptions(o);
  renderRecent(s.settings.recent);
  $("folder").value = (s.settings.recent || [])[0] || s.default_folder;
  if (!s.has_tk) $("pick").title = "폴더 선택 창을 쓸 수 없어 경로를 직접 붙여넣어야 합니다";
  if (s.job.running || s.job.result || s.job.error) {
    await scan(); startPolling();
  }
})();
</script>
</body>
</html>
"""
