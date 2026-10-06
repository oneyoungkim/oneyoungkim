# 행인1의 메인이벤트 — 입력 에셋(HInput.inputactions)에 M3 입력 맵을 넣는다(docs/09_M3_버티컬슬라이스_설계.md 2-2·2-10).
# 사용: python tools/input_maps.py [HInput.inputactions 경로]
# - Talk 맵(대화·컷신·선택지): Next(× · Enter · Space · 마우스 왼쪽) · Skip(길게 — Select/View · Tab) · Navigate(십자키·왼스틱·방향키·WS) · Pause(Esc · Options)
#   '길게 0.8초 건너뛰기'는 Next 를 누르고 있어도 되고 Skip 을 누르고 있어도 된다(DialogueRunner 가 시간을 잰다).
# 이미 있으면 그 맵을 지우고 같은 내용으로 다시 넣는다. id 는 uuid5(맵·액션·바인딩 이름)라 몇 번을 돌려도 파일이 같다.
import json
import sys
import uuid
from pathlib import Path

NS = uuid.UUID("6f1d2c4e-8b0a-4d6e-9a51-3c2b1e0f7a90")


def uid(*parts):
    return str(uuid.uuid5(NS, "/".join(parts)))


def action(m, name, typ, ctrl, initial=False):
    return {"name": name, "type": typ, "id": uid(m, "action", name), "expectedControlType": ctrl,
            "processors": "", "interactions": "", "initialStateCheck": initial}


def bind(m, act, path, group, name="", composite=False, part=False, k=0):
    return {"name": name, "id": uid(m, "bind", act, path, name, str(k)), "path": path, "interactions": "", "processors": "",
            "groups": group, "action": act, "isComposite": composite, "isPartOfComposite": part}


def talk_map():
    m = "Talk"
    acts = [action(m, "Next", "Button", "Button"), action(m, "Skip", "Button", "Button"),
            action(m, "Navigate", "Value", "Vector2", True), action(m, "Pause", "Button", "Button")]
    kb, gp = "Keyboard&Mouse", "Gamepad"
    b = [
        bind(m, "Next", "<Gamepad>/buttonSouth", gp), bind(m, "Next", "<Keyboard>/enter", kb), bind(m, "Next", "<Keyboard>/numpadEnter", kb),
        bind(m, "Next", "<Keyboard>/space", kb), bind(m, "Next", "<Keyboard>/e", kb), bind(m, "Next", "<Mouse>/leftButton", kb),
        bind(m, "Skip", "<Gamepad>/select", gp), bind(m, "Skip", "<Keyboard>/tab", kb),
        bind(m, "Navigate", "<Gamepad>/dpad", gp), bind(m, "Navigate", "<Gamepad>/leftStick", gp),
        bind(m, "Navigate", "2DVector", "", "Arrows", composite=True),
        bind(m, "Navigate", "<Keyboard>/upArrow", kb, "up", part=True), bind(m, "Navigate", "<Keyboard>/downArrow", kb, "down", part=True),
        bind(m, "Navigate", "<Keyboard>/leftArrow", kb, "left", part=True), bind(m, "Navigate", "<Keyboard>/rightArrow", kb, "right", part=True),
        bind(m, "Navigate", "2DVector", "", "WASD", composite=True, k=1),
        bind(m, "Navigate", "<Keyboard>/w", kb, "up", part=True), bind(m, "Navigate", "<Keyboard>/s", kb, "down", part=True),
        bind(m, "Navigate", "<Keyboard>/a", kb, "left", part=True), bind(m, "Navigate", "<Keyboard>/d", kb, "right", part=True),
        bind(m, "Pause", "<Keyboard>/escape", kb), bind(m, "Pause", "<Gamepad>/start", gp),
    ]
    return {"name": m, "id": uid(m, "map"), "actions": acts, "bindings": b}


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "unity/HaenginMainEvent/Assets/_Project/Input/HInput.inputactions"
    d = json.loads(path.read_text(encoding="utf-8"))
    maps = [x for x in d["maps"] if x["name"] != "Talk"]
    maps.append(talk_map())
    d["maps"] = maps
    text = json.dumps(d, ensure_ascii=False, indent=4)
    path.write_text(text, encoding="utf-8", newline="\n")
    print(f"[input_maps] {path.name}: 맵 {', '.join(x['name'] for x in maps)}")


if __name__ == "__main__":
    main()
