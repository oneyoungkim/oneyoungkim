# -*- coding: utf-8 -*-
"""M1 그레이박스 — 혜화동 1구역(성대 후문~와룡공원) 배치 데이터 생성기.

출력: unity/HaenginMainEvent/Assets/_Project/Data/zone1.json
설계 문서: docs/06_M1_그레이박스_설계.md (검증표는 tools/zone1_check.py 가 JSON 을 다시 읽어 만든다)

좌표 (게임 단위 m, Unity 축):
  원점 = 혜화동 로터리, +X = 동, +Z = 북, Y = 높이
  map_zones.json(실제 m, z = 남쪽 +, y = 해발)에서 바꾸는 식:
    X = x * 0.25,  Z = -z * 0.25,  Y = (y - 30) * 0.5     (03_맵 3.2 와 같은 식, Z 부호만 Unity 북쪽 + 로 뒤집음)
yaw = Unity Y축 회전(도). 0 이면 size[0](가로)이 +X(동), size[2](세로)가 +Z(북). 양수 = 위에서 볼 때 시계 방향.

실행: python tools/zone1_gen.py   (numpy, scipy 필요)
"""
import json
import math
import os
import sys

import numpy as np
from scipy.sparse import lil_matrix
from scipy.sparse.linalg import spsolve

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)  # haengin1/
MAP_ZONES = os.path.join(ROOT, "data", "map_zones.json")
OUT = os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json")

SIDEWALK_CURB = 0.15
ROAD_HALF = 3.5
SIDEWALK_W = 2.0


def r2(v):
    return round(float(v) + 0.0, 2)


def P(*a):
    return [r2(v) for v in a]


def to_game(pos):
    return (pos["x"] * 0.25, (pos["y"] - 30) * 0.5, -pos["z"] * 0.25)


# ---------------------------------------------------------------- 기하 도우미
def seg_project(a, b, px, pz):
    ax, ay, az = a
    bx, by, bz = b
    dx, dz = bx - ax, bz - az
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 == 0 else ((px - ax) * dx + (pz - az) * dz) / L2
    t = max(0.0, min(1.0, t))
    qx, qz = ax + t * dx, az + t * dz
    return math.hypot(px - qx, pz - qz), ay + t * (by - ay), t, qx, qz


def poly_project(pts, px, pz):
    best = None
    for i in range(len(pts) - 1):
        d, y, t, qx, qz = seg_project(pts[i], pts[i + 1], px, pz)
        if best is None or d < best[0]:
            best = (d, y, i, t, qx, qz)
    return best


def poly_at_x(pts, x):
    """x 가 단조 증가하는 폴리라인에서 x 위치의 (y, z, 진행방향 단위벡터 hx, hz)."""
    for i in range(len(pts) - 1):
        ax, ay, az = pts[i]
        bx, by, bz = pts[i + 1]
        if ax <= x <= bx:
            t = (x - ax) / (bx - ax)
            L = math.hypot(bx - ax, bz - az)
            return ay + t * (by - ay), az + t * (bz - az), (bx - ax) / L, (bz - az) / L
    raise ValueError("x out of polyline: %s" % x)


def offset_poly(pts, off, dy):
    """진행 방향 왼쪽(+off = 북쪽 계열) 으로 평행 이동. y 에 dy 더함."""
    out = []
    n = len(pts)
    for i in range(n):
        nx = nz = 0.0
        for j in (i - 1, i):
            if 0 <= j < n - 1:
                ax, _, az = pts[j]
                bx, _, bz = pts[j + 1]
                L = math.hypot(bx - ax, bz - az)
                hx, hz = (bx - ax) / L, (bz - az) / L
                nx += -hz
                nz += hx
        L = math.hypot(nx, nz)
        nx, nz = nx / L, nz / L
        x, y, z = pts[i]
        out.append((x + nx * off, y + dy, z + nz * off))
    return out


def yaw_from_dir(hx, hz):
    # local +X → world (cos yaw, -sin yaw)
    return math.degrees(math.atan2(-hz, hx))


def local_axes(yaw):
    a = math.radians(yaw)
    ux = (math.cos(a), -math.sin(a))   # local +X
    uz = (math.sin(a), math.cos(a))    # local +Z
    return ux, uz


def rect_corners(cx, cz, w, d, yaw):
    ux, uz = local_axes(yaw)
    out = []
    for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        out.append((cx + ux[0] * sx * w / 2 + uz[0] * sz * d / 2,
                    cz + ux[1] * sx * w / 2 + uz[1] * sz * d / 2))
    return out


def in_rect(px, pz, cx, cz, w, d, yaw, margin=0.0):
    ux, uz = local_axes(yaw)
    dx, dz = px - cx, pz - cz
    lx = dx * ux[0] + dz * ux[1]
    lz = dx * uz[0] + dz * uz[1]
    return abs(lx) <= w / 2 + margin and abs(lz) <= d / 2 + margin


# ---------------------------------------------------------------- map_zones 기준점
with open(MAP_ZONES, encoding="utf-8") as f:
    MZ = json.load(f)
ZONE = {z["id"]: z for z in MZ["zones"]}


def zg(zid):
    return to_game(ZONE[zid]["pos"])


REF = {  # 문서 정합표용 (이름, map_zones 출처, 게임 좌표)
    "hoomun_gate": ("성대 후문 게이트", "deliveryRoutes R4 #2", to_game({"x": -790, "y": 92, "z": -400})),
    "bus1_end": ("명륜3가 종점", "fastTravel bus1", to_game({"x": -800, "y": 92, "z": -390})),
    "bus1_hoomun": ("후문시장 앞 정류장", "fastTravel bus1", to_game({"x": -745, "y": 88, "z": -400})),
    "bus3": ("후문·와룡공원 정류장", "fastTravel bus3", to_game({"x": -790, "y": 95, "z": -440})),
    "hoomun_street": ("성대 후문 상가거리", "zones", zg("hoomun_street")),
    "haru_conv": ("하루편의점 후문점", "zones", zg("haru_conv")),
    "bungae_delivery": ("번개배달 사무실", "zones", zg("bungae_delivery")),
    "gukbap": ("성대후문 국밥", "zones", zg("gukbap")),
    "hoomun_parking": ("후문 뒷골목 주차장(Y4)", "zones/yachaSpots", zg("hoomun_parking")),
    "alley_in": ("골목 내리막 입구", "deliveryRoutes R1 #3", to_game({"x": -690, "y": 84, "z": -460})),
    "siu_house": ("반시우의 집", "zones", zg("siu_house")),
    "top_stair": ("성곽 아래 꼭대기 계단", "deliveryRoutes R1 #5", to_game({"x": -640, "y": 95, "z": -545})),
    "top_villa": ("꼭대기 다세대(R1 고객)", "deliveryRoutes R1 #6", to_game({"x": -630, "y": 99, "z": -570})),
    "waryong_entrance": ("와룡공원 입구·성곽 아래 공터", "zones", zg("waryong_entrance")),
    "y1": ("야차 Y1", "yachaSpots", to_game({"x": -770, "y": 106, "z": -575})),
    "waryong_trail_t1": ("와룡 성곽길(T1 #2)", "trainingRoutes T1", to_game({"x": -979, "y": 118, "z": -540})),
    "waryong_trail": ("와룡공원 성곽길(대표점)", "zones", zg("waryong_trail")),
    "wall_west": ("성곽: 와룡공원 서측", "wallLine #3", to_game({"x": -1000, "y": 125, "z": -560})),
    "wall_mid": ("성곽: 후문 북측", "wallLine #4", to_game({"x": -800, "y": 112, "z": -600})),
    "wall_east": ("성곽: 명륜3가 북측", "wallLine #5", to_game({"x": -560, "y": 104, "z": -660})),
    "myeongnyun1ga": ("명륜1가 비탈길(구역 밖 동쪽)", "zones", zg("myeongnyun1ga_slope")),
    "malbawi": ("말바위 전망대(구역 밖, 원경)", "zones", zg("malbawi")),
}

# ================================================================ 1. 길 (차도·인도·골목·산책로)
ROADS = []
STAIRS = []
PADS = []


def road(name, kind, width, pts, note=None):
    d = {"name": name, "kind": kind, "width": width, "points": [tuple(p) for p in pts]}
    if note:
        d["note"] = note
    ROADS.append(d)
    return d


def stair(name, a, b, width, note=None):
    d = {"name": name, "from": tuple(a), "to": tuple(b), "width": width}
    if note:
        d["note"] = note
    STAIRS.append(d)
    return d


def pad(name, shape, center, size, yaw=0.0, surface="dirt", note=None):
    d = {"name": name, "shape": shape, "center": tuple(center), "size": tuple(size), "yaw": yaw, "surface": surface}
    if note:
        d["note"] = note
    PADS.append(d)
    return d


# 1-1 성균관로 (마을버스 1번 길). 서쪽 끝 = 명륜3가 종점, 동쪽 끝 = 명륜1가 비탈길로 이어짐(구역 밖)
MAIN = [(-213.0, 31.5, 98.0), (-200.0, 31.0, 98.6), (-186.0, 29.2, 100.0), (-175.0, 27.8, 97.6),
        (-160.0, 25.8, 93.0), (-146.0, 24.0, 88.6), (-130.0, 22.2, 83.8)]
road("성균관로(후문~명륜1가 방면)", "road", 2 * ROAD_HALF, MAIN,
     "2차선, 마을버스 1번. 동쪽 끝은 명륜1가 비탈길(-117.5, 21, 82.5)로 이어짐 — M1 경계")
SW_N = offset_poly([p for p in MAIN if p[0] >= -200.0], ROAD_HALF + SIDEWALK_W / 2, SIDEWALK_CURB)
SW_S = offset_poly([p for p in MAIN if p[0] >= -200.0], -(ROAD_HALF + SIDEWALK_W / 2), SIDEWALK_CURB)
road("성균관로 북쪽 인도", "sidewalk", SIDEWALK_W, SW_N, "연석 0.15m")
road("성균관로 남쪽 인도", "sidewalk", SIDEWALK_W, SW_S, "연석 0.15m")


def main_at(x):
    return poly_at_x(MAIN, x)


def sidewalk_edge(x, side):
    """인도 바깥 가장자리 점(차도 중심에서 5.5m). side = +1 북, -1 남."""
    y, z, hx, hz = main_at(x)
    off = side * (ROAD_HALF + SIDEWALK_W)
    nx, nz = -hz, hx
    return (x + nx * off, y + SIDEWALK_CURB, z + nz * off)


def at_x(fn, x_target, side, off):
    """fn(station, side) 결과 점의 x 가 x_target 이 되도록 station 을 역산(길이 비스듬해서)."""
    s = x_target
    for _ in range(5):
        y, z, hx, hz = main_at(s)
        s = x_target - (-hz) * side * off
    return fn(s, side)


def sidewalk_edge_x(x, side):
    return at_x(sidewalk_edge, x, side, ROAD_HALF + SIDEWALK_W)


def curb_point(x, side, inset=0.35):
    """인도 안쪽(연석 쪽)에서 inset m 들어간 점 — 전봇대·정류장 표지 자리."""
    def f(st, sd):
        y, z, hx, hz = main_at(st)
        off = sd * (ROAD_HALF + inset)
        return (st + (-hz) * off, y + SIDEWALK_CURB, z + hx * off)
    return at_x(f, x, side, ROAD_HALF + inset)


def sidewalk_mid(x, side):
    y, z, hx, hz = main_at(x)
    off = side * (ROAD_HALF + SIDEWALK_W / 2)
    nx, nz = -hz, hx
    return (x + nx * off, y + SIDEWALK_CURB, z + nz * off)


# 1-2 명륜3가 종점 회차장 + 와룡공원길(삼청동 방면) 짧은 꼬리
pad("명륜3가 종점 회차장", "rect", (-206.0, 31.2, 99.5), (12.0, 10.0), 0.0, "asphalt",
    "마을버스 1번 기점·회차. 버스 정류장 표지 북쪽 가장자리")
road("와룡공원길(삼청동 방면, M1 경계)", "road", 6.0,
     [(-212.0, 31.45, 97.8), (-226.0, 33.2, 96.0), (-240.0, 35.0, 94.0)],
     "실제로는 말바위·삼청동으로 넘어가는 길. M1 은 x=-240 에서 차단")

# 1-3 상가 뒤 골목(후문 게이트 옆 → 주차장)
e = sidewalk_edge_x(-192.5, -1)
ALLEY_BACK = road("상가 뒤 골목", "alley", 2.4,
                  [e, (-192.5, e[1] - 0.05, 90.0), (-192.3, e[1] - 0.1, 85.6), (-190.6, e[1] - 0.1, 84.4)],
                  "후문 게이트 동쪽 기둥과 상가 사이로 들어가 상가 뒤를 돈다")

# 1-4 후문 뒷골목 주차장(Y4) — 성균관로 남쪽 인도에 붙인 회전 사각형
y_s, z_s, hx, hz = main_at(-169.0)
PARK_YAW = yaw_from_dir(hx, hz)
nx, nz = hz, -hx  # 남쪽 법선
ex, ez = -169.0 + nx * (ROAD_HALF + SIDEWALK_W), z_s + nz * (ROAD_HALF + SIDEWALK_W)
PARK_W, PARK_D, PARK_SET = 18.0, 12.0, 0.4   # 인도와 주차장 사이 0.4m 띠에 난간
PARK_C = (ex + nx * (PARK_SET + PARK_D / 2), 27.2, ez + nz * (PARK_SET + PARK_D / 2))
pad("후문 뒷골목 주차장(Y4)", "rect", PARK_C, (PARK_W, PARK_D), PARK_YAW, "asphalt",
    "M2 전투 후보 ① — 인카운터 1:3(황민재 무리, 1부 튜토리얼 보스전). 평평, 북쪽 인도와는 가운데 진입로(폭 6m)로만 이어짐")
ux, uz = local_axes(PARK_YAW)
park_west_mid = (PARK_C[0] - ux[0] * PARK_W / 2, PARK_C[2] - ux[1] * PARK_W / 2)
# 상가 뒤 골목 계단: 골목 끝 → 주차장 서쪽 가장자리 안쪽 0.6m
st_to = (park_west_mid[0] + ux[0] * 0.6, PARK_C[1], park_west_mid[1] + ux[1] * 0.6)
stair("상가 뒤 골목 계단", (-190.4, e[1] - 0.1, 84.4), st_to, 2.4)
# 주차장 진입로(북쪽 가장자리 가운데 → 인도)
park_north_mid = (PARK_C[0] - nx * PARK_D / 2, PARK_C[2] - nz * PARK_D / 2)
road("주차장 진입로", "driveway", 6.0,
     [(park_north_mid[0] + nx * 1.0, PARK_C[1], park_north_mid[1] + nz * 1.0),
      (ex - nx * 0.6, sidewalk_edge(-169.0, -1)[1], ez - nz * 0.6)],
     "주차장과 인도 높이차 ≤ 0.3m 인 구간")

# 1-5 골목 계단길(상가 → 명륜3가 골목) + 명륜3가 골목(시우네 골목) + 동쪽 가지
s0 = sidewalk_edge_x(-173.9, +1)
road("골목 계단길(상가→명륜3가 골목)", "alley", 3.0,
     [s0, (-173.9, s0[1], 105.0)], "하루편의점 동쪽 모서리 옆. 계단 4~5단으로 내려감")
stair("골목 계단(내리막)", (-173.9, s0[1], 105.0), (-173.8, 27.0, 107.0), 3.0, "03_맵 R1 '골목 계단(내리막)'")
ALLEY_IN = (-172.5, 27.0, 115.5)
road("명륜3가 골목 입구", "alley", 3.0, [(-173.8, 27.0, 107.0), (-173.2, 27.0, 112.0), ALLEY_IN])
SIU_FRONT = (-161.0, 28.0, 125.0)
road("명륜3가 골목(시우네 골목)", "alley", 3.0,
     [ALLEY_IN, (-167.5, 27.3, 119.5), (-163.0, 27.7, 122.5), SIU_FRONT, (-161.4, 28.2, 127.2)],
     "R1 배달 튜토리얼 동선. 화분·빨래 건조대·고양이")
road("명륜3가 골목 동쪽(명륜1가 방면)", "alley", 2.5,
     [(-164.5, 27.57, 121.5), (-150.0, 27.2, 118.6), (-140.0, 26.2, 116.6), (-130.0, 25.2, 113.8)],
     "03_맵 '주택가 골목' 연결. 동쪽 끝 M1 경계")

# 1-6 성곽 아래 꼭대기 계단 + 계단참 + 좁은 계단
LANDING = pad("성곽 아래 꼭대기 계단참", "rect", (-160.5, 32.5, 137.0), (4.0, 3.4), 0.0, "stone",
              "R1 #5. 동쪽 = 꼭대기 다세대 출입문, 북서쪽 = 성곽 아래 좁은 계단")
stair("성곽 아래 꼭대기 계단(시우네 옆)", (-161.4, 28.2, 127.2), (-160.9, 32.5, 135.6), 2.0)

# 1-7 성곽 안쪽 숲길(공터 → 혜성고 방면)
FOREST_E = road("성곽 안쪽 숲길(혜성고 방면)", "trail", 2.5,
                [(-185.6, 38.0, 142.4), (-176.0, 37.2, 147.5), (-167.5, 36.4, 149.5), (-155.0, 35.6, 152.5),
                 (-142.0, 34.8, 156.5), (-130.0, 34.1, 159.5)],
                "03_맵 '성곽 안쪽 숲길(학교 담장 옆)'·'성곽 안쪽 오솔길'. 동쪽 끝 M1 경계(혜성고 정문 앞까지 약 60m)")
d_, y_top, *_ = poly_project(FOREST_E["points"], -166.8, 148.9)
stair("성곽 아래 좁은 계단", (-161.6, 32.5, 138.6), (-166.8, y_top, 148.9), 1.6,
      "03_맵 '성곽 아래 좁은 계단'(명륜3가 골목 ↔ 와룡공원 입구)")

# 1-8 후문 옆 공원길(와룡공원 오르막) + 공원 계단
p0 = sidewalk_edge_x(-198.5, +1)
road("후문 옆 공원길(와룡공원 오르막)", "path", 3.0,
     [p0, (-198.5, 31.9, 112.0), (-199.5, 33.6, 119.5)], "마을버스 3번 '후문·와룡공원' 정류장이 옆에 있음")
stair("와룡공원 계단", (-199.5, 33.6, 119.8), (-197.4, 36.9, 129.8), 3.0)
road("와룡공원 계단 위 길", "path", 3.0, [(-197.4, 36.9, 129.8), (-195.2, 37.5, 133.4), (-193.6, 38.0, 136.0)])

# 1-9 와룡공원 성곽 아래 공터(Y1)
GONGTEO = pad("와룡공원 성곽 아래 공터(Y1)", "circle", (-192.0, 38.0, 142.0), (13.0, 13.0), 0.0, "dirt",
              "M2 전투 후보 ② — 야차 1:1(1부 첫눈의 야차, 2부 채강혁 리매치). 흙바닥 원형, 가로등 2·벤치 2. 새벽엔 배드민턴 네트")

# 1-10 와룡공원 성곽길(공터 → 서쪽 전망 쉼터)
TRAIL = road("와룡공원 성곽길", "wallpath", 3.0,
             [(-198.4, 38.0, 142.3), (-210.0, 39.4, 143.0), (-225.0, 41.2, 140.5), (-244.75, 44.0, 136.5),
              (-250.5, 45.1, 135.4), (-255.6, 46.0, 136.2)],
             "성벽 안쪽을 따라 걷는 길(성벽 접촉·등반 불가). 바닥 = 화강암 판석 포장(wallpath). 2023 정비 안전난간 느낌")
VIEW = pad("와룡공원 전망 쉼터", "rect", (-258.6, 46.0, 136.3), (6.0, 5.0), 0.0, "deck",
           "구역 서쪽 끝. 정자 1, 벤치, 말바위 방향 능선 조망. 서쪽은 '말바위 방면 출입 통제'로 막음")

# 1-11 와룡공원 안쪽 숲길(성곽길 ↔ 운동기구 마당 ↔ 공원 계단 아래)
EXER = pad("와룡공원 운동기구 마당", "rect", (-246.0, 42.5, 116.5), (10.0, 8.0), 0.0, "dirt",
           "map_zones waryong_trail 대표점(37.5906N 126.9906E 환산) 자리. 운동기구·벤치")
road("와룡공원 안쪽 숲길(북)", "path", 2.2, [(-225.0, 41.2, 140.5), (-233.0, 41.8, 130.0), (-244.0, 42.5, 120.2)])
road("와룡공원 안쪽 숲길(남)", "path", 2.2,
     [(-241.5, 42.5, 115.0), (-230.0, 39.6, 111.0), (-217.0, 35.8, 112.0), (-207.0, 33.9, 116.0),
      (-199.4, 32.95, 116.6)])

# ================================================================ 2. 성곽
WALL_KEYS = [(-266.0, 147.8), (-250.0, 140.0), (-200.0, 150.0), (-140.0, 165.0), (-128.0, 169.0)]
WALL_H, WALL_T = 4.5, 2.5


def densify(keys, step=8.0):
    out = []
    for i in range(len(keys) - 1):
        (ax, az), (bx, bz) = keys[i], keys[i + 1]
        L = math.hypot(bx - ax, bz - az)
        n = max(1, int(math.ceil(L / step)))
        for k in range(n):
            t = k / n
            out.append((ax + t * (bx - ax), az + t * (bz - az)))
    out.append(keys[-1])
    return out


WALK_FOR_WALL = [TRAIL, FOREST_E]


def wall_base_y(x, z):
    """성벽 기초 = 가장 가까운 성곽길/숲길/공터 높이 + 0.8 (서쪽 끝은 말바위 쪽 오르막으로 +)."""
    best = None
    for r in WALK_FOR_WALL:
        d, y, *_ = poly_project(r["points"], x, z)
        if best is None or d < best[0]:
            best = (d, y)
    dg = math.hypot(x - GONGTEO["center"][0], z - GONGTEO["center"][2]) - 6.5
    if dg < best[0]:
        best = (dg, GONGTEO["center"][1])
    y = best[1] + 0.8
    if x < -256.0:  # 전망 쉼터 서쪽: 말바위 쪽으로 오르는 능선
        y += (-256.0 - x) * 0.25
    return y


WALL_PTS = [(x, wall_base_y(x, z), z) for x, z in densify(WALL_KEYS)]

# ================================================================ 3. 높이 격자 (조화 보간)
X0, Z0, CELL = -274.0, 70.0, 2.0
NX, NZ = 73, 53  # x: -274..-130, z: 70..174
gx = X0 + CELL * np.arange(NX)
gz = Z0 + CELL * np.arange(NZ)


def ribbon_constraint(px, pz):
    """(우선순위, 거리, y) 또는 None."""
    best = None

    def take(c):
        nonlocal best
        if best is None or (c[0], c[1]) < (best[0], best[1]):
            best = c

    for p in PADS:
        cx, cy, cz = p["center"]
        w, d = p["size"]
        if p["shape"] == "circle":
            if math.hypot(px - cx, pz - cz) <= w / 2 + 0.6:
                take((0, 0.0, cy))
        elif in_rect(px, pz, cx, cz, w, d, p["yaw"], 0.6):
            take((0, 0.0, cy))
    for s in STAIRS:
        d, y, t, *_ = seg_project(s["from"], s["to"], px, pz)
        if d <= s["width"] / 2 + 0.6:
            take((1, d, y))
    for r in ROADS:
        prio = 3 if r["kind"] == "sidewalk" else 2
        d, y, *_ = poly_project(r["points"], px, pz)
        if d <= r["width"] / 2 + 0.6:
            take((prio, d, y))
    if best is None:
        d, y, *_ = poly_project(WALL_PTS, px, pz)
        if d <= WALL_T / 2 + 0.6:
            take((4, d, y))
    return best


def solve_ground():
    N = NX * NZ
    fixed = np.zeros(N, bool)
    val = np.zeros(N)
    idx = lambda i, j: j * NX + i  # noqa: E731  (i = x, j = z)
    for j in range(NZ):
        for i in range(NX):
            c = ribbon_constraint(gx[i], gz[j])
            if c is not None:
                fixed[idx(i, j)] = True
                val[idx(i, j)] = c[2]
    # 성곽 밖(북쪽 끝 줄): 성벽 기초보다 3m 낮게 — 성북동 쪽 내리막
    j = NZ - 1
    for i in range(NX):
        d, y, *_ = poly_project(WALL_PTS, gx[i], gz[j])
        fixed[idx(i, j)] = True
        val[idx(i, j)] = y - 3.0
    free = np.where(~fixed)[0]
    pos = -np.ones(N, int)
    pos[free] = np.arange(len(free))
    A = lil_matrix((len(free), len(free)))
    b = np.zeros(len(free))
    for k, n in enumerate(free):
        i, j = n % NX, n // NX
        nb = []
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ii, jj = i + di, j + dj
            if 0 <= ii < NX and 0 <= jj < NZ:
                nb.append(idx(ii, jj))
        A[k, k] = len(nb)
        for m in nb:
            if fixed[m]:
                b[k] += val[m]
            else:
                A[k, pos[m]] -= 1
    sol = spsolve(A.tocsr(), b)
    H = val.copy()
    H[free] = sol
    return H.reshape(NZ, NX), fixed.reshape(NZ, NX)


H, FIXED = solve_ground()


def ground_y(x, z):
    fi = (x - X0) / CELL
    fj = (z - Z0) / CELL
    i = int(max(0, min(NX - 2, math.floor(fi))))
    j = int(max(0, min(NZ - 2, math.floor(fj))))
    tx, tz = fi - i, fj - j
    h00, h10, h01, h11 = H[j, i], H[j, i + 1], H[j + 1, i], H[j + 1, i + 1]
    return (h00 * (1 - tx) * (1 - tz) + h10 * tx * (1 - tz) + h01 * (1 - tx) * tz + h11 * tx * tz)


# ================================================================ 4. 블록(건물·담장·나무·표지물)
BLOCKS = []


def block(name, kind, cx, cz, w, d, yaw, height, ref_y=None, use=None, min_show=None, extra=None):
    """지형 위에 세운다. 바닥 = 발자국 최저점 - 0.5, 윗면 = ref_y(정면 기준 높이, 없으면 최저점) + height.
    비탈에서 높은 쪽 땅 위로도 최소 min_show(기본 = min(height, 2.5)) 는 보이게."""
    pts = rect_corners(cx, cz, w, d, yaw) + [(cx, cz)]
    hs = [ground_y(x, z) for x, z in pts]
    base = min(hs) - 0.5
    top = (ref_y if ref_y is not None else min(hs)) + height
    if min_show is None:
        min_show = min(height, 2.5)
    top = max(top, max(hs) + min_show)
    b = {"name": name, "kind": kind, "center": P(cx, (base + top) / 2, cz), "size": P(w, top - base, d), "yaw": r2(yaw)}
    if use:
        b["use"] = use
    if extra:
        b.update(extra)
    BLOCKS.append(b)
    return b


def frontage(x_front, side, width, depth, setback=0.0):
    """성균관로 인도 바깥 가장자리에 정면을 붙인 건물 자리. x_front = 정면 가운데의 x. side +1 북, -1 남."""
    off = ROAD_HALF + SIDEWALK_W + setback
    s = x_front
    for _ in range(4):  # 길이 비스듬해서 법선이 x 를 밀어내므로, 정면 가운데가 x_front 에 오도록 역산
        y, z, hx, hz = main_at(s)
        nx_, nz_ = (-hz, hx) if side > 0 else (hz, -hx)
        s = x_front - nx_ * off
    y, z, hx, hz = main_at(s)
    nx_, nz_ = (-hz, hx) if side > 0 else (hz, -hx)
    fx, fz = s + nx_ * off, z + nz_ * off
    cx, cz = fx + nx_ * depth / 2, fz + nz_ * depth / 2
    return cx, cz, yaw_from_dir(hx, hz), (fx, y + SIDEWALK_CURB, fz)


DOORS = []


def shop(name, kind, x_front, side, width, depth, floors, use, doors=()):
    cx, cz, yaw, front = frontage(x_front, side, width, depth)
    b = block(name, kind, cx, cz, width, depth, yaw, floors * 3.0 + 0.6, ref_y=front[1], use=use,
              extra={"floors": floors})
    ux_, _ = local_axes(yaw)
    for dname, along, zone in doors:  # along = 정면 가운데에서 동쪽(+X 진행)으로 m
        fx, fz = front[0] + ux_[0] * along, front[2] + ux_[1] * along
        DOORS.append({"name": dname, "kind": "door", "pos": P(fx, main_at(fx)[0] + SIDEWALK_CURB, fz), "zone": zone})
    return b


# 4-1 성균관로 북쪽 줄
shop("후문상가 A동", "shop", -189.75, +1, 8.5, 9.0, 3,
     "1층 '반짝 문구·복사'(가칭) / 2층 번개배달 사무실(남쪽 출입 계단) / 3층 원룸",
     doors=[("번개배달 사무실 출입구(2층 계단)", 2.2, "bungae_delivery")])
shop("후문상가 B동", "shop", -180.8, +1, 8.0, 9.0, 2,
     "1층 서쪽 성대후문 국밥 / 1층 동쪽 하루편의점 후문점(골목 모서리, 동쪽 면도 유리) / 2층 하숙",
     doors=[("성대후문 국밥 출입문", -2.3, "gukbap"), ("하루편의점 후문점 출입문", 2.6, "haru_conv")])
shop("명륜3가 다세대 1(벽돌)", "house", -167.0, +1, 9.0, 8.5, 4, "다세대 4층, 1층 '빨랫줄 세탁'(가칭)")
shop("명륜3가 원룸 2", "house", -156.5, +1, 9.0, 8.0, 4, "원룸 4층")
shop("명륜3가 다세대 3", "house", -146.5, +1, 9.0, 8.0, 3, "다세대 3층, 1층 '나사 철물'(가칭)")
shop("명륜1가 방면 상가주택", "shop", -136.5, +1, 9.0, 8.0, 3, "1층 '고쳐요 PC'(가칭) / 위층 주택")
# 4-2 성균관로 남쪽 줄
shop("남쪽 상가 1(제본)", "shop", -188.6, -1, 4.5, 6.0, 2, "1층 '넘김 제본'(가칭) / 2층 하숙")
shop("남쪽 상가 2(분식)", "shop", -184.0, -1, 4.5, 6.0, 2, "1층 '계단분식'(가칭) / 2층 하숙")
shop("명륜3가 하숙집 A(남쪽)", "house", -156.0, -1, 9.0, 8.0, 3, "하숙 3층")
shop("명륜3가 원룸 B(남쪽)", "house", -145.5, -1, 9.0, 8.0, 4, "원룸 4층")
shop("명륜1가 방면 빌라(남쪽)", "house", -135.0, -1, 8.0, 8.0, 3, "빌라 3층")

# 4-3 명륜3가 골목 안쪽
block("반시우네 다세대", "house", -155.0, 127.0, 9.0, 9.0, 0.0, 9.0, ref_y=SIU_FRONT[1],
      use="다세대 2층 = 시우네(실내 씬). 3층 위 옥상(장독·빨랫줄·평상). 출입문 서쪽 면", extra={"floors": 3})
DOORS.append({"name": "반시우네 출입문", "kind": "door", "pos": P(-159.6, 28.0, 125.0), "zone": "siu_house"})
block("꼭대기 다세대(4층)", "house", -153.0, 141.5, 9.0, 9.0, 0.0, 12.0, ref_y=32.5,
      use="R1 배달 고객. 엘리베이터 없음(실내 계단 QTE). 출입문 서쪽 = 계단참", extra={"floors": 4})
DOORS.append({"name": "꼭대기 다세대 출입문", "kind": "door", "pos": P(-157.6, 32.5, 138.6), "zone": "myeongnyun3ga_alley"})
block("명륜3가 단독주택(기와)", "hanok", -144.5, 109.5, 9.0, 7.0, 0.0, 5.5, use="기와지붕 단층 + 다락. 담장 화분")
block("명륜3가 다세대 4", "house", -144.0, 126.0, 10.0, 8.0, 0.0, 9.0, use="다세대 3층", extra={"floors": 3})
block("골목 끝 다세대 5", "house", -133.5, 124.0, 6.0, 12.0, 0.0, 9.0, use="다세대 3층(구역 동쪽 끝)", extra={"floors": 3})
block("성곽 아래 빈집", "hanok", -142.5, 145.0, 9.0, 8.0, 0.0, 5.0, use="빈 한옥(문 잠김). 지붕 너머 성곽")
block("후문상가 뒤 하숙집", "house", -187.5, 121.0, 11.0, 8.0, 0.0, 7.5, use="하숙 2층, 비탈에 앉음", extra={"floors": 2})
block("명륜3가 다세대 6", "house", -176.5, 126.0, 8.0, 9.0, 0.0, 9.0, use="다세대 3층", extra={"floors": 3})

# 4-4 캠퍼스(대학 이름 미표기) + 후문 게이트
block("캠퍼스 건물(이름 미표기)", "campus", -221.0, 80.0, 34.0, 14.0, 0.0, 16.0, use="강의동 뒷면. 들어갈 수 없음")
GATE_Z = 92.7
block("후문 경비실", "campus", -205.0, 88.8, 2.6, 2.6, 0.0, 2.7, use="게이트 안쪽 경비실")
block("성대 후문 기둥(서)", "gate", -201.0, GATE_Z, 1.0, 1.0, 0.0, 3.2, use="후문 게이트 기둥")
block("성대 후문 기둥(동)", "gate", -195.0, GATE_Z, 1.0, 1.0, 0.0, 3.2, use="후문 게이트 기둥")
block("성대 후문 차단 펜스(닫힘)", "barrier", -198.0, GATE_Z, 5.0, 0.3, 0.0, 1.4,
      use="M1 에선 캠퍼스 관통 불가(캠퍼스 언덕길은 이후 마일스톤)")


def fence_line(name, kind, pts, height, thick=0.3, piece=8.0, use=None):
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        L = math.hypot(bx - ax, bz - az)
        n = max(1, int(math.ceil(L / piece)))
        for k in range(n):
            t0, t1 = k / n, (k + 1) / n
            x0, z0 = ax + t0 * (bx - ax), az + t0 * (bz - az)
            x1, z1 = ax + t1 * (bx - ax), az + t1 * (bz - az)
            block(name, kind, (x0 + x1) / 2, (z0 + z1) / 2, L / n, thick, yaw_from_dir((x1 - x0), (z1 - z0)),
                  height, min_show=height, use=use)


fence_line("캠퍼스 담장", "fence", [(-246.0, 89.4), (-226.0, 92.4), (-213.0, 93.0), (-201.6, GATE_Z)], 2.2,
           use="캠퍼스 경계. 넘을 수 없음")
fence_line("캠퍼스 담장(동쪽)", "fence", [(-194.4, GATE_Z), (-194.4, 86.6), (-194.4, 74.0)], 2.2)

# 4-5 주차장 둘레(난간·옹벽) + 자판기
cs = rect_corners(PARK_C[0], PARK_C[2], PARK_W, PARK_D, PARK_YAW)  # SW, SE, NE, NW (local)
# 북쪽 가장자리: 진입로(가운데 6m) 빼고 난간
ux, uz = local_axes(PARK_YAW)


def shift(p, v, d):
    return (p[0] + v[0] * d, p[1] + v[1] * d)


nw, ne = shift(cs[3], uz, PARK_SET / 2), shift(cs[2], uz, PARK_SET / 2)   # 북쪽 0.2m 띠 가운데
mid = ((nw[0] + ne[0]) / 2, (nw[1] + ne[1]) / 2)
fence_line("주차장 난간", "railing", [nw, (mid[0] - ux[0] * 3.0, mid[1] - ux[1] * 3.0)], 1.1, thick=0.2)
fence_line("주차장 난간", "railing", [(mid[0] + ux[0] * 3.0, mid[1] + ux[1] * 3.0), ne], 1.1, thick=0.2)
fence_line("주차장 옹벽(남)", "retaining", [shift(cs[0], uz, -0.35), shift(cs[1], uz, -0.35)], 2.5, thick=0.6)
fence_line("주차장 옹벽(동)", "retaining", [shift(cs[1], ux, 0.35), shift(cs[2], ux, 0.35)], 1.5, thick=0.6)
block("주차장 뒤 다세대", "house", PARK_C[0] + uz[0] * -11.5, PARK_C[2] + uz[1] * -11.5, 16.0, 9.0, PARK_YAW, 9.0,
      use="주차장 남쪽 담 너머 다세대 3층", extra={"floors": 3})
sx, sz = PARK_C[0] + uz[0] * -5.3 + ux[0] * 5.0, PARK_C[2] + uz[1] * -5.3 + ux[1] * 5.0
block("주차장 자판기", "prop", sx, sz, 1.0, 0.8, PARK_YAW, 1.9, use="밤 야차 조명(자판기 불빛)")
block("주차 차단기", "prop", mid[0] - ux[0] * 3.3 + uz[0] * -0.8, mid[1] - ux[1] * 3.3 + uz[1] * -0.8, 0.4, 0.4,
      PARK_YAW, 1.1, use="열린 차단기(진입로 서쪽)")

# 4-6 와룡공원 시설
block("와룡공원 공터 벤치(서)", "prop", -197.0, 137.0, 1.8, 0.6, 35.0, 0.5, use="공터 가장자리 벤치")
block("와룡공원 공터 벤치(동)", "prop", -186.4, 137.6, 1.8, 0.6, -35.0, 0.5, use="공터 가장자리 벤치")
block("전망 쉼터 정자", "pavilion", -258.6, 131.4, 3.6, 3.6, 0.0, 4.2, ref_y=46.0,
      use="정자(그레이박스는 상자, 쉼터 남쪽에 붙음). 공터·성곽길에서 서쪽 끝 목표로 보임")
block("운동기구", "prop", -248.0, 118.0, 3.0, 1.2, 0.0, 1.8, use="철봉·허리돌리기 등 묶음")
block("말바위 방면 출입 통제", "barrier", -261.9, 136.3, 0.4, 4.0, 0.0, 1.2, use="M1 서쪽 경계 표시")

# 4-7 M1 경계 차단물(구역 밖으로 이어지는 길 끝)
def end_barrier(name, pts, width, use):
    """길 끝(마지막 점)에서 0.4m 안쪽에 길을 가로지르는 낮은 차단물."""
    (ax, _, az), (bx, _, bz) = pts[-2], pts[-1]
    L = math.hypot(bx - ax, bz - az)
    hx, hz = (bx - ax) / L, (bz - az) / L
    block(name, "barrier", bx - hx * 0.4, bz - hz * 0.4, 0.4, width + 0.6, yaw_from_dir(hx, hz), 1.2, use=use)


end_barrier("성균관로 동쪽 경계", MAIN, 7.0, "명륜1가 방면(구역 밖)")
end_barrier("와룡공원길 서쪽 경계", ROADS[3]["points"], 6.0, "삼청동 방면(구역 밖)")
end_barrier("명륜3가 골목 동쪽 경계", [r for r in ROADS if r["name"].startswith("명륜3가 골목 동쪽")][0]["points"], 2.5,
            "명륜1가 주택가(구역 밖)")
end_barrier("성곽 안쪽 숲길 동쪽 경계", FOREST_E["points"], 2.5, "혜성고 방면(구역 밖)")

# 4-8 명륜3가 골목 벽(체크포인트 3→4→5→6, 2026-10-05 2차): 양옆이 트인 맨땅 비탈이라 골목으로 안 읽혀서
#     골목 가운데선에서 ALLEY_SET 떨어진 선에 낮은 단독주택·담장을 촘촘히 세운다(골목 폭 = 2 × SET).
ALLEY_SET = 1.8    # 골목(걷는 면 3.0m) → 집·담 앞면까지. 골목 폭 3.6m(걷는 면 + 양쪽 0.3m 배수로 자리)
STAIR_SET = 1.4    # 꼭대기 계단(걷는 면 2.0m) → 담 앞면. 계단 골목 폭 2.8m
POLE_SET = 1.65    # 골목 전봇대·보안등은 걷는 면 가장자리와 담 사이(지름 0.3m 가 걷는 면 밖에 들게)


def pl_len(pts):
    return sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][2] - pts[i][2]) for i in range(len(pts) - 1))


def pl_at(pts, s):
    """폴리라인 위 호 길이 s 의 (x, y, z, 진행 방향 hx, hz). 끝을 넘으면 마지막 방향으로 연장(앞은 첫 방향)."""
    if s <= 0:
        (ax, ay, az), (bx, by, bz) = pts[0], pts[1]
        L = math.hypot(bx - ax, bz - az)
        hx, hz = (bx - ax) / L, (bz - az) / L
        return ax + hx * s, ay, az + hz * s, hx, hz
    acc = 0.0
    for i in range(len(pts) - 1):
        (ax, ay, az), (bx, by, bz) = pts[i], pts[i + 1]
        L = math.hypot(bx - ax, bz - az)
        if s <= acc + L or i == len(pts) - 2:
            t = (s - acc) / L
            hx, hz = (bx - ax) / L, (bz - az) / L
            tc = min(1.0, t)
            return ax + (bx - ax) * t, ay + (by - ay) * tc, az + (bz - az) * t, hx, hz
        acc += L


def side_point(pts, s, side, off):
    """side +1 = 진행 방향 왼쪽, -1 = 오른쪽. 가운데선에서 off 떨어진 점 (x, y, z)."""
    x, y, z, hx, hz = pl_at(pts, s)
    nx, nz = (-hz, hx) if side > 0 else (hz, -hx)
    return x + nx * off, y, z + nz * off


def walk_dist(x, z):
    """(x,z) 에서 걷는 면(길·계단·패드) 가장자리까지 수평 거리(안쪽이면 음수)."""
    best = 1e9
    for r in ROADS:
        d, *_ = poly_project(r["points"], x, z)
        best = min(best, d - r["width"] / 2)
    for s_ in STAIRS:
        d, *_ = seg_project(s_["from"], s_["to"], x, z)
        best = min(best, d - s_["width"] / 2)
    for p in PADS:
        cx, cy, cz = p["center"]
        w, d = p["size"]
        if p["shape"] == "circle":
            best = min(best, math.hypot(x - cx, z - cz) - w / 2)
        else:
            ux_, uz_ = local_axes(p["yaw"])
            lx, lz = (x - cx) * ux_[0] + (z - cz) * ux_[1], (x - cx) * uz_[0] + (z - cz) * uz_[1]
            ex, ez = abs(lx) - w / 2, abs(lz) - d / 2
            best = min(best, max(ex, ez) if ex <= 0 and ez <= 0 else math.hypot(max(ex, 0), max(ez, 0)))
    return best


def footprint(cx, cz, w, d, yaw, step=0.4):
    ux_, uz_ = local_axes(yaw)
    nx_, nz_ = max(2, int(math.ceil(w / step)) + 1), max(2, int(math.ceil(d / step)) + 1)
    for i in range(nx_):
        for j in range(nz_):
            lx, lz = -w / 2 + w * i / (nx_ - 1), -d / 2 + d * j / (nz_ - 1)
            yield cx + ux_[0] * lx + uz_[0] * lz, cz + ux_[1] * lx + uz_[1] * lz


def block_fits(cx, cz, w, d, yaw, m_walk=0.25, m_blk=0.05, solid=None):
    """걷는 면에서 m_walk 이상 떨어지고, solid(기본 = 지금까지의 건물·담, 나무·풀숲 제외)와 겹치지 않으면 True."""
    if solid is None:
        solid = [b for b in BLOCKS if b["kind"] not in ("tree", "bush")]
    for x, z in footprint(cx, cz, w, d, yaw):
        if walk_dist(x, z) < m_walk:
            return False
        for b in solid:
            if in_rect(x, z, b["center"][0], b["center"][2], b["size"][0], b["size"][2], b["yaw"], m_blk):
                return False
    return True


ALLEY_LOG = []


def offset_line(pts, side, off):
    """가운데선 pts 를 side 쪽으로 off 만큼 평행 이동한 선(꺾이는 점은 마이터). 점의 y 는 가운데선 높이 그대로."""
    out = []
    n = len(pts)
    for i in range(n):
        ns = []
        for j in (i - 1, i):
            if 0 <= j < n - 1:
                (ax, _, az), (bx, _, bz) = pts[j], pts[j + 1]
                L = math.hypot(bx - ax, bz - az)
                hx, hz = (bx - ax) / L, (bz - az) / L
                ns.append((-hz, hx) if side > 0 else (hz, -hx))
        mx, mz = sum(v[0] for v in ns), sum(v[1] for v in ns)
        L = math.hypot(mx, mz)
        mx, mz = mx / L, mz / L
        k = off / max(0.5, mx * ns[0][0] + mz * ns[0][1])
        x, y, z = pts[i]
        out.append((x + mx * k, y, z + mz * k))
    return out


def fill_wall(tag, line, side, s0, s1, pattern, gap=0.3):
    """벽 선 line(offset_line 결과)의 호 길이 s0~s1 에 pattern(길이, 깊이, 종류, 바닥 위 높이, 이름, 층)을 차례로 세운다.
    조각은 꺾이는 점을 넘지 않는다. 앞면은 벽 선에 고정, 뒤가 걷는 면·기존 건물(골목 벽 이전 것)에 닿으면 깊이를 줄이고(최소 0.3m),
    그래도 안 되면 0.5m 앞으로 가서 다시 해 본다(기존 건물이 이미 벽인 곳은 비워 둠 — 결정적). 2m 보다 짧은 집 조각은 담으로."""
    placed = 0
    k = 0
    acc = 0.0
    for i in range(len(line) - 1):
        (ax, ay, az), (bx, by, bz) = line[i], line[i + 1]
        L = math.hypot(bx - ax, bz - az)
        hx, hz = (bx - ax) / L, (bz - az) / L
        nx_, nz_ = (-hz, hx) if side > 0 else (hz, -hx)
        yaw = yaw_from_dir(hx, hz)
        t = max(0.0, s0 - acc)
        end = min(L, s1 - acc)
        while end - t >= 1.0:
            L0, d0, kind, h, name, floors = pattern[k % len(pattern)]
            LL = min(L0, end - t)
            if end - t - LL < 1.0:      # 남는 토막이 1m 안 되면 이 조각을 늘려 끝까지
                LL = end - t
            if kind == "house" and LL < 2.0:
                L0, d0, kind, h, name, floors = WALL_
            mid = t + LL / 2
            fx, fz = ax + hx * mid, az + hz * mid
            y = ay + (by - ay) * mid / L
            done = None
            depths = [d0]
            while depths[-1] - 0.5 >= 0.8:
                depths.append(round(depths[-1] - 0.5, 2))
            if d0 > 0.3:
                depths.append(0.3)
            for dd in depths:
                cx, cz = fx + nx_ * dd / 2, fz + nz_ * dd / 2
                if block_fits(cx, cz, LL, dd, yaw, solid=BASE_SOLID):
                    done = (cx, cz, dd)
                    break
            if not done:
                t += 0.5
                continue
            cx, cz, dd = done
            if dd < 1.0 and kind == "house":   # 깊이가 1m 안 되면 집 대신 담
                kind, h, name, floors = "fence", WALL_[3], WALL_[4], 0
            ms = h if kind in ("fence", "railing") else (0.9 if kind == "retaining" else min(h, 2.5))
            block(name, kind, cx, cz, LL, dd, yaw, h, ref_y=y, min_show=ms,
                  use="%s 골목 벽(%s)" % (tag, "왼쪽" if side > 0 else "오른쪽"), extra={"floors": floors} if floors else None)
            placed += 1
            k += 1
            t += LL + gap
        acc += L
    ALLEY_LOG.append((tag, "왼쪽" if side > 0 else "오른쪽", placed))


BASE_SOLID = [b for b in BLOCKS if b["kind"] not in ("tree", "bush")]   # 골목 벽을 세우기 전 건물·담
ALLEY_E = [r for r in ROADS if r["name"] == "명륜3가 골목 입구"][0]["points"]
ALLEY_A = [r for r in ROADS if r["name"] == "명륜3가 골목(시우네 골목)"][0]["points"]
_st = [s_ for s_ in STAIRS if s_["name"].startswith("성곽 아래 꼭대기 계단")][0]
STAIR_A = [_st["from"], _st["to"]]
ALLEY_EA = ALLEY_E + ALLEY_A[1:]          # 계단 아래 → 체크포인트 4 → 시우네 집 앞 → 꼭대기 계단 밑
H1, H2 = 3.6, 6.6   # 단층(3.0 + 지붕 턱 0.6) / 2층
HOUSE1 = (6.0, 6.0, "house", H1, "골목 단독주택(단층)", 1)
HOUSE2 = (5.5, 6.5, "house", H2, "골목 단독주택(2층)", 2)
WALL_ = (3.0, 0.35, "fence", 2.1, "골목 담장", 0)
_L = offset_line(ALLEY_EA, +1, ALLEY_SET)
_R = offset_line(ALLEY_EA, -1, ALLEY_SET)
# 왼쪽(서·북서): 후문상가 B동이 벽인 곳은 건너뛰고, 모퉁이 → 시우네 앞집까지 집이 줄지어
fill_wall("명륜3가", _L, +1, 0.6, pl_len(_L), [HOUSE1, HOUSE2, WALL_, HOUSE1, HOUSE2, HOUSE1])
# 오른쪽(동·남동): 담 + 집, 동쪽 갈림길(명륜1가 방면) 앞에서 끝(그 뒤는 반시우네 다세대 벽)
_jx, _jz = -164.5, 121.5
_d, _y, _i, _t, _qx, _qz = poly_project(_R, _jx, _jz)
_sj = sum(math.hypot(_R[k + 1][0] - _R[k][0], _R[k + 1][2] - _R[k][2]) for k in range(_i)) + _t * math.hypot(
    _R[_i + 1][0] - _R[_i][0], _R[_i + 1][2] - _R[_i][2])
fill_wall("명륜3가", _R, -1, 0.6, _sj - 1.6, [WALL_, HOUSE1, WALL_, HOUSE2, WALL_, HOUSE1])
# 꼭대기 계단(체크포인트 5 → 6): 서쪽(오르막 쪽) = 단층집 + 낮은 축대(계단참에서 공터 쪽 시선을 막지 않게 1.4m). 동쪽 위 = 담
_SL = offset_line(STAIR_A, +1, STAIR_SET)
_SR = offset_line(STAIR_A, -1, STAIR_SET)
fill_wall("꼭대기 계단", _SL, +1, 1.3, 4.7, [(3.4, 4.5, "house", H1, "계단 옆 단층집", 1)])
fill_wall("꼭대기 계단", _SL, +1, 5.0, pl_len(_SL) - 0.9, [(3.5, 0.7, "retaining", 1.4, "계단 축대", 0)])
fill_wall("꼭대기 계단", _SR, -1, 4.6, pl_len(_SR) - 0.9, [(3.0, 0.35, "fence", 1.9, "골목 담장", 0)])


# 4-9 나무·풀숲 (결정적 배치: 후보 격자에서 길·건물과 겹치지 않는 곳만)
def clear_of_walk(x, z, rad):
    for r in ROADS:
        d, *_ = poly_project(r["points"], x, z)
        if d < r["width"] / 2 + rad:
            return False
    for s in STAIRS:
        d, *_ = seg_project(s["from"], s["to"], x, z)
        if d < s["width"] / 2 + rad:
            return False
    for p in PADS:
        cx, cy, cz = p["center"]
        w, d = p["size"]
        if p["shape"] == "circle":
            if math.hypot(x - cx, z - cz) < w / 2 + rad:
                return False
        elif in_rect(x, z, cx, cz, w, d, p["yaw"], rad):
            return False
    dw, *_ = poly_project(WALL_PTS, x, z)
    if dw < WALL_T / 2 + rad:
        return False
    for b in BLOCKS:
        if in_rect(x, z, b["center"][0], b["center"][2], b["size"][0], b["size"][2], b["yaw"], rad):
            return False
    return True


def north_of_wall(x, z):
    d, y, i, t, qx, qz = poly_project(WALL_PTS, x, z)
    a, b_ = WALL_PTS[i], WALL_PTS[i + 1]
    hx, hz = b_[0] - a[0], b_[2] - a[2]
    return (hx * (z - qz) - hz * (x - qx)) > 0  # 진행 방향 왼쪽(북)


# 시선 통로: 이 안에는 키 큰 나무 대신 낮은 풀숲만(성곽·공터 가로등이 길에서 보이게)
VIEW_CORRIDORS = [
    ("후문 → 공원 계단 → 성곽", [(-204.0, 96.0), (-190.0, 96.0), (-178.0, 153.0), (-218.0, 153.0)]),
    ("꼭대기 계단참 → 공터", [(-158.0, 133.0), (-158.0, 141.0), (-190.0, 147.5), (-190.0, 136.5)]),
    ("꼭대기 계단참 → 성곽(북)", [(-166.0, 138.0), (-154.0, 138.0), (-150.0, 162.0), (-170.0, 160.0)]),
]


def in_poly(x, z, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        (ax, az), (bx, bz) = poly[i], poly[(i + 1) % n]
        if (az > z) != (bz > z) and x < ax + (z - az) * (bx - ax) / (bz - az):
            inside = not inside
    return inside


def in_corridor(x, z):
    return any(in_poly(x, z, poly) for _, poly in VIEW_CORRIDORS)


TREE_AREAS = [  # (x0, x1, z0, z1, 간격)
    (-268, -200, 100, 148, 7.0),   # 와룡공원 숲
    (-196, -164, 112, 150, 6.5),   # 공터 아래 비탈·성곽 아래
    (-164, -130, 128, 160, 7.5),   # 성곽 아래 동쪽
]
k = 0
for x0, x1, z0, z1, step in TREE_AREAS:
    z = z0
    row = 0
    while z <= z1:
        x = x0 + (step / 2 if row % 2 else 0)
        while x <= x1:
            jx = ((k * 37) % 11 - 5) * 0.25
            jz = ((k * 53) % 9 - 4) * 0.25
            px, pz = x + jx, z + jz
            k += 1
            if north_of_wall(px, pz) or not (X0 + 2 < px < -130 and Z0 + 2 < pz < 172):
                x += step
                continue
            if clear_of_walk(px, pz, 2.2):
                # 와룡공원 숲(첫 구역)은 소나무 위주, 마을 쪽 비탈은 풀숲 위주(성곽이 지붕 너머로 보이게)
                tall = (k % 3 != 0) if x0 < -200 else (k % 4 == 0)
                if in_corridor(px, pz):
                    tall = False
                if tall:
                    block("소나무", "tree", px, pz, 2.4, 2.4, (k * 29) % 90, 7.5 + (k % 4) * 0.8, min_show=7.0,
                          use="와룡공원 소나무(그레이박스는 기둥 상자)")
                else:
                    block("풀숲", "bush", px, pz, 3.2, 2.4, (k * 29) % 90, 1.3, min_show=1.2)
            x += step
        z += step * 0.87
        row += 1

# 성벽 안쪽 기슭 풀숲 띠(성벽에 붙어 걷지 않게, 카메라 가림 적은 낮은 덤불)
for i in range(len(WALL_PTS) - 1):
    a, b_ = WALL_PTS[i], WALL_PTS[i + 1]
    mx, mz = (a[0] + b_[0]) / 2, (a[2] + b_[2]) / 2
    hx, hz = b_[0] - a[0], b_[2] - a[2]
    L = math.hypot(hx, hz)
    nx_, nz_ = hz / L, -hx / L  # 진행 방향 오른쪽 = 성 안(남)
    px, pz = mx + nx_ * 2.4, mz + nz_ * 2.4
    if clear_of_walk(px, pz, 0.9):
        block("성벽 기슭 풀숲", "bush", px, pz, min(L * 0.8, 6.0), 1.4, yaw_from_dir(hx / L, hz / L), 0.9, min_show=0.8)

# ================================================================ 5. 랜드마크
LANDMARKS = []


def lm(name, kind, x, y, z, **kw):
    d = {"name": name, "kind": kind, "pos": P(x, y, z)}
    d.update(kw)
    LANDMARKS.append(d)
    return d


gate_y = sidewalk_edge_x(-198.0, -1)[1]
lm("성대 후문", "gate", -198.0, gate_y, 93.4, zone="hoomun_street", note="시작 지점. 실명 랜드마크")
lm("명륜3가 종점(마을버스 1번)", "bus_stop", -206.0, 31.2, 104.9, zone="hoomun_street", height=2.6)
bs = curb_point(-186.25, +1)
lm("후문시장 앞 정류장(마을버스 1번)", "bus_stop", bs[0], bs[1], bs[2], zone="hoomun_street", height=2.6)
lm("후문·와룡공원 정류장(마을버스 3번, 확장)", "bus_stop", -196.4, 31.9, 110.5, zone="waryong_entrance", height=2.6)
_b = [b for b in BLOCKS if b["name"] == "후문상가 B동"][0]
_ux, _uz = local_axes(_b["yaw"])
_fe = (_b["center"][0] + _ux[0] * _b["size"][0] / 2 - _uz[0] * (_b["size"][2] / 2 + 0.45),
       _b["center"][2] + _ux[1] * _b["size"][0] / 2 - _uz[1] * (_b["size"][2] / 2 + 0.45))  # 정면 동쪽 모서리에서 인도 쪽 0.45m
lm("하루편의점 간판", "sign", _fe[0], main_at(_fe[0])[0] + SIDEWALK_CURB + 3.0, _fe[1], zone="haru_conv", height=1.2,
   note="골목 모서리 돌출 간판(바닥에서 3m 위). 후문에서 동쪽을 보면 첫 번째로 눈에 띄는 밝은 면 — 골목 입구 표시")
lm("와룡공원 안내판", "sign", -196.6, 31.4, 106.0, zone="waryong_entrance", height=2.0)
lm("한양도성 백악구간 안내판", "sign", -203.0, 38.6, 144.6, zone="waryong_trail", height=2.0)
lm("혜성고 방면 표지", "sign", -133.0, 34.3, 157.2, height=2.0, note="동쪽 경계 너머 = 혜성고 정문 앞(3구역)")
cp = curb_point(-133.0, +1)
lm("명륜1가 방면 표지", "sign", cp[0], cp[1], cp[2], height=2.0)
lm("말바위 방면(출입 시간 제한) 표지", "sign", -262.4, 46.0, 139.4, height=2.0)
cp = curb_point(-184.0, +1)
lm("전봇대 전단지 퀘스트 보드", "board", cp[0], cp[1], cp[2], zone="hoomun_street", height=8.0,
   note="전봇대에 붙은 전단지(퀘스트 보드)")
for i, xs in enumerate((-196.0, -170.0, -150.0, -138.0)):
    pe = curb_point(xs, +1)
    lm("전봇대 %d(성균관로)" % (i + 1), "pole", pe[0], pe[1], pe[2], height=9.0, note="전선 = 후지모토식 로우앵글 하늘")
# 골목 전봇대·보안등: 골목 벽(4-8)과 걷는 면 사이 띠에 세운다
for i, (pts_, s_, off_) in enumerate(((ALLEY_E, 6.1, POLE_SET), (ALLEY_A, 11.6, POLE_SET), (STAIR_A, 3.3, 1.2))):
    x, _, z = side_point(pts_, s_, +1, off_)
    lm("전봇대 %d(명륜3가 골목)" % (i + 1), "pole", x, ground_y(x, z), z, height=8.0)
lm("공터 가로등(서)", "lamp", -198.2, 38.0, 145.4, zone="waryong_entrance", height=4.5, note="야차 링 조명 2개 중 하나")
lm("공터 가로등(동)", "lamp", -185.6, 38.0, 145.6, zone="waryong_entrance", height=4.5)
for i, (x, z) in enumerate(((-216.0, 144.6), (-236.0, 141.0), (-250.5, 133.4))):
    lm("성곽길 가로등 %d" % (i + 1), "lamp", x, ground_y(x, z), z, height=4.0, note="밤에 서쪽 끝까지 점선처럼 이어져 길 안내")
_lamps = [side_point(ALLEY_A, 5.4, +1, POLE_SET), side_point(STAIR_A, 6.8, -1, 1.2), (-161.0, 0.0, 141.6)]
for i, (x, _, z) in enumerate(_lamps):
    lm("골목 보안등 %d" % (i + 1), "lamp", x, ground_y(x, z), z, height=3.6, note="나트륨 주황")
lm("암문(닫힘, 북정마을 방면)", "wall_gate", -215.0, wall_base_y(-215.0, 147.0), 147.0, zone="waryong_trail",
   note="03_맵 '성곽 틈 계단'. M1 은 닫힌 문, 북정마을(성곽 밖)은 이후 마일스톤")
lm("와룡공원 전망 쉼터", "viewpoint", VIEW["center"][0], VIEW["center"][1], VIEW["center"][2], zone="waryong_trail")
lm("배드민턴 네트(새벽만)", "prop", -192.0, 38.0, 142.0, zone="waryong_entrance", note="밤엔 걷혀서 공터 = 야차 링")
lm("말바위(원경)", "vista", *REF["malbawi"][2], note="구역 밖. 전망 쉼터에서 북서쪽 능선 위로 보이는 목표")
lm("북악산 능선(원경)", "vista", -420.0, 120.0, 230.0, note="스카이박스/빌보드. 북서쪽 수평선")
lm("성북동 지붕들(성곽 너머 원경)", "vista", -200.0, 25.0, 230.0, note="성곽길에서 성벽 너머로 보이는 저지대 지붕")
lm("남쪽 도심(캠퍼스 너머 원경)", "vista", -160.0, 0.0, 0.0, note="내리막 = 남쪽 = 대학로 방향")
lm("후문 고양이 '후문이' 자리", "npc_spot", -183.4, 29.4, 115.6, note="상가 뒤 하숙집 담 위. 03_맵 동네 고양이(가상)")
lm("Y4 후문 뒷골목 주차장", "arena", PARK_C[0], PARK_C[1], PARK_C[2], radius=6.0, zone="hoomun_parking",
   note="M2 전투 후보 ① 인카운터")
lm("Y1 와룡공원 성곽 아래 공터", "arena", -192.0, 38.0, 142.0, radius=6.5, zone="waryong_entrance",
   note="M2 전투 후보 ② 야차")
LANDMARKS.extend(DOORS)

# ================================================================ 6. 시작 지점·체크포인트
# 2차(카메라): 1차 자리(남쪽 인도, 닫힌 후문 펜스 앞 2m)는 등 뒤 4m 카메라가 펜스 너머에 놓여 다리 가림 검사가 카메라를 2.2m 로
# 당겼다(인물이 화면 2/3, 무릎 아래 잘림). 차도 남쪽 차선(연석에서 2m)으로 2.5m 앞에 세워 카메라(뒤 4m·높이 1.96m)가
# 펜스·기둥 앞 인도 위(펜스에서 약 1.2m)에 오게 했다 → 07 4-3 구도(인물 화면 높이 50%, 발 아래 10%) 그대로.
def road_pt(x, off):
    """성균관로 중심선에서 진행 방향 왼쪽(+북)으로 off m 떨어진 차도 위 점."""
    y, z, hx, hz = main_at(x)
    return (x + (-hz) * off, y, z + hx * off)


spawn_pt = road_pt(-197.6, -1.5)
SPAWN = {"pos": P(spawn_pt[0], spawn_pt[1] + 0.05, spawn_pt[2]), "yaw": 34.0,
         "note": "성대 후문 앞 차도 남쪽 차선(2차: 카메라가 펜스 앞에 오게). 화면 왼쪽 = 공원 오르막·와룡공원, 오른쪽 = 상가거리·다음 체크포인트 빛 기둥"}
cp2 = sidewalk_mid(-180.5, +1)
ROUTE = [
    {"name": "1. 성대 후문", "pos": SPAWN["pos"], "radius": 2.5, "zone": "hoomun_street"},
    {"name": "2. 후문 상가거리(국밥·하루편의점 앞)", "pos": P(*cp2), "radius": 3.0, "zone": "hoomun_street"},
    {"name": "3. 후문 뒷골목 주차장(Y4)", "pos": P(*PARK_C), "radius": 5.0, "zone": "hoomun_parking"},
    {"name": "4. 명륜3가 골목 입구", "pos": P(*ALLEY_IN), "radius": 2.5, "zone": "myeongnyun3ga_alley"},
    {"name": "5. 반시우네 집 앞", "pos": P(*SIU_FRONT), "radius": 2.5, "zone": "siu_house"},
    {"name": "6. 성곽 아래 꼭대기 계단참", "pos": P(*LANDING["center"]), "radius": 2.0, "zone": "myeongnyun3ga_alley"},
    {"name": "7. 와룡공원 성곽 아래 공터(Y1)", "pos": P(-192.0, 38.0, 142.0), "radius": 5.0, "zone": "waryong_entrance"},
    {"name": "8. 와룡공원 성곽길(암문 앞)", "pos": P(-215.0, 40.0, 142.2), "radius": 3.0, "zone": "waryong_trail"},
    {"name": "9. 와룡 성곽길 서쪽", "pos": P(-244.75, 44.0, 136.5), "radius": 3.0, "zone": "waryong_trail"},
    {"name": "10. 와룡공원 전망 쉼터(종점)", "pos": P(*VIEW["center"]), "radius": 3.0, "zone": "waryong_trail"},
]
# 8번 높이는 성곽길 위 실제 높이로
d_, y8, *_ = poly_project(TRAIL["points"], -215.0, 142.2)
ROUTE[7]["pos"][1] = r2(y8)

# ================================================================ 7. 색 규칙(그레이박스 단색)
COLORS = {  # 2026-10-05 2차: 명도 사다리(06 문서 10장, zone1_check 검증 8 이 화면 L* 로 계산해 확인).
    # 기준: 화면에서 맞붙는 넓은 면끼리 L* 8 이상 · 하늘과 건물·성곽의 가장 밝은 면 10 이상 · 하늘과 안개 6 이상
    # 화면 L*(윗면): 하늘 88 > 인도 83 > 골목 78 ≈ 성곽길 돌 포장 77 > 상가 76 > 성곽 73(안쪽 면 69) ≈ 주택 72 ≈ 흙 마당 71 > 땅 60 > 숲길·흙길 51 > 차도 49
    "ground": "#9C947C", "road": "#7A7570", "driveway": "#86817B", "sidewalk": "#DFD7CD",
    "alley": "#C5CACF", "path": "#8B7660", "trail": "#897A65", "wallpath": "#D5D1CA",
    "pad_asphalt": "#86817B", "pad_dirt": "#C3B198", "pad_stone": "#D5D1CA", "pad_deck": "#AA846B",
    "stairs": "#A9725F",
    "shop": "#CDBFAF", "house": "#C3B3AD", "hanok": "#4B4845", "campus": "#BBB9B1", "pavilion": "#9B5A4B",
    "wall": "#C8C3BC", "wallcap": "#6B6863", "fence": "#7F7873", "railing": "#7F7873", "retaining": "#B9B6AF",
    "tree": "#697059", "bush": "#7B7E68",
    "gate": "#9B5A4B", "barrier": "#4C4346", "prop": "#6F7680",
    "sign": "#3F5E6B", "board": "#3F5E6B", "pole": "#5B5552", "lamp": "#5B5552", "bus_stop": "#3F5E6B",
    "door": "#9A5446", "wall_gate": "#5E4A44", "viewpoint": "#3F5E6B", "npc_spot": "#B5A68A",
    # 체크포인트 = 길잡이 UI 색(시우 강조 주황 계열, 배경 소품에는 안 씀) · 전투 무대 표시는 디버그 청록
    "arena": "#3B5360", "checkpoint": "#E2582C",
    # 하늘 = 종이 크림, 안개 = 그보다 L* 7 낮은 회색 아지랑이(먼 건물·산이 하늘로 녹지 않고 실루엣이 남게)
    "sky": "#EEE3D1", "fog": "#D4CEC3",
}

# ================================================================ 8. 출력
heights = [[r2(v) for v in row] for row in H]  # heights[j][i], j=0 남쪽 줄, i=0 서쪽 칸
ground = [{
    "kind": "heightgrid", "name": "1구역 지형",
    "origin": P(X0, Z0), "cell": CELL, "nx": NX, "nz": NZ,
    "order": "heights[j][i] = 높이 at (x = origin[0] + i*cell, z = origin[1] + j*cell). j=0 이 남쪽 끝 줄, i=0 이 서쪽 끝 칸",
    "heights": heights,
    "note": "길·계단·패드·성벽 아래 격자점은 그 면 높이로 고정, 나머지는 조화 보간(라플라스). 길 리본은 이 격자 위에 따로 그린다",
}]
for p in PADS:
    g = {"kind": "pad", "name": p["name"], "shape": p["shape"], "center": P(*p["center"]),
         "size": P(*p["size"]), "yaw": r2(p["yaw"]), "surface": p["surface"]}
    if "note" in p:
        g["note"] = p["note"]
    ground.append(g)

roads_out = []
for r in ROADS:
    d = {"name": r["name"], "kind": r["kind"], "width": r2(r["width"]), "points": [P(*q) for q in r["points"]]}
    if "note" in r:
        d["note"] = r["note"]
    roads_out.append(d)

stairs_out = []
for s in STAIRS:
    a, b_ = s["from"], s["to"]
    run = math.hypot(b_[0] - a[0], b_[2] - a[2])
    rise = abs(b_[1] - a[1])
    steps = max(1, int(math.ceil(rise / 0.18 - 1e-9)))
    d = {"name": s["name"], "from": P(*a), "to": P(*b_), "width": r2(s["width"]),
         "steps": steps, "riser": r2(rise / steps), "tread": r2(run / steps),
         "angleDeg": r2(math.degrees(math.atan2(rise, run)))}
    if "note" in s:
        d["note"] = s["note"]
    stairs_out.append(d)

allx = [q[0] for r in ROADS for q in r["points"]]
data = {
    "meta": {
        "name": "혜화동 1구역 — 성대 후문~와룡공원 (M1 그레이박스)",
        "version": "0.2.0",
        "updated": "2026-10-05",
        "doc": "haengin1/docs/06_M1_그레이박스_설계.md",
        "generator": "haengin1/tools/zone1_gen.py",
        "units": "m (게임 단위 = Unity 1)",
        "axes": {"x": "동 +", "y": "높이 +", "z": "북 +"},
        "origin": "혜화동 로터리 중심(게임 좌표 0,0,0)",
        "fromMapZones": "X = x*0.25, Y = (y-30)*0.5, Z = -z*0.25  (map_zones.json 은 z = 남쪽 +, y = 해발)",
        "yaw": "Unity Y축 회전(도). 0 = size[0] 이 +X, size[2] 가 +Z. 양수 = 위에서 볼 때 시계 방향",
        "pointsY": "roads/stairs/pads/route/spawn 의 y = 그 위를 걷는 면 높이. walls 의 y = 성벽 기초(지면) 높이. blocks 의 center 는 상자 가운데",
        "controller": {"slopeLimitDeg": 35, "stepOffset": 0.3, "stairRiserMax": 0.18,
                       "note": "경사 35° 이하는 경사로, 그보다 가파르면 계단. 계단 단 높이 0.18m 이하"},
        "speeds": {"walk": 1.6, "run": 4.5, "sprint": 6.0},
        "playerHeights": {"siwoo": 1.74, "taeo": 1.83},
    },
    "bounds": {"min": P(X0, 15.0, Z0), "max": P(-130.0, 62.0, 174.0),
               "note": "보이지 않는 경계벽(높이 20m)을 이 사각형 둘레에 세운다. 북쪽은 성벽이 1차 경계"},
    "ground": ground,
    "roads": roads_out,
    "blocks": BLOCKS,
    "walls": [{"name": "한양도성 성곽(와룡공원 구간)", "points": [P(*q) for q in WALL_PTS],
               "height": WALL_H, "thickness": WALL_T,
               # 한양도성 단면(안쪽에서 본 모양): 체성(돌 줄눈 면) 위에 미석(눈썹돌 띠) → 바깥쪽 가장자리에 여장(타구가 있는 낮은 담) → 옥개석(지붕돌 띠)
               "body": {"course": 0.5, "stone": [0.5, 0.98],
                        "note": "체성 = 화강암 다듬은 돌 켜쌓기(줄눈 무늬는 Zone1Builder 생성 텍스처 T_Z1_Stone — 한 켜 0.5m, 돌 길이 0.5~0.98m. 빌더 상수와 같은 값을 적어 둠)"},
               "brow": {"height": 0.16, "overhang": 0.14, "color": "wallcap", "note": "미석 — 체성과 여장 사이 튀어나온 돌 띠"},
               "parapet": {"height": 1.15, "thickness": 0.8, "merlon": 3.4, "crenel": 0.45, "embrasure": [0.22, 0.3],
                           "note": "여장 — 성 바깥(북) 쪽 가장자리. 한 타(3.4m)마다 타구(틈 0.45m), 타마다 총안(작은 구멍) 하나"},
               "cap": {"height": 0.2, "overhang": 0.08, "color": "wallcap", "note": "옥개석 — 여장 윗면 지붕돌 띠(하늘 앞 어두운 선)"},
               "note": "오르기·부수기 불가(문화재). 카메라 충돌 대상. 기초 높이는 안쪽 성곽길 + 0.8m 로 맞춤(map_zones wallLine y 는 추정치라 0.5~2.8m 낮춤). 바깥(북)은 지형이 3m 낮아 더 높아 보임. 충돌은 체성 위로 +10m(여장 모양과 상관없이 넘을 수 없음)"}],
    "stairs": stairs_out,
    "landmarks": LANDMARKS,
    "spawn": SPAWN,
    "route": ROUTE,
    "colors": COLORS,
    "refs": {k: {"name": v[0], "source": v[1], "pos": P(*v[2])} for k, v in REF.items()},
}

def jd(v):
    return json.dumps(v, ensure_ascii=False, separators=(", ", ": "))


def write_json(path, d):
    """사람이 diff 로 읽을 수 있게: 목록 항목은 한 줄에 하나, 높이 격자는 한 줄에 한 줄(z)."""
    out = ["{"]
    keys = list(d.keys())
    for ki, k in enumerate(keys):
        v = d[k]
        tail = "," if ki < len(keys) - 1 else ""
        if isinstance(v, list):
            out.append('  "%s": [' % k)
            for ii, item in enumerate(v):
                t2 = "," if ii < len(v) - 1 else ""
                if isinstance(item, dict) and item.get("kind") == "heightgrid":
                    head = {kk: vv for kk, vv in item.items() if kk != "heights"}
                    s = jd(head)[:-1] + ', "heights": ['
                    out.append("    " + s)
                    rows = item["heights"]
                    for ri, row in enumerate(rows):
                        out.append("      " + jd(row) + ("," if ri < len(rows) - 1 else ""))
                    out.append("    ]}" + t2)
                else:
                    out.append("    " + jd(item) + t2)
            out.append("  ]" + tail)
        elif isinstance(v, dict) and k in ("meta", "refs", "colors"):
            out.append('  "%s": {' % k)
            sub = list(v.items())
            for si, (kk, vv) in enumerate(sub):
                out.append("    %s: %s%s" % (jd(kk), jd(vv), "," if si < len(sub) - 1 else ""))
            out.append("  }" + tail)
        else:
            out.append('  "%s": %s%s' % (k, jd(v), tail))
    out.append("}")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")


os.makedirs(os.path.dirname(OUT), exist_ok=True)
write_json(OUT, data)
json.load(open(OUT, encoding="utf-8"))  # 다시 읽어 문법 확인
print("wrote", OUT, os.path.getsize(OUT), "bytes;", len(BLOCKS), "blocks,", len(ROADS), "roads,",
      len(STAIRS), "stairs,", len(LANDMARKS), "landmarks")
