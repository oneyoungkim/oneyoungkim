# 간판 면 그림 다듬기(5차 패스 2026-10-25): concept/art/env/signs5/<이름>.png(GPT Image 2.5, 투명 배경) →
#   unity/.../Art/Env/Signs5/Face_<이름>.png(알파 문턱 사각형으로 자르고 가장자리 반투명 정리, 긴 변 ≤ 2048) + faces.json(가로/세로 비)
#   python tools/env_assets/sign_faces.py [이름 ...]
# 불투명 그림(cyl_·led_)은 그대로(LED 는 검은 위아래를 잘라 띠만).
import json, os, sys
from PIL import Image
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "concept", "art", "env", "signs5")
OUT = os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Art", "Env", "Signs5")


def prep(name):
    src = os.path.join(SRC, name + ".png")
    im = Image.open(src).convert("RGBA")
    a = np.asarray(im)
    if name.startswith("led_"):
        lum = a[..., :3].max(axis=2)
        rows = np.where((lum > 60).sum(axis=1) > a.shape[1] * 0.02)[0]
        y0, y1 = max(0, rows.min() - 12), min(a.shape[0], rows.max() + 12)
        im = im.crop((0, y0, a.shape[1], y1))
        im.putalpha(255)
    elif name.startswith("cyl_") or name.startswith("banner_") or name.startswith("int_") or name == "mural":
        im.putalpha(255)
    elif name.startswith("decals"):
        pass   # 3×3 아틀라스 — 칸 위치를 지키려고 자르지 않음
    else:
        al = a[..., 3]
        ys, xs = np.where(al > 128)
        if len(xs):
            im = im.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
        # 가장자리 반투명 화소의 색을 안쪽 색으로(밉맵에서 테두리가 검게 번지지 않게) — 알파는 0/255 로 또렷하게
        b = np.asarray(im).copy()
        solid = b[..., 3] > 128
        b[..., 3] = np.where(solid, 255, 0).astype(np.uint8)
        im = Image.fromarray(b, "RGBA")
    w, h = im.size
    s = min(1.0, 2048.0 / max(w, h))
    if s < 1.0:
        im = im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, "Face_%s.png" % name)
    im.save(dst)
    c = np.asarray(im.convert("RGBA")).reshape(-1, 4).astype(np.float32)
    c = c[c[:, 3] > 128][:, :3] / 255.0
    avg = c.mean(axis=0) if len(c) else np.array([1.0, 0.9, 0.7])
    hi = c[c.max(axis=1) > np.percentile(c.max(axis=1), 80)].mean(axis=0) if len(c) else avg   # 밝은 쪽 20% = 조명 상자 빛 색
    return dict(w=im.size[0], h=im.size[1], aspect=round(im.size[0] / im.size[1], 4),
                avg=[round(float(v), 3) for v in avg], glow=[round(float(v), 3) for v in hi])


def main():
    names = sys.argv[1:] or sorted(os.path.splitext(f)[0] for f in os.listdir(SRC) if f.endswith(".png"))
    meta_path = os.path.join(OUT, "faces.json")
    meta = {}
    if os.path.exists(meta_path):
        meta = json.load(open(meta_path, encoding="utf-8"))
    for n in names:
        meta[n] = prep(n)
        print(n, meta[n])
    json.dump(meta, open(meta_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)


main()
