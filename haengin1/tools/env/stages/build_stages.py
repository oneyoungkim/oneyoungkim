# -*- coding: utf-8 -*-
"""무대 4곳(09 설계 9단계 — 국밥집·시우네·하루편의점·혜성고 2-3반) 절차 키트 → concept/art/env/stages/St_<Key>.fbx + St_<Key>_manifest.json

실행(저장소 haengin1/ 에서):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/stages/build_stages.py -- [--only Gukbap,Home] [--blend x.blend]
좌표: Unity 무대 로컬 그대로(X 동, Y 위, Z 북), 원점 = 방 가운데 바닥(StorySetup 자리 무대와 같은 원점 — StagePlace 루트 아래 0,0,0 에 놓음).
오브젝트: <부분>_LOD0(메시) · Prop_<소품>_##(빈 오브젝트, +Z 정면, prop) · Sock_<종류>_<이름>(빈 오브젝트: spawn·actor·light·seat·photo·cam)
manifest: materials(색·결 크기) · sockets · colliders(축 정렬 상자 [x0,x1,y0,y1,z0,z1,layer]) · tris
체육관 키트(tools/env/gym/build_gym.py)와 같은 envkit.geom/blend 규약. 재질 이름 = M_* (STAGE_MATS — 결 텍스처는 make_textures.py 가 concept/art/env/stages/textures/ 에).
"""
import json
import math
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(ENV))
sys.path.insert(0, ENV)

from envkit.geom import MB, WORLD, box, cyl, tube, seg_box, UP, add, sub, mul, norm  # noqa: E402
from envkit.build import Obj  # noqa: E402
from envkit.mats import MATS  # noqa: E402

TEX = "concept/art/env/stages/textures"
# color = 텍스처 없을 때 평면 색, tile = 한 장이 덮는 m(월드 UV), uv world|unit
STAGE_MATS = {
    "M_Wall_Cream":      dict(color="#E6DAC0", tile=2.0, uv="world", note="크림 페인트 벽"),
    "M_Tile_WhiteWall":  dict(color="#E9E6DE", tile=0.6, uv="world", note="흰 사각 벽 타일 15cm + 회색 줄눈"),
    "M_Terrazzo":        dict(color="#A9A8A2", tile=1.0, uv="world", note="젖은 회색 테라조 바닥(주방)"),
    "M_Tile_Floor":      dict(color="#B98E6A", tile=0.6, uv="world", note="갈색 바닥 타일 30cm(국밥집 홀)"),
    "M_Tile_Store":      dict(color="#E4E2DC", tile=1.2, uv="world", note="편의점 흰 바닥 타일 60cm"),
    "M_Floor_School":    dict(color="#D8D1C2", tile=1.2, uv="world", note="학교 바닥 타일"),
    "M_Jangpan":         dict(color="#D9B46A", tile=1.8, uv="world", note="노란 장판"),
    "M_Wallpaper":       dict(color="#E7DCC4", tile=0.8, uv="world", note="바랜 꽃무늬 벽지"),
    "M_Ceiling":         dict(color="#EDE7D8", tile=0.6, uv="world", note="천장 텍스 판 60cm"),
    "M_Wainscot":        dict(color="#8E6444", tile=1.0, uv="world", note="나무 굽도리·홀 아랫벽"),
    "M_Wood_Light":      dict(color="#C9A77A", tile=1.0, uv="world", note="밝은 나무(교탁·밥상·책상)"),
    "M_Wood_Counter":    dict(color="#9A6B45", tile=1.0, uv="world", note="나무 계산대"),
    "M_Stainless":       dict(color="#BFC4C7", tile=1.0, uv="world", note="스테인리스(조리대·식탁·그릇)"),
    "M_Stainless_Dark":  dict(color="#7E8488", tile=1.0, uv="world", note="어두운 스테인리스(후드·다리)"),
    "M_Plastic_Red":     dict(color="#B5473C", tile=1.0, uv="world", note="빨간 플라스틱(김치통 뚜껑·바구니)"),
    "M_Plastic_Blue":    dict(color="#4E6E9A", tile=1.0, uv="world", note="파란 플라스틱(폐기 바구니)"),
    "M_Haru_Slate":      dict(color="#5B6B7A", tile=1.0, uv="world", note="하루편의점 슬레이트 블루"),
    "M_Haru_Cream":      dict(color="#E9E1CC", tile=1.0, uv="world", note="하루편의점 크림"),
    "M_Shelf_White":     dict(color="#E6E4DF", tile=1.0, uv="world", note="흰 진열대"),
    "M_Products":        dict(color="#C99A6A", tile=1.0, uv="unit", note="상품 진열 면(T_Products, 0~1 UV)"),
    "M_Glass_Fridge":    dict(color="#CFE3E6", tile=1.0, uv="world", note="냉장고 유리문(투명)"),
    "M_Fluor_Panel":     dict(color="#F2F5EF", tile=1.0, uv="unit", note="형광등·천장 등판(발광)"),
    "M_Chalkboard":      dict(color="#2F4A3E", tile=2.4, uv="world", note="초록 칠판 + 지운 분필 자국"),
    "M_Cork":            dict(color="#B98A5A", tile=0.8, uv="world", note="코르크 게시판"),
    "M_Paper_Blue":      dict(color="#8FA6C2", tile=1.0, uv="world", note="게시물·메뉴판 칸"),
    "M_Paper_Pink":      dict(color="#D9A3A8", tile=1.0, uv="world", note="게시물·메뉴판 칸"),
    "M_Paper_Yellow":    dict(color="#E3CC7E", tile=1.0, uv="world", note="게시물·메뉴판 칸"),
    "M_Paper_Green":     dict(color="#9DB592", tile=1.0, uv="world", note="게시물·메뉴판 칸"),
    "M_Paper_White":     dict(color="#F1ECE0", tile=1.0, uv="world", note="흰 종이·달력"),
    "M_Locker_Grey":     dict(color="#9CA1A6", tile=1.0, uv="world", note="교실 사물함"),
    "M_Steel_Frame":     dict(color="#7D8287", tile=1.0, uv="world", note="책상 다리·창틀·옷걸이"),
    "M_View_Fortress":   dict(color="#B9C4A8", tile=1.0, uv="unit", note="창밖 성곽(T_FortressView, 0~1 UV, Unlit)"),
    "M_View_Night":      dict(color="#2B3446", tile=1.0, uv="unit", note="창밖 밤거리(절차 그림, Unlit)"),
    "M_View_Dawn":       dict(color="#9DB6CF", tile=1.0, uv="unit", note="간유리 새벽빛(Unlit)"),
    "M_Glass_Frosted":   dict(color="#D7E0E3", tile=1.0, uv="world", note="김 서린 유리"),
    "M_Curtain":         dict(color="#E8DDC3", tile=1.0, uv="world", note="크림 커튼"),
    "M_Fabric_Futon":    dict(color="#D7AFA6", tile=1.0, uv="world", note="분홍 꽃 이불·전기장판 덮개"),
    "M_Fabric_Navy":     dict(color="#2C3442", tile=1.0, uv="world", note="교복 상의(옷걸이)·책가방"),
    "M_Cushion":         dict(color="#7E8FB0", tile=1.0, uv="world", note="방석"),
    "M_Cabinet_Green":   dict(color="#8FA08A", tile=1.0, uv="world", note="부엌 찬장(연초록)"),
    "M_TV_Screen":       dict(color="#3A4A55", tile=1.0, uv="world", note="TV 화면"),
    "M_Black_Plastic":   dict(color="#2A2A2C", tile=1.0, uv="world", note="검은 플라스틱(TV 틀·계산기·스피커)"),
    "M_Door_School":     dict(color="#A8ADA4", tile=1.0, uv="world", note="학교 미닫이문(회녹)"),
    # 룩데브 ⑥ 디테일(docs/14, 2026-10-26 — tools/lookdev/detail_tex.py 가 굽는 그림, 0~1 UV 판)
    "M_D14_Menu_Gukbap":   dict(color="#C9A070", tile=1.0, uv="unit", note="국밥집 메뉴판(칸 7)"),
    "M_D14_Poster_Gukbap": dict(color="#E8DCC4", tile=1.0, uv="unit", note="국밥집 포스터"),
    "M_D14_Origin_Gukbap": dict(color="#E2E8E2", tile=1.0, uv="unit", note="원산지 표시"),
    "M_D14_Calendar_3":    dict(color="#F4F0E6", tile=1.0, uv="unit", note="달력 3월(국밥집)"),
    "M_D14_Calendar_Home": dict(color="#F0ECE4", tile=1.0, uv="unit", note="달력 3월(시우네)"),
    "M_D14_TV_News":       dict(color="#365070", tile=1.0, uv="unit", note="TV 화면(뉴스)"),
    "M_D14_Poster_Gym":    dict(color="#222226", tile=1.0, uv="unit", note="혜화 MMA 전단(시우네 벽)"),
    "M_D14_Drawing_Haneul": dict(color="#FAF8F0", tile=1.0, uv="unit", note="하늘이 그림"),
    "M_D14_POP_Strip":     dict(color="#F6F4EE", tile=1.0, uv="unit", note="편의점 선반 가격표 띠(칸 8)"),
    "M_D14_Promo_Conv":    dict(color="#3C6088", tile=1.0, uv="unit", note="편의점 행사 포스터"),
    "M_D14_Aisle_Signs":   dict(color="#ECE8DC", tile=1.0, uv="unit", note="편의점 천장 분류 표지(칸 4)"),
    "M_D14_Board_School":  dict(color="#B98A5A", tile=1.0, uv="unit", note="교실 게시판 종이"),
    "M_D14_ClassSign":     dict(color="#EEECE4", tile=1.0, uv="unit", note="학급 표지 2-3"),
}
for k, v in STAGE_MATS.items():
    MATS.setdefault(k, dict(v, owner="stage"))
    p = os.path.join(ROOT, TEX, k + ".png")
    if os.path.exists(p):
        MATS[k]["texture"] = TEX + "/" + k + ".png"

SOCKETS = []
COLLIDERS = []
PARTS = []


def reset():
    SOCKETS.clear()
    COLLIDERS.clear()
    PARTS.clear()


def part(name):
    mb = MB()
    PARTS.append((name, mb))
    return mb


def sock(name, kind, pos, yaw=0.0, **info):
    a = math.radians(yaw)
    SOCKETS.append(dict(name=name, kind=kind, pos=tuple(float(v) for v in pos), fwd=(math.sin(a), 0.0, math.cos(a)), info=info))


def prop(name, n, pos, yaw=0.0):
    a = math.radians(yaw)
    SOCKETS.append(dict(name="Prop_%s_%02d" % (name, n), kind="prop", pos=tuple(float(v) for v in pos), fwd=(math.sin(a), 0.0, math.cos(a)),
                        info=dict(prop=name, glb="concept/art/env/stages/props/%s.glb" % name)))


def col(x0, x1, y0, y1, z0, z1, layer="Wall"):
    COLLIDERS.append([round(v, 3) for v in (x0, x1, y0, y1, z0, z1)] + [layer])


def B(mb, x0, x1, y0, y1, z0, z1, mat, mats=None, skip=(), collide=None):
    box(mb, WORLD, x0, x1, y0, y1, z0, z1, mat, skip=skip, mats=mats)
    if collide:
        col(x0, x1, y0, y1, z0, z1, collide)


# ---------------------------------------------------------------- 공통 부품
T = 0.12   # 벽 두께


def wall_x(mb, z, x0, x1, h, inner, mat_in, holes=(), mat_out="M_Plaster_Cream", band=None, collide=True):
    """z = 벽 안쪽 면 자리, 벽은 inner(+1 = 방이 +z 쪽, -1 = 방이 -z 쪽) 반대쪽으로 두께 T. holes = [(x0, x1, y0, y1)]. band = (높이, 재질) 아랫 띠."""
    za, zb = (z - T, z) if inner > 0 else (z, z + T)
    face_in = "+z" if inner > 0 else "-z"
    _wall(mb, x0, x1, h, holes, lambda u0, u1, y0, y1: (u0, u1, y0, y1, za, zb), face_in, mat_in, mat_out, band, collide)


def wall_z(mb, x, z0, z1, h, inner, mat_in, holes=(), mat_out="M_Plaster_Cream", band=None, collide=True):
    xa, xb = (x - T, x) if inner > 0 else (x, x + T)
    face_in = "+x" if inner > 0 else "-x"
    _wall(mb, z0, z1, h, holes, lambda u0, u1, y0, y1: (xa, xb, y0, y1, u0, u1), face_in, mat_in, mat_out, band, collide)


def _wall(mb, u0, u1, h, holes, geo, face_in, mat_in, mat_out, band, collide):
    hs = sorted(holes)
    cuts = [u0] + [v for a, b, _, _ in hs for v in (a, b)] + [u1]
    # 구멍 사이 기둥(바닥~천장)
    pieces = []
    for i in range(0, len(cuts), 2):
        a, b = cuts[i], cuts[i + 1]
        if b - a > 1e-3:
            pieces.append((a, b, 0.0, h))
    for a, b, y0, y1 in hs:
        if y0 > 1e-3:
            pieces.append((a, b, 0.0, y0))
        if y1 < h - 1e-3:
            pieces.append((a, b, y1, h))
    for a, b, y0, y1 in pieces:
        if band and y0 < band[0]:
            yb = min(y1, band[0])
            x0, x1, _, _, z0, z1 = geo(a, b, y0, yb)
            B(mb, x0, x1, y0, yb, z0, z1, mat_out, mats={face_in: band[1]})
            if y1 > yb:
                x0, x1, _, _, z0, z1 = geo(a, b, yb, y1)
                B(mb, x0, x1, yb, y1, z0, z1, mat_out, mats={face_in: mat_in})
        else:
            x0, x1, _, _, z0, z1 = geo(a, b, y0, y1)
            B(mb, x0, x1, y0, y1, z0, z1, mat_out, mats={face_in: mat_in})
        if collide and y0 < 1.8:
            x0, x1, _, _, z0, z1 = geo(a, b, y0, y1)
            col(x0, x1, y0, min(y1, 2.6), z0, z1)


def floor_ceil(mb, x0, x1, z0, z1, h, floor_mat, ceil_mat):
    B(mb, x0, x1, -0.1, 0.0, z0, z1, "M_Concrete", mats={"+y": floor_mat})
    B(mb, x0, x1, h, h + 0.1, z0, z1, "M_Concrete", mats={"-y": ceil_mat})
    col(x0, x1, -0.3, 0.0, z0, z1, "Ground")


def fluor_panel(mb, x, z, y, w=0.6, d=1.2):
    B(mb, x - w / 2 - 0.02, x + w / 2 + 0.02, y - 0.04, y, z - d / 2 - 0.02, z + d / 2 + 0.02, "M_AC_White")
    B(mb, x - w / 2, x + w / 2, y - 0.045, y - 0.04, z - d / 2, z + d / 2, "M_Fluor_Panel")


def fluor_tube(mb, x, z, y, L=1.2, along="x"):
    a, b = ((x - L / 2, y - 0.06, z), (x + L / 2, y - 0.06, z)) if along == "x" else ((x, y - 0.06, z - L / 2), (x, y - 0.06, z + L / 2))
    seg_box(mb, (a[0], y - 0.02, a[2]), (b[0], y - 0.02, b[2]), 0.08, 0.04, "M_AC_White")
    cyl(mb, a, norm(sub(b, a)), 0.022, 0.022, math.dist(a, b), 8, "M_Fluor_Panel", caps=(True, True))


def window(mb, axis, c, u0, u1, y0, y1, out_sign, view_mat, bars=False, frame="M_Metal_Grey", panes=2, view_dist=0.9, glass=None):
    """벽 구멍에 끼우는 창: 틀 + 창살 + (glass) + 바깥 경치 판. axis 'x' = 벽이 z = c 에 있고 u = x, 'z' = 벽이 x = c 에 있고 u = z. out_sign = 바깥 방향 부호."""
    def P(u, y, d):
        return (u, y, c + d) if axis == "x" else (c + d, y, u)

    def BX(ua, ub, ya, yb, da, db, mat):
        if axis == "x":
            B(mb, ua, ub, ya, yb, c + min(da, db), c + max(da, db), mat)
        else:
            B(mb, c + min(da, db), c + max(da, db), ya, yb, ua, ub, mat)
    s = out_sign
    f = 0.05
    BX(u0, u1, y0, y0 + f, -0.06 * s, 0.06 * s, frame)            # 아래틀
    BX(u0, u1, y1 - f, y1, -0.06 * s, 0.06 * s, frame)            # 위틀
    BX(u0, u0 + f, y0, y1, -0.06 * s, 0.06 * s, frame)
    BX(u1 - f, u1, y0, y1, -0.06 * s, 0.06 * s, frame)
    for k in range(1, panes):
        u = u0 + (u1 - u0) * k / panes
        BX(u - 0.025, u + 0.025, y0, y1, -0.04 * s, 0.04 * s, frame)
    BX(u0 - 0.04, u1 + 0.04, y0 - 0.03, y0, -0.18 * s, 0.02 * s, frame)   # 창턱
    if glass:
        BX(u0 + f, u1 - f, y0 + f, y1 - f, -0.005 * s, 0.005 * s, glass)
    if bars:
        n = max(3, int((u1 - u0) / 0.12))
        for k in range(1, n):
            u = u0 + (u1 - u0) * k / n
            BX(u - 0.008, u + 0.008, y0, y1, 0.09 * s, 0.11 * s, "M_AC_White")
        BX(u0, u1, (y0 + y1) / 2 - 0.01, (y0 + y1) / 2 + 0.01, 0.09 * s, 0.11 * s, "M_AC_White")
    # 바깥 경치(0~1 UV 판) — 창보다 넓게
    if view_mat:
        m = 0.9 * view_dist + 0.3
        d = view_dist * s
        a, b2, c2, d2 = P(u0 - m, y0 - 0.6, d), P(u1 + m, y0 - 0.6, d), P(u1 + m, y1 + 0.6, d), P(u0 - m, y1 + 0.6, d)
        out = (0.0, 0.0, -s) if axis == "x" else (-s, 0.0, 0.0)
        mb.face([a, b2, c2, d2], view_mat, out=out, uv=[(0, 0), (1, 0), (1, 1), (0, 1)] if (s < 0) == (axis == "x") else [(1, 0), (0, 0), (0, 1), (1, 1)])


def door_panel(mb, axis, c, u0, u1, h, mat, frame="M_Steel_Frame", glass_top=False, out_sign=1, open_frac=0.0):
    """문틀 + 문짝(open_frac 만큼 미닫이로 밀림)."""
    def BX(ua, ub, ya, yb, da, db, m):
        if axis == "x":
            B(mb, ua, ub, ya, yb, c + min(da, db), c + max(da, db), m)
        else:
            B(mb, c + min(da, db), c + max(da, db), ya, yb, ua, ub, m)
    BX(u0 - 0.05, u0, 0.0, h + 0.05, -0.08, 0.08, frame)
    BX(u1, u1 + 0.05, 0.0, h + 0.05, -0.08, 0.08, frame)
    BX(u0 - 0.05, u1 + 0.05, h, h + 0.06, -0.08, 0.08, frame)
    w = u1 - u0
    sh = w * open_frac
    a, b = u0 + sh, u1 + sh
    if glass_top:
        BX(a, b, 0.0, h * 0.55, -0.02 * out_sign, 0.02 * out_sign, mat)
        BX(a, b, h * 0.55, h, -0.02 * out_sign, 0.02 * out_sign, mat)
        BX(a + 0.08, b - 0.08, h * 0.6, h - 0.08, -0.025 * out_sign, 0.025 * out_sign, "M_Glass_Frosted")
    else:
        BX(a, b, 0.0, h, -0.02 * out_sign, 0.02 * out_sign, mat)
    # 손잡이
    hu = a + 0.08 if open_frac > 0 else b - 0.08
    BX(hu - 0.015, hu + 0.015, 0.95, 1.1, -0.05 * out_sign, -0.02 * out_sign, "M_Steel_Frame")


def table_steel(mb, x, z, w=0.9, d=0.9, h=0.72, top="M_Stainless", leg="M_Stainless_Dark", collide=True):
    B(mb, x - w / 2, x + w / 2, h - 0.035, h, z - d / 2, z + d / 2, top)
    B(mb, x - w / 2 + 0.03, x + w / 2 - 0.03, h - 0.09, h - 0.035, z - d / 2 + 0.03, z + d / 2 - 0.03, top)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl(mb, (x + sx * (w / 2 - 0.06), 0.0, z + sz * (d / 2 - 0.06)), UP, 0.022, 0.022, h - 0.09, 8, leg, caps=(False, False))
    if collide:
        col(x - w / 2, x + w / 2, 0.0, h, z - d / 2, z + d / 2, "Default")


def bowl_stack(mb, x, y, z, r=0.085, n=6, mat="M_Stainless"):
    for k in range(n):
        cyl(mb, (x, y + 0.028 * k, z), UP, r * 0.65, r, 0.05, 12, mat, caps=(True, False))


def cup(mb, x, y, z, mat="M_Stainless"):
    cyl(mb, (x, y, z), UP, 0.03, 0.035, 0.08, 10, mat, caps=(True, False))


def kimchi_box(mb, x, y, z, w=0.36, d=0.26, h=0.2, body="M_AC_White", lid="M_Plastic_Red"):
    B(mb, x - w / 2, x + w / 2, y, y + h, z - d / 2, z + d / 2, body)
    B(mb, x - w / 2 - 0.01, x + w / 2 + 0.01, y + h, y + h + 0.03, z - d / 2 - 0.01, z + d / 2 + 0.01, lid)


def chair_simple(mb, x, z, yaw, seat="M_Wood_Light", frame="M_Steel_Frame", h=0.44):
    """작은 의자(아이 책상·편의점 의자): 앉판 + 등판 + 다리 4."""
    a = math.radians(yaw)
    fx, fz = math.sin(a), math.cos(a)        # 앞
    rx, rz = fz, -fx
    def P(u, y, v):
        return (x + rx * u + fx * v, y, z + rz * u + fz * v)
    w = 0.38
    for su in (-1, 1):
        for sv in (-1, 1):
            p = P(su * (w / 2 - 0.03), 0.0, sv * (w / 2 - 0.03))
            cyl(mb, p, UP, 0.015, 0.015, h, 6, frame, caps=(False, False))
    c = P(0, h, 0)
    seg_box(mb, P(-w / 2, h + 0.015, 0), P(w / 2, h + 0.015, 0), w, 0.03, seat)
    seg_box(mb, P(-w / 2, h + 0.38, -w / 2 + 0.02), P(w / 2, h + 0.38, -w / 2 + 0.02), 0.2, 0.02, seat, up=(fx, 0.0, fz))
    for su in (-1, 1):
        a2, b2 = P(su * (w / 2 - 0.03), h, -w / 2 + 0.03), P(su * (w / 2 - 0.03), h + 0.45, -w / 2 + 0.03)
        cyl(mb, a2, UP, 0.013, 0.013, 0.45, 6, frame, caps=(False, True))
    _ = c


def menu_panels(mb, axis, c, u0, u1, y0, y1, depth_sign, n=7):
    mats = ["M_Paper_Yellow", "M_Paper_Pink", "M_Paper_Green", "M_Paper_Blue", "M_Paper_White", "M_Paper_Yellow", "M_Paper_Pink", "M_Paper_Green"]
    du = (u1 - u0) / n
    for k in range(n):
        a, b = u0 + du * k + 0.02, u0 + du * (k + 1) - 0.02
        if axis == "x":
            B(mb, a, b, y0 + 0.03, y1 - 0.03, c + min(0, depth_sign * 0.02), c + max(0, depth_sign * 0.02), mats[k % len(mats)])
        else:
            B(mb, c + min(0, depth_sign * 0.02), c + max(0, depth_sign * 0.02), y0 + 0.03, y1 - 0.03, a, b, mats[k % len(mats)])


def pic(mb, axis, c, u0, u1, y0, y1, out_sign, mat, d=0.006, rect=(0.0, 0.0, 1.0, 1.0)):
    """벽 방 쪽 면에 붙인 그림 판(0~1 UV, rect = 그림의 일부 칸 u, v, w, h). axis 'x' = z = c 벽(u = x), 'z' = x = c 벽(u = z). out_sign = 벽 바깥(방 반대) 방향.
    UV 방향은 window() 의 바깥 경치 판과 반대(그 규약을 그대로 쓰면 Unity 에서 글자가 좌우 거울상 — 2026-10-26 실측, 경치 그림은 티가 안 났음)."""
    s = out_sign
    dd = -d * s

    def P(u, y):
        return (u, y, c + dd) if axis == "x" else (c + dd, y, u)
    out = (0.0, 0.0, -s) if axis == "x" else (-s, 0.0, 0.0)
    ua, va, ub, vb = rect[0], rect[1], rect[0] + rect[2], rect[1] + rect[3]
    uv = [(ub, va), (ua, va), (ua, vb), (ub, vb)] if (s < 0) == (axis == "x") else [(ua, va), (ub, va), (ub, vb), (ua, vb)]
    mb.face([P(u0, y0), P(u1, y0), P(u1, y1), P(u0, y1)], mat, out=out, uv=uv)


def light(name, pos, color="#FFF2DD", intensity=1.6, rng=6.0, kind="point"):
    sock("Sock_Light_" + name, "light", pos, 0.0, color=color, intensity=intensity, range=rng, light=kind)


def door(name, pos, yaw, label, to="Zone1"):
    """출입문 자리(안쪽, 들어오면 서는 곳 = 스폰 'Door'). 2026-10-25 오픈월드 방향: Zone1 에서 걸어 들어오는 문."""
    sock("Sock_Door_" + name, "door", pos, yaw, label=label, to=to)


def marker(name, pos, yaw, func, label):
    """상호작용 표시 자리(기능은 새 설계 뒤 — 지금은 자리만): func = job_serving · job_dishes · job_counter · job_stock · job_waste · sleep · save · ledger · haneul · seat · class"""
    sock("Sock_Marker_" + name, "marker", pos, yaw, func=func, label=label)


def cam(name, pos, look, fov=48.0):
    d = sub(look, pos)
    yaw = math.degrees(math.atan2(d[0], d[2]))
    sock("Sock_Cam_" + name, "cam", pos, yaw, look=list(look), fov=fov)


# ================================================================ 국밥집 (10 × 8, h 2.8)
def gukbap():
    W, D, H = 10.0, 8.0, 2.8
    x0, x1, z0, z1 = -W / 2, W / 2, -D / 2, D / 2
    KZ = 0.3      # 주방 벽(홀 쪽 면)
    KX = 1.5      # 주방 동쪽 벽
    sh = part("Shell")
    # 바닥: 주방 테라조, 홀 갈색 타일
    B(sh, x0, KX, -0.1, 0.0, KZ, z1, "M_Concrete", mats={"+y": "M_Terrazzo"})
    B(sh, x0, x1, -0.1, 0.0, z0, KZ, "M_Concrete", mats={"+y": "M_Tile_Floor"})
    B(sh, KX, x1, -0.1, 0.0, KZ, z1, "M_Concrete", mats={"+y": "M_Tile_Floor"})
    col(x0, x1, -0.3, 0.0, z0, z1, "Ground")
    B(sh, x0, x1, H, H + 0.1, z0, z1, "M_Concrete", mats={"-y": "M_Ceiling"})
    # 바깥 벽: 북(주방 창 + 홀 쪽), 서(주방·홀), 동(홀), 남(유리 앞면 + 문)
    wall_x(sh, z1, x0, KX, H, -1, "M_Wall_Cream", holes=[(-3.3, -2.3, 1.4, 2.1)], band=(1.5, "M_Tile_WhiteWall"))
    wall_x(sh, z1, KX, x1, H, -1, "M_Wall_Cream", band=(1.0, "M_Wainscot"))
    wall_z(sh, x0, KZ, z1, H, 1, "M_Wall_Cream", band=(1.5, "M_Tile_WhiteWall"))
    wall_z(sh, x0, z0, KZ, H, 1, "M_Wall_Cream", band=(1.0, "M_Wainscot"))
    wall_z(sh, x1, z0, z1, H, -1, "M_Wall_Cream", band=(1.0, "M_Wainscot"))
    wall_x(sh, z0, x0, x1, H, 1, "M_Wall_Cream", holes=[(-4.6, 1.8, 0.35, 2.4), (2.4, 3.6, 0.0, 2.2)], band=(1.0, "M_Wainscot"))
    window(sh, "x", z1, -3.3, -2.3, 1.4, 2.1, 1, "M_View_Dawn", frame="M_AC_White", glass="M_Glass_Frosted", panes=2, view_dist=0.05)
    window(sh, "x", z0, -4.6, 1.8, 0.35, 2.4, -1, "M_View_Night", frame="M_Metal_Grey", panes=4, view_dist=1.2)
    for k in range(4):   # 김 서린 유리(아래 절반)
        u0 = -4.6 + 6.4 * k / 4 + 0.05
        B(sh, u0, u0 + 1.5, 0.4, 1.35, z0 - 0.01, z0 + 0.0, "M_Glass_Frosted")
    door_panel(sh, "x", z0 - 0.06, 2.4, 3.6, 2.2, "M_Metal_Grey", frame="M_Metal_Grey", glass_top=True, out_sign=-1)
    col(x0, x1, 0.0, 2.6, z0 - 0.4, z0 - 0.12)        # 문 밖으로 못 나감
    # 주방 칸막이(z = KZ): 홀 쪽 면 나무 아랫벽 + 크림, 주방 쪽 흰 타일 · 내다보는 창(-3.6~-1.4, 1.0~1.9) · 문 구멍(0.2~1.2)
    zp0, zp1 = KZ, KZ + T
    for a, b, y0, y1 in ((x0, -3.6, 0.0, H), (-3.6, -1.4, 0.0, 1.0), (-3.6, -1.4, 1.9, H), (-1.4, 0.2, 0.0, H), (0.2, 1.2, 2.1, H), (1.2, KX, 0.0, H)):
        B(sh, a, b, y0, y1, zp0, zp1, "M_Wall_Cream", mats={"-z": "M_Wainscot" if y1 <= 1.0 else "M_Wall_Cream", "+z": "M_Tile_WhiteWall" if y0 < 1.5 else "M_Wall_Cream"})
        if y0 < 1.8:
            col(a, b, y0, min(y1, 2.6), zp0, zp1)
    B(sh, -3.7, -1.3, 0.95, 1.0, zp0 - 0.18, zp1 + 0.18, "M_Stainless")         # 내다보는 창 선반
    # 주방 동쪽 벽(x = KX, z KZ~z1)
    wall_z(sh, KX, KZ + T, z1, H, -1, "M_Tile_WhiteWall", mat_out="M_Wall_Cream")
    B(sh, KX, KX + T, 0.0, 1.0, KZ + T, z1, "M_Wall_Cream", mats={"+x": "M_Wainscot"})
    # 메뉴판(홀 쪽, 칸막이 위)
    B(sh, -4.7, -0.4, 2.0, 2.62, KZ - 0.05, KZ, "M_Wood_Counter")
    pic(sh, "x", KZ - 0.05, -4.65, -0.45, 2.02, 2.6, 1, "M_D14_Menu_Gukbap")     # 룩데브 ⑥: 빈 색 칸 → 메뉴 글자 그림
    # 형광등: 홀 4 판, 주방 2 맨 관
    fx = part("Fixtures")
    for (x, z) in ((-3.6, -2.0), (-0.8, -2.0), (2.4, -2.0), (3.3, 2.0)):
        fluor_panel(fx, x, z, H, 0.6, 1.2)
    for (x, z) in ((-3.0, 2.2), (-0.4, 2.2)):
        fluor_tube(fx, x, z, H, 1.2, "x")
    # ---- 주방 설비
    k = part("Kitchen")
    # 북벽 조리대 + 개수대 + 선반 + 국자 걸이
    B(k, -1.9, 1.38, 0.0, 0.82, z1 - 0.65, z1, "M_Stainless", mats={"-z": "M_Stainless_Dark"}, collide="Default")
    B(k, -1.95, 1.4, 0.82, 0.86, z1 - 0.68, z1, "M_Stainless")
    B(k, -0.6, 0.2, 0.62, 0.86, z1 - 0.58, z1 - 0.1, "M_Stainless_Dark")                  # 개수대 구덩이
    seg_box(k, (-0.2, 0.86, z1 - 0.05), (-0.2, 1.15, z1 - 0.05), 0.03, 0.03, "M_Stainless")
    seg_box(k, (-0.2, 1.15, z1 - 0.05), (-0.2, 1.15, z1 - 0.25), 0.025, 0.025, "M_Stainless")
    B(k, -1.9, 1.38, 1.55, 1.58, z1 - 0.32, z1, "M_Stainless")                               # 선반
    for i, xx in enumerate((-1.6, -1.3, -1.0)):
        bowl_stack(k, xx, 1.58, z1 - 0.16, 0.08, 5)
    kimchi_box(k, 0.5, 1.58, z1 - 0.16, 0.3, 0.24, 0.16)
    kimchi_box(k, 0.95, 1.58, z1 - 0.16, 0.3, 0.24, 0.16)
    seg_box(k, (-1.9, 1.95, z1 - 0.05), (0.2, 1.95, z1 - 0.05), 0.02, 0.02, "M_Stainless")   # 국자 걸이
    for i in range(7):
        xx = -1.75 + 0.28 * i
        seg_box(k, (xx, 1.95, z1 - 0.07), (xx, 1.55, z1 - 0.07), 0.012, 0.012, "M_Stainless")
        cyl(k, (xx, 1.48, z1 - 0.1), UP, 0.04 + 0.01 * (i % 2), 0.05 + 0.01 * (i % 2), 0.07, 10, "M_Stainless", caps=(True, False))
    # 가마솥 위 후드
    B(k, -4.75, -1.95, 2.15, 2.6, z1 - 1.15, z1, "M_Stainless_Dark")
    B(k, -4.65, -2.05, 2.1, 2.15, z1 - 1.05, z1 - 0.1, "M_Stainless")
    B(k, -3.6, -3.2, 2.6, H, z1 - 0.6, z1 - 0.2, "M_Stainless_Dark")              # 배기 덕트
    # 서벽 스테인리스 선반대(그릇·김치통)
    xs0, xs1 = x0, x0 + 0.55
    for yy in (0.15, 0.75, 1.35):
        B(k, xs0, xs1, yy, yy + 0.03, 0.6, 2.6, "M_Stainless")
    for zz in (0.62, 2.58):
        for xx in (xs0 + 0.03, xs1 - 0.03):
            cyl(k, (xx, 0.0, zz), UP, 0.018, 0.018, 1.7, 6, "M_Stainless_Dark", caps=(False, True))
    col(xs0, xs1, 0.0, 1.7, 0.6, 2.6, "Default")
    for zz in (0.85, 1.25, 1.65, 2.05):
        bowl_stack(k, xs0 + 0.28, 0.78, zz, 0.09, 6)
    for zz in (0.85, 1.3, 1.75, 2.2):
        kimchi_box(k, xs0 + 0.28, 0.18, zz, 0.36, 0.3, 0.24, lid="M_Plastic_Red")
        kimchi_box(k, xs0 + 0.28, 1.38, zz, 0.3, 0.26, 0.18, lid="M_Plastic_Red")
    # 가운데 작업대(밥솥 놓을 자리)
    table_steel(k, -3.4, 1.0, 1.5, 0.7, 0.85)
    for xx in (-3.9, -3.65):
        bowl_stack(k, xx, 0.85, 0.9, 0.08, 4)
    # 냉장고(2 문)
    B(k, 0.62, 1.38, 0.0, 1.9, 2.25, 3.05, "M_Stainless", mats={"-x": "M_Stainless"}, collide="Default")
    seg_box(k, (0.6, 1.55, 2.5), (0.6, 1.05, 2.5), 0.02, 0.02, "M_Stainless_Dark")
    seg_box(k, (0.6, 0.95, 2.5), (0.6, 0.55, 2.5), 0.02, 0.02, "M_Stainless_Dark")
    B(k, 0.61, 0.62, 1.0, 1.01, 2.25, 3.05, "M_Stainless_Dark")
    # 고무 바닥 배수 홈
    B(k, -4.4, -0.8, 0.0, 0.004, 2.05, 2.25, "M_Stainless_Dark")
    prop("gamasot_stove", 1, (-3.95, 0.0, 3.25), 180.0)
    prop("gamasot_stove", 2, (-2.7, 0.0, 3.25), 180.0)
    prop("stockpot_burner", 1, (-1.45, 0.0, 2.85), 180.0)
    prop("rice_cooker", 1, (-2.95, 0.85, 1.05), 200.0)
    prop("yangeun_kettle", 1, (1.05, 0.86, 3.7), 200.0)
    col(-4.6, -2.0, 0.0, 1.0, 2.6, 3.9, "Default")
    # ---- 홀
    hl = part("Hall")
    tables = [(-3.6, -2.55), (-1.25, -2.55), (-3.6, -0.95), (-1.25, -0.95), (3.3, 1.35), (3.3, 3.05)]
    n = 0
    for ti, (tx, tz) in enumerate(tables):
        table_steel(hl, tx, tz, 0.9, 0.85, 0.72)
        for sx, sz in ((-0.55, 0.0), (0.55, 0.0), (0.0, -0.52), (0.0, 0.52)):
            n += 1
            if ti >= 4 and sx > 0:   # 동쪽 벽에 붙은 식탁은 벽 쪽 의자 없음
                continue
            prop("plastic_stool_red", n, (tx + sx + (0.04 if sz else 0.0), 0.0, tz + sz), (ti * 37 + n * 23) % 40 - 20)
        prop("yangeun_kettle", ti + 2, (tx + 0.25, 0.72, tz + 0.2), 30.0 * ti)
        for cx, cz in ((-0.2, -0.15), (-0.05, -0.2), (0.12, -0.22)):
            cup(hl, tx + cx, 0.72, tz + cz)
        B(hl, tx - 0.32, tx - 0.18, 0.72, 0.82, tz + 0.15, tz + 0.27, "M_AC_White")            # 휴지통(냅킨 상자)
        cyl(hl, (tx - 0.05, 0.72, tz + 0.05), UP, 0.07, 0.08, 0.03, 12, "M_AC_White", caps=(True, True))   # 깍두기 접시
        B(hl, tx - 0.09, tx - 0.01, 0.75, 0.78, tz + 0.01, tz + 0.09, "M_Plastic_Red")
    # 계산대(문 옆) + 계산기
    B(hl, 1.85, 2.95, 0.0, 1.0, -2.75, -1.75, "M_Wood_Counter", collide="Default")
    B(hl, 1.8, 3.0, 1.0, 1.05, -2.8, -1.7, "M_Wood_Light")
    B(hl, 2.25, 2.65, 1.05, 1.2, -2.45, -2.1, "M_Black_Plastic")
    B(hl, 2.3, 2.6, 1.2, 1.38, -2.2, -2.15, "M_Black_Plastic")
    B(hl, 2.32, 2.58, 1.22, 1.36, -2.21, -2.2, "M_TV_Screen")
    # 벽걸이 TV(북동 모서리)
    B(hl, x1 - 0.42, x1 - 0.12, 2.0, 2.06, z1 - 0.6, z1 - 0.4, "M_Black_Plastic")
    seg_box(hl, (x1 - 0.12, 2.03, z1 - 0.5), (x1 - 0.42, 2.03, z1 - 0.5), 0.03, 0.03, "M_Black_Plastic")
    B(hl, x1 - 0.5, x1 - 0.42, 1.85, 2.38, z1 - 0.95, z1 - 0.15, "M_Black_Plastic")
    B(hl, x1 - 0.505, x1 - 0.5, 1.88, 2.35, z1 - 0.92, z1 - 0.18, "M_TV_Screen")
    pic(hl, "z", x1 - 0.505, z1 - 0.9, z1 - 0.2, 1.9, 2.33, 1, "M_D14_TV_News", d=0.004)
    # 옷걸이 줄 + 달력(동벽)
    seg_box(hl, (x1 - 0.04, 1.75, -0.6), (x1 - 0.04, 1.75, 0.4), 0.04, 0.08, "M_Wood_Counter")
    for zz in (-0.4, -0.05, 0.25):
        seg_box(hl, (x1 - 0.06, 1.75, zz), (x1 - 0.14, 1.78, zz), 0.015, 0.015, "M_Steel_Frame")
    B(hl, x1 - 0.02, x1 - 0.01, 1.3, 1.75, 1.0, 1.35, "M_Paper_White")
    B(hl, x1 - 0.025, x1 - 0.02, 1.62, 1.72, 1.02, 1.33, "M_Paper_Pink")
    prop("water_cooler", 1, (x1 - 0.35, 0.0, -1.0), 270.0)
    # 불
    light("Hall_A", (-2.4, 2.5, -1.8), "#FFD9A2", 2.3, 7.0)      # 룩데브 ⑤: 형광 흰빛 → 백열 주황(프레임 s02 #F1D79A 쪽)
    light("Hall_B", (3.2, 2.5, 1.6), "#FFD9A2", 1.7, 5.5)
    detail_gukbap(x0, x1, z0, z1, KZ, KX, H, tables)
    light("Kitchen", (-1.6, 2.5, 2.2), "#F4F1E4", 2.0, 6.0)
    light("Dawn", (-2.8, 1.8, 3.6), "#9DB8DA", 0.8, 3.0)
    # 자리
    sock("Sock_Spawn_Spawn", "spawn", (0.7, 0.0, 1.05), 290.0)
    sock("Sock_Spawn_Hall", "spawn", (-2.4, 0.0, -1.25), 0.0)     # 홀 식탁 사이 통로(뒤로 2.8m 카메라 자리)
    door("Front", (3.0, 0.0, -3.45), 0.0, "나가기 — 상가거리")
    marker("Job_Serving", (-2.5, 0.0, -0.25), 180.0, "job_serving", "서빙 시작")
    marker("Job_Dishes", (-0.2, 0.0, 3.05), 0.0, "job_dishes", "설거지 시작")
    cam("s01", (1.05, 1.6, 0.75), (-2.6, 1.15, 3.7))
    cam("s02", (3.6, 1.6, -3.6), (-1.6, 1.0, 1.2))
    sock("Sock_Actor_엄마", "actor", (-2.05, 0.0, 2.35), 120.0, state="TalkHip")
    return dict(size=(W, D, H), title="국밥집")


# ================================================================ 시우네 (6 × 7, h 2.4)
def home():
    W, D, H = 6.0, 7.0, 2.4
    x0, x1, z0, z1 = -W / 2, W / 2, -D / 2, D / 2
    sh = part("Shell")
    B(sh, x0, x1, -0.1, 0.0, z0 + 1.2, z1, "M_Concrete", mats={"+y": "M_Jangpan"})
    B(sh, x0, 1.55, -0.1, 0.0, z0, z0 + 1.2, "M_Concrete", mats={"+y": "M_Jangpan"})
    B(sh, 1.55, x1, -0.25, -0.15, z0, z0 + 1.2, "M_Concrete", mats={"+y": "M_Terrazzo"})          # 현관(한 단 낮음)
    B(sh, 1.55, 1.6, -0.15, 0.0, z0, z0 + 1.2, "M_Wood_Light")
    B(sh, 1.55, x1, -0.15, 0.0, z0 + 1.15, z0 + 1.2, "M_Wood_Light")
    col(x0, x1, -0.3, 0.0, z0, z1, "Ground")
    B(sh, x0, x1, H, H + 0.1, z0, z1, "M_Concrete", mats={"-y": "M_Ceiling"})
    wall_x(sh, z1, x0, x1, H, -1, "M_Wallpaper", holes=[(-0.9, 1.7, 0.9, 2.0)], band=(0.08, "M_Wainscot"))
    wall_x(sh, z0, x0, x1, H, 1, "M_Wallpaper", holes=[(1.85, 2.75, 0.0, 2.05)], band=(0.08, "M_Wainscot"))
    wall_z(sh, x0, z0, z1, H, 1, "M_Wallpaper", band=(0.08, "M_Wainscot"))
    wall_z(sh, x1, z0, z1, H, -1, "M_Wallpaper", band=(0.08, "M_Wainscot"))
    window(sh, "x", z1, -0.9, 1.7, 0.9, 2.0, 1, "M_View_Fortress", bars=True, frame="M_Metal_Grey", panes=2, view_dist=1.4)
    door_panel(sh, "x", z0 + 0.06, 1.85, 2.75, 2.0, "M_Metal_Green", frame="M_Metal_Green", out_sign=-1)
    col(x0, x1, 0.0, 2.6, z0 - 0.4, z0 - 0.12)
    fx = part("Fixtures")
    cyl(fx, (0.2, H - 0.06, 0.8), UP, 0.3, 0.26, 0.06, 20, "M_Fluor_Panel", caps=(True, False))   # 둥근 천장 등
    cyl(fx, (0.2, H - 0.01, 0.8), UP, 0.31, 0.31, 0.01, 20, "M_AC_White", caps=(True, True))
    # ---- 부엌(서벽 — 스타일 프레임 s03 처럼 왼쪽 벽을 따라)
    k = part("Kitchenette")
    kz0, kz1 = -1.3, 1.45
    B(k, x0, x0 + 0.6, 0.0, 0.85, kz0, kz1, "M_Cabinet_Green", mats={"+y": "M_Stainless"}, collide="Default")
    B(k, x0, x0 + 0.62, 0.85, 0.88, kz0, kz1, "M_Stainless")
    B(k, x0 + 0.08, x0 + 0.5, 0.7, 0.88, -0.35, 0.35, "M_Stainless_Dark")
    seg_box(k, (x0 + 0.05, 0.88, 0.0), (x0 + 0.05, 1.12, 0.0), 0.025, 0.025, "M_Stainless")
    B(k, x0 + 0.08, x0 + 0.5, 0.88, 0.95, 0.6, 1.25, "M_Black_Plastic")
    for zz in (0.75, 1.1):
        cyl(k, (x0 + 0.3, 0.95, zz), UP, 0.08, 0.08, 0.01, 12, "M_Stainless_Dark", caps=(False, True))
    B(k, x0, x0 + 0.35, 1.5, 2.15, kz0, kz1, "M_Cabinet_Green")
    for zz in (-0.9, -0.1, 0.7):
        seg_box(k, (x0 + 0.36, 1.55, zz), (x0 + 0.36, 1.68, zz), 0.015, 0.015, "M_Steel_Frame")
    B(k, x0 + 0.02, x0 + 0.62, 0.0, 1.42, kz1 + 0.05, kz1 + 0.65, "M_AC_White", collide="Default")
    seg_box(k, (x0 + 0.64, 1.0, kz1 + 0.12), (x0 + 0.64, 1.25, kz1 + 0.12), 0.02, 0.02, "M_Steel_Frame")
    B(k, x0 + 0.1, x0 + 0.5, 1.42, 1.62, kz1 + 0.15, kz1 + 0.55, "M_Plastic_Red")
    prop("rice_cooker", 1, (x0 + 0.3, 0.88, -0.85), 90.0)
    bowl_stack(k, x0 + 0.3, 0.88, -0.6, 0.07, 3, "M_AC_White")
    kimchi_box(k, x0 + 0.3, 0.88, 0.45, 0.26, 0.2, 0.14)
    # ---- 거실
    lv = part("Living")
    tx, tz = 0.3, 1.15
    cyl(lv, (tx, 0.29, tz), UP, 0.45, 0.45, 0.03, 28, "M_Wood_Light", caps=(True, True))
    for a in (45, 135, 225, 315):
        r = math.radians(a)
        seg_box(lv, (tx + 0.3 * math.cos(r), 0.0, tz + 0.3 * math.sin(r)), (tx + 0.3 * math.cos(r), 0.29, tz + 0.3 * math.sin(r)), 0.035, 0.035, "M_Wood_Counter", up=(1.0, 0.0, 0.0))
    col(tx - 0.45, tx + 0.45, 0.0, 0.32, tz - 0.45, tz + 0.45, "Default")
    for (cx, cz, r0) in ((-0.05, 1.05, 0.06), (0.15, 0.95, 0.055), (0.45, 1.05, 0.07), (0.62, 1.25, 0.05)):
        cyl(lv, (tx + cx - 0.3, 0.32, tz + cz - 1.15), UP, r0 * 0.6, r0, 0.05, 12, "M_AC_White", caps=(True, False))
    cup(lv, tx + 0.25, 0.32, tz - 0.25, "M_Paper_Blue")
    B(lv, tx - 0.25, tx + 0.05, 0.32, 0.335, tz - 0.32, tz - 0.12, "M_Paper_White")
    B(lv, tx - 0.32, tx + 0.32, 0.0, 0.06, tz + 0.42, tz + 0.9, "M_Cushion")
    B(lv, tx - 0.3, tx + 0.3, 0.0, 0.05, tz - 0.95, tz - 0.4, "M_Cushion")
    # 하늘 책상(북벽, 창 동쪽) + 책꽂이 + 스탠드 + 의자 + 책가방
    dx0, dx1, dz0, dz1 = 1.85, 2.9, z1 - 0.55, z1
    B(lv, dx0, dx1, 0.62, 0.65, dz0, dz1, "M_Wood_Light")
    B(lv, dx0, dx1, 0.65, 1.25, dz1 - 0.25, dz1, "M_Wood_Light")
    B(lv, dx0 + 0.02, dx1 - 0.02, 0.92, 0.94, dz1 - 0.24, dz1 - 0.02, "M_Wood_Counter")
    for i, m in enumerate(("M_Paper_Blue", "M_Paper_Pink", "M_Paper_Yellow", "M_Paper_Green", "M_Paper_Blue", "M_Paper_White")):
        B(lv, dx0 + 0.06 + 0.07 * i, dx0 + 0.12 + 0.07 * i, 0.94, 1.14 - 0.02 * (i % 3), dz1 - 0.22, dz1 - 0.04, m)
    for xx in (dx0 + 0.03, dx1 - 0.03):
        B(lv, xx - 0.02, xx + 0.02, 0.0, 0.62, dz0, dz1, "M_Wood_Light")
    col(dx0, dx1, 0.0, 1.25, dz0, dz1, "Default")
    seg_box(lv, (dx1 - 0.2, 0.65, dz1 - 0.12), (dx1 - 0.2, 0.95, dz1 - 0.12), 0.02, 0.02, "M_Steel_Frame")
    seg_box(lv, (dx1 - 0.2, 0.95, dz1 - 0.12), (dx1 - 0.32, 1.05, dz1 - 0.28), 0.02, 0.02, "M_Steel_Frame")
    cyl(lv, (dx1 - 0.32, 0.98, dz1 - 0.28), UP, 0.07, 0.03, 0.07, 10, "M_Paper_White", caps=(True, False))
    chair_simple(lv, (dx0 + dx1) / 2, dz0 - 0.25, 0.0, h=0.4)
    B(lv, dx0 - 0.35, dx0 - 0.05, 0.0, 0.32, dz1 - 0.35, dz1 - 0.08, "M_Fabric_Navy")
    # 낮은 책꽂이(남벽) + 스탠드 선풍기
    B(lv, -0.7, 0.6, 0.0, 0.85, z0, z0 + 0.32, "M_Wood_Light", collide="Default")
    for i in range(11):
        m = ("M_Paper_Blue", "M_Paper_Pink", "M_Paper_Yellow", "M_Paper_Green", "M_Paper_White")[i % 5]
        B(lv, -0.65 + 0.11 * i, -0.56 + 0.11 * i, 0.45, 0.72 - 0.03 * (i % 3), z0 + 0.33, z0 + 0.34, m)
    prop("stand_fan", 1, (1.0, 0.0, z0 + 0.45), 0.0)
    # 옷걸이(동벽): 파이프 틀 + 교복 2·셔츠 1
    rx, rz0, rz1 = x1 - 0.32, -0.3, 1.1
    for zz in (rz0, rz1):
        cyl(lv, (rx, 0.0, zz), UP, 0.015, 0.015, 1.6, 6, "M_Steel_Frame", caps=(False, True))
        seg_box(lv, (rx - 0.2, 0.03, zz), (rx + 0.2, 0.03, zz), 0.03, 0.03, "M_Steel_Frame")
    seg_box(lv, (rx, 1.6, rz0), (rx, 1.6, rz1), 0.025, 0.025, "M_Steel_Frame")
    for i, (zz, m) in enumerate(((0.0, "M_Fabric_Navy"), (0.3, "M_Fabric_Navy"), (0.58, "M_Fabric_White"), (0.82, "M_Paper_Blue"))):
        B(lv, rx - 0.22, rx + 0.22, 0.85 + 0.05 * (i % 2), 1.55, zz - 0.05, zz + 0.05, m)
    col(rx - 0.25, rx + 0.25, 0.0, 1.6, rz0, rz1, "Default")
    # 이불·전기장판(남서)
    B(lv, -2.75, -1.25, 0.0, 0.025, -3.25, -2.0, "M_Fabric_Futon")
    B(lv, -2.7, -1.35, 0.025, 0.2, -3.25, -2.85, "M_Fabric_Futon")
    B(lv, -2.68, -1.37, 0.2, 0.3, -3.22, -2.88, "M_Fabric_White")
    # 벽: 아빠 사진 틀(사진 면은 Unity 가 소켓에) · 망치 · 하늘 그림 · 벽시계
    B(lv, -1.98, -1.52, 1.17, 1.67, z1 - 0.03, z1, "M_Wainscot")
    sock("Sock_Photo_Dad", "photo", (-1.75, 1.42, z1 - 0.035), 180.0, w=0.36, h=0.46)
    seg_box(lv, (-1.32, 1.3, z1 - 0.04), (-1.32, 1.62, z1 - 0.04), 0.035, 0.03, "M_Wood_Counter")
    B(lv, -1.38, -1.26, 1.62, 1.68, z1 - 0.07, z1 - 0.02, "M_Steel_Frame")
    B(lv, x1 - 0.015, x1, 1.35, 1.72, 1.6, 2.1, "M_Paper_White")
    B(lv, x1 - 0.02, x1 - 0.015, 1.4, 1.62, 1.65, 2.05, "M_Paper_Green")
    cyl(lv, (x1 - 0.02, 2.0, -1.2), (-1.0, 0.0, 0.0), 0.15, 0.15, 0.03, 18, "M_AC_White", caps=(False, True))
    # 현관: 신발장 + 슬리퍼
    prop("shoe_rack", 1, (x1 - 0.3, -0.15, z0 + 0.65), 270.0)
    prop("slippers_pile", 1, (2.1, -0.15, z0 + 0.5), 0.0)
    # 불
    light("Ceiling", (0.2, 2.2, 0.8), "#FFF1DA", 1.8, 6.0)
    light("Window", (0.4, 1.6, 3.1), "#E9F0F4", 1.0, 4.0)
    sock("Sock_Spawn_Spawn", "spawn", (0.3, 0.0, -0.55), 0.0)
    door("Front", (2.3, -0.15, z0 + 0.55), 0.0, "나가기 — 골목")
    marker("Sleep", (-2.0, 0.0, -1.65), 180.0, "sleep", "잠자기")
    marker("Save", (2.35, 0.0, z1 - 1.05), 0.0, "save", "공책(저장)")
    marker("Ledger", (tx, 0.0, tz - 0.62), 0.0, "ledger", "가계부")
    marker("Haneul", (tx + 0.55, 0.0, tz + 0.55), 225.0, "haneul", "하늘이")
    detail_home(x0, x1, z0, z1, H, tx, tz, dx0, dx1, dz1)
    cam("s03", (2.5, 1.55, -2.45), (-1.0, 0.8, 2.6))
    cam("s04", (2.55, 1.5, 2.9), (-1.6, 0.45, -1.9))
    sock("Sock_Actor_하늘", "actor", (tx, 0.06, tz + 0.66), 180.0, state="SitFloor")
    sock("Sock_Seat_Braid", "seat", (tx, 0.0, tz + 1.12), 180.0)
    return dict(size=(W, D, H), title="시우네")


# ================================================================ 하루편의점 (8 × 10, h 2.7)
def conv():
    W, D, H = 8.0, 10.0, 2.7
    x0, x1, z0, z1 = -W / 2, W / 2, -D / 2, D / 2
    sh = part("Shell")
    floor_ceil(sh, x0, x1, z0, z1, H, "M_Tile_Store", "M_Ceiling")
    wall_x(sh, z1, x0, x1, H, -1, "M_Wall_Cream", holes=[(2.6, 3.6, 0.0, 2.1)], band=(0.12, "M_Haru_Slate"))
    wall_z(sh, x0, z0, z1, H, 1, "M_Wall_Cream", band=(0.12, "M_Haru_Slate"))
    wall_z(sh, x1, z0, z1, H, -1, "M_Wall_Cream", band=(0.12, "M_Haru_Slate"))
    wall_x(sh, z0, x0, x1, H, 1, "M_Wall_Cream", holes=[(x0 + 0.15, 0.6, 0.0, 2.45), (0.9, 2.1, 0.0, 2.25)])
    window(sh, "x", z0, x0 + 0.15, 0.6, 0.02, 2.45, -1, "M_View_Night", frame="M_Metal_Grey", panes=3, view_dist=1.6)
    door_panel(sh, "x", z0 - 0.06, 0.9, 2.1, 2.25, "M_Metal_Grey", frame="M_Metal_Grey", glass_top=True, out_sign=-1)
    col(x0, x1, 0.0, 2.6, z0 - 0.4, z0 - 0.12)
    door_panel(sh, "x", z1 + 0.06, 2.6, 3.6, 2.1, "M_Haru_Slate", out_sign=1)        # 창고 문(닫힘)
    B(sh, x0, x1, H - 0.35, H - 0.3, z1 - 0.05, z1, "M_Haru_Slate")                   # 벽 띠
    fx = part("Fixtures")
    for x in (-2.6, -0.6, 1.4):
        for z in (-3.0, -0.6, 1.8, 4.0):
            fluor_panel(fx, x, z, H, 0.6, 1.2)
    st = part("Store")
    # 냉장고 벽(북): 6 문
    fz0, fz1 = z1 - 0.75, z1
    B(st, x0, 1.7, 0.0, 0.15, fz0, fz1, "M_Haru_Slate", collide="Default")
    B(st, x0, 1.7, 2.2, 2.45, fz0, fz1, "M_Haru_Slate")
    for i in range(6):
        a = x0 + 0.95 * i
        b = a + 0.95
        B(st, a, b, 0.15, 2.2, z1 - 0.12, z1, "M_Products", mats={"-z": "M_Products"})          # 안쪽 상품 면(빛남)
        for yy in (0.6, 1.0, 1.4, 1.8):
            B(st, a + 0.04, b - 0.04, yy, yy + 0.02, fz0 + 0.1, z1 - 0.12, "M_Shelf_White")
        B(st, a, a + 0.04, 0.15, 2.2, fz0, fz0 + 0.06, "M_Steel_Frame")
        B(st, a + 0.04, b - 0.01, 0.17, 2.18, fz0 + 0.02, fz0 + 0.03, "M_Glass_Fridge")
        seg_box(st, (b - 0.1, 0.9, fz0 - 0.01), (b - 0.1, 1.5, fz0 - 0.01), 0.025, 0.025, "M_Steel_Frame")
    col(x0, 1.7, 0.0, 2.45, fz0, fz1, "Default")
    # 진열대 2 줄(양면, 4단) + 끝판
    for gx in (-2.6, -0.55):
        g0, g1 = -2.9, 2.7
        B(st, gx - 0.05, gx + 0.05, 0.0, 1.45, g0, g1, "M_Shelf_White")
        B(st, gx - 0.45, gx + 0.45, 0.0, 0.12, g0, g1, "M_Haru_Slate")
        for sgn in (-1, 1):
            xa_o, xb_o = (gx + 0.05, gx + 0.45) if sgn > 0 else (gx - 0.45, gx - 0.05)
            xr0, xr1 = (gx + 0.43, gx + 0.46) if sgn > 0 else (gx - 0.46, gx - 0.43)
            xa, xb = (gx + 0.06, gx + 0.38) if sgn > 0 else (gx - 0.38, gx - 0.06)
            for yy in (0.12, 0.45, 0.78, 1.11):
                B(st, xa_o, xb_o, yy, yy + 0.025, g0, g1, "M_Shelf_White")
                B(st, xr0, xr1, yy + 0.02, yy + 0.06, g0, g1, "M_Haru_Cream")
                B(st, xa, xb, yy + 0.025, yy + 0.29, g0 + 0.05, g1 - 0.05, "M_Products", mats={"+x": "M_Products", "-x": "M_Products"})
        for zz in (g0 - 0.06, g1):
            B(st, gx - 0.46, gx + 0.46, 0.0, 1.5, zz, zz + 0.06, "M_Haru_Slate")
        B(st, gx - 0.3, gx + 0.3, 1.5, 1.75, g0 - 0.04, g0 - 0.02, "M_Haru_Cream")
        col(gx - 0.46, gx + 0.46, 0.0, 1.5, g0 - 0.06, g1 + 0.06, "Default")
    # 계산대(동쪽, ㄱ 자) + 계산기·카드기·껌 쟁반·호빵 찜기
    cx0, cx1, cz0, cz1 = 2.3, 2.95, -3.7, -0.7
    B(st, cx0, cx1, 0.0, 1.0, cz0, cz1, "M_Haru_Slate", mats={"+y": "M_Haru_Cream"}, collide="Default")
    B(st, cx0, x1, 0.0, 1.0, cz1, cz1 + 0.6, "M_Haru_Slate", mats={"+y": "M_Haru_Cream"}, collide="Default")
    B(st, cx0 - 0.03, cx1 + 0.03, 1.0, 1.04, cz0 - 0.03, cz1 + 0.03, "M_Haru_Cream")
    B(st, cx0 + 0.15, cx0 + 0.55, 1.04, 1.16, -2.4, -2.0, "M_Black_Plastic")
    B(st, cx0 + 0.3, cx0 + 0.36, 1.16, 1.42, -2.35, -2.05, "M_Black_Plastic")
    B(st, cx0 + 0.24, cx0 + 0.3, 1.18, 1.42, -2.33, -2.07, "M_TV_Screen")
    col(cx0 + 0.15, cx0 + 0.55, 1.04, 1.45, -2.4, -2.0, "CamBlock")   # QA Q20: 포스기 화면 — 카메라만 막음(계산대 알바 카메라가 화면 뒤에서 시우를 못 봄)
    B(st, cx0 + 0.1, cx0 + 0.3, 1.04, 1.1, -1.7, -1.55, "M_Black_Plastic")
    B(st, cx0 - 0.02, cx0 + 0.4, 1.04, 1.1, -3.4, -2.9, "M_Products")
    B(st, cx0 + 0.05, cx0 + 0.6, 1.04, 1.45, -1.3, -0.85, "M_Glass_Fridge", mats={"-y": "M_Steel_Frame"})
    bowl_stack(st, cx0 + 0.33, 1.06, -1.08, 0.05, 3, "M_AC_White")
    # 계산대 뒤 선반(동벽): 작은 물건 면
    B(st, x1 - 0.35, x1, 0.0, 2.1, -3.8, -0.8, "M_Shelf_White", collide="Default")
    for yy in (0.9, 1.3, 1.7):
        B(st, x1 - 0.37, x1 - 0.33, yy, yy + 0.3, -3.75, -0.85, "M_Products", mats={"-x": "M_Products"})
    # 창가 먹는 자리(앞 유리): 선반 + 의자 4
    B(st, x0 + 0.2, 0.45, 1.0, 1.04, z0 + 0.15, z0 + 0.5, "M_Haru_Cream")
    for xx in (x0 + 0.3, 0.35):
        seg_box(st, (xx, 0.0, z0 + 0.3), (xx, 1.0, z0 + 0.3), 0.04, 0.04, "M_Steel_Frame")
    for i, xx in enumerate((-3.2, -2.3, -1.4, -0.5)):
        cyl(st, (xx, 0.0, z0 + 0.85), UP, 0.025, 0.025, 0.72, 8, "M_Steel_Frame", caps=(False, False))
        cyl(st, (xx, 0.72, z0 + 0.85), UP, 0.17, 0.17, 0.05, 14, "M_Haru_Slate", caps=(True, True))
    # 온수기(창가 끝)
    B(st, x0 + 0.05, x0 + 0.4, 1.04, 1.5, z0 + 0.6, z0 + 0.95, "M_AC_White")
    # 폐기 바구니(파랑, 계산대 옆)
    B(st, 1.75, 2.2, 0.0, 0.3, -0.5, -0.1, "M_Plastic_Blue", collide="Default")
    for i in range(4):
        B(st, 1.8 + 0.1 * i, 1.88 + 0.1 * i, 0.3, 0.36, -0.45, -0.15, "M_Products")
    prop("ice_cream_freezer", 1, (2.15, 0.0, -4.35), 270.0)
    prop("plastic_crates_stack", 1, (3.45, 0.0, 4.4), 180.0)
    prop("cardboard_boxes", 1, (x0 + 0.5, 0.0, 3.55), 90.0)
    light("Store_A", (-1.6, 2.5, -1.0), "#EEF6F2", 2.4, 8.0)
    light("Store_B", (-0.6, 2.5, 3.0), "#EEF6F2", 2.0, 7.0)
    light("Counter", (3.2, 2.5, -2.2), "#F6F2E6", 1.4, 5.0)
    sock("Sock_Spawn_Spawn", "spawn", (1.5, 0.0, -1.6), 345.0)
    sock("Sock_Spawn_Counter", "spawn", (3.5, 0.0, -2.2), 270.0)
    door("Front", (1.5, 0.0, -4.4), 0.0, "나가기 — 상가거리")
    marker("Job_Counter", (3.5, 0.0, -2.2), 270.0, "job_counter", "계산대")
    marker("Job_Stock", (2.9, 0.0, 3.7), 0.0, "job_stock", "진열(창고 상자)")
    marker("Job_Waste", (1.95, 0.0, 0.25), 180.0, "job_waste", "폐기")
    detail_conv(x0, x1, z0, z1, H)
    cam("s05", (0.3, 1.65, -4.55), (-2.2, 1.0, 3.2))
    cam("s06", (3.65, 1.6, -3.9), (-0.8, 1.1, 3.6))
    return dict(size=(W, D, H), title="하루편의점")


# ================================================================ 혜성고 2-3반 + 복도 (13 × 25, h 3.0)
DESK_X = (-5.6, -4.25, -2.9, -1.55, -0.2, 1.15)
DESK_Z = (9.7, 8.5, 7.3, 6.1, 4.9)


def school():
    W, D, H = 13.0, 25.0, 3.0
    x0, x1, z0, z1 = -W / 2, W / 2, -D / 2, D / 2
    CX0, CX1, CZ0, CZ1 = x0, 2.5, 3.0, z1          # 교실
    HX0, HX1 = 2.5, 5.5                            # 복도
    sh = part("Shell")
    B(sh, CX0, HX1, -0.1, 0.0, z0, z1, "M_Concrete", mats={"+y": "M_Floor_School"})
    col(CX0, HX1, -0.3, 0.0, z0, z1, "Ground")
    B(sh, CX0, HX1, H, H + 0.1, z0, z1, "M_Concrete", mats={"-y": "M_Ceiling"})
    # 교실 벽: 서(창 4) · 북(칠판) · 남(사물함 뒷벽) · 동(복도 쪽: 앞문·뒷문 + 위 창)
    win_holes = [(3.7 + 2.15 * i, 3.7 + 2.15 * i + 1.95, 0.9, 2.65) for i in range(4)]
    wall_z(sh, CX0, CZ0, CZ1, H, 1, "M_Wall_Cream", holes=win_holes, band=(0.9, "M_Wainscot"))
    for a, b, y0, y1 in win_holes:
        window(sh, "z", CX0, a, b, y0, y1, -1, "M_View_Fortress", frame="M_Metal_Grey", panes=2, view_dist=2.2)
        # 커튼(반쯤 걷힘)
        B(sh, CX0 + 0.12, CX0 + 0.16, y0 - 0.25, H - 0.08, a - 0.15, a + 0.35, "M_Curtain")
    seg_box(sh, (CX0 + 0.14, H - 0.1, CZ0 + 0.3), (CX0 + 0.14, H - 0.1, CZ1 - 0.3), 0.03, 0.03, "M_Steel_Frame")
    wall_x(sh, CZ1, CX0, CX1, H, -1, "M_Wall_Cream", band=(0.9, "M_Wainscot"))
    wall_x(sh, CZ0, CX0, CX1, H, 1, "M_Wall_Cream", band=(0.9, "M_Wainscot"))
    doors = [(3.65, 4.6, 0.0, 2.15), (10.9, 11.85, 0.0, 2.15)]
    high = [(5.0 + 1.6 * i, 5.0 + 1.6 * i + 1.3, 2.3, 2.8) for i in range(4)]
    wall_z(sh, CX1, CZ0, CZ1, H, -1, "M_Wall_Cream", holes=doors + high, mat_out="M_Wall_Cream", band=(0.9, "M_Wainscot"))
    for a, b, y0, y1 in high:
        window(sh, "z", CX1, a, b, y0, y1, 1, None, frame="M_Steel_Frame", panes=2, glass="M_Glass_Frosted")
    door_panel(sh, "z", CX1 + 0.06, 3.65, 4.6, 2.15, "M_Door_School", glass_top=True, out_sign=1, open_frac=0.92)   # 뒷문 열림
    door_panel(sh, "z", CX1 + 0.06, 10.9, 11.85, 2.15, "M_Door_School", glass_top=True, out_sign=1)
    # 옆 교실(2-2반) 벽: 교실 남쪽, 복도 서쪽
    wall_z(sh, HX0, z0, CZ0 - T, H, -1, "M_Wall_Cream", holes=[(-4.0, -3.05, 0.0, 2.15), (2.0, 2.95, 0.0, 2.15)], band=(0.9, "M_Wainscot"))
    for u0 in (-4.0, 2.0):
        door_panel(sh, "z", HX0 + 0.06, u0, u0 + 0.95, 2.15, "M_Door_School", glass_top=True, out_sign=1)
    # 복도: 동벽 창(긴 줄) · 남쪽 끝 창 · 북쪽 끝 벽
    cwin = [(z0 + 1.2 + 3.0 * i, z0 + 1.2 + 3.0 * i + 2.4, 0.9, 2.5) for i in range(8)]
    cwin = [w for w in cwin if not (w[0] < -7.9 and w[1] > -10.2)]          # 현관 쪽 문 자리(-10.0 ~ -8.0)는 창 빼기
    wall_z(sh, HX1, z0, z1, H, -1, "M_Wall_Cream", holes=cwin + [(-10.0, -8.0, 0.0, 2.3)], band=(0.9, "M_Wainscot"))
    door_panel(sh, "z", HX1 + 0.06, -10.0, -9.0, 2.3, "M_Metal_Grey", frame="M_Metal_Grey", glass_top=True, out_sign=1)
    door_panel(sh, "z", HX1 + 0.06, -9.0, -8.0, 2.3, "M_Metal_Grey", frame="M_Metal_Grey", glass_top=True, out_sign=1)
    col(HX1 + 0.12, HX1 + 0.5, 0.0, 2.6, -10.0, -8.0)
    for a, b, y0, y1 in cwin:
        window(sh, "z", HX1, a, b, y0, y1, 1, "M_View_Fortress", frame="M_Metal_Grey", panes=2, view_dist=2.6)
    wall_x(sh, z0, HX0, HX1, H, 1, "M_Wall_Cream", holes=[(2.9, 5.1, 0.9, 2.6)], band=(0.9, "M_Wainscot"))
    window(sh, "x", z0, 2.9, 5.1, 0.9, 2.6, -1, "M_View_Fortress", frame="M_Metal_Grey", panes=2, view_dist=2.6)
    wall_x(sh, z1, HX0, HX1, H, -1, "M_Wall_Cream", band=(0.9, "M_Wainscot"))
    # 신발장(복도 서벽 남쪽) — 칸 격자
    sx0 = HX0
    B(sh, sx0, sx0 + 0.38, 0.0, 1.5, -11.6, -6.4, "M_Locker_Grey", collide="Default")
    for i in range(1, 13):
        zz = -11.6 + 5.2 * i / 13
        B(sh, sx0 + 0.38, sx0 + 0.39, 0.0, 1.5, zz - 0.01, zz + 0.01, "M_Steel_Frame")
    for yy in (0.3, 0.6, 0.9, 1.2):
        B(sh, sx0 + 0.38, sx0 + 0.39, yy - 0.01, yy + 0.01, -11.6, -6.4, "M_Steel_Frame")
    fx = part("Fixtures")
    for x in (-4.6, -1.6, 1.0):
        for z in (5.2, 8.0, 10.8):
            fluor_panel(fx, x, z, H, 0.3, 1.2)
    for z in range(-11, 12, 4):
        fluor_panel(fx, 4.0, float(z), H, 0.3, 1.2)
    cl = part("Classroom")
    # 칠판 + 분필 받침 + 양옆 게시판 + 시계·스피커
    bx0, bx1 = -4.6, 0.6
    B(cl, bx0 - 0.06, bx1 + 0.06, 0.84, 2.16, CZ1 - 0.06, CZ1, "M_Steel_Frame")
    B(cl, bx0, bx1, 0.9, 2.1, CZ1 - 0.065, CZ1 - 0.06, "M_Chalkboard")
    B(cl, bx0, bx1, 0.86, 0.9, CZ1 - 0.14, CZ1 - 0.06, "M_Steel_Frame")
    for i in range(5):
        B(cl, bx0 + 0.3 + 0.12 * i, bx0 + 0.38 + 0.12 * i, 0.9, 0.915, CZ1 - 0.12, CZ1 - 0.1, ("M_Paper_White", "M_Paper_Yellow", "M_Paper_Pink")[i % 3])
    for (a, b) in ((-6.2, -5.0), (1.0, 2.2)):
        B(cl, a, b, 1.2, 2.1, CZ1 - 0.03, CZ1, "M_Cork")
        for j in range(3):
            B(cl, a + 0.08 + 0.38 * j, a + 0.38 + 0.38 * j, 1.35 + 0.12 * (j % 2), 1.8 + 0.1 * (j % 2), CZ1 - 0.035, CZ1 - 0.03, ("M_Paper_White", "M_Paper_Blue", "M_Paper_Yellow")[j])
    cyl(cl, (-2.0, 2.55, CZ1 - 0.02), (0.0, 0.0, -1.0), 0.16, 0.16, 0.04, 20, "M_Steel_Frame", caps=(False, True))
    cyl(cl, (-2.0, 2.55, CZ1 - 0.06), (0.0, 0.0, -1.0), 0.14, 0.14, 0.005, 20, "M_AC_White", caps=(False, True))
    seg_box(cl, (-2.0, 2.55, CZ1 - 0.07), (-2.0, 2.66, CZ1 - 0.07), 0.012, 0.004, "M_Black_Plastic")
    seg_box(cl, (-2.0, 2.55, CZ1 - 0.07), (-1.92, 2.52, CZ1 - 0.07), 0.012, 0.004, "M_Black_Plastic")
    B(cl, -1.0, -0.6, 2.4, 2.7, CZ1 - 0.15, CZ1, "M_Black_Plastic")
    # 교탁 + 선생님 책상
    B(cl, -2.45, -1.55, 0.0, 1.05, 11.05, 11.6, "M_Wood_Light", mats={"-z": "M_Wood_Counter"}, collide="Default")
    B(cl, -2.5, -1.5, 1.05, 1.1, 11.0, 11.65, "M_Wood_Counter")
    B(cl, 0.7, 2.1, 0.72, 0.76, 11.3, 12.0, "M_Wood_Light")
    B(cl, 0.75, 1.15, 0.0, 0.72, 11.32, 11.98, "M_Steel_Frame", collide="Default")
    B(cl, 1.95, 2.05, 0.0, 0.72, 11.32, 11.98, "M_Steel_Frame")
    B(cl, 1.4, 1.75, 0.76, 0.98, 11.6, 11.9, "M_Paper_White")
    # 뒤: 사물함(4단 격자) + 게시판 + 게시물
    lz0, lz1 = CZ0, CZ0 + 0.42
    B(cl, -6.2, 1.8, 0.0, 1.2, lz0, lz1, "M_Locker_Grey", collide="Default")
    nx = 20
    for i in range(nx + 1):
        xx = -6.2 + 8.0 * i / nx
        B(cl, xx - 0.01, xx + 0.01, 0.0, 1.2, lz1, lz1 + 0.01, "M_Steel_Frame")
    for yy in (0.3, 0.6, 0.9, 1.2):
        B(cl, -6.2, 1.8, yy - 0.01, yy + 0.01, lz1, lz1 + 0.01, "M_Steel_Frame")
    for i in range(nx):
        xx = -6.2 + 8.0 * (i + 0.5) / nx
        for yy in (0.15, 0.45, 0.75, 1.05):
            B(cl, xx - 0.04, xx + 0.04, yy - 0.012, yy + 0.012, lz1 + 0.01, lz1 + 0.02, "M_Black_Plastic")
    B(cl, -5.5, 0.5, 1.45, 2.45, CZ0, CZ0 + 0.03, "M_Cork")
    for j, (a, w, y, h, m) in enumerate(((-5.3, 0.9, 1.6, 0.7, "M_Paper_White"), (-4.2, 0.8, 1.7, 0.6, "M_Paper_Blue"), (-3.2, 1.1, 1.55, 0.75, "M_Paper_Pink"),
                                          (-1.9, 0.7, 1.75, 0.55, "M_Paper_Yellow"), (-1.0, 0.9, 1.6, 0.7, "M_Paper_Green"), (0.0, 0.4, 1.9, 0.4, "M_Paper_White"))):
        B(cl, a, a + w, y, y + h, CZ0 + 0.03, CZ0 + 0.035, m)
    # 청소함(뒤 모서리)
    B(cl, 1.85, 2.38, 0.0, 1.8, CZ0, CZ0 + 0.45, "M_Locker_Grey", collide="Default")
    # 책상·의자 30(소품) + 학생 자리
    n = 0
    seat_names = {}
    for r, zz in enumerate(DESK_Z):
        for c, xx in enumerate(DESK_X):
            n += 1
            prop("school_desk_chair", n, (xx, 0.0, zz), 0.0)
            col(xx - 0.33, xx + 0.33, 0.0, 0.75, zz - 0.2, zz + 0.25, "Default")
            sock("Sock_Desk_%d_%d" % (r, c), "desk", (xx, 0.0, zz - 0.42), 0.0, row=r, col=c)
            seat_names[(r, c)] = n
    prop("stand_fan", 1, (CX0 + 0.45, 0.0, CZ0 + 0.6), 45.0)
    light("Class_A", (-3.0, 2.7, 6.0), "#F3F4EC", 2.4, 8.0)
    light("Class_B", (-1.5, 2.7, 10.0), "#F3F4EC", 2.0, 7.0)
    light("Hall", (4.0, 2.7, 0.0), "#F3F4EC", 1.8, 10.0)
    light("Window", (CX0 + 0.6, 2.0, 7.8), "#FFF4DE", 1.4, 5.5)
    # 자리
    sock("Sock_Spawn_Spawn", "spawn", (0.47, 0.0, 6.1), 0.0)       # 교실 통로(뒤 사물함·뒷벽까지 2.7m)
    sock("Sock_Spawn_BackDoor", "spawn", (3.75, 0.0, 4.1), 270.0)
    sock("Sock_Spawn_Hall", "spawn", (4.0, 0.0, -10.3), 180.0)
    door("Front", (4.75, 0.0, -9.0), 270.0, "나가기 — 교문")
    marker("Seat", (DESK_X[4], 0.0, DESK_Z[4] - 0.42), 0.0, "seat", "내 자리")
    marker("Class", (-2.0, 0.0, 10.55), 180.0, "class", "수업")
    detail_school(CX0, CX1, CZ0, CZ1, H, lz1)
    cam("s07", (2.2, 1.6, 4.05), (-3.0, 1.1, 11.0))
    cam("s08", (-0.6, 1.7, 11.7), (1.0, 1.0, 3.4))
    sock("Sock_Seat_시우", "seat", (DESK_X[4], 0.0, DESK_Z[4] - 0.42), 0.0)
    sock("Sock_Actor_도경민", "actor", (-2.0, 0.0, 11.95), 180.0, state="TalkOpen")
    return dict(size=(W, D, H), title="혜성고(2-3반·복도)")


# ================================================================ 룩데브 ⑥ 디테일(docs/14 4-6, 2026-10-26) — 부분 'Detail14'(Unity 비교 사진에서 켜고 끔)
def _spoon_can(mb, x, y, z):
    cyl(mb, (x, y, z), UP, 0.04, 0.04, 0.12, 10, "M_Stainless", caps=(True, False))
    for k in range(5):
        a = k * 1.25
        seg_box(mb, (x + 0.015 * math.cos(a), y + 0.1, z + 0.015 * math.sin(a)), (x + 0.03 * math.cos(a), y + 0.22, z + 0.03 * math.sin(a)), 0.008, 0.004, "M_Stainless")


def _dish(mb, x, y, z, r, food):
    cyl(mb, (x, y, z), UP, r * 0.75, r, 0.025, 12, "M_AC_White", caps=(True, False))
    cyl(mb, (x, y + 0.012, z), UP, r * 0.62, r * 0.55, 0.02, 10, food, caps=(False, True))


def _books(mb, x0, y, z0, z1, n, seed, along="z"):
    import random as _r
    rnd = _r.Random(seed)
    mats = ("M_Paper_Blue", "M_Paper_Pink", "M_Paper_Yellow", "M_Paper_Green", "M_Paper_White", "M_Fabric_Navy", "M_Plastic_Red")
    u = 0.0
    for k in range(n):
        w = rnd.uniform(0.02, 0.045)
        h = rnd.uniform(0.16, 0.26)
        dep = rnd.uniform(0.14, 0.2)
        if along == "z":
            B(mb, x0, x0 + dep, y, y + h, z0 + u, z0 + u + w, mats[rnd.randrange(len(mats))])
        else:
            B(mb, x0 + u, x0 + u + w, y, y + h, z0, z0 + dep, mats[rnd.randrange(len(mats))])
        u += w + 0.004
        if (z0 if along == "z" else x0) + u > (z1 if along == "z" else x0 + (z1 - z0)):
            break


def detail_gukbap(x0, x1, z0, z1, KZ, KX, H, tables):
    d = part("Detail14")
    # 벽 그림: 포스터(서벽 홀) · 원산지 표시(동벽) · 달력(문 옆 남벽)
    pic(d, "z", x0, -2.2, -1.4, 1.35, 2.47, -1, "M_D14_Poster_Gukbap")
    pic(d, "z", x1, -2.9, -2.15, 1.3, 2.35, 1, "M_D14_Origin_Gukbap")
    pic(d, "x", z0, 3.75, 4.3, 1.45, 2.24, -1, "M_D14_Calendar_3")
    # 식탁마다 수저통·반찬 두 접시(김치·깍두기)·물병
    for ti, (tx, tz) in enumerate(tables):
        _spoon_can(d, tx + 0.3, 0.72, tz - 0.22)
        _dish(d, tx - 0.18, 0.72, tz + 0.02, 0.075, "M_Plastic_Red")
        _dish(d, tx + 0.05, 0.72, tz + 0.18, 0.07, "M_Paper_Yellow")
        cyl(d, (tx - 0.32, 0.72, tz - 0.2), UP, 0.04, 0.04, 0.22, 10, "M_Glass_Frosted", caps=(True, True))
    # 정수기 옆 컵 쌓음 · 계산대 사탕 그릇 · 영수증 꽂이
    B(d, x1 - 0.72, x1 - 0.32, 0.86, 0.95, -1.5, -1.2, "M_Stainless")
    for k in range(3):
        for j in range(4 - k):
            cup(d, x1 - 0.66 + 0.075 * j + 0.037 * k, 0.95 + 0.075 * k, -1.35)
    cyl(d, (2.75, 1.05, -2.6), UP, 0.07, 0.09, 0.05, 12, "M_AC_White", caps=(True, False))
    for k in range(6):
        cyl(d, (2.72 + 0.03 * (k % 3), 1.09, -2.62 + 0.03 * (k // 3)), UP, 0.012, 0.012, 0.012, 6, ("M_Plastic_Red", "M_Paper_Yellow", "M_Paper_Green")[k % 3], caps=(True, True))
    seg_box(d, (2.05, 1.05, -2.6), (2.05, 1.22, -2.6), 0.008, 0.008, "M_Steel_Frame")
    # 옷걸이에 걸린 외투 2·모자
    for zz, m in ((-0.4, "M_Fabric_Navy"), (0.25, "M_Stainless_Dark")):
        B(d, x1 - 0.2, x1 - 0.08, 1.05, 1.72, zz - 0.18, zz + 0.18, m)
    cyl(d, (x1 - 0.12, 1.7, -0.05), (-1.0, 0.0, 0.0), 0.09, 0.08, 0.08, 12, "M_Paper_White", caps=(False, True))
    # 문 옆 우산꽂이(우산 3) · 바닥 매트
    cyl(d, (2.15, 0.0, z0 + 0.3), UP, 0.12, 0.12, 0.5, 14, "M_Stainless_Dark", caps=(True, False))
    for k, m in enumerate(("M_Fabric_Navy", "M_Plastic_Red", "M_Stainless_Dark")):
        seg_box(d, (2.12 + 0.04 * k, 0.1, z0 + 0.28), (2.1 + 0.05 * k, 0.86, z0 + 0.32), 0.03, 0.03, m)
    B(d, 2.4, 3.6, 0.0, 0.012, z0 + 0.1, z0 + 0.85, "M_Stainless_Dark")
    # 주방: 빨간 바구니(선반)·도마·고무장갑·쟁반 더미·노란 가스관
    for zz in (0.7, 1.55, 2.3):
        B(d, x0 + 0.04, x0 + 0.5, 1.38, 1.5, zz - 0.18, zz + 0.18, "M_Plastic_Red")
    B(d, -1.6, -1.05, 0.86, 0.885, z1 - 0.55, z1 - 0.2, "M_Wood_Light")
    for k in range(5):
        B(d, -3.95, -3.35, 0.85 + 0.018 * k, 0.86 + 0.018 * k, 0.75, 1.2, "M_Stainless")
    seg_box(d, (-0.85, 1.25, z1 - 0.02), (-0.85, 1.05, z1 - 0.02), 0.07, 0.02, "M_Plastic_Red")
    seg_box(d, (x0 + 0.05, 0.55, 3.6), (-1.95, 0.55, 3.6), 0.03, 0.03, "M_Paper_Yellow")
    seg_box(d, (-1.95, 0.55, 3.6), (-1.95, 0.25, 3.6), 0.03, 0.03, "M_Paper_Yellow")
    # 벽시계(서벽 위)
    cyl(d, (x0 + 0.02, 2.35, -0.6), (1.0, 0.0, 0.0), 0.16, 0.16, 0.03, 18, "M_AC_White", caps=(False, True))
    seg_box(d, (x0 + 0.055, 2.35, -0.6), (x0 + 0.055, 2.45, -0.6), 0.012, 0.004, "M_Black_Plastic")
    seg_box(d, (x0 + 0.055, 2.35, -0.6), (x0 + 0.055, 2.33, -0.52), 0.012, 0.004, "M_Black_Plastic")


def detail_home(x0, x1, z0, z1, H, tx, tz, dx0, dx1, dz1):
    d = part("Detail14")
    # 벽: 달력(부엌 옆 서벽) · 혜화 MMA 전단(동벽) · 하늘 그림(책상 위 북벽)
    pic(d, "z", x0, 1.75, 2.25, 1.35, 2.07, -1, "M_D14_Calendar_Home")
    pic(d, "z", x1, -0.95, -0.45, 1.45, 2.15, 1, "M_D14_Poster_Gym")
    pic(d, "x", z1, dx0 + 0.15, dx0 + 0.55, 1.4, 1.8, 1, "M_D14_Drawing_Haneul")
    # 밥상: 국그릇 2·수저 2·김 통
    for k, (cx, cz) in enumerate(((-0.12, 0.08), (0.14, 0.12))):
        cyl(d, (tx + cx, 0.32, tz + cz), UP, 0.04, 0.055, 0.05, 12, "M_Stainless", caps=(True, False))
        seg_box(d, (tx + cx + 0.08, 0.322, tz + cz - 0.05), (tx + cx + 0.08, 0.322, tz + cz + 0.1), 0.012, 0.004, "M_Stainless")
    B(d, tx - 0.05, tx + 0.07, 0.32, 0.36, tz - 0.1, tz - 0.02, "M_Paper_Green")
    # 책상: 교과서 더미·필통·책가방(의자) · 낮은 책꽂이 위 책 더
    for k in range(4):
        B(d, dx0 + 0.45, dx0 + 0.7, 0.65 + 0.025 * k, 0.673 + 0.025 * k, dz1 - 0.5 + 0.01 * k, dz1 - 0.32 + 0.01 * k, ("M_Paper_Blue", "M_Paper_Yellow", "M_Paper_Pink", "M_Paper_White")[k])
    seg_box(d, (dx0 + 0.15, 0.665, dz1 - 0.42), (dx0 + 0.35, 0.665, dz1 - 0.42), 0.05, 0.03, "M_Plastic_Red")
    B(d, (dx0 + dx1) / 2 - 0.15, (dx0 + dx1) / 2 + 0.15, 0.45, 0.8, dz1 - 1.08, dz1 - 0.92, "M_Fabric_Navy")
    _books(d, -0.66, 0.85, z0 + 0.04, z0 + 1.3, 22, 31, along="x")
    # 옷걸이 옆 빨래 바구니·갠 옷 · 현관 신발 두 켤레
    cyl(d, (x1 - 0.55, 0.0, 1.45), UP, 0.2, 0.24, 0.32, 14, "M_Plastic_Blue", caps=(True, False))
    B(d, x1 - 0.7, x1 - 0.4, 0.32, 0.38, 1.32, 1.58, "M_Fabric_White")
    for k, xx in enumerate((2.0, 2.45)):
        for sgn in (-1, 1):
            B(d, xx + 0.06 * sgn - 0.04, xx + 0.06 * sgn + 0.04, -0.15, -0.08, z0 + 0.25, z0 + 0.52, ("M_AC_White", "M_Fabric_Navy")[k])
    # 부엌: 냄비·양념통 줄·행주
    cyl(d, (x0 + 0.3, 0.95, 0.75), UP, 0.11, 0.11, 0.12, 14, "M_Stainless", caps=(True, False))
    for k in range(5):
        cyl(d, (x0 + 0.12, 0.88, -1.15 + 0.07 * k), UP, 0.025, 0.025, 0.1 + 0.02 * (k % 2), 8, ("M_Plastic_Red", "M_AC_White", "M_Paper_Yellow", "M_Paper_Green", "M_Plastic_Red")[k], caps=(True, True))
    B(d, x0 + 0.62, x0 + 0.64, 0.6, 0.8, -0.2, 0.05, "M_Paper_White")
    # 창턱 화분 2
    for xx in (-0.6, 1.3):
        cyl(d, (xx, 0.9, z1 - 0.12), UP, 0.06, 0.08, 0.1, 10, "M_Plastic_Red", caps=(True, False))
        cyl(d, (xx, 1.0, z1 - 0.12), UP, 0.1, 0.05, 0.14, 8, "M_Cabinet_Green", caps=(True, True))


def detail_conv(x0, x1, z0, z1, H):
    d = part("Detail14")
    # 진열대 가격표 띠(선반 앞, 단마다) · 끝판 행사 포스터 · 천장 분류 표지(양면)
    for gx in (-2.6, -0.55):
        g0, g1 = -2.9, 2.7
        for sgn in (-1, 1):
            xr = gx + 0.465 * sgn
            for k, yy in enumerate((0.12, 0.45, 0.78, 1.11)):
                off = 0.125 * ((k + (1 if sgn > 0 else 0)) % 2)
                pic(d, "z", xr, g0 + 0.05, g1 - 0.05, yy + 0.02, yy + 0.07, -sgn, "M_D14_POP_Strip", d=0.003, rect=(off, 0.0, 0.875, 1.0))
        pic(d, "x", g0 - 0.06, gx - 0.42, gx + 0.42, 0.25, 1.4, 1, "M_D14_Promo_Conv", d=0.004)
        seg_box(d, (gx, H, 0.0), (gx, H - 0.35, 0.0), 0.01, 0.01, "M_Steel_Frame")
        k = 0 if gx < -1 else 2
        pic(d, "x", 0.0, gx - 0.4, gx + 0.4, H - 0.6, H - 0.35, 1, "M_D14_Aisle_Signs", d=0.0, rect=(0.25 * k, 0.0, 0.25, 1.0))
        pic(d, "x", 0.0, gx - 0.4, gx + 0.4, H - 0.6, H - 0.35, -1, "M_D14_Aisle_Signs", d=0.0, rect=(0.25 * (k + 1), 0.0, 0.25, 1.0))
    # 냉장고 위 '음료' 표지 · 앞 유리 행사 포스터
    pic(d, "x", z1 - 0.76, x0 + 0.4, x0 + 1.5, 2.2, 2.42, 1, "M_D14_Aisle_Signs", d=0.004, rect=(0.0, 0.0, 0.25, 1.0))
    pic(d, "x", z0, 1.15, 1.5, 1.2, 1.75, -1, "M_D14_Promo_Conv", d=0.012)
    # 장바구니 더미(문 옆) — 파란 바구니 5단
    for k in range(5):
        B(d, 0.35, 0.8, 0.06 * k, 0.06 * k + 0.2, z0 + 0.25, z0 + 0.6, "M_Plastic_Blue", mats={"+y": "M_Black_Plastic"})
    # 커피 기계·컵 쌓음(창가 끝) · 계산대 껌·사탕 진열 · 바닥 줄 서기 표시
    B(d, x0 + 0.06, x0 + 0.42, 1.04, 1.55, z0 + 1.05, z0 + 1.4, "M_Black_Plastic")
    B(d, x0 + 0.42, x0 + 0.425, 1.25, 1.45, z0 + 1.12, z0 + 1.33, "M_TV_Screen")
    for k in range(6):
        cup(d, x0 + 0.24, 1.04 + 0.06 * k, z0 + 1.6, "M_AC_White")
    for k in range(8):
        B(d, 2.32 + 0.07 * (k % 4), 2.38 + 0.07 * (k % 4), 1.04, 1.12 + 0.03 * (k // 4), -0.98 + 0.08 * (k // 4), -0.92 + 0.08 * (k // 4), ("M_Plastic_Red", "M_Paper_Yellow", "M_Paper_Green", "M_Paper_Blue")[k % 4])
    for k in range(3):
        B(d, 1.55 - 0.55 * k, 1.85 - 0.55 * k, 0.0, 0.003, -2.3, -2.2, "M_Paper_Yellow")


def detail_school(CX0, CX1, CZ0, CZ1, H, lz1):
    d = part("Detail14")
    # 뒤 게시판(코르크 위 종이 그림) · 학급 표지(뒷문 위 복도 쪽) · 칠판 날짜 종이 · 태극기 액자
    pic(d, "x", CZ0 + 0.03, -5.45, 0.45, 1.5, 2.42, -1, "M_D14_Board_School", d=0.008)
    pic(d, "z", CX1 + 0.12, 4.65, 5.55, 2.25, 2.55, -1, "M_D14_ClassSign", d=-0.01)
    B(d, 0.2, 0.6, 1.7, 2.0, CZ1 - 0.07, CZ1 - 0.065, "M_Paper_White")
    B(d, -3.85, -3.25, 2.25, 2.65, CZ1 - 0.02, CZ1, "M_AC_White")
    B(d, -3.8, -3.3, 2.32, 2.58, CZ1 - 0.025, CZ1 - 0.02, "M_Plastic_Red")
    # 사물함 위 물건: 가방·공·책 더미 · 사물함 문 이름표
    import random as _r
    rnd = _r.Random(77)
    xx = -6.1
    while xx < 1.6:
        kind = rnd.randrange(4)
        if kind == 0:
            B(d, xx, xx + 0.32, 1.2, 1.45, CZ0 + 0.06, CZ0 + 0.34, ("M_Fabric_Navy", "M_Plastic_Blue", "M_Plastic_Red")[rnd.randrange(3)])
            xx += 0.4
        elif kind == 1:
            cyl(d, (xx + 0.13, 1.2, CZ0 + 0.2), UP, 0.12, 0.12, 0.24, 12, "M_Plastic_Red", caps=(True, True))
            xx += 0.3
        elif kind == 2:
            for k in range(rnd.randint(2, 5)):
                B(d, xx, xx + 0.24, 1.2 + 0.03 * k, 1.228 + 0.03 * k, CZ0 + 0.08, CZ0 + 0.3, ("M_Paper_White", "M_Paper_Blue", "M_Paper_Yellow")[k % 3])
            xx += 0.32
        else:
            xx += rnd.uniform(0.2, 0.6)
    for i in range(20):
        x = -6.2 + 8.0 * (i + 0.5) / 20
        for yy in (0.27, 0.57, 0.87):
            B(d, x - 0.07, x + 0.07, yy - 0.02, yy, lz1 + 0.02, lz1 + 0.024, "M_Paper_White")
    # 책상 옆 가방 · 책상 위 교과서(자리 30 중 2/3)
    for r, zz in enumerate(DESK_Z):
        for c, xx in enumerate(DESK_X):
            k = r * 6 + c
            if k % 2 == 0:
                B(d, xx + 0.3, xx + 0.36, 0.25, 0.62, zz - 0.08, zz + 0.16, ("M_Fabric_Navy", "M_Black_Plastic", "M_Plastic_Blue")[k % 3])
            if k % 3 != 1:
                B(d, xx - 0.18, xx + 0.05, 0.75, 0.77, zz - 0.12, zz + 0.06, ("M_Paper_Blue", "M_Paper_White", "M_Paper_Yellow", "M_Paper_Green")[k % 4])
    # 청소함 옆 빗자루·쓰레받기 · 교탁 위 출석부·분필 통
    seg_box(d, (CX1 - 0.18, 0.0, CZ0 + 0.55), (CX1 - 0.12, 1.25, CZ0 + 0.5), 0.03, 0.03, "M_Wood_Light")
    B(d, CX1 - 0.3, CX1 - 0.05, 0.0, 0.25, CZ0 + 0.7, CZ0 + 0.76, "M_Plastic_Blue")
    B(d, -2.3, -2.0, 1.1, 1.13, 11.1, 11.35, "M_Paper_Blue")
    cyl(d, (-1.75, 1.1, 11.3), UP, 0.04, 0.04, 0.08, 10, "M_AC_White", caps=(True, False))


STAGES = {"Gukbap": gukbap, "Home": home, "Conv": conv, "School": school}


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(out=os.path.join(ROOT, "concept", "art", "env", "stages"), only=None, blend=None)
    i = 0
    while i < len(a):
        k = a[i].lstrip("-")
        if k in o and i + 1 < len(a):
            o[k] = a[i + 1]
            i += 2
        else:
            i += 1
    return o


def _jsonable(v):
    if isinstance(v, dict):
        return {k: _jsonable(x) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [_jsonable(x) for x in v]
    return v


def main():
    import bpy
    from envkit import blend
    o = args()
    os.makedirs(o["out"], exist_ok=True)
    keys = o["only"].split(",") if o["only"] else list(STAGES)
    for key in keys:
        t0 = time.time()
        reset()
        info = STAGES[key]()
        objs = [Obj("%s_%s" % (key, nm), key, "kit", lods=[mb, None, None]) for nm, mb in PARTS]
        for s in SOCKETS:
            kind = "prop" if s["kind"] == "prop" else "socket"
            inf = dict(s["info"])
            if kind == "socket":
                inf["sock_kind"] = s["kind"]
            objs.append(Obj(s["name"], key, kind, empty=(s["pos"], s["fwd"]), info=inf))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("St_" + key)
        bpy.context.scene.collection.children.link(coll)
        root, made = blend.build_district(objs, key, coll)
        base = os.path.join(o["out"], "St_" + key)
        blend.export_fbx(base + ".fbx", made)
        if o.get("blend"):
            bpy.ops.wm.save_as_mainfile(filepath=o["blend"].replace(".blend", "_%s.blend" % key))
        used = sorted({m for ob in objs for mb in ob.all_mbs() for m in mb.mats()})
        tris = {nm: mb.tris() for nm, mb in PARTS}
        W, D, H = info["size"]
        man = dict(
            generator="haengin1/tools/env/stages/build_stages.py", doc="haengin1/docs/09_M3_버티컬슬라이스_설계.md 9단계",
            key=key, title=info["title"], room=dict(w=W, d=D, h=H),
            axes="Unity 무대 로컬(X 동, Y 위, Z 북), 원점 = 방 가운데 바닥. 프리팹을 무대 루트 아래 원점·회전 0·배율 1 로",
            materials={k: dict(MATS[k], tiling=round(1.0 / MATS[k]["tile"], 4)) for k in used},
            tris=tris, tris_total=sum(tris.values()),
            sockets=[dict(name=s["name"], kind=s["kind"], pos=[round(v, 3) for v in s["pos"]], fwd=[round(v, 3) for v in s["fwd"]], info=_jsonable(s["info"])) for s in SOCKETS],
            colliders=COLLIDERS,
            fbx_bytes=os.path.getsize(base + ".fbx"),
        )
        with open(base + "_manifest.json", "w", encoding="utf-8", newline="\n") as f:
            json.dump(man, f, ensure_ascii=False, indent=1)
        print("[stages] %s: 부분 %d · 소켓 %d · 충돌 %d · 삼각형 %d · 재질 %d · %.1fs" % (key, len(PARTS), len(SOCKETS), len(COLLIDERS), man["tris_total"], len(used), time.time() - t0), flush=True)


if __name__ == "__main__":
    main()
