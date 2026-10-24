# -*- coding: utf-8 -*-
"""혜화 MMA 체육관 절차 키트 → concept/art/env/gym/Gym_Hyehwa.fbx + Gym_Hyehwa_manifest.json (설계: docs/12_체육관_설계.md 1·5·6-3장)

실행(저장소 haengin1/ 에서):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/gym/build_gym.py -- [옵션]
옵션:
  --out <폴더>            기본 concept/art/env/gym
  --format fbx|glb|both   기본 fbx (Unity 는 FBX — _LOD0/1 이름으로 LOD Group 자동)
  --blend <파일.blend>    확인용 블렌더 파일(커밋하지 않음)

좌표: Unity 그대로(X 동, Y 위, Z 북), 원점 = 지하 바닥 남서 모서리. 블렌더 변환·FBX 설정은 envkit/blend.py 와 같다.
오브젝트: <부분>_LOD0/1 (LOD 2단), Fluor_##(형광등, LOD 밖), Poster_<칸>_##(0~1 UV → M_Poster_Atlas 칸),
          Prop_<소품>_##(빈 오브젝트, +Z 정면, 속성 prop·glb), Sock_<종류>_<이름>(빈 오브젝트: 조명·NPC·드릴 자리·카메라·오디오)
envkit 의 geom/blend 를 그대로 쓰고(건물 키트와 같은 메시 규약), 재질은 GYM_MATS 로 MATS 를 늘린다(docs/12 5-1 표).
"""
import json
import math
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.dirname(HERE)                      # tools/env
ROOT = os.path.dirname(os.path.dirname(ENV))     # haengin1/
sys.path.insert(0, ENV)

from envkit.geom import MB, WORLD, box, cyl, tube, seg_box, UP, add, sub, mul, norm, cross  # noqa: E402
from envkit.build import Obj  # noqa: E402
from envkit.mats import MATS  # noqa: E402

GYM_TEX = "concept/art/env/gym/textures"
# docs/12 5-1 표. color = 텍스처 없을 때 평면 색, tile = 한 장이 덮는 m, uv world|unit
GYM_MATS = {
    "M_Mat_Vinyl":        dict(color="#4A6B8A", tile=1.0, uv="world", owner="gym", note="퍼즐 매트"),
    "M_Cage_Canvas":      dict(color="#E8E2D6", tile=2.0, uv="world", owner="gym", note="케이지 캔버스·매트 테두리"),
    "M_Rubber_Floor":     dict(color="#3A3A3A", tile=1.0, uv="world", owner="gym", note="고무 바닥"),
    "M_Concrete_Ceiling": dict(color="#B9B6AF", tile=2.4, uv="world", owner="gym", note="노출 천장"),
    "M_Pad_Vinyl":        dict(color="#7A3A32", tile=1.0, uv="world", owner="gym", note="케이지 기둥 패드·벽 레슬링 패드"),
    "M_Fluorescent":      dict(color="#EAF0EC", tile=1.0, uv="unit", owner="gym", note="형광등 확산판(발광)"),
    "M_Mirror":           dict(color="#B8BEC2", tile=1.0, uv="world", owner="gym", note="거울(평면 반사 꺼질 때 판)"),
    "M_Cage_Mesh":        dict(color="#1A1417", tile=1.0, uv="world", owner="gym", note="케이지 철망(알파 컷아웃, 양면)"),
    "M_Paint_Gym":        dict(color="#4F5B55", tile=2.0, uv="world", owner="gym", note="벽 아랫 띠 1.2m"),
    "M_Glass_Clear":      dict(color="#DCE4E8", tile=1.0, uv="world", owner="gym", note="사무실 유리(투명 0.15)"),
    "M_Steel_Dark":       dict(color="#2A2A2C", tile=1.0, uv="world", owner="gym", note="케이지 틀·레일·트러스·체인·난간"),
    "M_Timer_Red":        dict(color="#C83C32", tile=1.0, uv="unit", owner="gym", note="라운드 타이머 숫자판(발광)·응급함 띠"),
    "M_Poster_Atlas":     dict(color="#EFE9DA", tile=1.0, uv="unit", owner="gym", note="포스터 8칸(poster_atlas.json)"),
}
for k, v in GYM_MATS.items():
    MATS.setdefault(k, dict(v))
    p = os.path.join(ROOT, GYM_TEX, k + ".png")
    if os.path.exists(p):
        MATS[k]["texture"] = GYM_TEX + "/" + k + ".png"

# 포스터 아틀라스 칸(4열 × 2행, 위 행 = v 0.5~1)
POSTER_CELLS = {"cage_seoul_a": (0, 0), "cage_seoul_b": (1, 0), "rules_board": (2, 0), "weight_chart": (3, 0),
                "old_fight_poster": (0, 1), "belt_plaque": (1, 1), "blank_sign": (2, 1), "gym_banner": (3, 1)}

# ---- 치수(docs/12 1장)
W, D, H = 20.0, 13.0, 3.2          # 방 안치수, 천장
BEAM_Y = 2.8                       # 보 밑
WALL = 0.3
BAND = 1.2                         # 벽 아랫 띠
CAGE_C, CAGE_R, CAGE_PLAT = (14.5, 6.0), 3.1, 0.25
LOD_SCREEN = (0.12, 0.0, 0.0)

MAT_PLASTER, MAT_CONC, MAT_METAL = "M_Plaster_Cream", "M_Concrete", "M_Metal_Grey"


class Part:
    def __init__(self, name):
        self.name = name
        self.lod = [MB(), MB()]

    def mb(self, lod=0):
        return self.lod[lod]


SOCKETS = []     # dict(name, kind, pos, fwd, info)


def sock(name, kind, pos, fwd=(0.0, 0.0, 1.0), **info):
    SOCKETS.append(dict(name=name, kind=kind, pos=tuple(float(v) for v in pos), fwd=tuple(float(v) for v in fwd), info=info))


def prop(name, n, pos, fwd=(0.0, 0.0, 1.0)):
    sock("Prop_%s_%02d" % (name, n), "prop", pos, fwd, prop=name, glb="concept/art/env/gym/props/%s.glb" % name)


def both(part, fn):
    """LOD0·LOD1 둘 다에 같은 기하."""
    fn(part.lod[0])
    fn(part.lod[1])


def wall_band(mb, x0, x1, y0, y1, z0, z1, inner):
    """벽 상자 하나: 안쪽 면 아랫 1.2 는 M_Paint_Gym, 위는 회벽. inner = 안쪽 면 이름('+z' 등)."""
    yb = min(y1, max(y0, BAND))
    if yb > y0:
        box(mb, WORLD, x0, x1, y0, yb, z0, z1, MAT_PLASTER, mats={inner: "M_Paint_Gym"})
    if y1 > yb:
        box(mb, WORLD, x0, x1, yb, y1, z0, z1, MAT_PLASTER)


# ---------------------------------------------------------------- 방 껍데기
def room_shell():
    P = Part("Room_Shell")

    def shell(mb):
        # 바닥(고무) — 위 면만 고무, 옆·밑 콘크리트
        box(mb, WORLD, -WALL, W + WALL, -0.05, 0.0, -WALL, D + WALL, MAT_CONC, mats={"+y": "M_Rubber_Floor"})
        # 벽: 남(z 0) · 북(z 13, 계단실 x 0~2.1 빼고) · 동(x 20) · 서(x 0)
        wall_band(mb, -WALL, W + WALL, 0.0, H + 0.3, -WALL, 0.0, "+z")
        wall_band(mb, 2.1, W + WALL, 0.0, H + 0.3, D, D + WALL, "-z")
        wall_band(mb, W, W + WALL, 0.0, H + 0.3, 0.0, D, "-x")
        wall_band(mb, -WALL, 0.0, 0.0, H + 0.3, 0.0, D, "+x")
        # 천장(노출 콘크리트 밑면) — 계단실 위(x 0~2.1, z 9.5~13)는 뚫림
        box(mb, WORLD, 2.1, W, H, H + 0.3, 0.0, D, MAT_CONC, mats={"-y": "M_Concrete_Ceiling"})
        box(mb, WORLD, 0.0, 2.1, H, H + 0.3, 0.0, 9.5, MAT_CONC, mats={"-y": "M_Concrete_Ceiling"})
        # 보 2 + 기둥 1 + 부벽
        box(mb, WORLD, 0.0, W, BEAM_Y, H, 6.3, 6.7, MAT_CONC)
        box(mb, WORLD, 9.8, 10.2, BEAM_Y, H, 0.0, D, MAT_CONC)
        box(mb, WORLD, 9.75, 10.25, 0.0, BEAM_Y, 6.25, 6.75, MAT_CONC)
        box(mb, WORLD, 9.75, 10.25, 0.0, H, 0.0, 0.3, MAT_CONC)
        box(mb, WORLD, 9.75, 10.25, 0.0, H, D - 0.3, D, MAT_CONC)
        # 화장실 문(남벽 x 0.4~1.3) + 틀
        box(mb, WORLD, 0.4, 1.3, 0.0, 2.1, 0.0, 0.05, "M_Wood_Dark")
        box(mb, WORLD, 0.35, 1.35, 2.1, 2.18, 0.0, 0.08, MAT_METAL)
        # 매트 구역(파란 퍼즐) + 크림 테두리, 벽 레슬링 매트 띠
        box(mb, WORLD, 1.6, 10.2, 0.0, 0.03, 0.6, 6.0, "M_Mat_Vinyl")
        for a, b, c, d in ((1.5, 10.3, 0.5, 0.6), (1.5, 10.3, 6.0, 6.1), (1.5, 1.6, 0.6, 6.0), (10.2, 10.3, 0.6, 6.0)):
            box(mb, WORLD, a, b, 0.0, 0.03, c, d, "M_Cage_Canvas")
        box(mb, WORLD, 10.8, 17.6, 0.0, 0.03, 0.08, 2.0, "M_Mat_Vinyl")

    both(P, shell)
    mb = P.mb(0)
    # LOD0 디테일: 덕트 2, 전선관 2, 서류장(사무실), 응급함·시계(기둥), 라운드 타이머, 휴지통, 스피커 선반
    for z in (3.2, 9.8):
        tube(mb, [(0.5, 2.75, z), (19.5, 2.75, z)], 0.25, 12, MAT_METAL)
        cyl(mb, (10.0, 2.75, z), (0.0, 1.0, 0.0), 0.25, 0.25, 0.45, 12, MAT_METAL, caps=(False, False))
    for x in (5.0, 15.0):
        tube(mb, [(x, 3.1, 0.2), (x, 3.1, D - 0.2)], 0.04, 6, "M_Steel_Dark")
    # 응급함(기둥 북면) · 벽시계(기둥 남면)
    box(mb, WORLD, 9.83, 10.17, 1.35, 1.65, 6.75, 6.87, "M_AC_White")
    box(mb, WORLD, 9.83, 10.17, 1.48, 1.52, 6.87, 6.88, "M_Timer_Red")
    cyl(mb, (10.0, 2.5, 6.25), (0.0, 0.0, -1.0), 0.16, 0.16, 0.04, 16, "M_Steel_Dark", caps=(False, True))
    cyl(mb, (10.0, 2.5, 6.21), (0.0, 0.0, -1.0), 0.14, 0.14, 0.005, 16, "M_AC_White", caps=(False, True))
    seg_box(mb, (10.0, 2.5, 6.2), (10.0, 2.61, 6.2), 0.012, 0.004, "M_Steel_Dark")
    seg_box(mb, (10.0, 2.5, 6.2), (10.08, 2.46, 6.2), 0.012, 0.004, "M_Steel_Dark")
    # 라운드 타이머(남벽 x 10.5, h 2.5): 상자 + 발광 숫자판
    box(mb, WORLD, 10.25, 10.75, 2.35, 2.65, 0.0, 0.15, "M_Steel_Dark")
    box(mb, WORLD, 10.3, 10.7, 2.42, 2.58, 0.15, 0.155, "M_Timer_Red")
    # 휴지통(음수대 옆)
    cyl(mb, (1.9, 0.0, 12.6), UP, 0.18, 0.17, 0.6, 12, MAT_METAL, caps=(False, True))
    # 스피커 선반 2
    box(mb, WORLD, 12.65, 13.15, 1.95, 2.0, 12.6, D, MAT_METAL)
    box(mb, WORLD, 10.25, 10.75, 1.95, 2.0, 0.0, 0.4, MAT_METAL)
    return P


# ---------------------------------------------------------------- 계단실 + 1층 현관
def stairwell():
    P = Part("Stair")
    N, RISE, TREAD = 18, 0.19, 0.28
    Z0 = 8.0

    def core(mb):
        for i in range(N):
            z0 = Z0 + TREAD * i
            box(mb, WORLD, 0.0, 1.8, 0.0, RISE * (i + 1), z0, z0 + TREAD, MAT_CONC)
        top = RISE * N
        # 1층 현관 바닥·벽·천장(계단실 위 뚫린 자리를 덮음)
        box(mb, WORLD, 0.0, 1.8, top - 0.3, top, Z0 + TREAD * N, 15.6, MAT_CONC)
        wall_band(mb, 1.8, 2.1, 0.0, 6.2, 11.0, 15.9, "-x")                               # 동쪽 계단실 벽(윗 7단만 막음 — 아래는 난간뿐, 내려오며 방이 보임)
        box(mb, WORLD, 1.8, 2.1, H + 0.3, 6.2, 9.5, 11.0, MAT_PLASTER)                     # 그 아래 구간은 천장 위만 막음
        wall_band(mb, -WALL, 0.0, 0.0, 6.2, D, 15.9, "+x")                                # 서벽 연장
        box(mb, WORLD, -WALL, 0.0, H + 0.3, 6.2, 9.5, D, MAT_PLASTER)                      # 서벽 위(계단 뚫린 구간)
        box(mb, WORLD, 2.1, 2.4, H + 0.3, 6.2, 9.5, D + WALL, MAT_PLASTER)                 # 북벽 윗부분 메움
        box(mb, WORLD, -WALL, 2.1, 6.2, 6.5, 9.5, 15.9, MAT_CONC, mats={"-y": "M_Concrete_Ceiling"})
        # 현관 끝 벽 + 출입문(유리문, 밖은 대학로)
        box(mb, WORLD, -WALL, 0.2, top, 6.2, 15.6, 15.9, MAT_PLASTER)
        box(mb, WORLD, 1.6, 2.1, top, 6.2, 15.6, 15.9, MAT_PLASTER)
        box(mb, WORLD, 0.2, 1.6, top + 2.2, 6.2, 15.6, 15.9, MAT_PLASTER)
        box(mb, WORLD, 0.2, 1.6, top, top + 2.2, 15.7, 15.75, "M_Glass_Dark")
        box(mb, WORLD, 0.15, 1.65, top + 2.2, top + 2.28, 15.65, 15.8, MAT_METAL)

    both(P, core)
    mb = P.mb(0)
    # 난간(동쪽 벽 쪽 철 난간 + 서벽 손잡이), 기둥 5개
    a, b = (1.62, 0.9, Z0), (1.62, 0.9 + RISE * N, Z0 + TREAD * N)
    tube(mb, [a, b, (1.62, 0.9 + RISE * N, 15.4)], 0.025, 8, "M_Metal_Green")
    tube(mb, [(0.08, 0.9, Z0), (0.08, 0.9 + RISE * N, Z0 + TREAD * N)], 0.02, 8, "M_Metal_Green")
    for k in range(5):
        t = k / 4.0
        p = (1.62, RISE * (N * t) if k else 0.0, Z0 + TREAD * N * t)
        cyl(mb, (p[0], p[1], p[2]), UP, 0.02, 0.02, 0.9, 8, "M_Metal_Green", caps=(False, False))
    # 계단실 형광등(천장에서 길게 매달린 맨 등)
    fixture(mb, (0.9, 4.6, 11.5), 6.2, "Fluor_Stair")
    sock("Sock_Light_Stair", "light", (0.9, 4.55, 11.5), (0.0, -1.0, 0.0), light="fluor", flicker=1)
    sock("Sock_Audio_Street", "audio", (0.9, 5.0, 15.0), (0.0, 0.0, -1.0), audio="daehakro_street")
    sock("Sock_Spawn_Entrance", "spawn", (0.9, RISE * N, 14.5), (0.0, 0.0, -1.0))
    sock("Sock_Trigger_Shoes", "trigger", (1.2, 0.0, 7.6), (0.0, 0.0, -1.0), action="shoes_off")
    return P


def fixture(mb, c, ceil_y, name=None):
    """형광등 1개: 몸통 1.2×0.3×0.08(아래 면 발광) + 매단 봉 2."""
    x, y, z = c
    box(mb, WORLD, x - 0.6, x + 0.6, y, y + 0.08, z - 0.15, z + 0.15, MAT_METAL, mats={"-y": "M_Fluorescent"})
    for dx in (-0.45, 0.45):
        cyl(mb, (x + dx, y + 0.08, z), UP, 0.008, 0.008, max(0.05, ceil_y - y - 0.08), 6, "M_Steel_Dark", caps=(False, False))


# ---------------------------------------------------------------- 케이지
def cage():
    P = Part("Cage")
    cx, cz = CAGE_C
    R, PL = CAGE_R, CAGE_PLAT
    POST_H = 1.85
    ang = [math.radians(22.5 + 45 * k) for k in range(8)]
    V = [(cx + R * math.cos(a), 0.0, cz + R * math.sin(a)) for a in ang]
    door_k = min(range(8), key=lambda k: ((V[k][0] + V[(k + 1) % 8][0]) / 2 - (cx - R)) ** 2 + ((V[k][2] + V[(k + 1) % 8][2]) / 2 - cz) ** 2)

    def core(mb):
        top = [(x, PL, z) for x, _, z in V]
        mb.face(top, "M_Cage_Canvas", out=(0.0, 1.0, 0.0))
        for k in range(8):
            a, b = V[k], V[(k + 1) % 8]
            out = norm(sub(mul(add(a, b), 0.5), (cx, 0.0, cz)))
            mb.face([a, b, (b[0], PL, b[2]), (a[0], PL, a[2])], "M_Steel_Dark", out=out)
            # 철망(기둥 사이 한 장, 양면은 Unity 컬링 끔) — 문 칸은 문 짝만
            y0, y1 = PL + 0.05, PL + POST_H - 0.1
            if k != door_k:
                mb.face([(a[0], y0, a[2]), (b[0], y0, b[2]), (b[0], y1, b[2]), (a[0], y1, a[2])], "M_Cage_Mesh", out=out)
            # 기둥: 철 심 + 패드
            cyl(mb, (a[0], PL, a[2]), UP, 0.05, 0.05, POST_H + 0.05, 8, "M_Steel_Dark", caps=(False, True))
            cyl(mb, (a[0], PL, a[2]), UP, 0.12, 0.12, POST_H - 0.05, 10, "M_Pad_Vinyl", caps=(False, True))
            # 상단 패드 레일 + 하단 철 레일
            tube(mb, [(a[0], PL + POST_H - 0.05, a[2]), (b[0], PL + POST_H - 0.05, b[2])], 0.045, 8, "M_Pad_Vinyl")
            tube(mb, [(a[0], PL + 0.05, a[2]), (b[0], PL + 0.05, b[2])], 0.025, 6, "M_Steel_Dark")
        # 문(door_k 변의 가운데 0.9): 틀 + 철망 짝 + 걸쇠, 밖에 디딤 1단
        a, b = V[door_k], V[(door_k + 1) % 8]
        out = norm(sub(mul(add(a, b), 0.5), (cx, 0.0, cz)))
        d = norm(sub(b, a))
        m = mul(add(a, b), 0.5)
        L = math.dist((a[0], a[2]), (b[0], b[2]))
        half = 0.45
        l0, l1 = add(m, mul(d, -half)), add(m, mul(d, half))
        for p in (l0, l1):
            cyl(mb, (p[0], PL, p[2]), UP, 0.03, 0.03, POST_H - 0.1, 8, "M_Steel_Dark", caps=(False, True))
        # 문 양 옆 고정 철망
        for p, q in ((a, l0), (l1, b)):
            mb.face([(p[0], PL + 0.05, p[2]), (q[0], PL + 0.05, q[2]), (q[0], PL + POST_H - 0.1, q[2]), (p[0], PL + POST_H - 0.1, p[2])], "M_Cage_Mesh", out=out)
        mb.face([(l0[0], PL + 0.08, l0[2]), (l1[0], PL + 0.08, l1[2]), (l1[0], PL + POST_H - 0.15, l1[2]), (l0[0], PL + POST_H - 0.15, l0[2])], "M_Cage_Mesh", out=out)
        for yy in (PL + 0.08, PL + POST_H - 0.15):
            tube(mb, [(l0[0], yy, l0[2]), (l1[0], yy, l1[2])], 0.02, 6, "M_Steel_Dark")
        step_c = add(m, mul(out, 0.35))
        box(mb, WORLD, step_c[0] - 0.5, step_c[0] + 0.5, 0.0, PL / 2, step_c[2] - 0.3, step_c[2] + 0.3, "M_Steel_Dark")
        sock("Sock_CageDoor", "door", (m[0], PL, m[2]), out, width=0.9, hinge="left")

    both(P, core)
    mb = P.mb(0)
    # 트러스 4×4(y 2.6) + 행어 + 스포트 4
    ty = 2.6
    corners = [(cx - 2.0, ty, cz - 2.0), (cx + 2.0, ty, cz - 2.0), (cx + 2.0, ty, cz + 2.0), (cx - 2.0, ty, cz + 2.0)]
    for k in range(4):
        seg_box(mb, corners[k], corners[(k + 1) % 4], 0.08, 0.08, "M_Steel_Dark")
        c = corners[k]
        cyl(mb, (c[0], ty + 0.04, c[2]), UP, 0.015, 0.015, H - ty - 0.04, 6, "M_Steel_Dark", caps=(False, False))
        # 스포트 몸통(안쪽 아래를 향함)
        aim = norm(sub((cx, PL, cz), (c[0], ty, c[2])))
        cyl(mb, (c[0] - aim[0] * 0.05, ty - 0.02, c[2] - aim[2] * 0.05), aim, 0.07, 0.09, 0.22, 10, "M_Steel_Dark", caps=(True, False))
        sock("Sock_Light_Spot_%02d" % (k + 1), "light", (c[0], ty - 0.05, c[2]), aim, light="spot", angle=40, color="#F2E9D8")
    sock("Sock_Drill_D11", "drill", (cx, PL, cz), (-1.0, 0.0, 0.0), drill="D11")
    sock("Sock_NPC_Gojun_Cage", "npc", (cx + 0.8, PL, cz), (-1.0, 0.0, 0.0), npc="gojun")
    sock("Sock_NPC_Coach_CageDoor", "npc", (cx - R - 0.9, 0.0, cz), (1.0, 0.0, 0.0), npc="yunjun")
    return P


# ---------------------------------------------------------------- 사무실
def office():
    P = Part("Office")
    X0, Z0 = 14.2, 10.2

    def core(mb):
        # 서쪽 내벽(바닥~천장), 앞면: 문 구간(14.2~15.4) + 허리벽·유리·상부 벽(15.4~20)
        wall_band(mb, X0, X0 + 0.15, 0.0, H, Z0, D, "-x")
        box(mb, WORLD, X0, X0 + 0.15, 0.0, H, Z0, D, MAT_PLASTER)
        box(mb, WORLD, X0, 14.4, 0.0, 2.1, Z0, Z0 + 0.15, MAT_PLASTER)
        box(mb, WORLD, 15.3, 15.4, 0.0, 2.1, Z0, Z0 + 0.15, MAT_PLASTER)
        box(mb, WORLD, X0, 15.4, 2.1, H, Z0, Z0 + 0.15, MAT_PLASTER)
        wall_band(mb, 15.4, W, 0.0, 0.9, Z0, Z0 + 0.15, "-z")
        box(mb, WORLD, 15.4, W, 2.5, H, Z0, Z0 + 0.15, MAT_PLASTER)
        # 유리(한 장) + 알루미늄 틀·멀리언
        box(mb, WORLD, 15.45, W - 0.05, 0.9, 2.5, Z0 + 0.06, Z0 + 0.07, "M_Glass_Clear")
        for x in (15.4, 16.6, 17.8, 19.0, W - 0.05):
            box(mb, WORLD, x - 0.025, x + 0.025, 0.9, 2.5, Z0 + 0.03, Z0 + 0.1, MAT_METAL)
        for y in (0.9, 2.5):
            box(mb, WORLD, 15.4, W, y - 0.03, y + 0.03, Z0 + 0.03, Z0 + 0.1, MAT_METAL)
        # 문(작은 유리창) + 틀
        box(mb, WORLD, 14.4, 15.3, 0.0, 2.1, Z0 + 0.05, Z0 + 0.1, "M_Wood_Dark")
        box(mb, WORLD, 14.65, 15.05, 1.3, 1.9, Z0 + 0.04, Z0 + 0.11, "M_Glass_Clear")
        box(mb, WORLD, 14.35, 15.35, 2.1, 2.18, Z0 + 0.02, Z0 + 0.13, MAT_METAL)

    both(P, core)
    mb = P.mb(0)
    # 서류장(철제 4단) · 벨트 걸이 판 밑 선반 · 창가 선반
    box(mb, WORLD, 19.1, 19.55, 0.0, 1.32, 12.3, 12.92, MAT_METAL)
    for k in range(4):
        box(mb, WORLD, 19.12, 19.53, 0.08 + 0.31 * k, 0.1 + 0.31 * k, 12.29, 12.3, "M_Steel_Dark")
    box(mb, WORLD, 15.0, 16.8, 1.0, 1.04, 12.6, D, "M_Wood_Dark")
    sock("Sock_Light_Office", "light", (17.0, H - 0.05, 11.6), (0.0, -1.0, 0.0), light="area", size="1.2x0.3")
    sock("Sock_NPC_Coach_Desk", "npc", (17.0, 0.0, 12.72), (0.0, 0.0, -1.0), npc="yunjun", pose="sit")
    sock("Sock_Trigger_OfficeDoor", "trigger", (14.85, 0.0, 10.0), (0.0, 0.0, 1.0), action="office_enter")
    prop("office_desk", 1, (17.0, 0.0, 12.1), (0.0, 0.0, -1.0))
    prop("office_chair", 1, (17.0, 0.0, 12.72), (0.0, 0.0, -1.0))
    return P


# ---------------------------------------------------------------- 거울·벽 패드·샌드백 레일·웨이트 코너 바닥 등
def mirror():
    P = Part("Mirror")

    def core(mb):
        box(mb, WORLD, 1.6, 10.2, 0.4, 2.4, 0.02, 0.03, "M_Mirror")
        for x0, x1, y0, y1 in ((1.56, 1.6, 0.36, 2.44), (10.2, 10.24, 0.36, 2.44), (1.56, 10.24, 0.36, 0.4), (1.56, 10.24, 2.4, 2.44)):
            box(mb, WORLD, x0, x1, y0, y1, 0.0, 0.035, MAT_METAL)

    both(P, core)
    mb = P.mb(0)
    tube(mb, [(1.7, 1.0, 0.14), (10.1, 1.0, 0.14)], 0.02, 8, MAT_METAL)
    for x in (1.8, 4.6, 7.3, 10.0):
        tube(mb, [(x, 1.0, 0.03), (x, 1.0, 0.14)], 0.012, 6, MAT_METAL)
    return P


def wall_pads():
    P = Part("WallPads")

    def core(mb):
        for k in range(8):
            x0 = 10.8 + 0.85 * k
            box(mb, WORLD, x0, x0 + 0.8, 0.0, 1.9, 0.0, 0.08, "M_Pad_Vinyl")

    both(P, core)
    sock("Sock_Drill_D09", "drill", (14.0, 0.0, 1.0), (0.0, 0.0, -1.0), drill="D09")
    return P


def bag_rail():
    P = Part("BagRail")
    zs = [2.6 + 1.7 * i for i in range(6)]
    kinds = ["heavy_bag_long", "heavy_bag_short", "heavy_bag_long", "heavy_bag_long", "heavy_bag_short", "heavy_bag_long"]

    def core(mb):
        box(mb, WORLD, 18.9, 19.5, 2.72, 2.8, 2.0, 11.7, "M_Steel_Dark")
        for z in (2.0, 11.7):
            box(mb, WORLD, 18.95, 19.45, 2.8, H, z - 0.05, z + 0.05, "M_Steel_Dark")
        for x in (19.2,):
            for z in (4.9, 8.4):
                cyl(mb, (x, 2.8, z), UP, 0.02, 0.02, H - 2.8, 6, "M_Steel_Dark", caps=(False, False))

    both(P, core)
    mb = P.mb(0)
    n_long = n_short = 0
    for i, (z, k) in enumerate(zip(zs, kinds)):
        cyl(mb, (19.2, 2.65, z), UP, 0.03, 0.03, 0.07, 6, "M_Steel_Dark", caps=(True, True))
        if k == "heavy_bag_long":
            n_long += 1
            prop(k, n_long, (19.2, 2.72 - 2.0, z), (-1.0, 0.0, 0.0))
        else:
            n_short += 1
            prop(k, n_short, (19.2, 2.72 - 1.4, z), (-1.0, 0.0, 0.0))
        sock("Sock_Drill_D05_%02d" % (i + 1), "drill", (18.4, 0.0, z), (1.0, 0.0, 0.0), drill="D05", bag=k)
    prop("speed_bag_platform", 1, (19.0, 1.45, 0.9), (-1.0, 0.0, 0.0))
    sock("Sock_Drill_D06", "drill", (18.3, 0.0, 0.9), (1.0, 0.0, 0.0), drill="D06")
    return P


def lights():
    """형광등 15개(LOD 밖, 각각 오브젝트)."""
    out = []
    n = 0
    for z in (2.3, 6.0, 10.5):
        for i in range(5):
            x = 2.4 + 3.6 * i
            if z > 10.0 and x > 14.0:
                continue          # 사무실 위는 사무실 조명
            n += 1
            mb = MB()
            fixture(mb, (x, 2.75, z), H)
            out.append(Obj("Fluor_%02d" % n, "Gym", "fluor", single=mb, info=dict(row=z)))
            sock("Sock_Light_Fluor_%02d" % n, "light", (x, 2.72, z), (0.0, -1.0, 0.0), light="fluor", row=round(z, 1))
    return out


# ---------------------------------------------------------------- 포스터(0~1 UV → 아틀라스 칸)
def poster_quad(cell, c, n, w, h):
    """벽 위 포스터 한 장. c 중심, n = 보는 사람 쪽(벽 안쪽 법선), w·h m. 0~1 UV 를 아틀라스 칸으로."""
    right = norm(cross(n, (0.0, 1.0, 0.0)))        # Unity: 보는 사람(fwd=-n)의 오른쪽
    up = (0.0, 1.0, 0.0)
    hw, hh = w / 2, h / 2
    pts = [add(add(c, mul(right, -hw)), mul(up, -hh)), add(add(c, mul(right, hw)), mul(up, -hh)),
           add(add(c, mul(right, hw)), mul(up, hh)), add(add(c, mul(right, -hw)), mul(up, hh))]
    col, row = POSTER_CELLS[cell]
    u0, u1 = col / 4.0, (col + 1) / 4.0
    v1, v0 = 1.0 - row / 2.0, 1.0 - (row + 1) / 2.0
    mb = MB()
    mb.face(pts, "M_Poster_Atlas", out=n, uv=[(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    return mb


def posters():
    out = []
    spec = [  # (칸, 중심, 법선, w, h)
        ("rules_board", (1.79, 1.7, 9.3), (-1.0, 0.0, 0.0), 0.5, 0.7),
        ("weight_chart", (13.5, 1.7, 12.99), (0.0, 0.0, -1.0), 0.6, 0.85),
        ("cage_seoul_a", (12.0, 2.5, 0.01), (0.0, 0.0, 1.0), 0.6, 0.85),
        ("cage_seoul_b", (15.5, 2.5, 0.01), (0.0, 0.0, 1.0), 0.6, 0.85),
        ("blank_sign", (1.55, 1.7, 0.01), (0.0, 0.0, 1.0), 0.22, 0.22),
        ("gym_banner", (9.3, 2.1, 12.99), (0.0, 0.0, -1.0), 0.55, 1.5),
        ("old_fight_poster", (18.2, 1.6, 12.99), (0.0, 0.0, -1.0), 0.6, 0.85),
        ("belt_plaque", (16.0, 1.7, 12.99), (0.0, 0.0, -1.0), 0.9, 0.5),
        ("cage_seoul_a", (4.0, 2.75, 0.01), (0.0, 0.0, 1.0), 0.42, 0.6),
        ("cage_seoul_b", (8.0, 2.75, 0.01), (0.0, 0.0, 1.0), 0.42, 0.6),
        ("gym_banner", (19.99, 1.9, 1.6), (-1.0, 0.0, 0.0), 0.5, 1.4),
    ]
    cnt = {}
    for cell, c, n, w, h in spec:
        cnt[cell] = cnt.get(cell, 0) + 1
        mb = poster_quad(cell, c, n, w, h)
        out.append(Obj("Poster_%s_%02d" % (cell, cnt[cell]), "Gym", "poster", single=mb, info=dict(cell=cell, size_m=(w, h))))
    return out


# ---------------------------------------------------------------- 소품·NPC·드릴·카메라 자리
def place_sockets():
    # 락커 8 + 신발장 + 슬리퍼 + 음수대
    for i in range(8):
        prop("locker_unit", i + 1, (0.225, 0.0, 1.1 + 0.6 * i), (1.0, 0.0, 0.0))
    prop("shoe_rack", 1, (0.18, 0.0, 6.8), (1.0, 0.0, 0.0))
    prop("slippers_pile", 1, (1.0, 0.0, 6.3), (1.0, 0.0, 0.0))
    prop("water_cooler", 1, (2.4, 0.0, 12.78), (0.0, 0.0, -1.0))
    # 웨이트 코너
    prop("barbell_rack", 1, (7.8, 0.0, 12.2), (0.0, 0.0, -1.0))
    prop("dumbbell_rack", 1, (4.5, 0.0, 12.6), (0.0, 0.0, -1.0))
    prop("weight_bench", 1, (5.5, 0.0, 10.0), (1.0, 0.0, 0.0))
    for i, (x, z) in enumerate(((3.0, 9.5), (3.4, 9.65), (3.8, 9.5))):
        prop("kettlebell", i + 1, (x, 0.0, z), (0.0, 0.0, -1.0))
    prop("tractor_tire", 1, (7.5, 0.0, 9.3))
    prop("battle_rope_coil", 1, (3.2, 0.0, 11.2))
    prop("whiteboard_stand", 1, (8.7, 0.0, 12.4), (0.0, 0.0, -1.0))
    prop("stand_fan", 1, (8.9, 0.0, 9.0), norm((-0.7, 0.0, -0.7)))
    # 의자 5, 스피커 2, 체중계, 더미, 매트 롤, 장비 모서리
    for i, x in enumerate((10.5, 11.3, 12.1)):
        prop("folding_chair", i + 1, (x, 0.0, 12.55), (0.0, 0.0, -1.0))
    prop("folding_chair", 4, (10.55, 0.0, 2.5), (-1.0, 0.0, 0.0))
    prop("folding_chair", 5, (10.55, 0.0, 3.3), (-1.0, 0.0, 0.0))
    prop("speaker_cabinet", 1, (12.9, 2.0, 12.8), (0.0, 0.0, -1.0))
    prop("speaker_cabinet", 2, (10.5, 2.0, 0.2), (0.0, 0.0, 1.0))
    sock("Sock_Audio_Speaker_01", "audio", (12.9, 2.3, 12.7), (0.0, 0.0, -1.0), audio="gym_hiphop")
    sock("Sock_Audio_Speaker_02", "audio", (10.5, 2.3, 0.3), (0.0, 0.0, 1.0), audio="gym_hiphop")
    prop("body_scale", 1, (13.5, 0.0, 12.4), (0.0, 0.0, -1.0))
    sock("Sock_Interact_Scale", "interact", (13.5, 0.0, 11.7), (0.0, 0.0, 1.0), action="weigh_in")
    prop("grappling_dummy", 1, (4.0, 0.03, 4.5), (0.0, 0.0, -1.0))
    prop("mat_roll", 1, (10.6, 0.0, 5.6))
    prop("focus_mitts_pair", 1, (10.0, 0.03, 5.8), (0.0, 0.0, -1.0))
    prop("mma_gloves_pair", 1, (9.6, 0.03, 5.8), (0.0, 0.0, -1.0))
    prop("jump_rope_coiled", 1, (9.2, 0.03, 5.8))
    # 드릴 자리(docs/12 4-2)
    drills = {"D01": ((3.5, 0.03, 3.0), (0.0, 0.0, -1.0)), "D02": ((3.5, 0.03, 3.0), (0.0, 0.0, -1.0)),
              "D03": ((6.0, 0.03, 2.5), (0.0, 0.0, -1.0)), "D04": ((8.5, 0.03, 4.0), (1.0, 0.0, 0.0)),
              "D07": ((4.5, 0.03, 5.0), (0.0, 0.0, 1.0)), "D08": ((4.0, 0.03, 3.6), (0.0, 0.0, 1.0)),
              "D10": ((7.5, 0.03, 4.5), (0.0, 0.0, -1.0)), "D12": ((5.0, 0.0, 9.3), (0.0, 0.0, 1.0)),
              "D13": ((7.5, 0.0, 8.3), (0.0, 0.0, 1.0)), "D14": ((8.7, 0.0, 11.6), (0.0, 0.0, 1.0))}
    for k, (p, f) in drills.items():
        sock("Sock_Drill_%s" % k, "drill", p, f, drill=k)
    # NPC
    sock("Sock_NPC_Coach_Weight", "npc", (5.5, 0.0, 10.6), (0.0, 0.0, -1.0), npc="yunjun", pose="sit_bench")
    sock("Sock_NPC_Coach_MatEdge", "npc", (9.6, 0.03, 4.0), (-1.0, 0.0, 0.0), npc="yunjun", pose="mitts")
    sock("Sock_NPC_Gojun_Bag", "npc", (18.4, 0.0, 6.0), (1.0, 0.0, 0.0), npc="gojun")
    for i in range(10):
        sock("Sock_NPC_Line_%02d" % (i + 1), "npc", (3.0 + 1.2 * (i % 5), 0.03, 2.2 if i < 5 else 4.4), (0.0, 0.0, -1.0), npc="member", slot=i + 1)
    sock("Sock_NPC_Member_Stretch", "npc", (6.5, 0.03, 1.8), (0.0, 0.0, -1.0), npc="member")
    sock("Sock_NPC_Member_Mirror", "npc", (3.0, 0.03, 1.2), (0.0, 0.0, -1.0), npc="member")
    # 카메라(스타일 프레임 6 자리) — look_at 은 속성으로
    cams = {"g01": ((0.9, 3.05, 10.4), (12.0, 0.5, 4.0)), "g02": ((11.7, 1.55, 6.0), (16.0, 1.0, 6.2)),
            "g03": ((3.0, 1.7, 7.4), (14.0, 1.0, 2.4)), "g04": ((9.2, 1.6, 8.3), (4.3, 0.9, 11.6)),
            "g05": ((16.6, 1.6, 8.0), (17.0, 1.3, 12.6)), "g06": ((9.4, 1.6, 2.6), (13.5, 1.1, 5.6))}
    for k, (p, t) in cams.items():
        sock("Sock_Cam_%s" % k, "camera", p, norm(sub(t, p)), look_at=t, fov_v=50)


# ---------------------------------------------------------------- 조립
def generate():
    SOCKETS.clear()
    parts = [room_shell(), stairwell(), cage(), office(), mirror(), wall_pads(), bag_rail()]
    objs = []
    for P in parts:
        objs.append(Obj(P.name, "Gym", "kit", lods=[P.lod[0], P.lod[1], None], info=dict(lod_screen_heights=LOD_SCREEN)))
    objs += lights()
    objs += posters()
    place_sockets()
    for s in SOCKETS:
        kind = "prop" if s["kind"] == "prop" else "socket"
        info = dict(s["info"])
        if kind == "socket":
            info["sock_kind"] = s["kind"]
        objs.append(Obj(s["name"], "Gym", kind, empty=(s["pos"], s["fwd"]), info=info))
    return objs


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(out=os.path.join(ROOT, "concept", "art", "env", "gym"), format="fbx", blend=None)
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
    t0 = time.time()
    objs = generate()
    os.makedirs(o["out"], exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Gym_Hyehwa")
    bpy.context.scene.collection.children.link(coll)
    root, made = blend.build_district(objs, "Gym", coll)
    # 소켓 속성(blend.build_district 는 sock_kind·tenant 만 쓰므로 나머지 정보를 덧붙임)
    byname = {s["name"]: s for s in SOCKETS}
    for e in made:
        s = byname.get(e.name)
        if s is None:
            continue
        for k, v in s["info"].items():
            e[k] = v if isinstance(v, (int, float, str)) else json.dumps(_jsonable(v))
        e["fwd"] = "%.3f,%.3f,%.3f" % s["fwd"]
    base = os.path.join(o["out"], "Gym_Hyehwa")
    files = {}
    if o["format"] in ("fbx", "both"):
        blend.export_fbx(base + ".fbx", made)
        files["Gym_Hyehwa.fbx"] = os.path.getsize(base + ".fbx")
    if o["format"] in ("glb", "both"):
        blend.export_glb(base + ".glb", made)
        files["Gym_Hyehwa.glb"] = os.path.getsize(base + ".glb")
    if o.get("blend"):
        bpy.ops.wm.save_as_mainfile(filepath=o["blend"])
    tris = {ob.name: ([m.tris() for m in ob.lods if m is not None] if ob.lods else (ob.single.tris() if ob.single is not None else 0)) for ob in objs if ob.kind not in ("prop", "socket")}
    used = sorted({m for ob in objs for mb in ob.all_mbs() for m in mb.mats()})
    man = dict(
        generator="haengin1/tools/env/gym/build_gym.py", doc="haengin1/docs/12_체육관_설계.md",
        axes="Unity 좌표 그대로(X 동, Y 위, Z 북). 원점 = 지하 바닥 남서 모서리. 프리팹을 원점·회전 0·배율 1 로 놓음",
        room=dict(w=W, d=D, h=H, beam_bottom=BEAM_Y, cage=dict(center=CAGE_C, circumradius=CAGE_R, platform=CAGE_PLAT)),
        lod=dict(screen_heights=list(LOD_SCREEN), note="Room/Stair/Cage/Office/Mirror/WallPads/BagRail = LOD0·LOD1. Fluor·Poster 는 LOD 밖"),
        materials={k: dict(MATS[k], tiling=round(1.0 / MATS[k]["tile"], 4)) for k in used},
        new_materials=list(GYM_MATS),
        poster_cells=POSTER_CELLS,
        tris=tris, tris_total_lod0=sum(v[0] if isinstance(v, list) else v for v in tris.values()),
        sockets=[dict(name=s["name"], kind=s["kind"], pos=[round(v, 3) for v in s["pos"]], fwd=[round(v, 3) for v in s["fwd"]], info=_jsonable(s["info"])) for s in SOCKETS],
        files_bytes=files,
    )
    with open(base + "_manifest.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump(man, f, ensure_ascii=False, indent=1)
    print("[gym] 오브젝트 %d · 소켓 %d · 삼각형 LOD0 %d · 파일 %s · %.1fs" % (len(made), len(SOCKETS), man["tris_total_lod0"], files, time.time() - t0))


if __name__ == "__main__":
    main()
