# -*- coding: utf-8 -*-
"""4차 패스(2026-10-24): 벽면 밀도 — 스타일 프레임처럼 모든 벽에 가스관·실외기·계량기·보일러 연통·빨래·화분·인입선 받는 자리.

생성기 본체(btypes)가 면마다 낸 구멍(emit_openings → o.plog)·간판(o.signs)·소품 자리(o.sockets)·외부 계단(o.stairs)을
'막힌 칸'으로 보고, 비어 있는 곳에만 규칙으로 더 놓는다. 본체의 난수 흐름은 건드리지 않는다(rng(b.key, "dense") 따로).

밀도 규칙(면 = 앞·옆·뒤, 옆집에 붙은 면 제외):
  가스관   세로관 1개 / 6.5m(앞·옆면, 뒷면은 1/9m·60%) — 땅에서 올라와 계량기(소품) → 층마다 가로 가지(슬래브 띠 +0.42)가
           가까운 창 쪽으로 가다 위로 꺾여(U) 벽에 들어감. 1층이 상가 유리면 상가 위에서 벽을 뚫고 나와 시작(L).
  실외기   2층 이상 창 옆(앞 35%·옆 50%·뒤 60%), 받침 앵글은 키트. 뒷면·옆면 1층 창 아래 땅에 20%(걷는 면 밖일 때만)
  연통     2층 이상 창 옆 보일러 연통(회색 관이 벽에서 나와 위로 꺾임) 25%
  빨래     방범창 앞 빨랫줄 + 빨래 2~4장(2층 이상 18%), 발코니 안 빨랫줄 45%
  방범창   3층 이상 창 중 본체가 안 단 것 40% 더(흰 세로살)
  출입구   1층 문 둘레 0.35~2.2m 에 화분·스티로폼 텃밭·대야를 칸마다 65%, 문 옆 전기 계량기(없으면) 70%
  벽 밑    문이 없는 앞·옆면 1층 벽 밑 화분 줄 20%(걷는 면 가운데는 건드리지 않음 — 벽에서 0.45m 안)
  인입선   앞·옆면 반대쪽 끝에도 받는 자리(전봇대 → 집 인입선이 늘어남, build._poles_and_wires)
"""
import math

from .geom import rng, madd, UP, pipe, obox, norm, sub
from .facade import pbox, _bars
from . import details as D

FLOOR = 3.0
GW = 0.05            # 벽에서 가스관 가운데까지(실외기 몸체 0.05 뒤 — 가로 가지가 실외기 뒤로 지나감)
FLUE_W = 0.1
DENSE_TYPES = ("villa", "shop", "house")
POTS = ("styrofoam_planter", "clay_pots_cluster", "potted_plant_a", "basin_planter_onion", "potted_plant_b", "styrofoam_planter")
SIZE = dict(D.PROP_SIZE)
SIZE.update({"potted_plant_a": (0.572, 0.285, 1.0), "potted_plant_b": (0.521, 0.269, 0.5), "basin_planter_onion": (0.706, 0.706, 0.55),
             "cardboard_boxes": (0.658, 0.594, 0.8), "plastic_crates_stack": (0.45, 0.399, 0.858)})
FABRICS = ("M_Fabric", "M_Fabric_White", "M_Fabric_Red", "M_Fabric_White")
# 구멍 종류별 막는 여유(가로, 아래, 위)
PAD = {"win": (0.12, 0.16, 0.12), "small": (0.1, 0.1, 0.1), "stair": (0.1, 0.1, 0.1), "base": (0.1, 0.1, 0.1),
       "balc": (0.42, 0.4, 0.15), "door": (0.22, 0.05, 0.8), "gate": (0.22, 0.05, 0.8), "shop": (0.12, 0.05, 1.15),
       "hanji": (0.1, 0.1, 0.1), "vent": (0.06, 0.06, 0.06)}


class Occ:
    """면 좌표(u = 오른쪽, y = 높이) 위의 막힌 사각형들."""

    def __init__(self):
        self.r = []
        self.soft = []   # 실외기: 가로 가스관은 그 뒤(벽 바로 앞)로 지나갈 수 있음

    def add(self, u0, u1, y0, y1, soft=False):
        (self.soft if soft else self.r).append((min(u0, u1), max(u0, u1), min(y0, y1), max(y0, y1)))

    def free(self, u0, u1, y0, y1, behind=False):
        u0, u1, y0, y1 = min(u0, u1), max(u0, u1), min(y0, y1), max(y0, y1)
        for a, b, c, d in (self.r if behind else self.r + self.soft):
            if u0 < b and u1 > a and y0 < d and y1 > c:
                return False
        return True


def uw(P, p):
    dx, dz = p[0] - P.O[0], p[2] - P.O[1]
    return dx * P.t[0] + dz * P.t[1], dx * P.n[0] + dz * P.n[1]


def occupancy(o, P, ops, meta):
    occ = Occ()
    for op in ops:
        pu, pd, pt = PAD.get(op.kind, (0.1, 0.1, 0.1))
        occ.add(op.u0 - pu, op.u1 + pu, op.y0 - pd, op.y1 + pt)
    for s in o.signs:
        pts = [q for f in s["faces"] for q in f]
        uws = [uw(P, q) for q in pts]
        if min(abs(w) for _, w in uws) < 1.3:
            us = [u for u, _ in uws]
            ys = [q[1] for q in pts]
            occ.add(min(us) - 0.18, max(us) + 0.18, min(ys) - 0.2, max(ys) + 0.2)
    for s in o.sockets:
        u, w = uw(P, s["pos"])
        if not (-0.25 < w < 1.3 and -0.5 < u < P.W + 0.5):
            continue
        y = s["pos"][1]
        if s["kind"] == "prop":
            W_, _, H = SIZE.get(s["prop"], (0.6, 0.5, 0.6))
            occ.add(u - W_ / 2 - 0.08, u + W_ / 2 + 0.08, y - 0.4, y + H + 0.08, soft=s["prop"] == "ac_outdoor_unit")
        else:
            occ.add(u - 0.18, u + 0.18, y - 0.18, y + 0.18)
    for pid, a, b in getattr(o, "stairs", []):
        if pid == id(P):
            occ.add(a - 0.3, b + 0.3, -1e3, 1e3)
    if meta.get("variant") == "piloti" and P.role == "front":
        occ.add(0.0, P.W, -1e3, meta.get("y0", P.bot) + 2.95)
    # 빗물관(본체가 0.12 / W−0.12 에 둘 수 있음)
    occ.add(-1.0, 0.3, -1e3, 1e3)
    occ.add(P.W - 0.3, P.W + 1.0, -1e3, 1e3)
    return occ


def _put(o, name, P, u, y, gap=0.0):
    d = SIZE[name][1]
    D.add_prop(o, name, P.p(u, y, gap + d / 2), P.out, note="dense")


def _fabric(det, P, a, b, y, n, r, w=0.14, o=None):
    """방범창·발코니 앞 빨랫줄(키트) + 빨래 n 장. 5차: 빨래는 '천 자리'(cloth 소켓)로 넘겨 Unity 가 ToonCloth(바람에 흔들림)로 만든다."""
    A, B = P.p(a, y, w), P.p(b, y, w)
    pipe(det, [A, B], 0.012, "M_Wire_Black", n=4)
    for k in range(n):
        t = (k + 0.5 + (r.random() - 0.5) * 0.4) / n
        u = a + (b - a) * t
        hw, hh = r.uniform(0.13, 0.24), r.uniform(0.2, 0.36)
        fab = r.choice(FABRICS)
        if o is not None:
            o.sockets.append(dict(kind="cloth", pos=P.p(u, y - 0.01, w + 0.01 * (k % 2)), n=P.out, size=(round(hw * 2, 3), round(hh * 2, 3)), tenant=fab))
        else:
            obox(det, P.p(u, y - hh - 0.02, w + 0.01 * (k % 2)), P.t3, UP, P.n3, hw, hh, 0.006, fab)


def densify(zone, b, o):
    """한 건물의 모든 면에 규칙 배치. 반환: 셈 dict(o.dense 에도 남김)."""
    meta = getattr(o, "meta", {}) or {}
    plog = getattr(o, "plog", None)
    if not plog:
        return {}
    r = rng(b.key, "dense")
    fl = int(meta.get("floors") or b.floors or 1)
    y0 = float(meta.get("y0", b.bottom))
    top = float(meta.get("top", y0 + FLOOR * fl + 0.6))
    fys = [y0 + FLOOR * k for k in range(fl)]
    n = dict(gas=0, branch=0, ac=0, flue=0, laundry=0, bars=0, pots=0, meters=0, anchors=0)
    gas_panels = getattr(o, "gas_panels", set())
    # 5차: 혜화 MMA 1층 계단 출입구(대학로 = 동쪽) — 명륜1가 방면 상가주택의 동쪽 옆면에 문 자리를 먼저 비워 둔다(Unity 가 문틀·간판)
    gym_pid = None
    if "명륜1가 방면" in (b.name or ""):
        side = [(P.n[0], pid) for pid, (P, ops) in plog.items() if P.role in ("street", "back") and P.W > 3.0]
        if side:
            gym_pid = max(side)[1]
    for pid, (P, ops) in sorted(plog.items(), key=lambda kv: (kv[1][0].face, kv[1][0].W)):
        role = P.role
        if role == "yard":
            gate_clutter(zone, b, o, P, ops, r, n)
            continue
        if role not in ("front", "street", "back") or P.W < 2.0:
            continue
        occ = occupancy(o, P, ops, meta)
        if pid == gym_pid:
            best = None
            u = 1.1
            while u < P.W - 1.1:
                g = P.ground(u)
                if occ.free(u - 0.85, u + 0.85, g - 0.1, g + 3.1) and abs(P.ground(u - 0.6) - P.ground(u + 0.6)) < 0.3:
                    sc = abs(u - 1.6)
                    if best is None or sc < best[0]:
                        best = (sc, u)
                u += 0.1
            if best:
                u = best[1]; g = P.ground(u)
                o.sockets.append(dict(kind="gym_door", pos=P.p(u, g, 0.0), n=P.out, size=(1.1, 2.25), tenant="혜화 MMA"))
                occ.add(u - 1.3, u + 1.3, g - 0.2, g + 4.9)
                n["gym_door"] = 1
        wins = [op for op in ops if op.kind in ("win", "small", "stair", "balc")]
        doors = [op for op in ops if op.kind in ("door", "gate") and op.y0 < P.ground((op.u0 + op.u1) / 2) + 0.6]
        p_vis = {"front": 1.0, "street": 0.9, "back": 0.6}[role]

        # ---- 1) 가스관(세로관 + 층 가지 U자 + 땅에서 오는 관 L자 + 계량기)
        if pid not in gas_panels and fl >= 1 and P.W >= 3.0 and r.random() < p_vis:
            want = max(1, int(round(P.W / (4.6 if role != "back" else 8.0))))
            branch_ys = [y0 + 2.5] + [fy + 0.42 for fy in fys[1:] if fy + 0.42 < top - 0.7]   # 1층은 창 위(2층 슬래브 아래), 2층부터 슬래브 띠 위
            y_last = branch_ys[-1]
            cands = []
            u = 0.45
            while u < P.W - 0.45:
                g = P.ground(u)
                lo = g + 0.3
                # 세로 띠(±0.14) 가 막히는 가장 높은 '아래쪽' 장애물 → 그 위에서 시작
                blocked_top = None
                ok = True
                for a, bb, c, d in occ.r:
                    if u - 0.14 < bb and u + 0.14 > a and c < y_last + 0.4 and d > lo:
                        if d < y0 + FLOOR + 1.3 and c < y0 + FLOOR:   # 1층 것(문·상가·창)
                            blocked_top = max(blocked_top or -1e3, d)
                        else:
                            ok = False
                            break
                if ok:
                    start = lo if blocked_top is None else blocked_top + 0.15
                    if start < y_last - 0.6:
                        edge = min(u, P.W - u)
                        score = (0 if blocked_top is None else 1.5) + abs(edge - 0.9) * 0.15 + r.random() * 0.6
                        cands.append((score, u, start, blocked_top is None))
                u += 0.1
            cands.sort()
            used = []
            for score, u, start, from_ground in cands:
                if len(used) >= want:
                    break
                if any(abs(u - q) < 2.6 for q in used):
                    continue
                pts = []
                g = P.ground(u)
                if from_ground:
                    ym = g + 0.78
                    if occ.free(u - 0.32, u + 0.32, ym - 0.08, ym + 0.66) and occ.free(u + 0.25, u + 0.47, g - 0.1, ym + 0.3):
                        _put(o, "gas_meter_pipe", P, u, ym)
                        n["meters"] += 1
                        occ.add(u - 0.3, u + 0.3, ym - 0.05, ym + 0.65)
                        # 땅에서 올라오는 관(L): 계량기 오른쪽으로 올라와 계량기 옆구리로
                        pipe(o.det, [P.p(u + 0.36, g - 0.05, GW), P.p(u + 0.36, ym + 0.22, GW), P.p(u + 0.27, ym + 0.22, GW)], D.PIPE_W, "M_Pipe_Yellow")
                        occ.add(u + 0.28, u + 0.44, g - 0.1, ym + 0.3)
                        ys = ym + 0.58
                    else:
                        ys = g + 0.25
                        pipe(o.det, [P.p(u, g - 0.05, GW), P.p(u, ys, GW)], D.PIPE_W, "M_Pipe_Yellow")
                else:
                    ys = start
                    # 벽을 뚫고 나옴(L)
                    pipe(o.det, [P.p(u, ys, 0.0), P.p(u, ys, GW)], D.PIPE_W, "M_Pipe_Yellow")
                pts = [P.p(u, ys, GW), P.p(u, y_last + 0.02, GW)]
                pipe(o.det, pts, D.PIPE_W, "M_Pipe_Yellow")
                pipe(o.lod[1], pts, D.PIPE_W * 1.2, "M_Pipe_Yellow", n=4)
                occ.add(u - 0.12, u + 0.12, ys, y_last + 0.1)
                used.append(u)
                n["gas"] += 1
                # 층 가지: 슬래브 띠를 따라 창 1~2개 쪽으로 가로로 달리고(실외기 뒤로 지나감), 창마다 위로 꺾여(U) 벽 속으로
                for yb in branch_ys:
                    if yb < ys + 0.3:
                        continue
                    first = yb < y0 + FLOOR
                    fy = y0 if first else yb - 0.42
                    row = [w for w in wins if fy + 0.4 < w.y0 < fy + 2.0]
                    best = None
                    for sg in ((1, -1) if r.random() < 0.5 else (-1, 1)):
                        edges = sorted((abs(e - u), e) for w in row for e in ((w.u0 - 0.24,) if sg > 0 else (w.u1 + 0.24,))
                                       if (e - u) * sg > 0.45 and abs(e - u) < 4.6 and 0.3 < e < P.W - 0.3)
                        drops = []
                        for _, e in edges[:2]:
                            a0, a1 = (u + 0.13, e + 0.06) if sg > 0 else (e - 0.06, u - 0.13)   # 세로관 자리는 빼고
                            if not occ.free(a0, a1, yb - 0.07, yb + 0.07, behind=True):
                                break
                            if first:
                                down = 0.35   # 1층 가지는 창 위에서 아래로 꺾여 들어감
                                if occ.free(e - 0.07, e + 0.07, yb - down - 0.05, yb, behind=True):
                                    drops.append((e, -down))
                            else:
                                up_ = 0.3 + 0.1 * r.random()
                                if occ.free(e - 0.07, e + 0.07, yb, yb + up_ + 0.05, behind=True):
                                    drops.append((e, up_))
                        if drops and (best is None or len(drops) > len(best)):
                            best = drops
                    if not best:
                        continue
                    far = max(best, key=lambda x: abs(x[0] - u))[0]
                    run = [P.p(u, yb, GW), P.p(far, yb, GW)]
                    pipe(o.det, run, D.PIPE_W, "M_Pipe_Yellow")
                    pipe(o.lod[1], run, D.PIPE_W * 1.2, "M_Pipe_Yellow", n=4)
                    occ.add(min(u, far) - 0.05, max(u, far) + 0.05, yb - 0.08, yb + 0.08)
                    for e, dy in best:
                        seq = [P.p(e, yb, GW), P.p(e, yb + dy, GW), P.p(e, yb + dy, 0.0)]
                        pipe(o.det, seq, D.PIPE_W, "M_Pipe_Yellow")
                        occ.add(e - 0.08, e + 0.08, min(yb, yb + dy) - 0.03, max(yb, yb + dy) + 0.05)
                        n["branch"] += 1

        # ---- 2) 위층 창 옆 실외기 · 보일러 연통 · 방범창 · 빨래
        p_ac = {"front": 0.5, "street": 0.6, "back": 0.65}[role]
        for w in wins:
            if w.y0 < y0 + FLOOR - 0.2:
                continue   # 1층은 아래 3)
            k = int((w.y0 - y0 + 0.3) // FLOOR)
            if w.kind == "balc":
                if r.random() < 0.45:
                    _fabric(o.det, P, w.u0 + 0.1, w.u1 - 0.1, w.y1 - 0.25, r.randint(3, 5), r, w=0.45, o=o)   # 발코니 안쪽 빨랫줄
                    n["laundry"] += 1
                continue
            if w.kind not in ("win",):
                continue
            side = 1 if r.random() < 0.5 else -1
            if r.random() < p_ac:
                for s in (side, -side):
                    ua = (w.u1 + 0.52) if s > 0 else (w.u0 - 0.52)
                    ya = w.y0 + 0.08
                    if 0.45 < ua < P.W - 0.45 and occ.free(ua - 0.37, ua + 0.37, ya - 0.36, ya + 0.68):
                        D.ac_unit(o, P, ua, ya, w_back=0.06)
                        occ.add(ua - 0.38, ua + 0.38, ya - 0.38, ya + 0.66, soft=True)
                        n["ac"] += 1
                        side = -s
                        break
            if r.random() < 0.25:
                uf = (w.u1 + 0.28) if side > 0 else (w.u0 - 0.28)
                yf = w.y0 + 0.95
                if 0.35 < uf < P.W - 0.35 and occ.free(uf - 0.1, uf + 0.1, yf - 0.1, yf + 0.5):
                    pipe(o.det, [P.p(uf, yf, 0.0), P.p(uf, yf, 0.32), P.p(uf, yf + 0.4, 0.32)], FLUE_W, "M_Metal_Grey")
                    occ.add(uf - 0.1, uf + 0.1, yf - 0.1, yf + 0.5)
                    n["flue"] += 1
            if not w.bars and k >= 2 and r.random() < 0.4:
                w.bar_mat = w.bar_mat or "M_AC_White"
                _bars(o.det, P, w)
                w.bars = True
                n["bars"] += 1
            if w.bars and r.random() < 0.18 and w.u1 - w.u0 > 0.9:
                _fabric(o.det, P, w.u0 + 0.02, w.u1 - 0.02, w.y1 - 0.12, r.randint(2, 4), r, o=o)
                n["laundry"] += 1

        # ---- 3) 1층: 땅 위 실외기(옆·뒷면) · 출입구 둘레 화분 · 문 옆 계량기 · 벽 밑 화분 줄
        def ground_ok(u, depth):
            x, _, z = P.p(u, 0.0, depth)
            if zone.solid_at(x, z, exclude=b, margin=0.02) is not None:
                return False
            return True

        for w in wins:
            if w.y0 >= y0 + FLOOR - 0.2 or role == "front" or r.random() > 0.2:
                continue
            uc = (w.u0 + w.u1) / 2
            g = P.ground(uc)
            if w.y0 - g > 0.8 and occ.free(uc - 0.4, uc + 0.4, g - 0.05, g + 0.7) and ground_ok(uc, 0.55):
                x, _, z = P.p(uc, 0.0, 0.5)
                if zone.walk_dist(x, z) > 0.25:
                    D.ac_unit(o, P, uc, g + 0.04, w_back=0.06, bracket=False)
                    occ.add(uc - 0.38, uc + 0.38, g, g + 0.66, soft=True)
                    n["ac"] += 1
        for dop in doors:
            g0 = P.ground((dop.u0 + dop.u1) / 2)
            # 문 옆 전기 계량기
            if r.random() < 0.7:
                for um in (dop.u1 + 0.42, dop.u0 - 0.42):
                    if 0.4 < um < P.W - 0.4 and occ.free(um - 0.2, um + 0.2, g0 + 1.15, g0 + 1.85):
                        _put(o, "electric_meter_box", P, um, g0 + 1.2)
                        occ.add(um - 0.18, um + 0.18, g0 + 1.15, g0 + 1.85)
                        n["meters"] += 1
                        break
            for sgn in (-1, 1):
                placed = 0
                for k in range(4):
                    if placed >= 3 or r.random() > 0.8:
                        continue
                    name = r.choice(POTS)
                    W_, Dd, H = SIZE[name]
                    uc = (dop.u0 - 0.35 - W_ / 2 - k * 0.75) if sgn < 0 else (dop.u1 + 0.35 + W_ / 2 + k * 0.75)
                    if not (W_ / 2 + 0.05 < uc < P.W - W_ / 2 - 0.05):
                        continue
                    g = P.ground(uc)
                    if not occ.free(uc - W_ / 2, uc + W_ / 2, g - 0.1, g + min(H, 0.9)) or not ground_ok(uc, Dd + 0.05):
                        continue
                    _put(o, name, P, uc, g, gap=0.02)
                    occ.add(uc - W_ / 2, uc + W_ / 2, g - 0.1, g + min(H, 0.9))
                    placed += 1
                    n["pots"] += 1
        if role in ("front", "street"):
            u = 0.6 + r.random() * 0.8
            while u < P.W - 0.6:
                if r.random() < 0.33:
                    name = r.choice(POTS)
                    W_, Dd, H = SIZE[name]
                    g = P.ground(u)
                    if occ.free(u - W_ / 2, u + W_ / 2, g - 0.1, g + min(H, 0.9)) and ground_ok(u, Dd + 0.05) and abs(P.ground(u - 0.4) - P.ground(u + 0.4)) < 0.25:
                        _put(o, name, P, u, g, gap=0.02)
                        occ.add(u - W_ / 2, u + W_ / 2, g - 0.1, g + min(H, 0.9))
                        n["pots"] += 1
                        u += W_ + 0.6
                        continue
                u += 0.9 + r.random() * 0.6

        # ---- 3b) 문 없는 앞·옆면에도 계량기 묶음(전기 1~2) 45%
        if role in ("front", "street") and not doors and r.random() < 0.45:
            for _ in range(6):
                um = r.uniform(0.6, P.W - 0.6)
                g = P.ground(um)
                if occ.free(um - 0.32, um + 0.32, g + 1.1, g + 1.85):
                    _put(o, "electric_meter_box", P, um - 0.15, g + 1.15)
                    if r.random() < 0.6:
                        _put(o, "electric_meter_box", P, um + 0.15, g + 1.15)
                    occ.add(um - 0.32, um + 0.32, g + 1.1, g + 1.85)
                    n["meters"] += 1
                    break

        # ---- 3c) 5차: 창 아래 빗물·녹물 자국 자리(Unity 데칼) · 상가 위층 창 시트지 자리(Unity TMP 글자)
        shopish = b.btype == "shop" or meta.get("variant") == "shop"
        for w in wins:
            if w.kind not in ("win", "small"):
                continue
            uc = (w.u0 + w.u1) / 2
            if r.random() < 0.45:
                o.sockets.append(dict(kind="streak", pos=P.p(uc, w.y0 - 0.07, 0.012), n=P.out, size=(round(w.u1 - w.u0, 3), round(min(1.6, w.y0 - P.ground(uc) - 0.3), 3)),
                                      tenant="rust" if (w.bars or r.random() < 0.35) else "rain"))
                n["streaks"] = n.get("streaks", 0) + 1
            if shopish and role in ("front", "street") and w.kind == "win" and w.y0 > y0 + FLOOR - 0.2 and w.u1 - w.u0 > 0.95 and r.random() < 0.6:
                k = int((w.y0 - y0 + 0.3) // FLOOR) + 1
                o.sockets.append(dict(kind="win_sheet", pos=P.p(uc, (w.y0 + w.y1) / 2, -w.depth + 0.02), n=P.out,
                                      size=(round(w.u1 - w.u0 - 0.06, 3), round(w.y1 - w.y0 - 0.06, 3)), tenant="%dF" % k))
                n["sheets"] = n.get("sheets", 0) + 1

        # ---- 4) 인입선 받는 자리(반대쪽 끝)
        if role in ("front", "street") and P.W > 3.0:
            have = [uw(P, s["pos"])[0] for s in o.sockets if s["kind"] == "wire_anchor" and abs(uw(P, s["pos"])[1]) < 0.3]
            ya = (y0 + FLOOR + 2.3) if fl >= 2 else (top - 0.45)
            for ua in (0.5, P.W - 0.5):
                if all(abs(ua - h) > 2.0 for h in have) and occ.free(ua - 0.12, ua + 0.12, ya - 0.12, ya + 0.12):
                    o.sockets.append(dict(kind="wire_anchor", pos=P.p(ua, ya, 0.06), n=P.out))
                    n["anchors"] += 1
                    break
    o.dense = n
    return n


# ---------------------------------------------------------------- 5차: 대문 둘레(마당 담) · 담장
def _addr(o, P, u, y, tenant="addr"):
    """도로명 주소판 자리(Unity 가 파란 판 + 흰 글자) + 초인종(작은 상자, 키트)."""
    o.sockets.append(dict(kind="addr_plate", pos=P.p(u, y, 0.02), n=P.out, size=(0.36, 0.16), tenant=tenant))
    pbox(o.det, P, u - 0.045, u + 0.045, y - 0.42, y - 0.30, 0.0, 0.035, "M_AC_White", skip=("-z",))
    pbox(o.det, P, u - 0.015, u + 0.015, y - 0.38, y - 0.34, 0.035, 0.045, "M_Metal_Grey", skip=("-z",))


def gate_clutter(zone, b, o, P, ops, r, n):
    """마당 담(대문 있는 면): 대문 옆 주소판·초인종·우편함·전기 계량기, 담 위쪽 따라 가스관(대문 기둥 쪽으로 꺾여 들어감)."""
    gates = [op for op in ops if op.kind in ("gate", "door")]
    if not gates:
        return
    g = gates[0]
    occ = occupancy(o, P, ops, getattr(o, "meta", {}) or {})
    for ua in (g.u1 + 0.45, g.u0 - 0.45, g.u1 + 0.95, g.u0 - 0.95, g.u1 + 1.4, g.u0 - 1.4):
        gy = P.ground(ua)
        if 0.3 < ua < P.W - 0.3 and occ.free(ua - 0.25, ua + 0.25, gy + 1.0, gy + 1.75):
            _addr(o, P, ua, gy + 1.62)
            occ.add(ua - 0.25, ua + 0.25, gy + 1.15, gy + 1.75)
            n["addr"] = n.get("addr", 0) + 1
            if r.random() < 0.6 and occ.free(ua - 0.45, ua + 0.45, gy + 0.85, gy + 1.15):
                _put(o, "mailbox_cluster", P, ua, gy + 0.55)
                n["meters"] += 1
            break
    # 담 위쪽 가스관: 대문 반대쪽 끝에서 대문 기둥까지 가로로, 대문 쪽 끝은 아래로 꺾여 벽 속으로
    top = P.top
    yb = top - 0.3
    if P.W > 2.5 and r.random() < 0.7:
        ue = g.u0 - 0.35 if g.u0 > P.W - g.u1 else g.u1 + 0.35
        us = 0.25 if ue > P.W / 2 else P.W - 0.25
        if abs(ue - us) > 1.0:
            pipe(o.det, [P.p(us, yb, 0.0), P.p(us, yb, GW), P.p(ue, yb, GW), P.p(ue, yb - 0.35, GW), P.p(ue, yb - 0.35, 0.0)], D.PIPE_W, "M_Pipe_Yellow")
            n["gas"] += 1


def fence_clutter(zone, b, o, P, r):
    """담장(fence_wall): 길 쪽 면에 가스관(담 위쪽 가로 + 끝에서 계량기로 내려옴)·전기 계량기·우편함·주소판(5차, 4차 남은 차이)."""
    n = dict(gas=0, meters=0, addr=0)
    if P.W < 2.2:
        return n
    h = P.top - P.ground(P.W / 2)
    if h < 1.3:
        return n
    yb = P.top - 0.28
    if r.random() < 0.55 and P.W > 3.0:
        a = r.uniform(0.3, 0.45) * P.W
        u0, u1 = (0.25, a) if r.random() < 0.5 else (P.W - a, P.W - 0.25)
        um = u0 if u1 > P.W - 0.3 else u1
        g = P.ground(um)
        ym = g + 0.85
        pipe(o.det, [P.p(u0 if um == u1 else u1, yb, 0.0), P.p(u0 if um == u1 else u1, yb, GW), P.p(um, yb, GW), P.p(um, ym + 0.58, GW)], D.PIPE_W, "M_Pipe_Yellow")
        _put(o, "gas_meter_pipe", P, um, ym)
        pipe(o.det, [P.p(um + 0.36, g - 0.05, GW), P.p(um + 0.36, ym + 0.22, GW), P.p(um + 0.27, ym + 0.22, GW)], D.PIPE_W, "M_Pipe_Yellow")
        n["gas"] += 1
        n["meters"] += 1
    if r.random() < 0.3:
        u = r.uniform(0.6, P.W - 0.6)
        _put(o, "electric_meter_box", P, u, P.ground(u) + 1.05)
        n["meters"] += 1
    if r.random() < 0.45:
        u = r.uniform(0.5, P.W - 0.5)
        _addr(o, P, u, min(P.top - 0.15, P.ground(u) + 1.6))
        n["addr"] += 1
    return n
