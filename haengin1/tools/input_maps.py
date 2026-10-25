# 행인1의 메인이벤트 — 입력 에셋(HInput.inputactions)에 M3 입력 맵을 넣는다(docs/09_M3_버티컬슬라이스_설계.md 2-2·2-10).
# 사용: python tools/input_maps.py [HInput.inputactions 경로]
# - Bike 맵(2026-10-25, 8단계 배달 자전거): Steer(왼스틱 x·A D·← →) · Pedal(R2·W·↑, 아날로그) · Brake(L2·S·↓) · RhythmL(□·J·마우스 왼쪽) · RhythmR(○·K·마우스 오른쪽)
#   · Handover(×·E·Space — 멈춰 있을 때만) · LookPad(오른스틱) · LookMouse · Pause(Esc·Options). 내리기는 없다
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


def bike_map():
    """Bike 맵(docs/09 2-6, 8단계 배달 자전거): 조향·페달·브레이크·일어서기 리듬 □○·건네기·시점·일시정지. 내리기는 없다."""
    m = "Bike"
    acts = [action(m, "Steer", "Value", "Axis", True), action(m, "Pedal", "Value", "Axis", True), action(m, "Brake", "Value", "Axis", True),
            action(m, "RhythmL", "Button", "Button"), action(m, "RhythmR", "Button", "Button"), action(m, "Handover", "Button", "Button"),
            action(m, "LookPad", "Value", "Vector2", True), {**action(m, "LookMouse", "PassThrough", "Vector2"), "type": "PassThrough"},
            action(m, "Pause", "Button", "Button")]
    kb, gp = "Keyboard&Mouse", "Gamepad"
    b = [
        bind(m, "Steer", "<Gamepad>/leftStick/x", gp),
        bind(m, "Steer", "1DAxis", "", "AD", composite=True),
        bind(m, "Steer", "<Keyboard>/a", kb, "negative", part=True), bind(m, "Steer", "<Keyboard>/d", kb, "positive", part=True),
        bind(m, "Steer", "1DAxis", "", "Arrows", composite=True, k=1),
        bind(m, "Steer", "<Keyboard>/leftArrow", kb, "negative", part=True), bind(m, "Steer", "<Keyboard>/rightArrow", kb, "positive", part=True),
        bind(m, "Pedal", "<Gamepad>/rightTrigger", gp), bind(m, "Pedal", "<Keyboard>/w", kb), bind(m, "Pedal", "<Keyboard>/upArrow", kb),
        bind(m, "Brake", "<Gamepad>/leftTrigger", gp), bind(m, "Brake", "<Keyboard>/s", kb), bind(m, "Brake", "<Keyboard>/downArrow", kb),
        bind(m, "RhythmL", "<Gamepad>/buttonWest", gp), bind(m, "RhythmL", "<Keyboard>/j", kb), bind(m, "RhythmL", "<Mouse>/leftButton", kb),
        bind(m, "RhythmR", "<Gamepad>/buttonEast", gp), bind(m, "RhythmR", "<Keyboard>/k", kb), bind(m, "RhythmR", "<Mouse>/rightButton", kb),
        bind(m, "Handover", "<Gamepad>/buttonSouth", gp), bind(m, "Handover", "<Keyboard>/e", kb), bind(m, "Handover", "<Keyboard>/space", kb),
        bind(m, "LookPad", "<Gamepad>/rightStick", gp), bind(m, "LookMouse", "<Mouse>/delta", kb),
        bind(m, "Pause", "<Keyboard>/escape", kb), bind(m, "Pause", "<Gamepad>/start", gp),
    ]
    return {"name": m, "id": uid(m, "map"), "actions": acts, "bindings": b}


def add_kick(d):
    """Combat 맵에 Kick(docs/12 4-1 'R2 킥' 예약): R2 는 M2 달리기(Run)가 쓰므로 패드 L2 + 키보드 R 로 둔다(대표 결정 대기). 이미 있으면 그대로."""
    for m in d["maps"]:
        if m["name"] != "Combat":
            continue
        if any(a["name"] == "Kick" for a in m["actions"]):
            return False
        mn = m["name"]
        m["actions"].append(action(mn, "Kick", "Button", "Button"))
        m["bindings"] += [bind(mn, "Kick", "<Gamepad>/leftTrigger", "Gamepad"), bind(mn, "Kick", "<Keyboard>/r", "Keyboard&Mouse")]
        return True
    return False


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "unity/HaenginMainEvent/Assets/_Project/Input/HInput.inputactions"
    d = json.loads(path.read_text(encoding="utf-8"))
    maps = [x for x in d["maps"] if x["name"] not in ("Talk", "Bike")]
    maps.append(talk_map())
    maps.append(bike_map())
    d["maps"] = maps
    kick = add_kick(d)
    text = json.dumps(d, ensure_ascii=False, indent=4)
    path.write_text(text, encoding="utf-8", newline="\n")
    print(f"[input_maps] {path.name}: 맵 {', '.join(x['name'] for x in maps)} · Combat/Kick {'추가' if kick else '있음'}")


if __name__ == "__main__":
    main()
