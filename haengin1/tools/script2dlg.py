# 행인1의 메인이벤트 — 원고(.md) → 대사 TSV 초안 (docs/09_M3_버티컬슬라이스_설계.md 2-2 '대사 데이터')
# 사용: python tools/script2dlg.py <원고.md> [출력.tsv]      (출력 생략 = 표준 출력)
#       python tools/script2dlg.py --all [출력 폴더]          (story/시우루트/0N화_*.md 전부 → epNN_draft.tsv)
# 원고 본문을 장면 표시 '＊' 단위로 자르고(장면 번호 s1, s2 …), 따옴표 대사 = 자막, 그 밖의 서술 문장 = 속마음으로 한 줄씩 뽑는다.
# '---' 또는 '게임 연출 노트' 아래는 읽지 않는다. 제목(#)·굵은 줄(**)·빈 줄은 건너뛴다.
# 화자는 바로 앞 서술 줄에서 아는 이름을 찾아 추정(못 찾으면 '?'). 32자 넘는 줄은 띄어쓰기에서 '\n' 으로 두 줄로 나누고,
# 두 줄(64자)로도 안 되면 메모에 '길다 N자'를 남긴다 — 사람이 09 1-6 원칙(고쳐 쓰지 않고 고른다, 장면당 3~8줄, 속마음 40% 안쪽)으로 줄여 확정한다.
# 결과는 초안이다: 확정 TSV(Data/Story/dlg/epNN.tsv)는 장면 id(1-1 …)로 다시 번호를 붙여 사람이 만든다.
import re
import sys
from pathlib import Path

NAMES = ["엄마", "하늘", "민재", "임찬", "건우", "태오", "강태오", "황민재", "곽반장", "곽춘식", "최사장", "나리", "유나리", "도경민", "할머니", "할아버지", "고준", "오대현", "아빠", "형"]
HEAD = "id\t화자\t표정\t표시\t대사\t대기\t카메라\t메모"
MAX = 32


def wrap(text):
    """32자 넘으면 띄어쓰기에서 두 줄로. 돌려주는 값: (글, 메모)"""
    if len(text) <= MAX:
        return text, ""
    best = -1
    for i, ch in enumerate(text):
        if ch == " " and i <= MAX and len(text) - i - 1 <= MAX:
            if best < 0 or abs(i - len(text) / 2) < abs(best - len(text) / 2):
                best = i
    if best > 0:
        return text[:best] + "\\n" + text[best + 1:], ""
    return text, f"길다 {len(text)}자"


def sentences(line):
    # 서술 줄을 문장으로(마침표·물음표·느낌표·말줄임 뒤 띄어쓰기)
    parts = re.split(r"(?<=[.?!…])\s+", line.strip())
    return [p for p in parts if p]


def speaker(prev):
    if not prev:
        return "?"
    for n in sorted(NAMES, key=len, reverse=True):
        if n in prev:
            return {"강태오": "태오", "황민재": "민재", "곽춘식": "곽반장", "유나리": "나리"}.get(n, n)
    return "?"


def convert(md: Path):
    ep = re.match(r"(\d+)화", md.name)
    epn = int(ep.group(1)) if ep else 0
    rows = []
    scene, n, prev = 1, 0, ""
    for raw in md.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if line.startswith("---") or "게임 연출 노트" in line:
            break
        if not line or line.startswith("#") or (line.startswith("**") and line.endswith("**")):
            continue
        if line == "＊":
            scene += 1
            n = 0
            prev = ""
            continue
        quotes = re.findall(r"[\"“]([^\"”]+)[\"”]", line)
        if quotes and line.lstrip().startswith(("\"", "“")):
            for q in quotes:
                n += 1
                t, memo = wrap(q.strip())
                rows.append((f"{epn}.s{scene}.{n:02d}", speaker(prev), "", "자막", t, "0", "", memo))
            continue
        for s in sentences(line):
            n += 1
            t, memo = wrap(s)
            rows.append((f"{epn}.s{scene}.{n:02d}", "시우", "", "속마음", t, "0", "", memo))
        prev = line
    return rows


def write(rows, out):
    text = HEAD + "\n" + "\n".join("\t".join(r) for r in rows) + "\n"
    if out is None:
        sys.stdout.write(text)
    else:
        Path(out).parent.mkdir(parents=True, exist_ok=True)
        Path(out).write_text(text, encoding="utf-8", newline="\n")


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__ or "사용: python tools/script2dlg.py <원고.md> [출력.tsv] | --all [폴더]")
        sys.exit(1)
    if args[0] == "--all":
        root = Path(__file__).resolve().parents[1]
        out_dir = Path(args[1]) if len(args) > 1 else root / "unity/HaenginMainEvent/Assets/_Project/Data/Story/dlg/draft"
        total = 0
        for md in sorted((root / "story/시우루트").glob("0[1-9]화_*.md")):
            rows = convert(md)
            ep = int(re.match(r"(\d+)화", md.name).group(1))
            write(rows, out_dir / f"ep{ep:02d}_draft.tsv")
            long = sum(1 for r in rows if r[7])
            inner = sum(1 for r in rows if r[3] == "속마음")
            print(f"[script2dlg] {md.name}: {len(rows)}줄(자막 {len(rows) - inner} · 속마음 {inner}) · 장면 {max((int(r[0].split('.')[1][1:]) for r in rows), default=0)}개 · 64자 넘음 {long}줄")
            total += len(rows)
        print(f"[script2dlg] 합계 {total}줄 → {out_dir}")
        return
    rows = convert(Path(args[0]))
    write(rows, args[1] if len(args) > 1 else None)


if __name__ == "__main__":
    main()
