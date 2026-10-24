# -*- coding: utf-8 -*-
"""골목 디테일. 담당은 docs/10 4장 표를 따른다.
  키트(코드)  : 노란 가스관(벽 따라 꺾임)·방범창·난간·외부 철계단·옥탑·실외기 받침·담장 위 철망·빨랫줄·전봇대와 전선(현수선)
  소품(GLB)   : 계량기·실외기·물탱크·빨래 건조대·화분·장독·우편함 → 키트는 '자리 표시'(Prop_<소품>_<n>)만 남긴다.
                자리 표시 = 소품 바닥 가운데, +Z(Unity) = 소품 정면(벽에서 바깥), +Y 위. 소품 이름은 concept/art/env/props/<이름>.glb
PROP_GEOMETRY = True 로 돌리면(build_env --prop-geometry 1) 소품 자리에 키트의 저면 수 대용품도 LOD0 디테일로 넣는다(소품 없이 쓸 때)."""
import math

from .geom import (MB, madd, add, sub, mul, norm, length, cross, UP, obox, box, cyl, pipe, tube, catenary,
                   seg_box, quad, ring)
from .facade import pbox

PIPE_W = 0.06
RISER, TREAD = 0.2, 0.21   # 외부 철제 계단(가파른 편)
PROP_GEOMETRY = False
# 소품 실측(concept/art/env/props/props_report.json 의 w·d·h) — 벽에 붙일 때 깊이 절반만큼 띄우는 데 씀
PROP_SIZE = {
    "ac_outdoor_unit": (0.685, 0.353, 0.62), "gas_meter_pipe": (0.531, 0.158, 0.6), "electric_meter_box": (0.262, 0.161, 0.6),
    "water_tank_roof": (0.917, 0.924, 1.7), "laundry_rack": (1.7, 1.082, 0.775), "clay_pots_cluster": (0.785, 0.799, 0.6),
    "styrofoam_planter": (0.6, 0.448, 0.336), "basin_planter_onion": (0.706, 0.706, 0.55), "jangdok_set": (0.958, 0.961, 0.8),
    "mailbox_cluster": (0.797, 0.098, 0.6),
}


def add_prop(o, name, pos, n_out, note=None):
    """소품 자리 표시. pos = 소품 바닥 가운데(Unity), n_out = 정면 방향(수평)."""
    n = norm((n_out[0], 0.0, n_out[2]))
    o.sockets.append(dict(kind="prop", prop=name, pos=pos, n=n, host=o.block.name if o.block else None, note=note))


def wall_prop(o, P, name, u, y, gap=0.0):
    """벽 P 에 등을 붙인 소품: 바닥 가운데 = 벽에서 (깊이/2 + gap)."""
    d = PROP_SIZE[name][1]
    add_prop(o, name, P.p(u, y, gap + d / 2), P.out)


# ---------------------------------------------------------------- 실외기·계량기·우편함
def ac_unit(o, P, u, y, w_back=0.04, bracket=True, wall_mount=True):
    """에어컨 실외기: 받침 앵글(키트) + 소품 ac_outdoor_unit 자리. (u, y) = 실외기 아래 가운데."""
    W, Dd, H = PROP_SIZE["ac_outdoor_unit"]
    if bracket and wall_mount:
        for du in (-0.26, 0.26):
            pbox(o.det, P, u + du - 0.02, u + du + 0.02, y - 0.05, y, 0.0, w_back + Dd + 0.05, "M_Metal_Grey", skip=("-z",))
            pbox(o.det, P, u + du - 0.02, u + du + 0.02, y - 0.35, y - 0.05, 0.0, 0.04, "M_Metal_Grey", skip=("-z",))
    wall_prop(o, P, "ac_outdoor_unit", u, y, gap=w_back)
    if PROP_GEOMETRY:
        mb = o.det
        pbox(mb, P, u - W / 2, u + W / 2, y, y + H, w_back, w_back + Dd, "M_AC_White", skip=("-z",))
        c = P.p(u - 0.08, y + H / 2, w_back + Dd + 0.005)
        pts = [madd(c, (P.t3, 0.2 * math.cos(2 * math.pi * k / 10)), (UP, 0.2 * math.sin(2 * math.pi * k / 10))) for k in range(10)]
        mb.face(pts, "M_Metal_Grey", out=P.n3)


def meter_box(o, P, u, y, kind="gas"):
    """계량기: gas → 소품 gas_meter_pipe, elec → electric_meter_box. (u, y) = 아래 가운데."""
    name = "gas_meter_pipe" if kind == "gas" else "electric_meter_box"
    wall_prop(o, P, name, u, y)
    if PROP_GEOMETRY:
        W, Dd, H = PROP_SIZE[name]
        pbox(o.det, P, u - W * 0.3, u + W * 0.3, y, y + H * 0.6, 0.0, Dd, "M_AC_White" if kind == "gas" else "M_Metal_Grey", skip=("-z",))


def mailbox(o, P, u, y):
    wall_prop(o, P, "mailbox_cluster", u, y)
    if PROP_GEOMETRY:
        pbox(o.det, P, u - 0.4, u + 0.4, y, y + 0.6, 0.0, 0.1, "M_Metal_Grey", skip=("-z",))


# ---------------------------------------------------------------- 가스관
def gas_lines(o, P, u_riser, y_meter, floor_ys, run_dir, run_len, wrap=None, w=0.09):
    """세로관(계량기 위 → 꼭대기 층) + 층마다 가로 가지(run_dir 쪽으로 run_len) + 가지 끝 계량기(소품).
    wrap = (옆 Panel, 길이): 홀수 층 가지는 모서리를 돌아 옆 면으로 이어짐(벽 따라 꺾임). 관은 키트(코드)."""
    det = o.det
    if hasattr(o, "gas_panels"):
        o.gas_panels.add(id(P))
    top = floor_ys[-1] + 0.45
    pts = [P.p(u_riser, y_meter + 0.55, w), P.p(u_riser, top, w)]
    pipe(det, pts, PIPE_W, "M_Pipe_Yellow")
    meter_box(o, P, u_riser, y_meter - 0.05, "gas")
    for k, fy in enumerate(floor_ys):
        y = fy + 0.42
        if y > top - 0.05:
            continue
        u_end = u_riser + run_dir * run_len
        if wrap is not None and k % 2 == 1 and (u_end < 0.15 or u_end > P.W - 0.15):
            P2, L2 = wrap
            uc = P.W if run_dir > 0 else 0.0
            corner = P.p(uc + run_dir * w, y, w)
            seq = [P.p(u_riser, y, w), corner, P2.p(L2 if run_dir > 0 else P2.W - L2, y, w)]
            pipe(det, seq, PIPE_W, "M_Pipe_Yellow")
            uu = L2 if run_dir > 0 else P2.W - L2
            pipe(det, [P2.p(uu, y, w), P2.p(uu, y, 0.0)], PIPE_W, "M_Pipe_Yellow")
            continue
        u_end = max(0.2, min(P.W - 0.2, u_end))
        pipe(det, [P.p(u_riser, y, w), P.p(u_end, y, w), P.p(u_end, y, 0.0)], PIPE_W, "M_Pipe_Yellow")


# ---------------------------------------------------------------- 옥상
def water_tank(o, F, x, z, y, r=0.55, h=1.15, levels=(0, 1, 2)):
    """옥상 물탱크: 소품 water_tank_roof 자리."""
    add_prop(o, "water_tank_roof", F.p(x, y, z), F.d(0, 0, -1))
    if not PROP_GEOMETRY:
        return
    for L in levels:
        mb = o.lod[L]
        n = (14, 10, 8)[L]
        cyl(mb, F.p(x, y + 0.35, z), UP, r, r, h, n, "M_Tank_Blue", caps=(True, False))
        cyl(mb, F.p(x, y + 0.35 + h, z), UP, r, 0.18, 0.22, n, "M_Tank_Blue", caps=(False, True))
        box(mb, F, x - r * 0.75, x + r * 0.75, y, y + 0.35, z - r * 0.75, z + r * 0.75, "M_Metal_Grey", skip=("-y",))


def stair_house(o, F, x0, x1, z0, z1, y, h=2.4, wall="M_Plaster_White", door_side="-z", levels=(0, 1, 2)):
    """옥탑 계단실: 상자 + 철문 + 처마 슬래브."""
    from .facade import Panel, wall_grid, emit_openings, Op
    for L in levels:
        mb = o.lod[L]
        box(mb, F, x0, x1, y, y + h, z0, z1, wall, skip=("-y", "+y", door_side))
        box(mb, F, x0 - 0.12, x1 + 0.12, y + h, y + h + 0.15, z0 - 0.12, z1 + 0.12, "M_Concrete")
    # 문 쪽 면
    if door_side == "-z":
        O = F.p(x0, 0, z0)
        t = (F.ux[0], F.ux[2])
        n = (-F.uz[0], -F.uz[2])
        W = x1 - x0
    else:
        O = F.p(x1, 0, z1)
        t = (-F.ux[0], -F.ux[2])
        n = (F.uz[0], F.uz[2])
        W = x1 - x0
    P = Panel((O[0], O[2]), t, n, W, "front", [], y, y + h)
    door = Op("door", W / 2 - 0.45, W / 2 + 0.45, y, y + 2.0, rev=wall, leaf="M_Metal_Green")
    for L in levels:
        wall_grid(o.lod[L], P, [(y, y + h, wall)], [door.hole()], default=wall)
    emit_openings(o, P, [door])


def laundry_rack(o, F, x, z, y, yaw_x=True, r=None):
    """접이식 빨래 건조대: 소품 laundry_rack 자리(긴 쪽 = 틀 x 또는 z)."""
    ax = F.ux if yaw_x else F.uz
    az = F.uz if yaw_x else F.ux
    add_prop(o, "laundry_rack", F.p(x, y, z), az)
    if not PROP_GEOMETRY:
        return
    det = o.det
    L, W, H = 1.6, 0.6, 1.0
    c = F.p(x, y, z)
    for s_ in (-1, 1):
        e = madd(c, (ax, s_ * L / 2))
        seg_box(det, madd(e, (az, -W / 2)), madd(e, (az, W / 2), (UP, H)), 0.025, 0.025, "M_Metal_Grey")
        seg_box(det, madd(e, (az, W / 2)), madd(e, (az, -W / 2), (UP, H)), 0.025, 0.025, "M_Metal_Grey")
    for k in range(3):
        u = -L / 2 + 0.3 + (L - 0.6) * (k + 0.5) / 3
        obox(det, madd(c, (ax, u), (UP, H - 0.25)), ax, UP, az, 0.18, 0.22, 0.006, "M_Fabric")


def clothesline(det, a, b, y, cloths=4):
    """빨랫줄(기둥 2 + 줄 + 빨래)."""
    for p in (a, b):
        cyl(det, (p[0], y, p[2]), UP, 0.03, 0.03, 1.8, 6, "M_Metal_Grey", caps=(False, True))
    A, B = (a[0], y + 1.75, a[2]), (b[0], y + 1.75, b[2])
    pts = catenary(A, B, 0.12, 8)
    tube(det, pts, 0.006, 3, "M_Wire_Black")
    d = norm(sub(B, A))
    s = norm(cross(UP, d))
    for k in range(cloths):
        t = (k + 0.7) / (cloths + 0.4)
        p = pts[int(t * 8)]
        obox(det, madd(p, (UP, -0.3)), d, UP, s, 0.2, 0.28, 0.006, "M_Fabric")


def jars(o, F, x, z, y, n, r):
    """장독대: 낮은 돌판(키트) + 소품 jangdok_set 자리."""
    for L in (0, 1):
        box(o.lod[L], F, x - 0.9, x + 0.9, y, y + 0.15, z - 0.6, z + 0.6, "M_Granite", skip=("-y",))
    add_prop(o, "jangdok_set", F.p(x, y + 0.15, z), F.d(0, 0, -1))
    if PROP_GEOMETRY:
        for k in range(n):
            px = x - 0.6 + 1.2 * (k % 3) / 2
            pz = z - 0.3 + 0.6 * (k // 3)
            s_ = 0.8 + 0.4 * r.random()
            base = F.p(px, y + 0.15, pz)
            cyl(o.det, base, UP, 0.16 * s_, 0.24 * s_, 0.25 * s_, 8, "M_Brick_Brown", caps=(False, False))
            cyl(o.det, madd(base, (UP, 0.25 * s_)), UP, 0.24 * s_, 0.14 * s_, 0.22 * s_, 8, "M_Brick_Brown", caps=(False, True))


def pyeongsang(o, F, x, z, y, levels=(0, 1)):
    """평상(나무 마루 1.8 × 1.4, 다리 4)."""
    for L in levels:
        mb = o.lod[L]
        box(mb, F, x - 0.9, x + 0.9, y + 0.38, y + 0.45, z - 0.7, z + 0.7, "M_Wood_Dark")
        if L == 0:
            for sx in (-1, 1):
                for sz in (-1, 1):
                    box(mb, F, x + sx * 0.8 - 0.04, x + sx * 0.8 + 0.04, y, y + 0.38, z + sz * 0.6 - 0.04,
                        z + sz * 0.6 + 0.04, "M_Wood_Dark", skip=("-y", "+y"))


# ---------------------------------------------------------------- 화분(소품)
POTS = ("clay_pots_cluster", "styrofoam_planter", "basin_planter_onion")


def pot(o, base, r_, h, kind, rr, facing=(0.0, 0.0, 1.0)):
    """화분 하나 자리: kind styro → styrofoam_planter, 그 밖 → clay_pots_cluster."""
    name = "styrofoam_planter" if kind == "styro" else "clay_pots_cluster"
    add_prop(o, name, base, facing)
    if PROP_GEOMETRY:
        _pot_geom(o.det, base, r_, h, kind, rr)


def _pot_geom(det, base, r_, h, kind, rr):
    if kind == "styro":
        a = 0.55 * r_ * 2
        c = madd(base, (UP, h / 2))
        obox(det, c, (1, 0, 0), UP, (0, 0, 1), a / 2 + 0.05, h / 2, a / 2 - 0.02, "M_AC_White")
        obox(det, madd(base, (UP, h + 0.06)), (1, 0, 0), UP, (0, 0, 1), a / 2, 0.08, a / 2 - 0.05, "M_Plant_Green")
        return
    cyl(det, base, UP, r_ * 0.75, r_, h, 8, "M_Brick_Brown", caps=(False, True), cap_mat="M_Wood_Dark")
    top = madd(base, (UP, h))
    hh = r_ * (1.2 + rr.random())
    pts = ring(madd(top, (UP, hh * 0.35)), UP, r_ * 1.1, 6, phase=rr.random())
    apex = madd(top, (UP, hh))
    for i in range(6):
        det.face([pts[i], pts[(i + 1) % 6], apex], "M_Plant_Green", out=sub(mul(add(pts[i], pts[(i + 1) % 6]), 0.5), top))


def pot_row(o, P, u0, u1, y_of, w, rr, density=0.55, on_wall_top=False):
    """벽 앞(또는 담 위) 화분 소품 줄: 3~5m 에 1~2묶음(docs/10 4장). 바닥 가운데 = 벽에서 깊이/2.
    벽 앞 화분은 걷는 면 가장자리를 0.15m 까지 넘을 수 있다(docs/10 '걷는 면 폭 1.2m 는 비움')."""
    u = u0 + 0.2
    while u < u1 - 0.3:
        if rr.random() < density:
            name = "styrofoam_planter" if on_wall_top else rr.choice(POTS)
            W, Dd, H = PROP_SIZE[name]
            if u + W > u1:
                break
            uc = u + W / 2
            ww = (w - Dd / 2) if on_wall_top else (w + Dd / 2 - 0.15)
            add_prop(o, name, P.p(uc, y_of(uc), ww), P.out)
            if PROP_GEOMETRY:
                _pot_geom(o.det, P.p(uc, y_of(uc), ww), 0.14, 0.3, "styro" if name == "styrofoam_planter" else "clay", rr)
            u += W + 0.4 + 0.6 * rr.random()
        else:
            u += 0.8


# ---------------------------------------------------------------- 외부 철제 계단
def ext_stair(o, P, u_start, direction, levels_y, width=0.9, w0=0.04, mat="M_Metal_Green"):
    """벽을 따라 지그재그로 오르는 철제 계단. levels_y = [땅, 층1, 층2 ...] (각 층참 높이).
    한 단 높이 ≤ 0.2, 디딤 0.21. 디딤판·옆판·바깥 난간·층참·기둥."""
    wa, wb = w0, w0 + width
    u = u_start
    d = direction
    landing = 1.0
    if hasattr(o, "stairs"):
        run_max = max(max(2, int(math.ceil((levels_y[i + 1] - levels_y[i]) / RISER))) * TREAD for i in range(len(levels_y) - 1))
        o.stairs.append((id(P), min(u_start, u_start + direction * (run_max + landing)), max(u_start, u_start + direction * (run_max + landing))))
    for i in range(len(levels_y) - 1):
        ya, yb = levels_y[i], levels_y[i + 1]
        rise = yb - ya
        n = max(2, int(math.ceil(rise / RISER)))
        tread = TREAD
        run = n * tread
        ua, ub = u, u + d * run
        # 계단(LOD0 = 디딤판, LOD1·2 = 경사판)
        for k in range(n):
            uu = ua + d * (k + 0.5) * tread
            yy = ya + rise * (k + 1) / n
            pbox(o.lod[0], P, uu - tread / 2, uu + tread / 2, yy - 0.04, yy, wa, wb, mat)
        for L in (0, 1, 2):
            mb = o.lod[L]
            for wv in ((wa, wa + 0.05), (wb - 0.05, wb)) if L < 2 else ((wa, wb),):
                a = P.p(ua, ya - 0.05, (wv[0] + wv[1]) / 2)
                b = P.p(ub, yb - 0.05, (wv[0] + wv[1]) / 2)
                seg_box(mb, a, b, wv[1] - wv[0], 0.22 if L < 2 else 0.12, mat)
        # 바깥 난간(경사 손잡이 + 기둥)
        for L in (0, 1):
            mb = o.lod[L]
            seg_box(mb, P.p(ua, ya + 0.95, wb - 0.02), P.p(ub, yb + 0.95, wb - 0.02), 0.04, 0.04, mat)
        nposts = max(2, int(run / 1.0))
        for k in range(nposts + 1):
            t = k / nposts
            uu, yy = ua + d * run * t, ya + rise * t
            pbox(o.lod[0], P, uu - 0.02, uu + 0.02, yy, yy + 0.95, wb - 0.04, wb, mat)
        for k in range(1, int(run / 0.12)):
            t = k * 0.12 / run
            uu, yy = ua + d * run * t, ya + rise * t
            pbox(o.det, P, uu - 0.008, uu + 0.008, yy + 0.05, yy + 0.9, wb - 0.03, wb - 0.015, mat, skip=("+y", "-y"))
        # 층참(끝)
        lu0, lu1 = sorted((ub, ub + d * landing))
        for L in (0, 1, 2):
            pbox(o.lod[L], P, lu0, lu1, yb - 0.08, yb, wa, wb, mat)
        for L in (0, 1):
            mb = o.lod[L]
            pbox(mb, P, lu0, lu1, yb + 0.92, yb + 0.96, wb - 0.04, wb, mat)
            ue = lu1 if d > 0 else lu0
            pbox(mb, P, ue - 0.02, ue + 0.02, yb, yb + 0.96, wa, wb, mat)
            pbox(mb, P, (lu1 - 0.04) if d > 0 else lu0, (lu1) if d > 0 else lu0 + 0.04, yb + 0.92, yb + 0.96, wa, wb, mat)
        # 층참 받침 기둥(아래 땅까지)
        ue = lu1 - 0.05 if d > 0 else lu0 + 0.05
        g = P.ground(ue)
        if yb - g > 0.5:
            for L in (0, 1):
                pbox(o.lod[L], P, ue - 0.05, ue + 0.05, g - 0.2, yb - 0.08, wb - 0.1, wb, mat, skip=("-y",))
        # 다음 계단: 층참 먼 끝에서 반대 방향으로(같은 띠 안 지그재그 — 전체 길이 = 한 계단 + 층참)
        u = ub + d * landing
        d = -d
    return True


def stair_fits(P, u_start, direction, levels_y):
    """외부 계단이 이 면 안에 들어가는지(지그재그라 한 방향 길이 + 층참)."""
    run_max = 0.0
    for i in range(len(levels_y) - 1):
        n = max(2, int(math.ceil((levels_y[i + 1] - levels_y[i]) / RISER)))
        run_max = max(run_max, n * TREAD)
    span = run_max + 1.0
    if direction > 0:
        return u_start >= 0.0 and u_start + span <= P.W
    return u_start <= P.W and u_start - span >= 0.0


# ---------------------------------------------------------------- 담장 위 철망
def barbed(det, P, u0, u1, y, w=0.0, lean=0.18):
    """담장 위 철망: 기울인 기둥(2m 마다) + 줄 3가닥."""
    n = max(1, int((u1 - u0) / 2.0))
    us = [u0 + 0.1 + (u1 - u0 - 0.2) * k / n for k in range(n + 1)]
    tops = []
    for u in us:
        a = P.p(u, y, w)
        b = P.p(u, y + 0.5, w + lean)
        seg_box(det, a, b, 0.035, 0.035, "M_Metal_Grey")
        tops.append(u)
    for f in (0.35, 0.65, 0.95):
        pts = [P.p(u, y + 0.5 * f, w + lean * f) for u in us]
        dense = []
        for i in range(len(pts) - 1):
            for k in range(4):
                p = [pts[i][j] + (pts[i + 1][j] - pts[i][j]) * k / 4 for j in range(3)]
                dense.append((p[0], p[1] - 0.03 * math.sin(math.pi * k / 4), p[2]))
        dense.append(pts[-1])
        tube(det, dense, 0.007, 3, "M_Wire_Black")


# ---------------------------------------------------------------- 전봇대·전선
def utility_pole(o, base, h, toward, r, transformer=False):
    """콘크리트 전봇대(팔각 원뿔대) + 위 가로대(고압 3) + 아래 가로대(저압 2) + (선택) 변압기.
    toward = 전선이 지나가는 방향(가로대는 이에 수직)."""
    t = norm((toward[0], 0.0, toward[2]))
    s = norm(cross(UP, t))
    for L in (0, 1, 2):
        mb = o.lod[L]
        n = (8, 6, 5)[L]
        cyl(mb, madd(base, (UP, -0.3)), UP, 0.19, 0.12, h + 0.3, n, "M_Concrete", caps=(False, True))
        if L <= 1:
            for yy, half in ((h - 0.45, 0.85), (h - 1.45, 0.55)):
                c = madd(base, (UP, yy))
                obox(mb, c, s, UP, t, half, 0.05, 0.05, "M_Metal_Grey")
    det = o.lod[0]
    arms = {}
    for yy, offs in ((h - 0.45, (-0.7, 0.0, 0.7)), (h - 1.45, (-0.42, 0.42))):
        pts = []
        for of in offs:
            c = madd(base, (UP, yy + 0.05), (s, of))
            cyl(det, c, UP, 0.045, 0.03, 0.14, 6, "M_AC_White", caps=(False, True))
            pts.append(madd(c, (UP, 0.14)))
        arms[round(yy, 2)] = pts
    if transformer:
        c = madd(base, (UP, h - 3.0), (t, 0.0), (s, 0.42))
        for L in (0, 1):
            cyl(o.lod[L], c, UP, 0.28, 0.28, 0.9, (10, 6)[L], "M_Metal_Grey", caps=(True, True))
        obox(det, madd(base, (UP, h - 2.6), (s, 0.2)), s, UP, t, 0.2, 0.04, 0.04, "M_Metal_Grey")
    # 발판 못(사다리 손잡이)
    for k in range(6):
        yy = 2.2 + k * 0.45
        ss = s if k % 2 else mul(s, -1.0)
        seg_box(det, madd(base, (UP, yy), (ss, 0.12)), madd(base, (UP, yy), (ss, 0.3)), 0.025, 0.025, "M_Metal_Grey")
    return arms


def wire(mb, a, b, sag, r=0.012, n=4, seg=12):
    tube(mb, catenary(a, b, sag, seg), r, n, "M_Wire_Black", smooth=True)
