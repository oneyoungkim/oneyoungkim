# 자리 장면 '스토리보드 카드' 데이터(2026-10-25 대표 피드백 "뭔 스토리인지도 모르겠고 막 지나감")
# 사용: python tools/storyboard.py   → unity/.../Data/Story/m3_story.json 의 장면마다 "board" 를 채우고, 맨 위에 "intro"(1막 소개 카드)를 넣는다
# board = {"lines": 원고 흐름 3~5줄, "quotes": [[화자, 대사], …] 2~3개}
#   lines  = 장면 요약(1-4 표 summary)을 '→' 로 끊은 박자. 5개 넘으면 이웃끼리 ' · ' 로 묶고, 3개 안 되면 원고 초안(script2dlg)의 속마음 줄로 채움.
#            '(N단계)' 같은 제작 메모는 뺀다.
#   quotes = 요약 속 따옴표 대사를 원고 초안(Data/Story/dlg/draft/epNN_draft.tsv)에서 찾아 그 줄 그대로(화자 칸이 '?' 면 요약에서 대사 바로 앞 이름).
#            초안에 없으면 요약의 따옴표 글 그대로. 32자 넘는 대사는 뺀다(카드 한 줄).
# 이미 손으로 고친 board 가 있어도 덮어쓴다(원본은 요약·초안이라 그쪽을 고칠 것).
import csv, json, re, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.dont_write_bytecode = True
from storyboard_curated import CURATED   # 손으로 쓴 장면별 흐름·대사(있으면 그것을 씀)

ROOT = Path(__file__).resolve().parents[1]
STORY = ROOT / "unity/HaenginMainEvent/Assets/_Project/Data/Story/m3_story.json"
DRAFT = ROOT / "unity/HaenginMainEvent/Assets/_Project/Data/Story/dlg/draft"
NAMES = ["엄마", "할머니", "할아버지", "하늘", "태오", "민재", "임찬", "건우", "나리", "최사장", "곽반장", "형들", "담임", "도경민", "시우", "경비", "오대현"]

# 7단계 의상(Outfit): 새벽 배달(1-1~1-4)·노가다 현장(3-5·4-1~4-3)은 후드 + 주황 배달 조끼, 나머지는 교복
OUTFIT = {k: "배달복" for k in ("1-1", "1-2", "1-3", "1-4", "3-5", "4-1", "4-2", "4-3")}

# 8단계 배달 자전거(BikeCourse 다리 0·1·2 = 1-2·1-3·1-4): 시작 자리·방향·목표를 코스 정차점에 맞춤(Editor/Game/BikeSetup.cs RouteXZ 와 같은 값)
BIKE = {
    "1-2": {"pos": [-184.0, 29.3, 102.4], "yaw": 248, "summary": "주행 교습 — 앉아서 밟기·브레이크·조향. 후문 경비실 창 앞에 멈춰 건네기(× · E) → \"국밥\" → 도감 첫 칸",
            "targets": [{"label": "후문 경비실", "pos": [-202.4, 31.0, 95.9], "r": 2.4}]},
    "1-3": {"pos": [-202.4, 31.0, 95.9], "yaw": 285, "summary": "와룡공원길(7° 엉덩이 들기) → 와룡 오르막 자전거길(13~14° 일어서기 리듬 □ ○) · 태오 추월(측면 트래킹) → 꼭대기 집 앞 정차",
            "targets": [{"label": "꼭대기 집", "pos": [-263.3, 43.0, 125.6], "r": 2.4}]},
    "1-4": {"pos": [-263.3, 43.0, 125.6], "yaw": 184, "summary": "할아버지 \"어이. N분 늦었다\"(국물 70 이상이면 \"국은 안 식었네. 됐다.\") → 내리막에서 태오 앞지르기 \"쉬이익.\" → 성균관로 동쪽 끝",
            "targets": [{"label": "성균관로 동쪽 끝(소극장 방면)", "pos": [-134.5, 22.6, 85.0], "r": 3.5}]},
}

INTRO = {
    "title": "1막 「행인1」",
    "lines": [
        "누가 — 반시우, 17살. 혜성고 2학년 3반. 새벽엔 엄마 국밥집 배달, 교실에선 출석부에 이름이 안 불리는 '행인1'.",
        "어디서 — 서울 혜화동. 성대 후문 골목, 와룡공원 성곽길, 2026년 봄.",
        "무엇을 — 동생 하늘이의 열한 살 생일(5월 1일)까지 케이크값 4만 원을 봉투에 모은다. 대신 아무리 건드려도 '참는다'.",
        "그리고 — 참은 만큼 쌓인 것이 처음으로 터지는 날까지, 7화.",
    ],
    "note": "제작 중인 장면은 '스토리보드 카드'로 보인다(원고 요약과 핵심 대사). × · Enter · 마우스 왼쪽을 눌러야 넘어간다.",
}


def norm(t):
    return re.sub(r"[\s\.\,…!?~'\"“”‘’·\-—]", "", t.replace("\\n", ""))


def drafts(ep):
    p = DRAFT / f"ep{ep:02d}_draft.tsv"
    if not p.exists():
        return []
    with open(p, encoding="utf-8") as f:
        return [r for r in csv.DictReader(f, delimiter="\t")]


def clean(part):
    part = re.sub(r"\((?:[^()]*단계[^()]*)\)", "", part)
    part = re.sub(r"\s+", " ", part).strip(" .·")
    return part


def board(scene, rows):
    summ = scene.get("summary", "")
    parts = [clean(p) for p in re.split(r"\s*→\s*", summ) if clean(p)]
    while len(parts) > 5:
        # 가장 짧은 이웃 둘을 묶는다
        k = min(range(len(parts) - 1), key=lambda i: len(parts[i]) + len(parts[i + 1]))
        parts[k:k + 2] = [parts[k] + " · " + parts[k + 1]]
    quotes, used = [], set()
    for m in re.finditer(r'"([^"]+)"', summ):
        q = m.group(1).strip().strip("'")
        if len(quotes) >= 3:
            break
        before = summ[max(0, m.start() - 12):m.start()]
        who = next((n for n in NAMES if n in before.split("→")[-1]), "")
        hit = None
        nq = norm(q)
        if len(nq) >= 2:
            for r in rows:
                nt = norm(r.get("대사", ""))
                if nq and (nq in nt or (len(nt) >= 4 and nt in nq)):
                    hit = r
                    break
        if hit is not None:
            text = hit["대사"].replace("\\n", " ").strip()
            w = hit.get("화자", "").strip()
            if w in ("", "?"):
                w = who
            if hit.get("표시") == "속마음" and not who:
                w = "시우(속마음)"
        else:
            text, w = q, who
        if len(text) > 32 or norm(text) in used:
            continue
        used.add(norm(text))
        quotes.append([w, text])
    if len(parts) < 3 and quotes and rows:
        # 첫 대사가 나온 자리 뒤의 속마음 줄로 채움
        idx = next((i for i, r in enumerate(rows) if norm(quotes[0][1]) in norm(r.get("대사", ""))), -1)
        for r in rows[idx + 1:idx + 12] if idx >= 0 else []:
            if len(parts) >= 3:
                break
            if r.get("표시") == "속마음" and 6 <= len(r["대사"]) <= 40:
                parts.append("(시우) " + r["대사"].replace("\\n", " "))
    return {"lines": parts[:5], "quotes": quotes[:3]}


def dump(d):
    """원래 파일 모양 그대로: 맨 위 키는 한 줄씩, 목록은 원소 하나가 한 줄(diff 가 작게)"""
    c = lambda v: json.dumps(v, ensure_ascii=False, separators=(", ", ": "))
    out = ["{"]
    keys = list(d.keys())
    for i, k in enumerate(keys):
        v = d[k]
        tail = "," if i < len(keys) - 1 else ""
        if isinstance(v, list) and v:
            out.append(f'  "{k}": [')
            for j, e in enumerate(v):
                out.append("    " + c(e) + ("," if j < len(v) - 1 else ""))
            out.append("  ]" + tail)
        else:
            out.append(f'  "{k}": {c(v)}{tail}')
    out.append("}")
    return "\n".join(out) + "\n"


def main():
    d = json.loads(STORY.read_text(encoding="utf-8"))
    cache = {}
    n = 0
    for s in d["scenes"]:
        if s.get("kind") == "card":
            continue
        ep = int(s.get("ep", 1))
        rows = cache.setdefault(ep, drafts(ep))
        if s["id"] in CURATED:
            lines, quotes = CURATED[s["id"]]
            s["board"] = {"lines": lines, "quotes": [list(q) for q in quotes]}
        else:
            s["board"] = board(s, rows)
        n += 1
    for s in d["scenes"]:
        if s["id"] in OUTFIT:
            s["outfit"] = OUTFIT[s["id"]]
        if s["id"] in BIKE:
            s.update(BIKE[s["id"]])
        if s["id"] == "1-6":
            s["spawn"] = "BackDoor"     # 1화 교실(CastSetup): 뒷문 앞에서 출석을 듣는다
    d["intro"] = INTRO
    STORY.write_text(dump(d), encoding="utf-8", newline="\n")
    print(f"[storyboard] 장면 {n}개 board + 1막 소개 → {STORY.name}")
    if "-v" in sys.argv:
        for s in d["scenes"]:
            if "board" in s:
                print(s["id"], s["title"])
                for l in s["board"]["lines"]:
                    print("   ·", l)
                for w, t in s["board"]["quotes"]:
                    print("   ", w or "—", ":", t)


if __name__ == "__main__":
    main()
