# -*- coding: utf-8 -*-
"""재질 이름 규약. 이름이 곧 재질 슬롯 이름이고, Unity 에서는 concept/art/env/textures/<이름>.png 를 붙인다.

color : 기본색(sRGB). docs/10_맵_아트패스.md 5장 팔레트 그대로(미리 보기에서 텍스처가 없을 때 이 색으로 칠함)
tile  : 텍스처 한 장이 덮는 실제 크기(m). concept/art/env/textures/textures.json 의 meters 를 읽어 덮어쓴다(그 파일이 기준).
        메시 UV 는 월드 미터라 Unity 재질 Tiling = 1 / tile
uv    : 'world' = 월드 미터 UV(타일 텍스처) / 'unit' = 면마다 0~1(간판·창문 불)
owner : 'conv' = 처음 정한 규약 21개 / 'art' = 아트 작업자가 더한 이름(docs/10) / 'kit' = 건물 키트가 더한 이름(docs/11)
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))   # haengin1/
TEX_DIR = os.path.join(ROOT, "concept", "art", "env", "textures")

MATS = {
    # ---- 규약 21개(색 = docs/10 5장)
    "M_Brick_Red":     dict(color="#8E4A3A", tile=1.0, uv="world", owner="conv", note="빨간 벽돌(다세대 60%·담장)"),
    "M_Brick_Brown":   dict(color="#6E4B3A", tile=1.0, uv="world", owner="conv", note="고동 벽돌(다세대 40%·하숙·상가 20%)"),
    "M_Plaster_White": dict(color="#E8E2D6", tile=2.0, uv="world", owner="conv", note="흰 회벽(한옥 윗벽)·미장 띠·옥탑"),
    "M_Plaster_Cream": dict(color="#E3D5B8", tile=2.0, uv="world", owner="conv", note="크림 뿜칠(단독·담장·학교)·층 띠"),
    "M_Tile_Beige":    dict(color="#CDBFA6", tile=1.0, uv="world", owner="conv", note="상가 외벽 타일 20×10cm"),
    "M_Concrete":      dict(color="#B9B6AF", tile=2.4, uv="world", owner="conv", note="옹벽·기초·갓돌·필로티·학교 층 띠·전봇대"),
    "M_Granite":       dict(color="#CFC6B4", tile=3.0, uv="world", owner="conv", note="성곽(LOD1·2 면 — 성돌 5단 그림)·미석·옥개석·교문 기둥·상가 1층 마감"),
    "M_Giwa_Dark":     dict(color="#3A3A3A", tile=1.2, uv="world", owner="conv", note="기와 지붕 면·마루"),
    "M_Wood_Red":      dict(color="#9B4A3A", tile=1.0, uv="world", owner="conv", note="바랜 주칠(한옥 기둥·창살·서까래 끝·정자)"),
    "M_Wood_Dark":     dict(color="#4A3A30", tile=1.0, uv="world", owner="conv", note="짙은 나무(대문·풍판·마루·처마 안)"),
    "M_Metal_Green":   dict(color="#4F6B58", tile=1.0, uv="world", owner="conv", note="철대문·현관 철문·외부 철계단·캠퍼스 펜스·난간"),
    "M_Metal_Rust":    dict(color="#8A5A3C", tile=1.0, uv="world", owner="conv", note="녹슨 철(오래된 철대문·자물쇠·암문 띠쇠)"),
    "M_Metal_Grey":    dict(color="#8C8F93", tile=1.0, uv="world", owner="conv", note="상가 새시·셔터 박스·간판 틀·빗물관·실외기 받침"),
    "M_Glass_Dark":    dict(color="#2E3A44", tile=1.0, uv="world", owner="conv", note="낮 유리·창"),
    "M_Glass_Lit":     dict(color="#F1D79A", tile=1.0, uv="unit", owner="conv", note="창문 불 판(WinLit_t##). 0~1 UV"),
    "M_Roof_Green":    dict(color="#6E8F6A", tile=2.0, uv="world", owner="conv", note="옥상 녹색 방수 바닥"),
    "M_Pipe_Yellow":   dict(color="#D8B23A", tile=1.0, uv="world", owner="conv", note="노란 가스관"),
    "M_AC_White":      dict(color="#E6E2D8", tile=1.0, uv="world", owner="conv", note="흰 새시 틀·흰 방범창(docs/10 3-1)·(--prop-geometry 때) 실외기"),
    "M_Asphalt":       dict(color="#6F6C69", tile=4.0, uv="world", owner="conv", note="(키트는 안 씀 — 도로)"),
    "M_Paving":        dict(color="#BDB3A3", tile=1.2, uv="world", owner="conv", note="단독주택 마당 바닥"),
    "M_Sign_Atlas":    dict(color="#EFE9DA", tile=1.0, uv="unit", owner="conv", note="간판 자리 Sign_<n>(0~1 UV → sign_atlas.json 사각형으로 바꿔 씀)"),
    # ---- 아트 작업자가 더한 이름(docs/10 5장)
    "M_Stone_Base":    dict(color="#9C9890", tile=1.5, uv="world", owner="art", note="한옥 아랫벽 돌 블록(굵은 먹 줄눈)"),
    "M_Stone_Rubble":  dict(color="#B8AE9C", tile=2.0, uv="world", owner="art", note="막돌 옹벽 — 키트의 '계단 축대'"),
    "M_Dirt":          dict(color="#D9C9A8", tile=3.0, uv="world", owner="art", note="(키트는 안 씀 — 흙 땅)"),
    "M_Paint_SchoolZone": dict(color="#A85A4C", tile=2.0, uv="world", owner="art", note="(키트는 안 씀)"),
    "M_Decal_Atlas":   dict(color="#FFFFFF", tile=1.0, uv="unit", owner="art", note="(키트는 안 씀 — Unity 데칼)"),
    # ---- 건물 키트가 더한 이름(텍스처 없음 — 평면 색으로 충분, 필요하면 다른 재질 텍스처로 돌려 써도 됨)
    "M_Granite_Dark":  dict(color="#5E5A55", tile=3.0, uv="world", owner="kit", note="LOD0 돌 쌓기 뒤 줄눈 면(먹 줄눈)·총안 안·기단 갓돌·기둥 갓"),
    "M_Granite_Block": dict(color="#CFC6B4", tile=1.0, uv="world", owner="kit", note="LOD0 성돌 한 장 면(줄눈 없는 화강석 결) — 텍스처가 없으면 M_Granite 색 평면"),
    "M_Hanji":         dict(color="#F1EAD8", tile=1.0, uv="world", owner="kit", note="창호지(한옥 창살 뒤)"),
    "M_Wire_Black":    dict(color="#1A1417", tile=1.0, uv="world", owner="kit", note="전선·빨랫줄·철망 줄(docs/10 4장 색 #1A1417)"),
    "M_Plant_Green":   dict(color="#7E9A5E", tile=1.0, uv="world", owner="kit", note="(--prop-geometry 때) 화분 식물"),
    "M_Fabric":        dict(color="#A9B7C2", tile=1.0, uv="world", owner="kit", note="빨랫줄 빨래·(--prop-geometry 때) 건조대 빨래"),
    "M_Tank_Blue":     dict(color="#7FB2C9", tile=1.0, uv="world", owner="kit", note="(--prop-geometry 때) 옥상 물탱크 — 기본은 소품 water_tank_roof"),
}


def _load_tiles():
    p = os.path.join(TEX_DIR, "textures.json")
    try:
        with open(p, encoding="utf-8") as f:
            d = json.load(f)
    except OSError:
        return
    for k, v in d.items():
        if k in MATS and v.get("meters"):
            MATS[k]["tile"] = float(v["meters"])
            MATS[k]["texture"] = "concept/art/env/textures/%s.png" % k


_load_tiles()


def texture_path(name):
    p = os.path.join(TEX_DIR, name + ".png")
    return p if os.path.exists(p) else None


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def srgb_to_linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)
