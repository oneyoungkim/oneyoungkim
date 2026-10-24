# -*- coding: utf-8 -*-
"""zone1.json 읽기 + 키트가 묻는 질문들(땅 높이, 걷는 면까지 거리, 블록 유형·구역, 면 역할).

zone1.json 은 tools/zone1_gen.py 가 만든다(손으로 고치지 않음). 좌표: X 동, Y 높이, Z 북(Unity), 원점 = 혜화동 로터리.
yaw: 0 이면 size[0] 이 +X, size[2] 가 +Z, 위에서 볼 때 시계 방향 +. 블록 center 는 상자 가운데(바닥은 땅속 0.5m).
"""
import json
import math
import re

from .geom import Frame

CARVE_MARGIN = 2.0   # Zone1Builder 와 같은 값: 걷는 면 둘레 2m 안 지형 점은 그 면보다 0.12m 아래로
CARVE_EPS = 0.12

SOLID_KINDS = ("shop", "house", "hanok", "campus", "pavilion", "gate", "fence", "retaining", "railing", "barrier", "prop")
BUILD_KINDS = ("shop", "house", "hanok", "campus", "pavilion", "gate", "fence", "retaining", "railing")


def seg_closest(ax, ay, az, bx, by, bz, x, z):
    dx, dz = bx - ax, bz - az
    l2 = dx * dx + dz * dz
    t = 0.0 if l2 < 1e-10 else max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / l2))
    px, pz = ax + dx * t, az + dz * t
    return math.hypot(x - px, z - pz), ay + (by - ay) * t, t


def poly_closest(pts, x, z):
    best = (1e18, 0.0, 0, 0.0)
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        d, y, t = seg_closest(a[0], a[1], a[2], b[0], b[1], b[2], x, z)
        if d < best[0]:
            best = (d, y, i, t)
    return best


class Block:
    def __init__(self, idx, d):
        self.idx = idx
        self.raw = d
        self.name = d["name"]
        self.kind = d["kind"]
        self.cx, self.cy, self.cz = d["center"]
        self.w, self.h, self.d = d["size"]
        self.yaw = d.get("yaw", 0.0)
        self.floors = d.get("floors")
        self.use = d.get("use", "")
        self.top = self.cy + self.h / 2
        self.bottom = self.cy - self.h / 2
        self.F = Frame(self.cx, self.cz, self.yaw)
        self.key = "%s@%.2f,%.2f" % (self.name, self.cx, self.cz)
        # 아래는 분류 뒤에 채움
        self.btype = None
        self.district = None
        self.roles = {}
        self.ground_faces = {}

    def corners(self, margin=0.0):
        w, d = self.w / 2 + margin, self.d / 2 + margin
        return [self.F.p(sx * w, 0, sz * d) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]

    def contains(self, x, z, margin=0.0):
        lx, lz = self.F.local(x, z)
        return abs(lx) <= self.w / 2 + margin and abs(lz) <= self.d / 2 + margin


class Zone:
    def __init__(self, path):
        with open(path, encoding="utf-8") as f:
            D = json.load(f)
        self.path = path
        self.D = D
        self.meta = D["meta"]
        g = [x for x in D["ground"] if x["kind"] == "heightgrid"][0]
        self.gx0, self.gz0 = g["origin"]
        self.cell = g["cell"]
        self.nx, self.nz = g["nx"], g["nz"]
        self.H = [list(r) for r in g["heights"]]
        self.pads = [x for x in D["ground"] if x["kind"] == "pad"]
        self.roads = D["roads"]
        self.stairs = D["stairs"]
        self.walls = D["walls"]
        self.landmarks = D["landmarks"]
        self.colors = D["colors"]
        self.blocks = [Block(i, b) for i, b in enumerate(D["blocks"])]
        self.main_road = self.roads[0]["points"]
        self._carve()

    # ------------------------------------------------------------ 걷는 면
    def walk_info(self, x, z):
        """(가장 가까운 걷는 면 가장자리까지 거리(안쪽이면 음수), 그 면 높이, 종류)."""
        best = (1e18, 0.0, None)
        for r in self.roads:
            d, y, _, _ = poly_closest(r["points"], x, z)
            dd = d - r["width"] / 2
            if dd < best[0]:
                best = (dd, y, r["kind"])
        for s in self.stairs:
            a, b = s["from"], s["to"]
            d, y, _ = seg_closest(a[0], a[1], a[2], b[0], b[1], b[2], x, z)
            dd = d - s["width"] / 2
            if dd < best[0]:
                best = (dd, y, "stairs")
        for p in self.pads:
            dd = self.pad_dist(p, x, z)
            if dd < best[0]:
                best = (dd, p["center"][1], "pad")
        return best

    def walk_dist(self, x, z):
        return self.walk_info(x, z)[0]

    @staticmethod
    def pad_dist(p, x, z):
        cx, _, cz = p["center"]
        if p["shape"] == "circle":
            return math.hypot(x - cx, z - cz) - p["size"][0] / 2
        a = math.radians(p["yaw"])
        dx, dz = x - cx, z - cz
        u = dx * math.cos(a) - dz * math.sin(a)
        v = dx * math.sin(a) + dz * math.cos(a)
        ex, ez = abs(u) - p["size"][0] / 2, abs(v) - p["size"][1] / 2
        if ex <= 0 and ez <= 0:
            return max(ex, ez)
        return math.hypot(max(ex, 0.0), max(ez, 0.0))

    def walk_limit(self, x, z, margin):
        lim = 1e18
        for r in self.roads:
            d, y, _, _ = poly_closest(r["points"], x, z)
            if d - r["width"] / 2 <= margin:
                lim = min(lim, y)
        for s in self.stairs:
            a, b = s["from"], s["to"]
            d, y, _ = seg_closest(a[0], a[1], a[2], b[0], b[1], b[2], x, z)
            half = abs(b[1] - a[1]) / max(1, s["steps"]) * 0.5
            if d - s["width"] / 2 <= margin:
                lim = min(lim, y - half)
        for p in self.pads:
            if self.pad_dist(p, x, z) <= margin:
                lim = min(lim, p["center"][1])
        return lim

    # ------------------------------------------------------------ 지형
    def _carve(self):
        """Zone1Builder.BuildGround 와 같은 깎기(걷는 면 둘레 2m 안 점을 면 높이 - 0.12 로)."""
        self.HC = [row[:] for row in self.H]
        for j in range(self.nz):
            for i in range(self.nx):
                x, z = self.gx0 + i * self.cell, self.gz0 + j * self.cell
                lim = self.walk_limit(x, z, CARVE_MARGIN) - CARVE_EPS
                if self.HC[j][i] > lim:
                    self.HC[j][i] = lim

    def grid_y(self, x, z, carved=True):
        H = self.HC if carved else self.H
        fi = (x - self.gx0) / self.cell
        fj = (z - self.gz0) / self.cell
        i = int(max(0, min(self.nx - 2, math.floor(fi))))
        j = int(max(0, min(self.nz - 2, math.floor(fj))))
        tx, tz = min(1.0, max(0.0, fi - i)), min(1.0, max(0.0, fj - j))
        # Unity 지형 삼각형 분할과 같게: (i,j)-(i+1,j+1) 대각선
        h00, h10, h01, h11 = H[j][i], H[j][i + 1], H[j + 1][i], H[j + 1][i + 1]
        if tz >= tx:   # 삼각형 a(i,j) c(i,j+1) d(i+1,j+1)
            return h00 + (h11 - h01) * tx + (h01 - h00) * tz
        return h00 + (h10 - h00) * tx + (h11 - h10) * tz

    def ground(self, x, z):
        """발밑 높이: 걷는 면 안이면 그 면, 아니면 깎은 지형."""
        d, y, kind = self.walk_info(x, z)
        if d <= 0.0:
            return y + (0.0 if kind != "stairs" else 0.0)
        return self.grid_y(x, z)

    def road_z(self, x):
        pts = self.main_road
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            if a[0] <= x <= b[0]:
                t = (x - a[0]) / (b[0] - a[0])
                return a[2] + (b[2] - a[2]) * t
        return pts[0][2] if x < pts[0][0] else pts[-1][2]

    def solid_at(self, x, z, exclude=None, margin=0.0, kinds=SOLID_KINDS):
        for b in self.blocks:
            if b is exclude or b.kind not in kinds:
                continue
            if b.contains(x, z, margin):
                return b
        return None

    def landmarks_of(self, kind):
        return [l for l in self.landmarks if l["kind"] == kind]


# ---------------------------------------------------------------- 분류
def classify(zone):
    for b in zone.blocks:
        b.btype = block_type(b)
        b.district = district_of(zone, b)
    for b in zone.blocks:
        if b.btype:
            analyse_faces(zone, b)


def block_type(b):
    n, k = b.name, b.kind
    area = b.w * b.d
    if k == "shop":
        return "shop"
    if k == "house":
        fl = b.floors or max(1, int(round((b.h - 1.1) / 3.0)))
        b.floors = fl
        if "단독" in n or "단층" in n:
            return "house"
        if fl >= 3:
            return "villa"
        if area >= 50 and min(b.w, b.d) >= 6.5:
            return "villa"
        return "house"
    if k == "hanok":
        return "hanok"
    if k == "campus":
        return "school" if area > 60 else "booth"
    if k == "gate":
        return "gatepost"
    if k == "pavilion":
        return "pavilion"
    if k == "fence":
        return "fence_grille" if n.startswith("캠퍼스") else "fence_wall"
    if k == "retaining":
        return "retaining_stone" if "축대" in n else "retaining_concrete"
    if k == "railing":
        return "railing"
    return None   # barrier·prop·tree·bush 는 키트 밖(Unity 쪽 그대로)


DISTRICTS = {
    "HoomunN": "성균관로 북쪽 줄(후문 상가거리·명륜3가 다세대)",
    "HoomunS": "성균관로 남쪽 줄 + 후문 뒷골목 주차장 둘레",
    "Alley": "명륜3가 골목·시우네·꼭대기 계단·한옥",
    "Campus": "캠퍼스 건물·후문 경비실·기둥·캠퍼스 담장",
    "Waryong": "와룡공원(정자)",
    "Wall": "한양도성 성곽(와룡공원 구간)",
    "Wires": "전봇대·전선(구역 전체)",
}


def district_of(zone, b):
    if b.btype is None:
        return None
    if b.kind in ("campus", "gate") or b.name.startswith("캠퍼스"):
        return "Campus"
    if b.kind == "pavilion" or (b.cx < -205 and b.cz > 110):
        return "Waryong"
    rz = zone.road_z(b.cx)
    d, *_ = poly_closest(zone.main_road, b.cx, b.cz)
    if b.cz < rz:
        return "HoomunS"
    if d < 16.0:
        return "HoomunN"
    return "Alley"


FACES = ("+x", "-x", "+z", "-z")


def face_geom(b, f):
    """면 f 의 (왼쪽 끝 점(바깥에서 볼 때), 오른쪽 방향 t, 바깥 법선 n, 너비) — 모두 월드 xz 2D."""
    F = b.F
    hw, hd = b.w / 2, b.d / 2
    if f == "+x":
        n = (F.ux[0], F.ux[2]); half_n, W = hw, b.d
    elif f == "-x":
        n = (-F.ux[0], -F.ux[2]); half_n, W = hw, b.d
    elif f == "+z":
        n = (F.uz[0], F.uz[2]); half_n, W = hd, b.w
    else:
        n = (-F.uz[0], -F.uz[2]); half_n, W = hd, b.w
    t = (-n[1], n[0])          # 바깥에서 볼 때 오른쪽(Unity 왼손 좌표 숫자 기준 — geom.world_uv 와 같은 식)
    c = (b.cx + n[0] * half_n, b.cz + n[1] * half_n)
    O = (c[0] - t[0] * W / 2, c[1] - t[1] * W / 2)
    return O, t, n, W


def analyse_faces(zone, b):
    """면 역할: front(걷는 면에 붙은 정면) / street(걷는 면에서 보이는 옆면) / back / party(옆 건물에 붙음).
    ground_faces[f] = 면 바로 바깥(0.3m) 땅 높이 표본 [(u, y)...]."""
    info = {}
    for f in FACES:
        O, t, n, W = face_geom(b, f)
        ws, blocked, gs = [], 0, []
        us = [W * k / 8 for k in range(1, 8)]
        for u in us:
            px, pz = O[0] + t[0] * u, O[1] + t[1] * u
            ws.append(min(zone.walk_dist(px + n[0] * 1.0, pz + n[1] * 1.0),
                          zone.walk_dist(px + n[0] * 2.5, pz + n[1] * 2.5) + 1.0))
            if zone.solid_at(px + n[0] * 0.45, pz + n[1] * 0.45, exclude=b,
                             kinds=("shop", "house", "hanok", "campus")):
                blocked += 1
        for k in range(0, 13):
            u = W * k / 12
            px, pz = O[0] + t[0] * u + n[0] * 0.3, O[1] + t[1] * u + n[1] * 0.3
            gs.append((u, zone.ground(px, pz)))
        info[f] = dict(walk=min(ws), walk_mean=sum(ws) / len(ws), blocked=blocked / len(us), W=W)
        b.ground_faces[f] = gs
    # 출입문 랜드마크가 붙은 면
    door_face = None
    for lm in zone.landmarks:
        if lm["kind"] != "door":
            continue
        x, _, z = lm["pos"]
        lx, lz = b.F.local(x, z)
        if abs(lx) <= b.w / 2 + 1.2 and abs(lz) <= b.d / 2 + 1.2:
            dxp, dxn, dzp, dzn = abs(lx - b.w / 2), abs(lx + b.w / 2), abs(lz - b.d / 2), abs(lz + b.d / 2)
            m = min(dxp, dxn, dzp, dzn)
            door_face = {dxp: "+x", dxn: "-x", dzp: "+z", dzn: "-z"}[m]
            break
    roles = {}
    cand = sorted(FACES, key=lambda f: (info[f]["blocked"] > 0.5, info[f]["walk"], info[f]["walk_mean"]))
    front = door_face or cand[0]
    if info[front]["blocked"] > 0.5 and door_face is None:
        front = cand[0]
    for f in FACES:
        i = info[f]
        if f == front:
            roles[f] = "front"
        elif i["blocked"] >= 0.5:
            roles[f] = "party"
        elif i["walk"] <= 3.2:
            roles[f] = "street"
        else:
            roles[f] = "back"
    b.roles = roles
    b.face_info = info


def _split_outside_parens(t, sep=","):
    out, depth, cur = [], 0, ""
    for ch in t:
        depth += (ch == "(") - (ch == ")")
        if ch == sep and depth == 0:
            out.append(cur)
            cur = ""
        else:
            cur += ch
    out.append(cur)
    return out


def parse_use(use):
    """'1층 서쪽 성대후문 국밥 / 1층 동쪽 하루편의점 후문점(...) / 2층 하숙' → {층: [세그먼트...]}"""
    out = {}
    parts = [p.strip() for seg in use.split("/") for p in _split_outside_parens(seg)]
    for seg in [p for p in parts if p]:
        m = re.match(r"(\d)층\s+(.+)", seg)        # '1층 ...' 만(‘다세대 4층’ 같은 건물 층수는 아님)
        if m:
            out.setdefault(int(m.group(1)), []).append(m.group(2))
        elif seg.startswith("위층"):
            out.setdefault(99, []).append(seg[2:].strip())
    return out


RESIDENTIAL = ("하숙", "원룸", "주택", "다세대", "빌라", "가정")


def tenant_name(seg):
    q = re.findall(r"'([^']+)'", seg)
    if q:
        return q[0]
    s = re.sub(r"\(.*?\)", "", seg)
    s = re.sub(r"^(서쪽|동쪽|남쪽|북쪽)\s*", "", s).strip()
    return s
