# 잎 카드 알파 텍스처: 흰 배경 참조(GPT Image 2.5) → 흰색 키잉 → RGBA PNG (3차 패스 2026-10-24)
#   python tools/env_assets/leaf_cards.py  → unity/.../Art/Env/Textures/Leaf_{pine,broad,cherry}.png (1024², 알파, 가장자리 색 번짐 제거)
import os, sys
from PIL import Image, ImageFilter
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "concept", "art", "env", "veg")
OUT = os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Art", "Env", "Textures")

def key(path, out, size=1024):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255.0
    # 흰색과의 거리 → 알파(흰 배경 0, 잎 1), 부드러운 문턱
    dist = np.sqrt(((1.0 - a) ** 2).sum(axis=2) / 3.0)
    alpha = np.clip((dist - 0.06) / 0.14, 0.0, 1.0)
    # 가장자리 흰 번짐 제거: 알파가 낮은 화소의 색을 이웃 불투명 색으로(간단히: 색을 알파로 나눠 되돌리기)
    rgb = a.copy()
    inv = np.clip(alpha, 0.05, 1.0)[..., None]
    rgb = np.clip((rgb - (1.0 - inv)) / inv, 0.0, 1.0)
    rgba = np.concatenate([rgb, alpha[..., None]], axis=2)
    img = Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")
    # 내용이 차지하는 사각형으로 자르고 정사각형 여백
    bbox = Image.fromarray((alpha * 255).astype(np.uint8)).getbbox()
    if bbox:
        img = img.crop(bbox)
    w, h = img.size; s = max(w, h)
    sq = Image.new("RGBA", (s, s), (0, 0, 0, 0)); sq.paste(img, ((s - w) // 2, (s - h) // 2))
    sq = sq.resize((size, size), Image.LANCZOS)
    sq.save(out)
    print("wrote", out, "alpha coverage %.0f%%" % (100.0 * (np.asarray(sq)[..., 3] > 127).mean()))

def main():
    os.makedirs(OUT, exist_ok=True)
    for kind in ("pine", "broad", "cherry"):
        src = os.path.join(SRC, f"leaf_card_{kind}.png")
        if not os.path.exists(src):
            print("missing", src); continue
        key(src, os.path.join(OUT, f"Leaf_{kind}.png"))

main()
