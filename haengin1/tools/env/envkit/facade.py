# -*- coding: utf-8 -*-
"""벽 면(Panel)과 그 위의 구멍(창·문·상가 유리), 발코니·차양·간판 자리.

Panel 좌표: u = 바깥에서 볼 때 왼쪽 끝에서 오른쪽으로 잰 거리(0..W), y = 월드 높이, w = 바깥으로 띄운 거리.
LOD 규칙(창 하나 기준):
  LOD0  구멍 + 안쪽 테두리(문설주) + 새시 틀·가운데 살 + 유리 + 창턱 + (디테일 묶음) 방범창
  LOD1  구멍 + 테두리 + 유리 + 창턱
  LOD2  구멍 + 테두리 + 유리
창문 불 판(M_Glass_Lit)은 모든 LOD 의 유리보다 1.5cm 앞(벽 면에서 REC - 0.015 안쪽)이라 LOD 가 바뀌어도 같은 자리에 보인다.
"""
from .geom import MB, obox, quad, madd, UP, pipe, cyl

REC = 0.12      # 창 유리 깊이(벽 면에서 안으로)
FW = 0.05       # 새시 틀 굵기
FD = 0.06       # 새시 틀 깊이


class Out:
    """한 블록(건물 하나)의 결과: LOD 3단 + 디테일(LOD0 전용, 그림자 끔) + 창문 불 판 + 간판 자리 + 자리 표시."""

    def __init__(self, block):
        self.block = block
        self.lod = [MB(), MB(), MB()]
        self.det = MB()
        self.lit = []        # (점 4개, 문턱 0~1)
        self.signs = []      # dict
        self.sockets = []    # dict(kind, pos, yaw, note)
        self.notes = []

    def L(self, *levels):
        return [self.lod[i] for i in levels]


class Panel:
    __slots__ = ("O", "t", "n", "W", "role", "gs", "bot", "top", "out", "t3", "n3", "face")

    def __init__(self, O, t, n, W, role, gs, bot, top, face=""):
        self.O, self.t, self.n, self.W = O, t, n, W
        self.role = role
        self.gs = gs
        self.bot, self.top = bot, top
        self.out = (n[0], 0.0, n[1])
        self.t3 = (t[0], 0.0, t[1])
        self.n3 = (n[0], 0.0, n[1])
        self.face = face

    def p(self, u, y, w=0.0):
        return (self.O[0] + self.t[0] * u + self.n[0] * w, y, self.O[1] + self.t[1] * u + self.n[1] * w)

    def ground(self, u):
        gs = self.gs
        if not gs:
            return self.bot
        if u <= gs[0][0]:
            return gs[0][1]
        for i in range(len(gs) - 1):
            if gs[i][0] <= u <= gs[i + 1][0]:
                a, b = gs[i], gs[i + 1]
                t = (u - a[0]) / max(1e-6, b[0] - a[0])
                return a[1] + (b[1] - a[1]) * t
        return gs[-1][1]

    def gmax(self, u0, u1):
        return max(self.ground(u0 + (u1 - u0) * k / 4) for k in range(5))

    def gmin(self, u0, u1):
        return min(self.ground(u0 + (u1 - u0) * k / 4) for k in range(5))


def pbox(mb, P, u0, u1, y0, y1, w0, w1, mat, skip=(), mats=None):
    """Panel 틀 안의 상자: x = u(오른쪽), y = 높이, z = w(바깥). skip '-z' = 벽에 붙은 뒷면."""
    if u1 - u0 < 1e-4 or y1 - y0 < 1e-4 or w1 - w0 < 1e-4:
        return
    c = P.p((u0 + u1) / 2, (y0 + y1) / 2, (w0 + w1) / 2)
    obox(mb, c, P.t3, UP, P.n3, (u1 - u0) / 2, (y1 - y0) / 2, (w1 - w0) / 2, mat, skip, mats)


def pquad(mb, P, u0, u1, y0, y1, w, mat, uv=None):
    return quad(mb, P.p(u0, y0, w), P.p(u1, y0, w), P.p(u1, y1, w), P.p(u0, y1, w), mat, out=P.out, uv=uv)


# ---------------------------------------------------------------- 벽(구멍 뚫린 격자)
def band_at(bands, y, default):
    for y0, y1, m in bands:
        if y0 - 1e-6 <= y < y1 + 1e-6:
            return m
    return default


def wall_grid(mb, P, bands, holes, w=0.0, default="M_Concrete", u_lo=0.0, u_hi=None, y_lo=None, y_hi=None):
    """bands = [(y0, y1, 재질)], holes = [(u0, u1, y0, y1)]. 구멍을 뺀 칸을 줄 단위로 메운다."""
    u_hi = P.W if u_hi is None else u_hi
    y_lo = P.bot if y_lo is None else y_lo
    y_hi = P.top if y_hi is None else y_hi
    ys = {y_lo, y_hi}
    for b0, b1, _ in bands:
        for v in (b0, b1):
            if y_lo < v < y_hi:
                ys.add(v)
    hs = []
    for h in holes:
        u0, u1, h0, h1 = max(u_lo, h[0]), min(u_hi, h[1]), max(y_lo, h[2]), min(y_hi, h[3])
        if u1 - u0 > 1e-4 and h1 - h0 > 1e-4:
            hs.append((u0, u1, h0, h1))
            ys.add(h0)
            ys.add(h1)
    ys = sorted(ys)
    for k in range(len(ys) - 1):
        ya, yb = ys[k], ys[k + 1]
        if yb - ya < 1e-4:
            continue
        mat = band_at(bands, (ya + yb) / 2, default)
        cover = sorted((h[0], h[1]) for h in hs if h[2] <= ya + 1e-6 and h[3] >= yb - 1e-6)
        u = u_lo
        segs = []
        for a, b in cover:
            if a > u + 1e-4:
                segs.append((u, a))
            u = max(u, b)
        if u_hi > u + 1e-4:
            segs.append((u, u_hi))
        for a, b in segs:
            pquad(mb, P, a, b, ya, yb, w, mat)


def reveal(mb, P, u0, u1, y0, y1, depth, mat, top_mat=None, bot_mat=None):
    quad(mb, P.p(u0, y1, 0), P.p(u1, y1, 0), P.p(u1, y1, -depth), P.p(u0, y1, -depth), top_mat or mat, out=(0, -1, 0))
    quad(mb, P.p(u0, y0, 0), P.p(u1, y0, 0), P.p(u1, y0, -depth), P.p(u0, y0, -depth), bot_mat or mat, out=(0, 1, 0))
    quad(mb, P.p(u0, y0, 0), P.p(u0, y1, 0), P.p(u0, y1, -depth), P.p(u0, y0, -depth), mat, out=P.t3)
    quad(mb, P.p(u1, y0, 0), P.p(u1, y1, 0), P.p(u1, y1, -depth), P.p(u1, y0, -depth), mat,
         out=(-P.t3[0], 0, -P.t3[2]))


# ---------------------------------------------------------------- 구멍 종류
class Op:
    """구멍 하나. kind: win small balc stair base shop door gate hanji vent"""
    __slots__ = ("kind", "u0", "u1", "y0", "y1", "depth", "rev", "frame", "glass", "bars", "bar_mat", "mull",
                 "lit", "leaf", "transom", "extra")

    def __init__(self, kind, u0, u1, y0, y1, rev="M_Concrete", frame="M_Metal_Grey", glass="M_Glass_Dark",
                 bars=False, bar_mat="M_Metal_Grey", mull=1, lit=None, leaf=None, depth=None, transom=None, extra=None):
        self.kind, self.u0, self.u1, self.y0, self.y1 = kind, u0, u1, y0, y1
        self.rev, self.frame, self.glass = rev, frame, glass
        self.bars, self.bar_mat, self.mull = bars, bar_mat, mull
        self.lit, self.leaf, self.transom, self.extra = lit, leaf, transom, extra or {}
        self.depth = depth if depth is not None else (0.16 if kind in ("door", "gate") else (0.10 if kind == "shop" else REC))

    def hole(self):
        return (self.u0, self.u1, self.y0, self.y1)


def emit_openings(o, P, ops):
    """구멍들의 테두리·유리·틀·창턱(LOD별) + 방범창(디테일) + 창문 불 판."""
    for op in ops:
        u0, u1, y0, y1, dp = op.u0, op.u1, op.y0, op.y1, op.depth
        for L in (0, 1, 2):
            mb = o.lod[L]
            reveal(mb, P, u0, u1, y0, y1, dp, op.rev)
            if op.kind in ("door", "gate"):
                _door_leaf(mb, P, op, L)
            elif op.kind == "hanji":
                _hanji(mb, o.det if L == 0 else None, P, op, L)
            elif op.kind == "vent":
                pquad(mb, P, u0, u1, y0, y1, -dp, "M_Metal_Grey")
            else:
                pquad(mb, P, u0, u1, y0, y1, -dp, op.glass)
                if L == 0:
                    _frame(mb, P, op)
                if L <= 1 and op.kind in ("win", "small", "stair", "base"):
                    pbox(mb, P, u0 - 0.04, u1 + 0.04, y0 - 0.06, y0, -0.02, 0.05, "M_Concrete", skip=("-z",))
                if L == 1 and op.kind == "shop":
                    _frame(mb, P, op, light=True)
        if op.bars:
            _bars(o.det, P, op)
        if op.lit is not None:
            inset = 0.03
            pts = [P.p(u0 + inset, y0 + inset, -dp + 0.015), P.p(u1 - inset, y0 + inset, -dp + 0.015),
                   P.p(u1 - inset, y1 - inset, -dp + 0.015), P.p(u0 + inset, y1 - inset, -dp + 0.015)]
            o.lit.append((pts, op.lit, P.out))


def _frame(mb, P, op, light=False):
    u0, u1, y0, y1, dp = op.u0, op.u1, op.y0, op.y1, op.depth
    w0, w1 = -dp, -dp + FD
    fm = op.frame
    sk = ("-z",)
    if not light:
        pbox(mb, P, u0, u0 + FW, y0, y1, w0, w1, fm, skip=sk)
        pbox(mb, P, u1 - FW, u1, y0, y1, w0, w1, fm, skip=sk)
        pbox(mb, P, u0 + FW, u1 - FW, y1 - FW, y1, w0, w1, fm, skip=sk)
        pbox(mb, P, u0 + FW, u1 - FW, y0, y0 + FW, w0, w1, fm, skip=sk)
    if op.kind == "shop":
        nseg = max(1, int(round((u1 - u0) / 1.25)))
        ums = [u0 + (u1 - u0) * k / nseg for k in range(1, nseg)]
    else:
        ums = [u0 + (u1 - u0) * k / (op.mull + 1) for k in range(1, op.mull + 1)]
    for um in ums:
        pbox(mb, P, um - FW / 2, um + FW / 2, y0 + FW, y1 - FW, w0, w1 + 0.01, fm, skip=sk)
    if op.transom:
        pbox(mb, P, u0 + FW, u1 - FW, op.transom - FW / 2, op.transom + FW / 2, w0, w1, fm, skip=sk)


def _bars(mb, P, op):
    """방범창: 벽 앞 0.03~0.11m, 테두리 + 세로살 0.13m 간격(docs/10 3-1 은 0.12 — 더 촘촘하면 외곽선이 겹쳐 검은 덩어리)."""
    u0, u1, y0, y1 = op.u0 - 0.04, op.u1 + 0.04, op.y0 - 0.02, op.y1 + 0.04
    m = op.bar_mat
    w0, w1 = 0.03, 0.11
    t = 0.025
    pbox(mb, P, u0, u1, y1 - t, y1, w0, w1, m, skip=("-z",))
    pbox(mb, P, u0, u1, y0, y0 + t, w0, w1, m, skip=("-z",))
    pbox(mb, P, u0, u0 + t, y0 + t, y1 - t, w0, w1, m, skip=("-z", "+y", "-y"))
    pbox(mb, P, u1 - t, u1, y0 + t, y1 - t, w0, w1, m, skip=("-z", "+y", "-y"))
    if op.y1 - op.y0 > 0.9:
        ym = (y0 + y1) / 2
        pbox(mb, P, u0 + t, u1 - t, ym - 0.012, ym + 0.012, w1 - 0.03, w1 - 0.01, m, skip=("-z",))
    n = max(2, int((u1 - u0 - 2 * t) / 0.13))
    for k in range(1, n):
        u = u0 + t + (u1 - u0 - 2 * t) * k / n
        pbox(mb, P, u - 0.007, u + 0.007, y0 + t, y1 - t, w1 - 0.028, w1 - 0.014, m, skip=("-z", "+y", "-y"))


def _door_leaf(mb, P, op, L):
    u0, u1, y0, y1, dp = op.u0, op.u1, op.y0, op.y1, op.depth
    leaf = op.leaf or "M_Metal_Green"
    if leaf == "glass":
        pquad(mb, P, u0, u1, y0, y1, -dp, "M_Glass_Dark")
        if L <= 1:
            fm = op.frame
            for a, b in ((u0, u0 + 0.07), (u1 - 0.07, u1)):
                pbox(mb, P, a, b, y0, y1, -dp, -dp + 0.07, fm, skip=("-z",))
            pbox(mb, P, u0 + 0.07, u1 - 0.07, y1 - 0.07, y1, -dp, -dp + 0.07, fm, skip=("-z",))
            pbox(mb, P, u0 + 0.07, u1 - 0.07, y0, y0 + 0.12, -dp, -dp + 0.07, fm, skip=("-z",))
            if (u1 - u0) > 1.4:
                um = (u0 + u1) / 2
                pbox(mb, P, um - 0.035, um + 0.035, y0 + 0.12, y1 - 0.07, -dp, -dp + 0.07, fm, skip=("-z",))
        return
    pquad(mb, P, u0, u1, y0, y1, -dp, leaf)
    if L == 0:
        # 문 테 + 손잡이 + (철대문이면) 위쪽 살
        pbox(mb, P, u0, u0 + 0.06, y0, y1, -dp, -dp + 0.05, leaf, skip=("-z",))
        pbox(mb, P, u1 - 0.06, u1, y0, y1, -dp, -dp + 0.05, leaf, skip=("-z",))
        pbox(mb, P, u0 + 0.06, u1 - 0.06, y1 - 0.06, y1, -dp, -dp + 0.05, leaf, skip=("-z",))
        hu = u1 - 0.15 if (op.extra.get("hinge", "L") == "L") else u0 + 0.15
        pbox(mb, P, hu - 0.02, hu + 0.02, y0 + 0.95, y0 + 1.1, -dp, -dp + 0.08, "M_Metal_Grey", skip=("-z",))
        if op.kind == "gate":
            n = max(2, int((u1 - u0) / 0.14))
            yb = y0 + (y1 - y0) * 0.55
            pbox(mb, P, u0 + 0.06, u1 - 0.06, yb - 0.04, yb, -dp, -dp + 0.05, leaf, skip=("-z",))
            if (u1 - u0) > 1.6:
                um = (u0 + u1) / 2
                pbox(mb, P, um - 0.03, um + 0.03, y0, y1, -dp, -dp + 0.06, leaf, skip=("-z",))
            for k in range(1, n):
                u = u0 + (u1 - u0) * k / n
                pbox(mb, P, u - 0.012, u + 0.012, yb, y1 - 0.06, -dp + 0.01, -dp + 0.04, leaf, skip=("-z", "+y", "-y"))


def _hanji(mb, det, P, op, L):
    """한옥 창호: 창호지 판(유리 자리) + 주칠 문얼굴 + 띠살(세로살·가로 띠)."""
    u0, u1, y0, y1, dp = op.u0, op.u1, op.y0, op.y1, op.depth
    pquad(mb, P, u0, u1, y0, y1, -dp, "M_Hanji")
    fm = op.frame if op.frame != "M_Metal_Grey" else "M_Wood_Red"
    if L <= 1:
        t = 0.06
        pbox(mb, P, u0, u0 + t, y0, y1, -dp, -dp + 0.05, fm, skip=("-z",))
        pbox(mb, P, u1 - t, u1, y0, y1, -dp, -dp + 0.05, fm, skip=("-z",))
        pbox(mb, P, u0 + t, u1 - t, y1 - t, y1, -dp, -dp + 0.05, fm, skip=("-z",))
        pbox(mb, P, u0 + t, u1 - t, y0, y0 + t, -dp, -dp + 0.05, fm, skip=("-z",))
        nleaf = max(1, int(round((u1 - u0) / 0.75)))
        for k in range(1, nleaf):
            u = u0 + (u1 - u0) * k / nleaf
            pbox(mb, P, u - 0.03, u + 0.03, y0 + t, y1 - t, -dp, -dp + 0.05, fm, skip=("-z",))
    if L == 0 and det is not None:
        # 띠살: 세로살 촘촘히 + 가로 띠 3줄
        n = max(3, int((u1 - u0) / 0.09))
        for k in range(1, n):
            u = u0 + (u1 - u0) * k / n
            pbox(det, P, u - 0.008, u + 0.008, y0 + 0.06, y1 - 0.06, -dp, -dp + 0.025, fm, skip=("-z", "+y", "-y"))
        H = y1 - y0
        for f in (0.22, 0.5, 0.78):
            yc = y0 + H * f
            for dyy in (-0.05, 0.0, 0.05):
                pbox(det, P, u0 + 0.06, u1 - 0.06, yc + dyy - 0.008, yc + dyy + 0.008, -dp, -dp + 0.025, fm,
                     skip=("-z", "+x", "-x"))


# ---------------------------------------------------------------- 붙는 것들
def balcony(o, P, u0, u1, yf, depth=0.9, rail="M_Metal_Grey", slab="M_Concrete", solid_rail=False):
    """작은 발코니: 바닥판 + 난간(LOD0 = 기둥·손잡이 + 디테일 세로살 / LOD1 = 판)."""
    for L in (0, 1, 2):
        pbox(o.lod[L], P, u0, u1, yf - 0.16, yf, 0.0, depth, slab, skip=("-z",))
    H = 1.0
    if solid_rail:
        for L in (0, 1):
            mb = o.lod[L]
            pbox(mb, P, u0, u1, yf, yf + H, depth - 0.06, depth, rail, skip=())
            pbox(mb, P, u0, u0 + 0.06, yf, yf + H, 0.0, depth - 0.06, rail)
            pbox(mb, P, u1 - 0.06, u1, yf, yf + H, 0.0, depth - 0.06, rail)
        return
    mb = o.lod[0]
    pr = 0.035
    for u in (u0, u1):
        pbox(mb, P, u - pr if u == u1 else u, u if u == u1 else u + pr, yf, yf + H, depth - pr, depth, rail)
    pbox(mb, P, u0, u1, yf + H - 0.05, yf + H, depth - 0.06, depth, rail)
    pbox(mb, P, u0, u0 + 0.05, yf + H - 0.05, yf + H, 0.0, depth - 0.06, rail)
    pbox(mb, P, u1 - 0.05, u1, yf + H - 0.05, yf + H, 0.0, depth - 0.06, rail)
    pbox(mb, P, u0, u1, yf + 0.08, yf + 0.12, depth - 0.05, depth - 0.01, rail)
    n = max(2, int((u1 - u0) / 0.12))
    for k in range(1, n):
        u = u0 + (u1 - u0) * k / n
        pbox(o.det, P, u - 0.01, u + 0.01, yf + 0.12, yf + H - 0.05, depth - 0.04, depth - 0.02, rail, skip=("+y", "-y"))
    for wv in (0.3, 0.6):
        for u in (u0, u1):
            uu = u + 0.01 if u == u0 else u - 0.03
            pbox(o.det, P, uu, uu + 0.02, yf + 0.12, yf + H - 0.05, wv - 0.01, wv + 0.01, rail, skip=("+y", "-y"))
    mb1 = o.lod[1]
    pbox(mb1, P, u0, u1, yf, yf + H, depth - 0.04, depth, rail)
    pbox(mb1, P, u0, u0 + 0.04, yf, yf + H, 0.0, depth - 0.04, rail)
    pbox(mb1, P, u1 - 0.04, u1, yf, yf + H, 0.0, depth - 0.04, rail)


def canopy(o, P, u0, u1, y, depth=0.7, mat="M_Concrete", t=0.12, levels=(0, 1, 2)):
    for L in levels:
        pbox(o.lod[L], P, u0, u1, y, y + t, 0.0, depth, mat, skip=("-z",))


def sign_band(o, P, u0, u1, y0, y1, tenant, block, depth=0.12, kind="band"):
    """간판 띠: 틀 상자(회색 금속, 앞면 없음) + 앞면 = 간판 자리 Sign_<n>(별도 오브젝트, 0~1 UV)."""
    for L in (0, 1, 2):
        pbox(o.lod[L], P, u0, u1, y0, y1, 0.0, depth, "M_Metal_Grey", skip=("-z", "+z"))
    w = depth + 0.003
    pts = [P.p(u0 + 0.03, y0 + 0.03, w), P.p(u1 - 0.03, y0 + 0.03, w), P.p(u1 - 0.03, y1 - 0.03, w), P.p(u0 + 0.03, y1 - 0.03, w)]
    o.signs.append(dict(kind=kind, faces=[pts], size=(round(u1 - u0 - 0.06, 3), round(y1 - y0 - 0.06, 3)),
                        normal=P.out, tenant=tenant, block=block.name))


def sign_projecting(o, P, u, y0, y1, tenant, block, reach=0.75, gap=0.15, thick=0.2):
    """돌출 간판: 벽에 수직인 세로 판(양면). 판 두 면이 같은 Sign 하나."""
    a, b = u - thick / 2, u + thick / 2
    for L in (0, 1, 2):
        pbox(o.lod[L], P, a, b, y0, y1, gap, gap + reach, "M_Metal_Grey", skip=("+x", "-x"))
    for yy in (y0 + 0.2, y1 - 0.2):
        pbox(o.lod[0], P, u - 0.03, u + 0.03, yy - 0.03, yy + 0.03, 0.0, gap, "M_Metal_Grey", skip=("-z",))
    e = 0.004
    A = [P.p(b + e, y0 + 0.03, gap + 0.03), P.p(b + e, y0 + 0.03, gap + reach - 0.03),
         P.p(b + e, y1 - 0.03, gap + reach - 0.03), P.p(b + e, y1 - 0.03, gap + 0.03)]
    B = [P.p(a - e, y0 + 0.03, gap + reach - 0.03), P.p(a - e, y0 + 0.03, gap + 0.03),
         P.p(a - e, y1 - 0.03, gap + 0.03), P.p(a - e, y1 - 0.03, gap + reach - 0.03)]
    o.signs.append(dict(kind="projecting", faces=[A, B], size=(round(reach - 0.06, 3), round(y1 - y0 - 0.06, 3)),
                        normal=P.t3, tenant=tenant, block=block.name))


def downpipe(mb, P, u, y0, y1, w=0.08, mat="M_Metal_Grey"):
    """선홈통(세로 빗물관)."""
    pipe(mb, [P.p(u, y0, w), P.p(u, y1, w)], 0.09, mat, n=6)
