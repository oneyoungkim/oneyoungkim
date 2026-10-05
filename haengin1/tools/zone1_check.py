# -*- coding: utf-8 -*-
"""zone1.json 자기 검증 — 생성기와 독립적으로 JSON 만 읽어서 확인한다.

확인하는 것
  1. 길·계단 경사 (경사로 ≤ 35°, 계단 단 높이 ≤ 0.3m)
  2. 걸을 수 있는 면(차도·인도·골목·산책로·계단·패드)끼리 이어졌는지 (높이차 ≤ 0.35m 로 맞닿음, 담·난간·건물로 막힌 곳 제외)
  3. 시작 지점·체크포인트가 걸을 수 있는 면 위에 있고 모두 한 덩어리로 이어졌는지
  4. 체크포인트 사이 직선거리·고도차·경사, 길 따라 최단 거리, 걷기·달리기 시간
  5. 지형 격자가 길 높이와 맞는지, 길 밖 맨땅 경사, 건물이 길을 막는지
  6. 시선(정보용): 성곽 윗선·다음 체크포인트 표지·길잡이가 보이는지
  7. 골목 폭(2차): 체크포인트 4→6 골목 양옆이 건물·담으로 막히고 폭 2.5~4m 인지
  8. 색 명도 차(2차): 화면 L* 모형으로 넓은 면끼리 ≥ 8, 하늘↔건물 ≥ 10, 하늘↔안개 ≥ 6

실행: python tools/zone1_check.py [zone1.json 경로] [--md 출력.md]
"""
import heapq
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT = os.path.join(os.path.dirname(HERE), "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json")
argv = sys.argv[1:]
MD_OUT = None
if "--md" in argv:
    k = argv.index("--md")
    MD_OUT = argv[k + 1]
    argv = argv[:k] + argv[k + 2:]
PATH = argv[0] if argv else DEFAULT

with open(PATH, encoding="utf-8") as f:
    Z = json.load(f)

TOL = 0.3     # 수평 허용(m)
DY = 0.35     # 맞닿은 두 면 높이차 허용(m) — 컨트롤러 stepOffset 0.3 + 여유
SOLID = {"fence", "railing", "retaining", "barrier", "gate", "house", "shop", "campus", "hanok", "pavilion"}
WALK, RUN = Z["meta"]["speeds"]["walk"], Z["meta"]["speeds"]["run"]
out = []


def say(s=""):
    out.append(s)


# ---------------------------------------------------------------- 기하
def seg_proj(a, b, x, z):
    dx, dz = b[0] - a[0], b[2] - a[2]
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 == 0 else ((x - a[0]) * dx + (z - a[2]) * dz) / L2
    tc = max(0.0, min(1.0, t))
    qx, qz = a[0] + tc * dx, a[2] + tc * dz
    return math.hypot(x - qx, z - qz), a[1] + tc * (b[1] - a[1]), t


def local_axes(yaw):
    r = math.radians(yaw)
    return (math.cos(r), -math.sin(r)), (math.sin(r), math.cos(r))


def in_rect(x, z, c, w, d, yaw, m=0.0):
    ux, uz = local_axes(yaw)
    dx, dz = x - c[0], z - c[2]
    return abs(dx * ux[0] + dz * ux[1]) <= w / 2 + m and abs(dx * uz[0] + dz * uz[1]) <= d / 2 + m


# ---------------------------------------------------------------- 걸을 수 있는 면
class Feat:
    def __init__(self, name, kind, typ, **kw):
        self.name, self.kind, self.typ = name, kind, typ
        self.__dict__.update(kw)

    def surface(self, x, z, tol=TOL):
        """(x,z) 가 이 면 위면 그 높이, 아니면 None."""
        if self.typ == "ribbon":
            best = None
            for i in range(len(self.pts) - 1):
                d, y, t = seg_proj(self.pts[i], self.pts[i + 1], x, z)
                if d <= self.width / 2 + tol and (best is None or d < best[0]):
                    best = (d, y)
            return None if best is None else best[1]
        if self.typ == "stair":
            d, y, t = seg_proj(self.a, self.b, x, z)
            run = math.hypot(self.b[0] - self.a[0], self.b[2] - self.a[2])
            if d <= self.width / 2 + tol and -tol / run <= t <= 1 + tol / run:
                return y
            return None
        if self.typ == "circle":
            return self.c[1] if math.hypot(x - self.c[0], z - self.c[2]) <= self.r + tol else None
        if self.typ == "rect":
            return self.c[1] if in_rect(x, z, self.c, self.w, self.d, self.yaw, tol) else None

    def samples(self, step=1.0):
        """(x, y, z, 가운데선 여부) 표본."""
        pts = []
        if self.typ in ("ribbon", "stair"):
            seq = self.pts if self.typ == "ribbon" else [self.a, self.b]
            for i in range(len(seq) - 1):
                a, b = seq[i], seq[i + 1]
                L = math.hypot(b[0] - a[0], b[2] - a[2])
                n = max(1, int(math.ceil(L / step)))
                hx, hz = (b[0] - a[0]) / L, (b[2] - a[2]) / L
                for k in range(n + (1 if i == len(seq) - 2 else 0)):
                    t = k / n
                    x, y, z = a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), a[2] + t * (b[2] - a[2])
                    pts.append((x, y, z, True))
                    for s in (-1, 1):
                        off = self.width / 2 - 0.1
                        pts.append((x - hz * off * s, y, z + hx * off * s, False))
        elif self.typ == "circle":
            n = int(math.ceil(self.r / step))
            for i in range(-n, n + 1):
                for j in range(-n, n + 1):
                    x, z = i * step, j * step
                    if math.hypot(x, z) <= self.r - 0.1:
                        pts.append((self.c[0] + x, self.c[1], self.c[2] + z, True))
            m = max(8, int(2 * math.pi * self.r / step))
            for k in range(m):  # 가장자리 한 바퀴
                a = 2 * math.pi * k / m
                pts.append((self.c[0] + (self.r - 0.1) * math.cos(a), self.c[1], self.c[2] + (self.r - 0.1) * math.sin(a), True))
        else:
            ux, uz = local_axes(self.yaw)
            nx = max(2, int(self.w / step) + 1)
            nz = max(2, int(self.d / step) + 1)
            for i in range(nx):
                for j in range(nz):
                    lx = -self.w / 2 + 0.1 + (self.w - 0.2) * i / (nx - 1)
                    lz = -self.d / 2 + 0.1 + (self.d - 0.2) * j / (nz - 1)
                    pts.append((self.c[0] + ux[0] * lx + uz[0] * lz, self.c[1], self.c[2] + ux[1] * lx + uz[1] * lz, True))
        return pts


FEATS = []
for r in Z["roads"]:
    FEATS.append(Feat(r["name"], r["kind"], "ribbon", pts=r["points"], width=r["width"]))
for s in Z["stairs"]:
    FEATS.append(Feat(s["name"], "stairs", "stair", a=s["from"], b=s["to"], width=s["width"]))
for g in Z["ground"]:
    if g["kind"] != "pad":
        continue
    if g["shape"] == "circle":
        FEATS.append(Feat(g["name"], "pad", "circle", c=g["center"], r=g["size"][0] / 2))
    else:
        FEATS.append(Feat(g["name"], "pad", "rect", c=g["center"], w=g["size"][0], d=g["size"][1], yaw=g["yaw"]))

SOLIDS = [b for b in Z["blocks"] if b["kind"] in SOLID]


def blocked(x, z):
    for b in SOLIDS:
        if in_rect(x, z, b["center"], b["size"][0], b["size"][2], b["yaw"], 0.05):
            return b["name"]
    return None


# ---------------------------------------------------------------- 1. 경사
say("### 검증 1 — 길·계단 경사")
say("")
say("| 길 | 종류 | 폭(m) | 길이(m) | 고도차(m) | 최대 경사 | 판정 |")
say("|---|---|---:|---:|---:|---:|---|")
lim = Z["meta"]["controller"]["slopeLimitDeg"]
bad_slope = 0
for r in Z["roads"]:
    p = r["points"]
    L = sum(math.hypot(p[i + 1][0] - p[i][0], p[i + 1][2] - p[i][2]) for i in range(len(p) - 1))
    mx = 0.0
    for i in range(len(p) - 1):
        h = math.hypot(p[i + 1][0] - p[i][0], p[i + 1][2] - p[i][2])
        if h > 0:
            mx = max(mx, math.degrees(math.atan2(abs(p[i + 1][1] - p[i][1]), h)))
    ok = mx <= lim
    bad_slope += (not ok)
    say("| %s | %s | %.1f | %.1f | %+.1f | %.1f° (%.0f%%) | %s |" % (
        r["name"], r["kind"], r["width"], L, p[-1][1] - p[0][1], mx, math.tan(math.radians(mx)) * 100,
        "OK" if ok else "**초과**"))
say("")
say("| 계단 | 폭(m) | 수평(m) | 높이(m) | 단 수 | 단 높이(m) | 디딤(m) | 기울기 | 판정 |")
say("|---|---:|---:|---:|---:|---:|---:|---:|---|")
for s in Z["stairs"]:
    a, b = s["from"], s["to"]
    run = math.hypot(b[0] - a[0], b[2] - a[2])
    rise = abs(b[1] - a[1])
    riser = rise / s["steps"]
    ok = riser <= 0.3 + 1e-9 and riser <= Z["meta"]["controller"]["stairRiserMax"] + 1e-9
    bad_slope += (not ok)
    say("| %s | %.1f | %.1f | %.2f | %d | %.3f | %.2f | %.1f° | %s |" % (
        s["name"], s["width"], run, rise, s["steps"], riser, run / s["steps"],
        math.degrees(math.atan2(rise, run)), "OK" if ok else "**초과**"))
say("")

# ---------------------------------------------------------------- 2. 연결
parent = list(range(len(FEATS)))


def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i


links = {}
mismatch = []
for i, A in enumerate(FEATS):
    for (x, y, z, _) in A.samples(0.5):
        for j, B in enumerate(FEATS):
            if j == i:
                continue
            yb = B.surface(x, z)
            if yb is None:
                continue
            if abs(yb - y) <= DY:
                if blocked(x, z):
                    continue
                key = (min(i, j), max(i, j))
                links[key] = links.get(key, 0) + 1
                parent[find(i)] = find(j)
            elif abs(yb - y) <= 1.5:
                mismatch.append((A.name, B.name, round(abs(yb - y), 2)))

comps = {}
for i in range(len(FEATS)):
    comps.setdefault(find(i), []).append(FEATS[i].name)
say("### 검증 2 — 걸을 수 있는 면 연결")
say("")
say("- 면 %d개(길 %d·계단 %d·패드 %d), 맞닿은 쌍 %d개, 연결 덩어리 **%d개**" % (
    len(FEATS), len(Z["roads"]), len(Z["stairs"]), len(FEATS) - len(Z["roads"]) - len(Z["stairs"]), len(links), len(comps)))
for k, v in comps.items():
    say("  - 덩어리(%d면): %s" % (len(v), ", ".join(v)))
iso = [FEATS[i].name for i in range(len(FEATS)) if not any(i in k for k in links)]
say("- 아무 데도 안 이어진 면: %s" % (", ".join(iso) if iso else "없음"))
mm = sorted(set((a, b) for a, b, d in mismatch if (a, b)))
say("- 참고: 겹치는 자리 중 높이가 0.35~1.5m 달라 이어지지 않은 표본이 있는 쌍 %d개(계단 몸통이 위·아래 길과 겹치는 곳, 주차장과 인도 사이 난간 자리 등 — 실제 이음은 계단 끝·진입로에서 됨)" % len(mm))
say("")

# ---------------------------------------------------------------- 3. 그래프(최단 거리)
nodes = []      # (x,y,z,feat)
adj = []


def add_node(x, y, z, f):
    nodes.append((x, y, z, f))
    adj.append([])
    return len(nodes) - 1


def link(a, b):
    xa, ya, za, _ = nodes[a]
    xb, yb, zb, _ = nodes[b]
    c = math.sqrt((xa - xb) ** 2 + (ya - yb) ** 2 + (za - zb) ** 2)
    adj[a].append((b, c))
    adj[b].append((a, c))


feat_nodes = []
for fi, F in enumerate(FEATS):
    ids = []
    if F.typ in ("ribbon", "stair"):
        S = F.samples(1.0)
        last_c = None
        for (x, y, z, cen) in S:
            n = add_node(x, y, z, fi)
            ids.append(n)
            if cen:
                if last_c is not None:
                    link(last_c, n)
                last_c = n
            else:
                link(last_c, n)
    else:
        S = F.samples(1.5)
        for (x, y, z, _) in S:
            ids.append(add_node(x, y, z, fi))
        for a in ids:
            for b in ids:
                if a < b and math.hypot(nodes[a][0] - nodes[b][0], nodes[a][2] - nodes[b][2]) <= 2.3:
                    link(a, b)
    feat_nodes.append(ids)

# 면 사이 연결: 한 면의 표본이 다른 면 위(높이차 ≤ DY, 막힘 없음)면 그 면의 가장 가까운 표본과 잇는다
for fi, ids in enumerate(feat_nodes):
    for n in ids:
        x, y, z, _ = nodes[n]
        if blocked(x, z):
            continue
        for fj, F in enumerate(FEATS):
            if fj == fi:
                continue
            yb = F.surface(x, z)
            if yb is None or abs(yb - y) > DY:
                continue
            best = min(feat_nodes[fj], key=lambda m: (nodes[m][0] - x) ** 2 + (nodes[m][2] - z) ** 2)
            link(n, best)


def on_walk(p, tol=0.5):
    res = []
    for fi, F in enumerate(FEATS):
        y = F.surface(p[0], p[2], TOL)
        if y is not None and abs(y - p[1]) <= tol:
            res.append((fi, y))
    return res


def attach(p):
    hits = on_walk(p)
    if not hits:
        return None, []
    n = add_node(p[0], p[1], p[2], -1)
    for fi, _ in hits:
        cand = sorted(feat_nodes[fi], key=lambda m: (nodes[m][0] - p[0]) ** 2 + (nodes[m][2] - p[2]) ** 2)[:3]
        for m in cand:
            link(n, m)
    return n, [FEATS[fi].name for fi, _ in hits]


def dijkstra(s, t):
    dist = {s: 0.0}
    prev = {}
    pq = [(0.0, s)]
    while pq:
        d, u = heapq.heappop(pq)
        if u == t:
            break
        if d > dist.get(u, 1e18):
            continue
        for v, c in adj[u]:
            nd = d + c
            if nd < dist.get(v, 1e18):
                dist[v] = nd
                prev[v] = u
                heapq.heappush(pq, (nd, v))
    if t not in dist:
        return None, []
    path = [t]
    while path[-1] != s:
        path.append(prev[path[-1]])
    return dist[t], path[::-1]


say("### 검증 3 — 시작 지점·체크포인트가 길 위에 있는지")
say("")
say("| # | 체크포인트 | 좌표 (x, y, z) | 반경 | 올라선 면 |")
say("|---:|---|---|---:|---|")
sp_node, sp_on = attach(Z["spawn"]["pos"])
route_nodes = []
off_walk = 0
for i, r in enumerate(Z["route"]):
    n, on = attach(r["pos"])
    route_nodes.append(n)
    off_walk += (n is None)
    say("| %d | %s | (%.1f, %.1f, %.1f) | %.1f | %s |" % (
        i + 1, r["name"], r["pos"][0], r["pos"][1], r["pos"][2], r["radius"], ", ".join(on) if on else "**길 밖**"))
say("")
say("- 시작 지점 (%.1f, %.1f, %.1f) yaw %.0f° → %s" % (
    *Z["spawn"]["pos"], Z["spawn"]["yaw"], ", ".join(sp_on) if sp_on else "**길 밖**"))
say("")

say("### 검증 4 — 체크포인트 구간 계산표")
say("")
say("직선 = 두 점 사이 수평 거리. 길 따라 = 걸을 수 있는 면만 지나는 최단 경로(계단 포함). 시간 = 길 따라 거리 ÷ 걷기 %.1fm/s · 달리기 %.1fm/s." % (WALK, RUN))
say("")
say("| 구간 | 직선(m) | 고도차(m) | 평균 경사 | 길 따라(m) | 경사로 최대(계단 제외) | 계단 | 걷기 | 달리기 | 지나는 면 |")
say("|---|---:|---:|---:|---:|---:|---|---:|---:|---|")
tot_d = tot_up = tot_down = 0.0
broken = 0
for i in range(len(Z["route"]) - 1):
    a, b = Z["route"][i]["pos"], Z["route"][i + 1]["pos"]
    h = math.hypot(b[0] - a[0], b[2] - a[2])
    dy = b[1] - a[1]
    d, path = (None, [])
    if route_nodes[i] is not None and route_nodes[i + 1] is not None:
        d, path = dijkstra(route_nodes[i], route_nodes[i + 1])
    if d is None:
        broken += 1
        say("| %d→%d | %.1f | %+.1f | %.1f° | **끊김** | — | — | — | — | — |" % (i + 1, i + 2, h, dy, math.degrees(math.atan2(abs(dy), h))))
        continue
    mx = 0.0
    used = []
    stairs_used = []
    up = down = 0.0
    for k in range(len(path) - 1):
        p, q = nodes[path[k]], nodes[path[k + 1]]
        hh = math.hypot(q[0] - p[0], q[2] - p[2])
        dd = q[1] - p[1]
        if dd > 0:
            up += dd
        else:
            down -= dd
        f = q[3]
        # 같은 면 안의 경사만(면 사이 연석·계단 끝 이음은 단차라 뺌), 계단은 따로 적음
        if f >= 0 and f == p[3] and hh > 0.5:
            if FEATS[f].typ == "stair":
                if FEATS[f].name not in stairs_used:
                    stairs_used.append(FEATS[f].name)
            else:
                mx = max(mx, math.degrees(math.atan2(abs(dd), hh)))
        if f >= 0 and (not used or used[-1] != FEATS[f].name):
            used.append(FEATS[f].name)
    tot_d += d
    tot_up += up
    tot_down += down
    say("| %d→%d | %.1f | %+.1f | %.1f° | %.1f | %.1f° | %s | %.0f초 | %.0f초 | %s |" % (
        i + 1, i + 2, h, dy, math.degrees(math.atan2(abs(dy), h)), d, mx, ", ".join(stairs_used) or "—",
        d / WALK, d / RUN, " → ".join(used)))
say("| **합계** |  | %+.1f |  | **%.1f** | 오르막 누적 %.1f m |  | **%.0f초 (%.1f분)** | **%.0f초** |  |" % (
    Z["route"][-1]["pos"][1] - Z["route"][0]["pos"][1], tot_d, tot_up, tot_d / WALK, tot_d / WALK / 60, tot_d / RUN))
say("")
say("- 끊긴 구간: %s" % ("없음" if broken == 0 else "**%d곳**" % broken))
# 시작 → 종점 직행(지름길 포함)
if route_nodes[0] is not None and route_nodes[-1] is not None:
    d, _ = dijkstra(route_nodes[0], route_nodes[-1])
    if d is None:
        broken += 1
        say("- 성대 후문 → 전망 쉼터: **끊김**")
    else:
        say("- 성대 후문 → 전망 쉼터 최단(체크포인트 무시): %.1f m — 걷기 %.0f초, 달리기 %.0f초" % (d, d / WALK, d / RUN))
if route_nodes[0] is not None and route_nodes[6] is not None:
    d, _ = dijkstra(route_nodes[0], route_nodes[6])
    if d is None:
        broken += 1
        say("- 성대 후문 → 와룡공원 공터: **끊김**")
    else:
        say("- 성대 후문 → 와룡공원 공터 최단(공원 계단): %.1f m — 걷기 %.0f초, 달리기 %.0f초" % (d, d / WALK, d / RUN))
say("")

# ---------------------------------------------------------------- 5. 지형·건물
G = [g for g in Z["ground"] if g["kind"] == "heightgrid"][0]
x0, z0 = G["origin"]
cell, nxg, nzg = G["cell"], G["nx"], G["nz"]
Hm = G["heights"]
assert len(Hm) == nzg and all(len(r) == nxg for r in Hm), "높이 격자 크기 불일치"


def gy(x, z):
    fi, fj = (x - x0) / cell, (z - z0) / cell
    i = int(max(0, min(nxg - 2, math.floor(fi))))
    j = int(max(0, min(nzg - 2, math.floor(fj))))
    tx, tz = fi - i, fj - j
    return (Hm[j][i] * (1 - tx) * (1 - tz) + Hm[j][i + 1] * tx * (1 - tz) + Hm[j + 1][i] * (1 - tx) * tz
            + Hm[j + 1][i + 1] * tx * tz)


diffs = []
for F in FEATS:
    for (x, y, z, cen) in F.samples(1.0):
        if cen:
            diffs.append((abs(gy(x, z) - y), F.name))
diffs.sort(reverse=True)
say("### 검증 5 — 지형 격자·건물")
say("")
say("- 높이 격자 %d×%d (칸 %.0fm), 높이 범위 %.1f ~ %.1f m" % (
    nxg, nzg, cell, min(min(r) for r in Hm), max(max(r) for r in Hm)))
say("- 길 가운데선 아래 지형과 길 면의 높이차: 평균 %.2f m, 최대 %.2f m (%s) — 길 리본을 1m 두께로 아래로 내려 그리면 틈이 안 생김" % (
    sum(d for d, _ in diffs) / len(diffs), diffs[0][0], diffs[0][1]))


def under_walk(x, z, m):
    for F in FEATS:
        if F.surface(x, z, m) is not None:
            return True
    return False


def under_block(x, z):
    for b in Z["blocks"]:
        if in_rect(x, z, b["center"], b["size"][0], b["size"][2], b["yaw"], 0.5):
            return True
    return False


steep = []
for j in range(nzg - 1):
    for i in range(nxg - 1):
        x, z = x0 + (i + 0.5) * cell, z0 + (j + 0.5) * cell
        gx_ = ((Hm[j][i + 1] - Hm[j][i]) + (Hm[j + 1][i + 1] - Hm[j + 1][i])) / (2 * cell)
        gz_ = ((Hm[j + 1][i] - Hm[j][i]) + (Hm[j + 1][i + 1] - Hm[j][i + 1])) / (2 * cell)
        ang = math.degrees(math.atan(math.hypot(gx_, gz_)))
        if ang <= 25:
            continue
        # 성벽 북쪽(구역 밖)은 제외
        wall = Z["walls"][0]["points"]
        best = None
        for k in range(len(wall) - 1):
            d, _, t = seg_proj(wall[k], wall[k + 1], x, z)
            if best is None or d < best[0]:
                a, b = wall[k], wall[k + 1]
                best = (d, (b[0] - a[0]) * (z - a[2]) - (b[2] - a[2]) * (x - a[0]))
        if best[1] > 0:
            continue
        if under_walk(x, z, 0.8) or under_block(x, z):
            continue
        steep.append((ang, x, z))
steep.sort(reverse=True)
n30 = sum(1 for a, _, _ in steep if a > 30)
n35 = sum(1 for a, _, _ in steep if a > 35)
say("- 길·건물 밖 맨땅(성 안쪽) 경사: 25° 넘는 칸 %d개, 30° 넘는 칸 %d개, **35° 넘는 칸 %d개**%s" % (
    len(steep), n30, n35, (" — 가장 가파른 곳: " + ", ".join("%.1f° @ (%.0f, %.0f)" % s for s in steep[:4])) if steep else ""))

# 건물이 걸을 수 있는 면을 막는지(차단물·소품은 의도된 것이라 따로)
hit = {}
for b in Z["blocks"]:
    if b["kind"] in ("barrier", "prop", "tree", "bush"):
        continue
    ux, uz = local_axes(b["yaw"])
    w, d = b["size"][0], b["size"][2]
    for i in range(int(w / 0.5) + 1):
        for j in range(int(d / 0.5) + 1):
            lx, lz = -w / 2 + min(w, i * 0.5), -d / 2 + min(d, j * 0.5)
            x = b["center"][0] + ux[0] * lx + uz[0] * lz
            z = b["center"][2] + ux[1] * lx + uz[1] * lz
            for F in FEATS:
                if F.surface(x, z, -0.05) is not None:
                    hit.setdefault((b["name"], F.name), 0)
                    hit[(b["name"], F.name)] += 1
say("- 건물·담·난간이 길/계단/패드 안으로 들어간 곳: %s" % (
    "없음" if not hit else "; ".join("%s↔%s(%d점)" % (a, f, n) for (a, f), n in sorted(hit.items()))))
# 성벽과 걷는 면 사이 간격(성벽에 길이 붙거나 파고들면 안 됨)
WP = Z["walls"][0]["points"]
WT = Z["walls"][0]["thickness"]


def wall_dist(x, z):
    return min(seg_proj(WP[k], WP[k + 1], x, z)[0] for k in range(len(WP) - 1))


clear = []
for F in FEATS:
    m = 1e9
    for (x, y, z, cen) in F.samples(0.5):
        if F.typ in ("ribbon", "stair") and cen:
            m = min(m, wall_dist(x, z) - F.width / 2 - WT / 2)
        elif F.typ in ("rect", "circle"):
            m = min(m, wall_dist(x, z) - WT / 2)
    clear.append((m, F.name))
clear.sort()
wall_hit = [(m, n) for m, n in clear if m < 1.0]
say("- 성벽 안쪽 면과 가장 가까운 걷는 면: %s — 1m 안으로 붙은 면 %s" % (
    ", ".join("%s %.1fm" % (n, m) for m, n in clear[:3]), ("**%d개**" % len(wall_hit)) if wall_hit else "없음"))
lm_bad = []
for L in Z["landmarks"]:
    if L["kind"] not in ("pole", "lamp", "sign", "board", "bus_stop"):
        continue
    x, z = L["pos"][0], L["pos"][2]
    inb = blocked(x, z) or ("성벽" if wall_dist(x, z) < WT / 2 + 0.2 else None)
    onw = [F.name for F in FEATS if F.surface(x, z, -0.5) is not None]  # 가장자리 0.5m 안쪽은 허용(연석 쪽 기둥)
    if inb or onw:
        lm_bad.append("%s(%s)" % (L["name"], inb or ",".join(onw)))
say("- 길 한가운데·건물 안에 선 기둥·가로등·표지: %s" % (", ".join(lm_bad) if lm_bad else "없음"))
trees_on = 0
for b in Z["blocks"]:
    if b["kind"] in ("tree", "bush") and on_walk((b["center"][0], 0, b["center"][2]), 1e9):
        trees_on += 1
say("- 길 위에 놓인 나무·풀숲: %d개" % trees_on)
bmin, bmax = Z["bounds"]["min"], Z["bounds"]["max"]
outside = [r["name"] for r in Z["route"] if not (bmin[0] <= r["pos"][0] <= bmax[0] and bmin[2] <= r["pos"][2] <= bmax[2])]
say("- 경계 밖 체크포인트: %s" % (", ".join(outside) if outside else "없음"))
kinds = {}
for b in Z["blocks"]:
    kinds[b["kind"]] = kinds.get(b["kind"], 0) + 1
say("- 블록 %d개: %s" % (len(Z["blocks"]), ", ".join("%s %d" % kv for kv in sorted(kinds.items()))))
say("")

# ---------------------------------------------------------------- 6. 시선(정보용)
BOXES = []
for b in Z["blocks"]:
    ux, uz = local_axes(b["yaw"])
    BOXES.append((b, ux, uz))


def in_box(x, y, z):
    for b, ux, uz in BOXES:
        cx, cy, cz = b["center"]
        w, h, d = b["size"]
        if abs(y - cy) > h / 2:
            continue
        dx, dz = x - cx, z - cz
        if abs(dx * ux[0] + dz * ux[1]) <= w / 2 and abs(dx * uz[0] + dz * uz[1]) <= d / 2:
            return b["name"]
    return None


def sight(a, b):
    L = math.dist(a, b)
    n = int(L / 0.4)
    for k in range(2, n - 1):
        t = k / n
        p = [a[i] + t * (b[i] - a[i]) for i in range(3)]
        if p[1] < gy(p[0], p[2]) - 0.05:
            return "지형"
        h = in_box(*p)
        if h:
            return h
    return None


CAM_H, BEACON_H = 2.2, 3.0
W0 = Z["walls"][0]
wtop = []
for i in range(len(W0["points"]) - 1):
    a, b = W0["points"][i], W0["points"][i + 1]
    L = math.hypot(b[0] - a[0], b[2] - a[2])
    n = max(1, int(L / 4))
    for k in range(n):
        t = k / n
        wtop.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]) + W0["height"] - 0.2, a[2] + t * (b[2] - a[2])))
named = {L["name"]: L for L in Z["landmarks"]}
LOOK = ["공터 가로등(서)", "공터 가로등(동)", "하루편의점 간판"]
pav = [b for b in Z["blocks"] if b["kind"] == "pavilion"]
say("### 검증 6 — 시선(정보용, 합격 기준 아님)")
say("")
say("카메라 높이 = 서 있는 면 + %.1fm. 성곽 윗선 표본 %d점 중 가리지 않고 보이는 수, 다음 체크포인트 표지(높이 %.0fm 기둥) 보임 여부. 나무·풀숲 상자도 가림으로 셈." % (CAM_H, len(wtop), BEACON_H))
say("")
say("| # | 체크포인트 | 성곽 윗선 보임 | 다음 체크포인트 표지 | 보이는 길잡이 |")
say("|---:|---|---:|---|---|")
for i, r in enumerate(Z["route"]):
    e = (r["pos"][0], r["pos"][1] + CAM_H, r["pos"][2])
    nv = sum(1 for w in wtop if sight(e, w) is None)
    nxt = "—"
    if i + 1 < len(Z["route"]):
        q = Z["route"][i + 1]["pos"]
        why = sight(e, (q[0], q[1] + BEACON_H, q[2]))
        nxt = "보임" if why is None else "가림(%s)" % why
    seen = []
    for nm in LOOK:
        L = named[nm]
        t = (L["pos"][0], L["pos"][1] + L.get("height", 3.0), L["pos"][2])
        if math.dist(e, t) > 1.0 and sight(e, t) is None:
            seen.append(nm)
    if pav:
        pb = pav[0]
        t = (pb["center"][0], pb["center"][1] + pb["size"][1] / 2 + 0.3, pb["center"][2])
        if sight(e, t) is None:
            seen.append("전망 정자")
    say("| %d | %s | %d/%d | %s | %s |" % (i + 1, r["name"], nv, len(wtop), nxt, ", ".join(seen) or "—"))
say("")

# ---------------------------------------------------------------- 7. 골목 폭(체크포인트 3→6 골목이 '골목'으로 읽히는지)
ALLEY_NAMES = ["명륜3가 골목 입구", "명륜3가 골목(시우네 골목)", "성곽 아래 꼭대기 계단(시우네 옆)"]
ALLEY_MIN, ALLEY_MAX, ALLEY_SHARE = 2.5, 4.0, 0.6
say("### 검증 7 — 골목 폭(체크포인트 4 → 5 → 6)")
say("")
say("골목 가운데선을 0.5m 마다 잘라 양옆(수직)으로 건물·담·축대·난간까지 거리를 잰다(6m 안에 없으면 '트임'). "
    "합격 기준: 양옆이 막히고 폭이 %.1f~%.1fm 인 표본('골목') %d%% 이상, 양옆이 막힌 곳은 어디도 %.1fm 보다 좁지 않음. "
    "옆으로 1m 넘게 나가서 다른 걷는 면(갈림길·계단참)을 먼저 만나는 표본은 뺀다." % (ALLEY_MIN, ALLEY_MAX, ALLEY_SHARE * 100, ALLEY_MIN))
say("")
say("| 길 | 표본(갈림길 뺌) | 골목(폭 %.1f~%.1fm) | 양옆 막힘(전체) | 한쪽만 | 트임 | 갈림길 | 막힌 곳 폭 최소·중앙·최대(m) | 판정 |" % (ALLEY_MIN, ALLEY_MAX))
say("|---|---:|---:|---:|---:|---:|---:|---|---|")


def side_hit(x, z, nx, nz, own, lim=6.0):
    """수직으로 걸어 나가며 처음 닿는 것: 건물·담이면 거리, 다른 걷는 면(갈림길)이면 'branch', 6m 안에 없으면 None."""
    d = 0.05
    while d <= lim:
        px, pz = x + nx * d, z + nz * d
        if blocked(px, pz):
            return d
        if d > 1.0 and any(G is not own and G.surface(px, pz, 0.0) is not None for G in FEATS):
            return "branch"
        d += 0.05
    return None


alley_bad = 0
for F in FEATS:
    if F.name not in ALLEY_NAMES:
        continue
    seq = F.pts if F.typ == "ribbon" else [F.a, F.b]
    both = one = none = branch = 0
    widths = []
    for i in range(len(seq) - 1):
        a, b = seq[i], seq[i + 1]
        L = math.hypot(b[0] - a[0], b[2] - a[2])
        hx, hz = (b[0] - a[0]) / L, (b[2] - a[2]) / L
        n = max(1, int(L / 0.5))
        for k in range(n):
            t = (k + 0.5) / n
            x, z = a[0] + t * (b[0] - a[0]), a[2] + t * (b[2] - a[2])
            dl, dr = side_hit(x, z, -hz, hx, F), side_hit(x, z, hz, -hx, F)
            if "branch" in (dl, dr):
                branch += 1
            elif dl is not None and dr is not None:
                both += 1
                widths.append(dl + dr)
            elif dl is not None or dr is not None:
                one += 1
            else:
                none += 1
    tot = both + one + none
    widths.sort()
    wtxt = "%.1f · %.1f · %.1f" % (widths[0], widths[len(widths) // 2], widths[-1]) if widths else "—"
    lane = sum(1 for w in widths if ALLEY_MIN - 1e-6 <= w <= ALLEY_MAX + 1e-6)
    good = tot > 0 and lane / tot >= ALLEY_SHARE and (not widths or widths[0] >= ALLEY_MIN - 1e-6)
    alley_bad += (not good)
    say("| %s | %d | %d (%.0f%%) | %d | %d | %d | %d | %s | %s |" % (F.name, tot, lane, 100 * lane / max(1, tot), both, one, none, branch,
                                                                      wtxt, "OK" if good else "**확인**"))
say("")

# ---------------------------------------------------------------- 8. 색 명도 차(넓은 면끼리·하늘과 건물이 섞이지 않는지)
# 화면 L* 모형 — Zone1Builder 의 빛·후처리와 같은 값(바꾸면 여기도):
#   해 = 오일러 (48°, 335°) 흰빛 #FFF8EE ×1 · 그림자 세기 .7 · 주변광 평면 (0.62, 0.60, 0.57) · URP Lit 확산 0.96(스무스니스 0)
#   후처리 = 대비 −6(로그 공간에서 중간 회색 0.18 기준 ×0.94) · 채도 −15·스플릿 톤은 명도에 거의 영향 없어 뺌 · 톤매핑 없음(1 넘으면 잘림)
#   생성 텍스처 평균 밝기: 성곽 돌 줄눈 0.88 · 판석(성곽길·계단참) 0.90
# 하늘(카메라 배경)은 빛을 안 받고 후처리만. 2026-10-05 배치 촬영 화소 15곳으로 맞춰 봄: 모형과 화면 차 평균 1.3, 최대 3.8 L*(06 문서 10장).
def _lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _Y(h):
    r, g, b = (int(h[i:i + 2], 16) for i in (1, 3, 5))
    return 0.2126 * _lin(r) + 0.7152 * _lin(g) + 0.0722 * _lin(b)


def _L(y):
    y = min(1.0, max(0.0, y))
    f = y ** (1.0 / 3.0) if y > 0.008856 else 7.787 * y + 16.0 / 116.0
    return 116.0 * f - 16.0


_sp, _sy = math.radians(48.0), math.radians(335.0)
SUN = (-math.cos(_sp) * math.sin(_sy), math.sin(_sp), -math.cos(_sp) * math.cos(_sy))     # 빛이 오는 쪽(단위 벡터)
SUN_Y = 0.2126 * _lin(255) + 0.7152 * _lin(0.973 * 255) + 0.0722 * _lin(0.933 * 255)
AMB_Y = 0.2126 * _lin(0.62 * 255) + 0.7152 * _lin(0.60 * 255) + 0.0722 * _lin(0.57 * 255)
FACES = {"윗면": (0, 1, 0), "남쪽 면": (0, 0, -1), "동쪽 면": (1, 0, 0), "그늘 면": (0, 0, 1)}
TEX = {"wall": 0.88, "wallpath": 0.90, "pad_stone": 0.90}
COL = Z["colors"]


def screen_L(key, face="윗면", shadow=False):
    n = FACES[face]
    nd = max(0.0, sum(a * b for a, b in zip(n, SUN)))
    light = 0.96 * (nd * SUN_Y * (0.3 if shadow else 1.0) + AMB_Y)
    y = _Y(COL[key]) * TEX.get(key, 1.0) * light
    return _L(0.18 * (y / 0.18) ** 0.94)


SKY_L = _L(0.18 * (_Y(COL["sky"]) / 0.18) ** 0.94)
FOG_L = _L(0.18 * (_Y(COL["fog"]) / 0.18) ** 0.94)
NEIGHBOR, SKY_GAP, FOG_GAP = 8.0, 10.0, 6.0
say("### 검증 8 — 색 명도 차(넓은 면·하늘)")
say("")
say("화면 L*(0 검정 ~ 100 흰색) = 종류 색 × 그 면이 받는 빛(해 48° 남남동 + 주변광) → 필름 후처리(대비 −6). "
    "기준: ① 화면에서 맞붙는 넓은 면끼리 **%.0f 이상** ② 하늘과 건물·성곽의 가장 밝은 면(윗면·햇빛 받는 남쪽 면) **%.0f 이상** "
    "③ 하늘과 안개 **%.0f 이상**(먼 건물·산이 안개 색으로 녹아도 하늘과 갈림)." % (NEIGHBOR, SKY_GAP, FOG_GAP))
say("")
say("| 종류 | 색 | 색 자체 L* | 윗면 | 윗면(그림자) | 남쪽 면(햇빛) | 동쪽 면 | 그늘 면 |")
say("|---|---|---:|---:|---:|---:|---:|---:|")
for k in ["sidewalk", "alley", "wallpath", "shop", "wall", "campus", "house", "pad_dirt", "retaining", "ground", "trail",
          "path", "stairs", "road", "fence", "wallcap", "hanok"]:
    say("| %s | `%s` | %.1f | %.1f | %.1f | %.1f | %.1f | %.1f |" % (
        k, COL[k], _L(_Y(COL[k])), screen_L(k), screen_L(k, "윗면", True), screen_L(k, "남쪽 면"), screen_L(k, "동쪽 면"), screen_L(k, "그늘 면")))
say("| 하늘(빛 없음) | `%s` | %.1f | 화면 %.1f | | | | |" % (COL["sky"], _L(_Y(COL["sky"])), SKY_L))
say("| 안개(빛 없음) | `%s` | %.1f | 화면 %.1f | | | | |" % (COL["fog"], _L(_Y(COL["fog"])), FOG_L))
say("")
PAIRS = [  # (설명, (종류, 면), (종류, 면), 기준)
    ("인도 ↔ 차도", ("sidewalk", "윗면"), ("road", "윗면"), NEIGHBOR),
    ("인도 ↔ 상가 정면(햇빛)", ("sidewalk", "윗면"), ("shop", "남쪽 면"), NEIGHBOR),
    ("땅 ↔ 골목 바닥", ("ground", "윗면"), ("alley", "윗면"), NEIGHBOR),
    ("땅 ↔ 공원 흙길", ("ground", "윗면"), ("path", "윗면"), NEIGHBOR),
    ("땅 ↔ 숲길", ("ground", "윗면"), ("trail", "윗면"), NEIGHBOR),
    ("땅 ↔ 성곽길 돌 포장", ("ground", "윗면"), ("wallpath", "윗면"), NEIGHBOR),
    ("땅 ↔ 흙 공터·마당", ("ground", "윗면"), ("pad_dirt", "윗면"), NEIGHBOR),
    ("땅 ↔ 상가 벽(햇빛)", ("ground", "윗면"), ("shop", "남쪽 면"), NEIGHBOR),
    ("땅 ↔ 주택 벽(햇빛)", ("ground", "윗면"), ("house", "남쪽 면"), NEIGHBOR),
    ("골목 바닥 ↔ 주택 벽(햇빛)", ("alley", "윗면"), ("house", "남쪽 면"), NEIGHBOR),
    ("골목 바닥 ↔ 담장(햇빛)", ("alley", "윗면"), ("fence", "남쪽 면"), NEIGHBOR),
    ("골목 바닥 ↔ 계단", ("alley", "윗면"), ("stairs", "윗면"), NEIGHBOR),
    ("성곽(안쪽 = 남쪽 면) ↔ 성곽길", ("wall", "남쪽 면"), ("wallpath", "윗면"), NEIGHBOR),
    ("성곽(남쪽 면) ↔ 땅", ("wall", "남쪽 면"), ("ground", "윗면"), NEIGHBOR),
    ("하늘 ↔ 상가 윗면", ("sky", None), ("shop", "윗면"), SKY_GAP),
    ("하늘 ↔ 상가 정면(햇빛)", ("sky", None), ("shop", "남쪽 면"), SKY_GAP),
    ("하늘 ↔ 주택 윗면", ("sky", None), ("house", "윗면"), SKY_GAP),
    ("하늘 ↔ 캠퍼스 윗면", ("sky", None), ("campus", "윗면"), SKY_GAP),
    ("하늘 ↔ 성곽 안쪽 면(햇빛)", ("sky", None), ("wall", "남쪽 면"), SKY_GAP),
    ("하늘 ↔ 옹벽 윗면", ("sky", None), ("retaining", "윗면"), SKY_GAP),
    ("하늘 ↔ 안개", ("sky", None), ("fog", None), FOG_GAP),
]
say("| 붙는 두 면 | 화면 L* | 화면 L* | 차 | 기준 | 판정 |")
say("|---|---:|---:|---:|---:|---|")
color_bad = 0


def _pl(kf):
    k, f = kf
    if k == "sky":
        return SKY_L
    if k == "fog":
        return FOG_L
    return screen_L(k, f)


for name, a, b, need in PAIRS:
    la, lb = _pl(a), _pl(b)
    good = abs(la - lb) >= need
    color_bad += (not good)
    say("| %s | %.1f | %.1f | %.1f | ≥ %.0f | %s |" % (name, la, lb, abs(la - lb), need, "OK" if good else "**확인**"))
say("")

ok = (bad_slope == 0 and len(comps) == 1 and off_walk == 0 and broken == 0 and n35 == 0 and not hit and sp_on
      and not wall_hit and not lm_bad and alley_bad == 0 and color_bad == 0)
say("**종합: %s**" % ("통과 — 경사·계단 기준 충족, 걸을 수 있는 면 한 덩어리, 체크포인트 전 구간 길로 이어짐" if ok else "확인 필요"))

text = "\n".join(out)
if MD_OUT:
    with open(MD_OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text + "\n")
sys.stdout.reconfigure(encoding="utf-8")
print(text)
sys.exit(0 if ok else 1)
