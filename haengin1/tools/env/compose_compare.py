# 맵 아트 패스 확인 사진 붙이기 (docs/11 7장 11 · docs/10 P3)
#   python tools/env/compose_compare.py <EnvSetup.Shots 출력 폴더> <결과 폴더>
#   → compare.jpg : 키트 미리 보기 8곳 + 밤 2곳 = 10줄 × [Unity 게임 카메라 | 스타일 프레임(목표 그림)]
#   → tod.jpg     : 시간대 4종(새벽·낮·노을·밤) × 3곳(상가거리·주차장·와룡 오르막)
import os, sys
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FRAMES = os.path.join(ROOT, "concept", "art", "env", "frames")
PAIRS = [  # (게임 사진, 자리 이름, 스타일 프레임)
    ("shot_1", "1 후문 상가거리(낮)", "f01_hoomun_street_night"),
    ("shot_2", "2 뒷골목 주차장(노을)", "f02_parking_sunset"),
    ("shot_3", "3 골목 계단(새벽)", "f03_alley_stairs_dawn"),
    ("shot_4", "4 와룡 오르막(새벽)", "f05_waryong_climb_dawn"),
    ("shot_5", "5 성곽길(낮)", "f06_fortress_trail_day"),
    ("shot_6", "6 한옥 골목(낮)", "f04_siwoo_house_morning"),
    ("shot_7", "7 명륜3가 골목(낮)", "f03_alley_stairs_dawn"),
    ("shot_8", "8 성곽 아래 빈집(낮)", "f04_siwoo_house_morning"),
    ("shot_1_night", "1 후문 상가거리(밤)", "f01_hoomun_street_night"),
    ("shot_7_night", "7 명륜3가 골목(밤)", "f08_convenience_night"),
]
TOD = [("1", "후문 상가거리"), ("2", "뒷골목 주차장"), ("4", "와룡 오르막")]
PARTS = [("Dawn", "새벽"), ("Day", "낮"), ("Dusk", "노을"), ("Night", "밤")]

def font(size):
    for f in (r"C:\Windows\Fonts\malgun.ttf", r"C:\Windows\Fonts\NotoSansKR-Regular.otf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()

def fit(path, w, h):
    im = Image.open(path).convert("RGB")
    return im.resize((w, h), Image.LANCZOS)

def main():
    shots, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    W, H, G, T = 800, 450, 8, 26
    f = font(16)
    sheet = Image.new("RGB", (2 * W + 3 * G, len(PAIRS) * (H + T + G) + G), (36, 36, 36))
    dr = ImageDraw.Draw(sheet)
    for i, (shot, name, frame) in enumerate(PAIRS):
        y = G + i * (H + T + G)
        dr.text((G + 4, y + 4), f"{name} — Unity 게임 카메라", fill=(240, 240, 240), font=f)
        dr.text((2 * G + W + 4, y + 4), f"스타일 프레임 {frame}", fill=(240, 240, 240), font=f)
        sp = os.path.join(shots, shot + ".png")
        if os.path.exists(sp):
            sheet.paste(fit(sp, W, H), (G, y + T))
        fp = os.path.join(FRAMES, frame + ".png")
        if os.path.exists(fp):
            sheet.paste(fit(fp, W, H), (2 * G + W, y + T))
    sheet.save(os.path.join(out, "compare.jpg"), quality=86)
    w2, h2 = 640, 360
    tod = Image.new("RGB", (4 * w2 + 5 * G, len(TOD) * (h2 + T + G) + G), (36, 36, 36))
    dr = ImageDraw.Draw(tod)
    for r, (sp, nm) in enumerate(TOD):
        y = G + r * (h2 + T + G)
        for c, (part, pn) in enumerate(PARTS):
            x = G + c * (w2 + G)
            dr.text((x + 4, y + 4), f"{nm} — {pn}", fill=(240, 240, 240), font=f)
            p = os.path.join(shots, f"tod_{sp}_{part}.png")
            if os.path.exists(p):
                tod.paste(fit(p, w2, h2), (x, y + T))
    tod.save(os.path.join(out, "tod.jpg"), quality=86)
    print("wrote", os.path.join(out, "compare.jpg"), sheet.size, "and tod.jpg", tod.size)

main()
