# -*- coding: utf-8 -*-
"""유형별 건물 생성기. 각 함수는 (zone, block, Out) 을 받아 Out 에 LOD 메시·디테일·창문 불·간판 자리를 채운다.

공통 규칙(docs/11 2장):
- 발자국은 zone1 상자 그대로(충돌은 Unity 상자 콜라이더를 계속 씀). 땅 높이(2.2m) 아래로는 상자 밖으로 0.3m 넘게
  튀어나오는 것이 없게 하고(골목 걷는 면과 상자 사이 여유 0.3m), 2.4m 위의 발코니·간판·처마·실외기만 더 나간다.
- 층 높이 3.0m, 옥상 난간 0.6m(zone1_gen 의 floors × 3.0 + 0.6). 1층 바닥 = 상자 윗면 − 0.6 − 층수 × 3.0.
- 면 역할(zone.analyse_faces): front = 걷는 면에 붙은 정면(출입문·간판), street = 걷는 면에서 보이는 옆면,
  back = 안 보이는 뒷면(작은 창·실외기·배관), party = 옆 건물에 붙은 면(창 없음).
"""
import math

from .geom import (rng, Frame, madd, add, sub, mul, norm, length, cross, UP, obox, box, cyl, quad, seg_box,
                   clip_poly, MB, pipe)
from .facade import (Out, Panel, Op, pbox, pquad, wall_grid, emit_openings, balcony, canopy, sign_band,
                     sign_projecting, downpipe, REC)
from .roofs import flat_roof, slab_roof, Giwa, wall_giwa_cap
from . import details as D
from .zone import FACES, face_geom, parse_use, tenant_name, RESIDENTIAL

FLOOR = 3.0
PARAPET = 0.6
DIRWORD = {"동쪽": (1, 0), "서쪽": (-1, 0), "남쪽": (0, -1), "북쪽": (0, 1)}


# ---------------------------------------------------------------- 공통 도우미
def rect_face(F, x0, x1, z0, z1, f):
    """틀 F 안 직사각형(로컬)의 면 f 기하: (O(xz), t, n, W)."""
    if f == "+x":
        n = (F.ux[0], F.ux[2]); c = F.p(x1, 0, (z0 + z1) / 2); W = z1 - z0
    elif f == "-x":
        n = (-F.ux[0], -F.ux[2]); c = F.p(x0, 0, (z0 + z1) / 2); W = z1 - z0
    elif f == "+z":
        n = (F.uz[0], F.uz[2]); c = F.p((x0 + x1) / 2, 0, z1); W = x1 - x0
    else:
        n = (-F.uz[0], -F.uz[2]); c = F.p((x0 + x1) / 2, 0, z0); W = x1 - x0
    t = (-n[1], n[0])
    O = (c[0] - t[0] * W / 2, c[2] - t[1] * W / 2)
    return O, t, n, W


def make_panel(zone, F, rect, f, role, bot, top, gs_off=0.3):
    O, t, n, W = rect_face(F, *rect, f)
    gs = []
    for k in range(13):
        u = W * k / 12
        gs.append((u, zone.ground(O[0] + t[0] * u + n[0] * gs_off, O[1] + t[1] * u + n[1] * gs_off)))
    return Panel(O, t, n, W, role, gs, bot, top, face=f)


def block_rect(b):
    return (-b.w / 2, b.w / 2, -b.d / 2, b.d / 2)


def neighbors(f):
    """면 f 의 (오른쪽 이웃, 왼쪽 이웃) — 바깥에서 볼 때."""
    order = ["-z", "+x", "+z", "-x"]   # 위에서 볼 때 시계 방향(yaw + 규약)
    # 바깥에서 -z 면을 보면 오른쪽 = +x 쪽 끝(로컬 +X 가 오른쪽). 시계 방향 다음 면이 오른쪽 이웃.
    i = order.index(f)
    return order[(i + 1) % 4], order[(i - 1) % 4]


def front_face(b):
    for f in FACES:
        if b.roles.get(f) == "front":
            return f
    return "-z"


def free_strip(zone, P, b, need=1.05, u0=0.0, u1=None):
    """면 바깥 need m 띠가 걷는 면·다른 블록과 겹치지 않는가(외부 계단 자리)."""
    u1 = P.W if u1 is None else u1
    for k in range(9):
        u = u0 + (u1 - u0) * k / 8
        for w in (0.3, need * 0.6, need):
            x, _, z = P.p(u, 0, w)
            if zone.walk_dist(x, z) < 0.35:
                return False
            if zone.solid_at(x, z, exclude=b, margin=0.05):
                return False
    return True


def gap_ok(zone, P, u, w):
    x, _, z = P.p(u, 0, w)
    return zone.walk_dist(x, z) > 0.04 and zone.solid_at(x, z, margin=0.02) is None


def lit_val(r):
    return round(r.random(), 3)


def add_anchor(o, P, u, y, w=0.06):
    o.sockets.append(dict(kind="wire_anchor", pos=P.p(u, y, w), n=P.out))


# ================================================================ 다세대 빌라
def villa(zone, b, o):
    r = rng(b.key, "villa")
    fl = b.floors
    F = b.F
    use = parse_use(b.use)
    shop_units = [s for s in use.get(1, []) if not any(k in s for k in RESIDENTIAL)]
    ff = front_face(b)
    rect = block_rect(b)
    y0 = b.top - PARAPET - FLOOR * fl
    Pf0 = make_panel(zone, F, rect, ff, "front", b.bottom, b.top)
    gvar = Pf0.gmax(0, Pf0.W) - Pf0.gmin(0, Pf0.W)
    flat_front = gvar < 1.2 and -0.7 < Pf0.gmin(0, Pf0.W) - y0 < 0.5
    oneroom_ = "원룸" in b.name or "원룸" in b.use
    if shop_units:
        variant = "shop"
    elif fl >= 4 and flat_front and b.w * b.d >= 60 and (oneroom_ or r.random() < 0.4):
        variant = "piloti"   # 원룸 4층 이상 = 1층 필로티 주차(신축 원룸 건물의 흔한 꼴)
    elif fl == 3 and r.random() < 0.45:
        variant = "semi"
    else:
        variant = "plain"
    if variant == "semi":
        y0 -= 1.2
    top = y0 + FLOOR * fl + PARAPET
    y_roof = y0 + FLOOR * fl
    fys = [y0 + FLOOR * k for k in range(fl)]
    oneroom = "원룸" in b.name or "원룸" in b.use
    if oneroom:
        main = r.choice(["M_Tile_Beige", "M_Plaster_Cream", "M_Brick_Red", "M_Tile_Beige"])
    else:
        main = r.choices(["M_Brick_Red", "M_Brick_Brown"], [60, 40])[0]   # docs/10 3-1
    brick = main.startswith("M_Brick")
    band_mat = "M_Plaster_Cream" if brick else "M_Concrete"
    use_bands = brick or r.random() < 0.5
    plinth = "M_Granite" if r.random() < 0.3 else "M_Concrete"
    gf_mat = "M_Granite" if (oneroom and r.random() < 0.45) else main
    if variant == "shop":
        gf_mat = r.choice(["M_Granite", "M_Tile_Beige"])
    rev = main if brick else "M_Concrete"
    bar_mat = "M_AC_White"          # 흰 방범창(docs/10 3-1)
    frame_mat = "M_AC_White"        # 흰 새시 틀
    bands = []
    if use_bands:
        for k in range(1, fl + 1):
            fy = y0 + FLOOR * k
            bands.append((fy - 0.08, fy + 0.12, band_mat))   # 슬래브 끝 띠 0.2
    bands += [(b.bottom - 1, y0, plinth), (y0, y0 + FLOOR, gf_mat), (y0 + FLOOR, top + 1, main)]
    o.notes.append("빌라 %s층 %s · %s" % (fl, variant, main))
    o.meta = dict(variant=variant, main=main, floors=fl, y0=round(y0, 2), top=round(top, 2))

    panels = {f: make_panel(zone, F, rect, f, b.roles[f], b.bottom, top - 0.07) for f in FACES}
    Pf = panels[ff]
    right, left = neighbors(ff)

    # ---- 외부 계단(옥상까지) 자리
    stair = None
    want_stair = ("옥상" in b.use) or (fl >= 2 and r.random() < 0.45)
    if want_stair:
        for f in sorted(FACES, key=lambda f: (b.roles[f] != "street", b.roles[f] != "back")):
            if b.roles[f] in ("front", "party"):
                continue
            P = panels[f]
            levels = [P.ground(1.0)] + [y0 + FLOOR * k for k in range(1, fl)] + [top - 0.05]
            if variant == "semi":
                levels = [P.ground(1.0)] + [y0 + FLOOR * k for k in range(1, fl)] + [top - 0.05]
            for d_, us in ((1, 0.35), (-1, P.W - 0.35)):
                if D.stair_fits(P, us, d_, levels) and free_strip(zone, P, b, 1.05):
                    stair = (f, us, d_, levels)
                    break
            if stair:
                break

    # ---- 면마다 구멍
    stair_bay = None
    for f in FACES:
        P = panels[f]
        role = b.roles[f]
        ops = []
        holes_extra = []
        W = P.W
        if role == "party" or W < 1.8:
            wall_grid_all(o, P, bands, [], main)
            continue
        nb = max(1, int(round(W / r.uniform(2.7, 3.4))))
        bay = W / nb
        st_u = None
        if stair and stair[0] == f:
            run = max(n_ * D.TREAD for n_ in [max(2, int(math.ceil((stair[3][i + 1] - stair[3][i]) / D.RISER))) for i in range(len(stair[3]) - 1)])
            a, bb = sorted((stair[1], stair[1] + stair[2] * (run + 1.0)))
            st_u = (a - 0.2, bb + 0.2)
        if f == ff:
            stair_bay = 0 if nb == 1 else r.choice([0, nb - 1])
        balc_bay = None
        if f == ff and nb >= 2 and fl >= 2 and r.random() < 0.6:
            balc_bay = r.choice([i for i in range(nb) if i != stair_bay])
        for k, fy in enumerate(fys):
            for i in range(nb):
                uc = (i + 0.5) * bay
                if st_u and st_u[0] - 0.5 < uc < st_u[1] + 0.5:
                    continue
                if f == ff and i == stair_bay and variant != "piloti":
                    if k == 0:
                        g = P.ground(uc)
                        dy = g
                        ops.append(Op("door", uc - 0.55, uc + 0.55, dy, dy + 2.15, rev=rev,
                                      leaf=r.choice(["M_Metal_Green", "M_Metal_Green", "M_Wood_Dark"]), frame="M_Metal_Grey"))
                        canopy(o, P, uc - 0.85, uc + 0.85, dy + 2.35, 0.75)
                        side = 1 if uc + 1.4 < W else -1
                        D.meter_box(o, P, uc + side * 0.85, dy + 1.0, "elec")
                        D.meter_box(o, P, uc + side * 1.15, dy + 1.0, "elec")
                        if uc - side * 1.15 > 0.4 and uc - side * 1.15 < W - 0.4:
                            D.mailbox(o, P, uc - side * 1.1, dy + 1.0)
                    # 계단실 세로 창(층 사이 참) 0.6 × 1.6
                    ys = fy + 1.7
                    if ys + 1.6 < y_roof - 0.1 and (k > 0 or variant != "shop"):
                        ops.append(Op("stair", uc - 0.3, uc + 0.3, ys, ys + 1.6, rev=rev, mull=0, lit=lit_val(r), frame=frame_mat))
                    continue
                if k == 0 and variant == "piloti" and f == ff:
                    continue
                if k == 0 and variant == "shop" and f == ff:
                    continue
                lit = lit_val(r)
                bars = k <= 1 or r.random() < 0.5          # 1~2층 100%, 3층 이상 50%(docs/10 3-1)
                if k == 0 and variant == "semi":
                    g = P.gmax(uc - 0.6, uc + 0.6)
                    s0 = max(g + 0.1, fy + 1.25)
                    if fy + 2.15 - s0 > 0.35:
                        ops.append(Op("base", uc - 0.6, uc + 0.6, s0, fy + 2.15, rev=rev, bars=True, bar_mat=bar_mat, lit=lit))
                    continue
                if f == ff and i == balc_bay and k >= 1:
                    bw = bay * 0.62
                    ops.append(Op("balc", uc - bw / 2, uc + bw / 2, fy + 0.08, fy + 2.2, rev=rev, mull=1, lit=lit, transom=fy + 1.95,
                                  frame=frame_mat))
                    balcony(o, P, uc - bw / 2 - 0.25, uc + bw / 2 + 0.25, fy + 0.0, 0.85, rail="M_Metal_Grey")
                    if r.random() < 0.4:
                        D.ac_unit(o, P, uc + bw / 2 - 0.1, fy + 0.02, w_back=0.35, bracket=False)
                    if r.random() < 0.4:
                        D.pot(o, P.p(uc - bw / 2 + 0.05, fy, 0.55), 0.12, 0.25, "clay", r)
                    continue
                if role == "back" and r.random() < 0.35:
                    ops.append(Op("small", uc - 0.3, uc + 0.3, fy + 1.5, fy + 2.0, rev=rev, mull=0, lit=lit, bars=bars and k < 2,
                                  bar_mat=bar_mat, frame=frame_mat))
                    continue
                if role in ("back", "street") and r.random() < 0.12:
                    continue
                ww = bay * (r.uniform(0.5, 0.64) if role == "front" else r.uniform(0.42, 0.56))
                sill = fy + 0.9
                if k == 0:
                    sill = max(sill, P.gmax(uc - ww / 2, uc + ww / 2) + 0.35)
                if fy + 2.25 - sill < 0.5:
                    continue
                ops.append(Op("win", uc - ww / 2, uc + ww / 2, sill, fy + 2.25, rev=rev, bars=bars, bar_mat=bar_mat, lit=lit,
                              frame=frame_mat))
                # 실외기: 창 아래 받침(창의 60% — docs/10 4장). 1층은 안 함(골목 머리 높이)
                if k >= 1 and r.random() < 0.6 and sill - fy > 0.75:
                    D.ac_unit(o, P, uc, fy + 0.12, w_back=0.05)
        # 반지하 창: 비탈 아래쪽에서 드러난 기초(0.8m 이상)
        if variant != "semi":
            for i in range(nb):
                uc = (i + 0.5) * bay
                if st_u and st_u[0] - 0.5 < uc < st_u[1] + 0.5:
                    continue
                g = P.gmax(uc - 0.55, uc + 0.55)
                if y0 - g >= 0.85 and not (f == ff and i == stair_bay):
                    ops.append(Op("base", uc - 0.55, uc + 0.55, g + 0.12, min(y0 - 0.12, g + 0.75), rev="M_Concrete",
                                  bars=True, bar_mat=bar_mat, lit=lit_val(r)))
        # 외부 계단 층참 문
        if stair and stair[0] == f:
            _, us, d_, levels = stair
            run_list = [max(2, int(math.ceil((levels[i + 1] - levels[i]) / D.RISER))) * D.TREAD for i in range(len(levels) - 1)]
            u = us
            dd = d_
            for i in range(len(levels) - 1):
                ub = u + dd * run_list[i]
                lu0, lu1 = sorted((ub, ub + dd * 1.0))
                yb = levels[i + 1]
                if yb < y_roof - 0.5:
                    ops.append(Op("door", lu0 + 0.1, lu1 - 0.05, yb, yb + 2.05, rev=rev, leaf="M_Metal_Green"))
                u = ub + dd * 1.0
                dd = -dd
            D.ext_stair(o, P, us, d_, levels)
        if variant == "shop" and f == ff:
            _storefront_row(zone, o, P, b, r, y0, [u for u in [stair_bay]], nb, shop_units, gf_mat, rev)
        if variant == "piloti" and f == ff:
            holes_extra.append((0.4, W - 0.4, max(y0, P.gmin(0.4, W - 0.4) - 0.02), y0 + 2.7))
            _piloti(o, P, b, r, y0, W, bay, nb, main)
        wall_grid_all(o, P, bands, [op.hole() for op in ops] + holes_extra, main)
        emit_openings(o, P, ops)
        # 선홈통·전선 받는 자리
        if role in ("front", "street"):
            if r.random() < 0.7:
                uu = 0.12 if r.random() < 0.5 else W - 0.12
                downpipe(o.det, P, uu, P.ground(uu) - 0.1, top - 0.15)
            add_anchor(o, P, 0.35 if r.random() < 0.5 else W - 0.35, y0 + FLOOR + 2.3)

    # ---- 가스관(벽 따라 꺾임): 정면 옆 면 중 보이는 쪽
    gface = None
    for cand in (right, left):
        if b.roles[cand] in ("street", "back") and (not stair or stair[0] != cand):
            gface = cand
            break
    if gface is None and (not stair or stair[0] != ff):
        gface = ff
    if gface:
        P = panels[gface]
        if gface == right:
            ur, away, wrap = 0.45, 1, (Pf, 1.2)
        elif gface == left:
            ur, away, wrap = P.W - 0.45, -1, (Pf, 1.2)
        else:
            ur, away, wrap = (P.W - 0.4, -1, None) if stair_bay == 0 else (0.4, 1, None)
        gy = P.ground(ur) + 1.0
        D.gas_lines(o, P, ur, gy, [fy for fy in fys if fy + 0.5 > gy], away, r.uniform(1.0, 2.0), wrap=wrap)
    # ---- 옥상
    hw, hd = b.w / 2, b.d / 2
    flat_roof(o, F, hw, hd, y_roof, top)
    if r.random() < 0.4:
        _roof_rail(o, F, hw, hd, top)
    _rooftop(zone, o, b, r, F, hw, hd, y_roof, stair is not None, fl)
    # 입구 화분
    if r.random() < 0.6:
        uc = (stair_bay + 0.5) * (Pf.W / max(1, int(round(Pf.W / 3.0)))) if stair_bay is not None else Pf.W / 2
        for side in (-1, 1):
            u0 = uc + side * 0.9
            if 0.3 < u0 < Pf.W - 0.3 and gap_ok(zone, Pf, u0, 0.2):
                D.pot_row(o, Pf, min(u0, u0 + side * 1.0), max(u0, u0 + side * 1.0), Pf.ground, 0.17, r, 0.7)


def wall_grid_all(o, P, bands, holes, default, levels=(0, 1, 2)):
    for L in levels:
        wall_grid(o.lod[L], P, bands, holes, default=default)


def _storefront_row(zone, o, P, b, r, y0, skip_bays, nb, units, pier, rev):
    """빌라 1층 상가(계단 칸 빼고 나머지)."""
    bay = P.W / nb
    spans = []
    cur = None
    for i in range(nb):
        if i in skip_bays:
            if cur:
                spans.append(cur)
                cur = None
            continue
        cur = [i * bay, (i + 1) * bay] if cur is None else [cur[0], (i + 1) * bay]
    if cur:
        spans.append(cur)
    tenant = tenant_name(units[0]) if units else "상가"
    for a, c in spans:
        a2, c2 = a + 0.25, c - 0.25
        if c2 - a2 < 1.2:
            continue
        _storefront(zone, o, P, b, r, y0, a2, c2, tenant, None, rev)


def _storefront(zone, o, P, b, r, y0, u0, u1, tenant, door_u, rev, sign=True, glass_door=True):
    """상가 한 칸: 유리 + 유리문 + 셔터 박스 + 간판 띠(Sign)."""
    g = P.gmax(u0, u1)
    yt = y0 + 2.45
    ys = max(y0 + 0.3, g + 0.12)
    if door_u is None:
        door_u = u0 + 0.75 if r.random() < 0.5 else u1 - 0.75
    dw = 1.0 if (u1 - u0) < 3.5 else 1.5
    d0, d1 = max(u0, door_u - dw / 2), min(u1, door_u + dw / 2)
    ops = []
    gd = P.ground((d0 + d1) / 2)
    ops.append(Op("door", d0, d1, gd, yt, rev="M_Metal_Grey", leaf="glass" if glass_door else "M_Metal_Grey"))
    for a, c in ((u0, d0), (d1, u1)):
        if c - a > 0.35:
            ops.append(Op("shop", a, c, max(ys, P.gmax(a, c) + 0.12), yt, rev="M_Metal_Grey", frame="M_Metal_Grey"))
    for op in ops:
        o._pending_holes.append((P, op.hole()))
    emit_openings(o, P, ops)
    # 셔터 박스 + 레일
    for L in (0, 1, 2):
        pbox(o.lod[L], P, u0 - 0.05, u1 + 0.05, yt + 0.02, yt + 0.4, 0.0, 0.26, "M_Metal_Grey", skip=("-z",))
    for uu in (u0 + 0.02, u1 - 0.06):
        pbox(o.lod[0], P, uu, uu + 0.04, ys - 0.1, yt, 0.0, 0.05, "M_Metal_Grey", skip=("-z",))
    if sign:
        sign_band(o, P, u0, u1, yt + 0.45, yt + 1.32, tenant, b, depth=0.25)
    o.sockets.append(dict(kind="shop_light", pos=P.p((u0 + u1) / 2, yt - 0.3, -0.6), n=P.out, tenant=tenant))


def _piloti(o, P, b, r, y0, W, bay, nb, main):
    """필로티 주차(1층 트임): 기둥 + 천장 + 안쪽 벽(현관) + 바닥 + 앞 철책(상자 콜라이더 자리에 맞춤)."""
    Dp = min(5.6, (b.d if P.face in ("+z", "-z") else b.w) - 1.8)
    yc = y0 + 2.7
    y0 = max(y0, P.gmin(0.4, W - 0.4) - 0.02)   # 바닥 = 앞 인도 가장 낮은 곳(비탈이면 높은 쪽은 턱 하나)
    for L in (0, 1, 2):
        mb = o.lod[L]
        quad(mb, P.p(0.2, yc, 0), P.p(W - 0.2, yc, 0), P.p(W - 0.2, yc, -Dp), P.p(0.2, yc, -Dp), "M_Concrete", out=(0, -1, 0))
        quad(mb, P.p(0.2, y0 + 0.01, 0), P.p(W - 0.2, y0 + 0.01, 0), P.p(W - 0.2, y0 + 0.01, -Dp), P.p(0.2, y0 + 0.01, -Dp),
             "M_Concrete", out=UP)
        # 안쪽 벽(현관 면)
        pd = Op("door", W / 2 - 0.6, W / 2 + 0.6, y0, y0 + 2.2)
        Pin = Panel(P.p(0, 0, -Dp)[0::2], P.t, P.n, W, "front", [(0, y0), (W, y0)], y0, yc)
        wall_grid(mb, Pin, [(y0 - 1, yc + 1, "M_Plaster_White")], [pd.hole()], default="M_Plaster_White", u_lo=0.2, u_hi=W - 0.2)
        # 옆 안쪽 벽
        for uu, sg in ((0.2, 1), (W - 0.2, -1)):
            quad(mb, P.p(uu, y0, -0.4), P.p(uu, y0, -Dp), P.p(uu, yc, -Dp), P.p(uu, yc, -0.4), "M_Concrete", out=mul(P.t3, sg))
        for uu, sg in ((0.4, 1), (W - 0.4, -1)):
            quad(mb, P.p(uu, y0, 0), P.p(uu, y0, -0.4), P.p(uu, yc, -0.4), P.p(uu, yc, 0), main, out=mul(P.t3, sg))
            a, c = sorted((uu, uu - sg * 0.2))
            quad(mb, P.p(a, y0, -0.4), P.p(c, y0, -0.4), P.p(c, yc, -0.4), P.p(a, yc, -0.4), "M_Concrete", out=mul(P.n3, -1))
        quad(mb, P.p(0.4, yc, 0), P.p(W - 0.4, yc, 0), P.p(W - 0.4, y0 + 2.7, 0), P.p(0.4, y0 + 2.7, 0), main, out=P.out)
        for k in range(1, nb):
            u = k * bay
            pbox(mb, P, u - 0.2, u + 0.2, y0, yc, -0.45, -0.05, "M_Concrete", skip=("+y", "-y"))
    Pin = Panel(P.p(0, 0, -Dp)[0::2], P.t, P.n, W, "front", [(0, y0), (W, y0)], y0, yc)
    emit_openings(o, Pin, [Op("door", W / 2 - 0.6, W / 2 + 0.6, y0, y0 + 2.2, rev="M_Plaster_White", leaf="glass")])
    # 앞 철책(높이 1.5): 기둥 + 위아래 가로대 + 세로살 — 가운데 1.3m 는 통로(2026-10-24 3차: 걸어 들어갈 수 있게, 콜라이더는 Unity EnvSetup 이 meta 로 다시 짬)
    mg = "M_Metal_Green"
    hgt = 1.5
    g0, g1 = W / 2 - 0.65, W / 2 + 0.65
    spans = [(0.4, g0), (g1, W - 0.4)]
    for L in (0, 1, 2):
        mb = o.lod[L]
        for a, c in spans:
            pbox(mb, P, a, c, y0 + hgt - 0.05, y0 + hgt, -0.08, -0.02, mg)
            pbox(mb, P, a, c, y0 + 0.08, y0 + 0.13, -0.08, -0.02, mg)
        posts = sorted(set([min(W - 0.45, max(0.45, k * bay)) for k in range(nb + 1)] + [g0 - 0.04, g1 + 0.04]))
        for u in posts:
            if g0 - 0.1 < u < g1 + 0.1 and u not in (g0 - 0.04, g1 + 0.04):
                continue
            pbox(mb, P, u - 0.04, u + 0.04, y0, y0 + hgt, -0.09, -0.01, mg)
        if L < 2:
            step = 0.15 if L == 0 else 0.3
            n = int((W - 0.9) / step)
            for k in range(1, n):
                u = 0.45 + (W - 0.9) * k / n
                if g0 - 0.02 < u < g1 + 0.02:
                    continue
                pbox(mb, P, u - 0.012, u + 0.012, y0 + 0.13, y0 + hgt - 0.05, -0.06, -0.035, mg, skip=("+y", "-y"))
    o.sockets.append(dict(kind="ceiling_light", pos=P.p(W / 2, yc - 0.05, -Dp / 2), n=(0, -1, 0)))
    o.meta["piloti_depth"] = round(Dp, 2)
    # Unity 콜라이더용(월드 좌표): 기둥(앞줄, 중심) · 앞선·뒷선 끝점 · 통로 가운데 · 천장 높이 · 바닥 · 바깥 법선
    o.meta["piloti_cols"] = [[round(c, 3) for c in P.p(k * bay, y0, -0.25)] for k in range(1, nb)]
    o.meta["piloti_front"] = [[round(c, 3) for c in P.p(0, y0, 0)], [round(c, 3) for c in P.p(W, y0, 0)]]
    o.meta["piloti_back"] = [[round(c, 3) for c in P.p(0, y0, -Dp)], [round(c, 3) for c in P.p(W, y0, -Dp)]]
    o.meta["piloti_gate"] = [round(c, 3) for c in P.p(W / 2, y0, 0)]
    o.meta["piloti_gap"] = 1.3
    o.meta["piloti_yc"] = round(yc, 3)
    o.meta["piloti_y0"] = round(y0, 3)
    o.meta["piloti_out"] = [round(c, 4) for c in P.out]


def _roof_rail(o, F, hw, hd, top, h=0.5, mat="M_Metal_Green"):
    """옥상 난간 위 철 난간(난간 0.6 + 0.5 = 1.1, docs/10 3-1)."""
    for L in (0, 1):
        mb = o.lod[L]
        ix, iz = hw - 0.1, hd - 0.1
        for (x0, x1, z0, z1) in ((-ix, ix, iz - 0.02, iz + 0.02), (-ix, ix, -iz - 0.02, -iz + 0.02),
                                 (ix - 0.02, ix + 0.02, -iz, iz), (-ix - 0.02, -ix + 0.02, -iz, iz)):
            box(mb, F, x0, x1, top + h - 0.04, top + h, z0, z1, mat)
        if L == 0:
            for sx, sz, L_, along in ((1, 1, 2 * ix, "x"), (1, -1, 2 * ix, "x"), (1, 1, 2 * iz, "z"), (-1, 1, 2 * iz, "z")):
                n = max(2, int(L_ / 1.5))
                for k in range(n + 1):
                    t = -L_ / 2 + L_ * k / n
                    x, z = (t, sz * iz) if along == "x" else (sx * ix, t)
                    box(mb, F, x - 0.02, x + 0.02, top, top + h, z - 0.02, z + 0.02, mat, skip=("-y",))


def _rooftop(zone, o, b, r, F, hw, hd, y_roof, has_ext_stair, fl):
    ix, iz = hw - 0.5, hd - 0.5
    slots = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    r.shuffle(slots)
    used = 0
    if not has_ext_stair and fl >= 3 and r.random() < 0.6 and ix > 1.6 and iz > 1.6:
        sx, sz = slots[used]
        used += 1
        x0, x1 = sorted((sx * ix, sx * (ix - 2.3)))
        z0, z1 = sorted((sz * iz, sz * (iz - 2.7)))
        D.stair_house(o, F, x0, x1, z0, z1, y_roof, 2.4, wall=r.choice(["M_Plaster_White", "M_Concrete"]),
                      door_side="-z" if sz > 0 else "+z")
    if True:
        sx, sz = slots[used]
        used += 1
        D.water_tank(o, F, sx * (ix - 0.7), sz * (iz - 0.7), y_roof)
    n_ac = r.randint(1, 3)
    for k in range(n_ac):
        if used >= 4:
            break
        sx, sz = slots[used]
        x = sx * (ix - 0.5) - sx * 0.95 * k
        z = sz * (iz - 0.1)
        Pr = Panel(F.p(x, 0, z)[0::2], (F.ux[0] * sz, F.ux[2] * sz), (F.uz[0] * -sz, F.uz[2] * -sz), 0.0,
                   "back", [], y_roof, y_roof + 1)
        D.ac_unit(o, Pr, 0.0, y_roof + 0.05, w_back=0.05, bracket=False)
    if "장독" in b.use:
        D.jars(o, F, -ix * 0.35, iz * 0.45, y_roof, 6, r)
    if "평상" in b.use:
        D.pyeongsang(o, F, ix * 0.3, -iz * 0.35, y_roof)
    if "빨랫줄" in b.use:
        D.clothesline(o.det, F.p(-ix * 0.8, 0, -iz * 0.1), F.p(ix * 0.6, 0, -iz * 0.1), y_roof, 4)
    elif r.random() < 0.45:
        D.laundry_rack(o, F, 0.0, 0.0, y_roof, yaw_x=r.random() < 0.5, r=r)
    if r.random() < 0.35:
        # 옥상 스티로폼 텃밭
        for k in range(r.randint(2, 4)):
            D.pot(o, F.p(-ix + 0.4 + 0.5 * k, y_roof, -iz + 0.35), 0.18, 0.25, "styro", r)


# ================================================================ 상가 건물
def shop(zone, b, o):
    r = rng(b.key, "shop")
    fl = b.floors or 2
    F = b.F
    ff = front_face(b)
    rect = block_rect(b)
    y0 = b.top - PARAPET - FLOOR * fl
    top = b.top
    y_roof = y0 + FLOOR * fl
    use = parse_use(b.use)
    units = use.get(1, ["상가"])
    main = r.choices(["M_Tile_Beige", "M_Plaster_Cream", "M_Brick_Brown"], [50, 30, 20])[0]   # docs/10 3-2
    pier = r.choice(["M_Granite", "M_Tile_Beige", "M_Concrete"])
    if pier == main:
        pier = "M_Granite"
    rev = "M_Concrete"
    bar_mat = "M_Metal_Grey"
    bands = [(b.bottom - 1, y0 + 0.3, "M_Granite"), (y0 + 0.3, y0 + FLOOR + 0.9, pier), (y0 + FLOOR + 0.9, top + 1, main)]
    if r.random() < 0.6:
        bands.insert(0, (y_roof - 0.1, y_roof + 0.15, "M_Concrete"))
    o.meta = dict(variant="shop", main=main, floors=fl, y0=round(y0, 2), top=round(top, 2))
    o._pending_holes = []
    panels = {f: make_panel(zone, F, rect, f, b.roles[f], b.bottom, top - 0.07) for f in FACES}
    Pf = panels[ff]
    right, left = neighbors(ff)
    # 층별 용도
    floor_use = {}
    for k in range(2, fl + 1):
        segs = use.get(k) or use.get(99) or ["주택"]
        floor_use[k] = segs[0]
    # ---- 1층 칸 나누기
    W = Pf.W
    tvec = Pf.t
    stair_doors, unit_doors = [], []
    for lm in zone.landmarks:
        if lm["kind"] != "door":
            continue
        x, _, z = lm["pos"]
        du = (x - Pf.O[0]) * tvec[0] + (z - Pf.O[1]) * tvec[1]
        dw = (x - Pf.O[0]) * Pf.n[0] + (z - Pf.O[1]) * Pf.n[1]
        if -0.5 <= du <= W + 0.5 and abs(dw) < 1.3:
            if "계단" in lm["name"] or "2층" in lm["name"]:
                stair_doors.append((max(0.8, min(W - 0.8, du)), lm["name"]))
            else:
                unit_doors.append((max(0.7, min(W - 0.7, du)), lm["name"]))

    def unit_key(seg):
        for wd, v in DIRWORD.items():
            if wd in seg:
                return v[0] * tvec[0] + v[1] * tvec[1]
        return 0.0
    units_sorted = sorted(units, key=unit_key)
    ops_front = []
    reserved = []
    for su, nm in stair_doors:
        reserved.append((su - 0.7, su + 0.7))
        g = Pf.ground(su)
        ops_front.append(Op("door", su - 0.55, su + 0.55, g, g + 2.2, rev=rev, leaf="M_Metal_Grey"))
        o.signs_extra = True
        sign_band(o, Pf, su + 0.62, su + 0.98, g + 0.9, g + 2.1, tenant_name(floor_use.get(2, nm)), b, kind="plate")
        D.meter_box(o, Pf, su - 0.95, g + 1.2, "elec")
    free = [(0.3, W - 0.3)]
    for a, c in reserved:
        nf = []
        for x0, x1 in free:
            if c <= x0 or a >= x1:
                nf.append((x0, x1))
            else:
                if a - x0 > 0.3:
                    nf.append((x0, a))
                if x1 - c > 0.3:
                    nf.append((c, x1))
        free = nf
    # 칸: 자유 구간들을 이어서 n 등분
    total = sum(x1 - x0 for x0, x1 in free)
    n = len(units_sorted)
    per = total / n
    segs = []
    acc = 0.0
    for x0, x1 in free:
        u = x0
        while u < x1 - 1e-6 and len(segs) < n:
            take = min(x1 - u, per * (len(segs) + 1) - acc)
            segs.append((u, u + take))
            acc += take
            u += take
            if acc >= per * len(segs) - 1e-6:
                pass
    # 칸 사이 기둥 0.3
    for i, (u0, u1) in enumerate(segs[:n]):
        a, c = u0 + (0.15 if i > 0 else 0.0), u1 - (0.15 if i < n - 1 else 0.0)
        seg = units_sorted[i]
        door_u = None
        for du, nm in unit_doors:
            if a + 0.5 <= du <= c - 0.5:
                door_u = du
        _storefront(zone, o, Pf, b, r, y0, a, c, tenant_name(seg), door_u, rev)
        if "면도 유리" in seg or "모서리" in seg:
            # 모서리 상가: 이름에 적힌 방향의 옆 면도 유리
            best = None
            for nf in (right, left):
                Pn = panels[nf]
                v = None
                for wd, dv in DIRWORD.items():
                    if wd in seg:
                        v = dv
                sc = (Pn.n[0] * v[0] + Pn.n[1] * v[1]) if v else 0
                if best is None or sc > best[0]:
                    best = (sc, nf)
            Ps = panels[best[1]]
            L = min(4.0, Ps.W - 1.0)
            ua, ub = (0.3, 0.3 + L) if best[1] == right else (Ps.W - 0.3 - L, Ps.W - 0.3)
            _storefront(zone, o, Ps, b, r, y0, ua, ub, tenant_name(seg), None, rev, sign=True, glass_door=False)
            o.meta["corner_glass"] = best[1]
    # ---- 돌출 간판: 근처 랜드마크 간판 자리 / 위층 업소
    for lm in zone.landmarks:
        if lm["kind"] != "sign":
            continue
        x, y, z = lm["pos"]
        lx, lz = F.local(x, z)
        if abs(lx) <= b.w / 2 + 1.5 and abs(lz) <= b.d / 2 + 1.5 and y - zone.ground(x, z) > 1.5:
            du = (x - Pf.O[0]) * tvec[0] + (z - Pf.O[1]) * tvec[1]
            du = max(0.15, min(W - 0.15, du))
            h = lm.get("height", 1.2)
            sign_projecting(o, Pf, du, y, y + h, lm["name"].replace(" 간판", ""), b, reach=0.9)
            o.meta["landmark_sign"] = lm["name"]
    for k in range(2, fl + 1):
        seg = floor_use[k]
        if not any(x in seg for x in RESIDENTIAL):
            fy = y0 + FLOOR * (k - 1)
            sign_band(o, Pf, 0.6, W - 0.6, fy + 0.12, fy + 0.72, tenant_name(seg), b)
            if r.random() < 0.7 and fl >= 3 and "landmark_sign" not in o.meta:
                uu = 0.2 if r.random() < 0.5 else W - 0.2
                sign_projecting(o, Pf, uu, fy + 0.9, fy + 0.9 + min(2.4, FLOOR * (fl - k + 1) - 1.2), tenant_name(seg), b)
    # ---- 위층 창(모든 면)
    for f in FACES:
        P = panels[f]
        role = b.roles[f]
        ops = list(ops_front) if f == ff else []
        if role == "party" or P.W < 1.8:
            holes = [h for (PP, h) in o._pending_holes if PP is P]
            wall_grid_all(o, P, bands, holes, main)
            continue
        nb = max(1, int(round(P.W / r.uniform(2.5, 3.2))))
        bay = P.W / nb
        for k in range(2, fl + 1):
            fy = y0 + FLOOR * (k - 1)
            seg = floor_use[k]
            comm = not any(x in seg for x in RESIDENTIAL)
            for i in range(nb):
                uc = (i + 0.5) * bay
                lit = lit_val(r)
                if comm and role in ("front", "street"):
                    ww = bay * 0.84
                    ops.append(Op("win", uc - ww / 2, uc + ww / 2, fy + 0.85, fy + 2.5, rev=rev, mull=2, lit=lit))
                elif role == "back" and r.random() < 0.4:
                    ops.append(Op("small", uc - 0.3, uc + 0.3, fy + 1.45, fy + 2.0, rev=rev, mull=0, lit=lit))
                else:
                    ww = bay * r.uniform(0.45, 0.6)
                    ops.append(Op("win", uc - ww / 2, uc + ww / 2, fy + 0.9, fy + 2.25, rev=rev, lit=lit,
                                  bars=(k == 2 and r.random() < 0.35), bar_mat=bar_mat))
                    if role != "front" and r.random() < 0.3:
                        ua = uc + (ww / 2 + 0.55) * (1 if r.random() < 0.5 else -1)
                        if 0.5 < ua < P.W - 0.5:
                            D.ac_unit(o, P, ua, fy + 0.3)
        # 1층 옆·뒷면 작은 창(창고·주방)
        if f != ff and role in ("back", "street") and not o.meta.get("corner_glass") == f:
            for i in range(nb):
                if r.random() < (0.9 if role == "street" else 0.4):
                    uc = (i + 0.5) * bay
                    g = P.gmax(uc - 0.4, uc + 0.4)
                    if y0 + 2.2 - max(g + 0.8, y0 + 1.4) > 0.4:
                        ops.append(Op("small", uc - 0.4, uc + 0.4, max(g + 0.8, y0 + 1.4), y0 + 2.2, rev=rev, mull=0,
                                      bars=True, bar_mat=bar_mat, lit=lit_val(r)))
        holes = [op.hole() for op in ops] + [h for (PP, h) in o._pending_holes if PP is P]
        wall_grid_all(o, P, bands, holes, main)
        emit_openings(o, P, [op for op in ops if op not in ops_front] if f == ff else ops)
        if role in ("front", "street"):
            add_anchor(o, P, 0.3 if r.random() < 0.5 else P.W - 0.3, y0 + FLOOR + 2.3)
            if r.random() < 0.6:
                uu = 0.1 if r.random() < 0.5 else P.W - 0.1
                downpipe(o.det, P, uu, P.ground(uu) - 0.1, top - 0.15)
    emit_openings(o, Pf, ops_front)
    # 가스관: 옆면
    for cand in (right, left):
        if b.roles[cand] in ("street", "back") and o.meta.get("corner_glass") != cand:
            P = panels[cand]
            ur, away = (0.5, 1) if cand == right else (P.W - 0.5, -1)
            gy = y0 + FLOOR + 0.2
            D.gas_lines(o, P, ur, gy, [y0 + FLOOR * k for k in range(1, fl)], away, 1.4,
                        wrap=(Pf, 0.9) if fl >= 3 else None)
            break
    # 옆벽 실외기 무더기(3~8, docs/10 3-2): 걷는 면에서 안 보이는 옆·뒷면 위쪽
    for f in FACES:
        if b.roles[f] in ("back", "party") and f != ff:
            P = panels[f]
            if b.roles[f] == "party" or P.W < 3.0:
                continue
            n = r.randint(3, 6)
            for k in range(n):
                uu = 0.7 + (P.W - 1.4) * (k % 3) / 2.0
                yy = y0 + FLOOR + 0.3 + 1.1 * (k // 3)
                if yy + 0.7 < top - 0.3:
                    D.ac_unit(o, P, uu, yy, w_back=0.05)
            break
    hw, hd = b.w / 2, b.d / 2
    flat_roof(o, F, hw, hd, y_roof, top)
    _rooftop(zone, o, b, r, F, hw, hd, y_roof, False, fl)


# ================================================================ 단독주택
def house(zone, b, o):
    r = rng(b.key, "house")
    fl = b.floors or 1
    F = b.F
    ff = front_face(b)
    y0 = b.top - PARAPET - FLOOR * fl
    right, left = neighbors(ff)
    depth_f = b.d if ff in ("+z", "-z") else b.w
    width_f = b.w if ff in ("+z", "-z") else b.d
    yard = min(1.5, depth_f - 3.2) if (depth_f >= 4.6 and width_f >= 3.0) else 0.0
    x0, x1, z0, z1 = block_rect(b)
    body = [x0, x1, z0, z1]
    if yard > 0:
        if ff == "+z":
            body[3] -= yard
        elif ff == "-z":
            body[2] += yard
        elif ff == "+x":
            body[1] -= yard
        else:
            body[0] += yard
    main = r.choice(["M_Plaster_Cream", "M_Plaster_White", "M_Brick_Red", "M_Plaster_Cream", "M_Brick_Brown"])
    wall_mat = r.choices(["M_Plaster_Cream", "M_Brick_Red", "M_Concrete"], [55, 30, 15])[0]   # docs/10 3-3 시멘트 블록 + 미장
    old = r.random() < 0.45
    giwa_ok = fl == 1 and min(body[1] - body[0], body[3] - body[2]) >= 2.6
    roof_kind = "giwa" if giwa_ok else "slab"   # 단층 = 시멘트 기와(우진각) 지붕, 2층 = 슬래브(1970~80년대 골목집 꼴)
    rev = "M_Concrete"
    bar_mat = "M_Metal_Rust" if old else "M_Metal_Grey"
    y_body_top = y0 + FLOOR * fl
    o.meta = dict(variant="yard" if yard else "plain", main=main, roof=roof_kind, floors=fl, y0=round(y0, 2))
    plinth = "M_Concrete"
    bands = [(b.bottom - 1, y0 + 0.25, plinth), (y0 + 0.25, y_body_top + 2, main)]
    wtop_extra = 0.35 if roof_kind == "slab" else 0.0
    top_body = y_body_top + (0.0 if roof_kind == "giwa" else 0.0)
    # 몸채 면
    panels = {}
    for f in FACES:
        role = b.roles[f]
        if yard and f == ff:
            role = "yard"
        panels[f] = make_panel(zone, F, tuple(body), f, role, b.bottom, top_body)
    # 담장(앞 + 양옆 마당 구간)
    if yard:
        Pw = make_panel(zone, F, block_rect(b), ff, "front", b.bottom, 0)
        wt = Pw.gmax(0, Pw.W) + 1.85
        Pw.top = wt
        gw = min(r.uniform(2.0, 2.4), Pw.W - 1.0)
        gu = r.uniform(0.5 + gw / 2, Pw.W - 0.5 - gw / 2) if Pw.W > gw + 1.2 else Pw.W / 2
        gg = Pw.ground(gu)
        gate_top = min(wt + 0.15, gg + 2.15)
        gate_top = gg + 2.1
        gate = Op("gate", gu - gw / 2, gu + gw / 2, gg, gate_top, rev=wall_mat,
                  leaf=r.choice(["M_Metal_Green", "M_Metal_Green", "M_Metal_Green", "M_Wood_Dark", "M_Metal_Grey"]), depth=0.12)
        wb = [(b.bottom - 1, wt + 1, wall_mat)]
        wall_grid_all(o, Pw, wb, [gate.hole()], wall_mat)
        emit_openings(o, Pw, [gate])
        # 대문 기둥·지붕(문 위가 담보다 높으면)
        if gg + 2.15 > wt - 0.05:
            for L in (0, 1, 2):
                for uu in (gu - gw / 2 - 0.25, gu + gw / 2):
                    pbox(o.lod[L], Pw, uu, uu + 0.25, Pw.bot, gg + 2.35, -0.2, 0.05, wall_mat)
                pbox(o.lod[L], Pw, gu - gw / 2 - 0.4, gu + gw / 2 + 0.4, gg + 2.35, gg + 2.47, -0.5, 0.35, "M_Concrete")
        _wall_top(o, Pw, 0.0, Pw.W, wt, 0.2, r, old)
        D.meter_box(o, Pw, gu + gw / 2 + 0.45 if gu + gw / 2 + 0.6 < Pw.W else gu - gw / 2 - 0.45, gg + 1.2, "gas")
        # 마당 바닥
        for L in (0, 1, 2):
            quad(o.lod[L], Pw.p(0.2, gg + 0.02, -0.2), Pw.p(Pw.W - 0.2, gg + 0.02, -0.2), Pw.p(Pw.W - 0.2, gg + 0.02, -yard),
                 Pw.p(0.2, gg + 0.02, -yard), "M_Paving", out=UP)
        # 옆 담(마당 구간)
        for nf, side in ((right, 1), (left, -1)):
            Ps = make_panel(zone, F, block_rect(b), nf, b.roles[nf], b.bottom, wt)
            ua, ub = (0.0, yard) if side == 1 else (Ps.W - yard, Ps.W)
            for L in (0, 1, 2):
                wall_grid(o.lod[L], Ps, wb, [], default=wall_mat, u_lo=ua, u_hi=ub)
            _wall_top(o, Ps, ua, ub, wt, 0.2, r, old, ends=False)
        # 담 안쪽 면(마당에서 보이는 쪽 — 위에서 내려다볼 때)
        for L in (0, 1):
            quad(o.lod[L], Pw.p(0, Pw.bot, -0.2), Pw.p(Pw.W, Pw.bot, -0.2), Pw.p(Pw.W, wt - 0.06, -0.2), Pw.p(0, wt - 0.06, -0.2),
                 wall_mat, out=mul(Pw.n3, -1))
        if r.random() < 0.6:
            for side in (-1, 1):
                ua = gu + side * (gw / 2 + 0.4)
                ub = ua + side * 1.6
                a, c = sorted((ua, ub))
                a, c = max(0.2, a), min(Pw.W - 0.2, c)
                if c - a > 0.4 and gap_ok(zone, Pw, (a + c) / 2, 0.2):
                    D.pot_row(o, Pw, a, c, Pw.ground, 0.17, r, 0.65)
        if r.random() < 0.35:
            D.barbed(o.det, Pw, 0.1, Pw.W - 0.1, wt, -0.1)
            o.meta["barbed"] = True
        elif r.random() < 0.35:
            D.pot_row(o, Pw, 0.3, Pw.W - 0.3, lambda u: wt, -0.1, r, 0.5, on_wall_top=True)
    # 몸채
    for f in FACES:
        P = panels[f]
        role = P.role
        ops = []
        W = P.W
        if W < 1.5 or role == "party":
            wall_grid_all(o, P, bands, [], main)
            continue
        nb = max(1, int(round(W / r.uniform(2.4, 3.2))))
        bay = W / nb
        door_bay = r.randrange(nb) if (f == ff) else None
        for k in range(fl):
            fy = y0 + FLOOR * k
            for i in range(nb):
                uc = (i + 0.5) * bay
                if k == 0 and i == door_bay:
                    g = P.ground(uc) if not yard else y0
                    dg = max(g, y0)
                    ops.append(Op("door", uc - 0.45, uc + 0.45, dg, dg + 2.1, rev=rev,
                                  leaf=r.choice(["M_Wood_Dark", "M_Metal_Green", "glass"])))
                    if fl >= 1:
                        canopy(o, P, uc - 0.7, uc + 0.7, dg + 2.25, 0.6, mat="M_Concrete")
                    continue
                if role in ("back", "street") and r.random() < 0.2:
                    continue
                lit = lit_val(r)
                if role == "back" and r.random() < 0.35:
                    ops.append(Op("small", uc - 0.3, uc + 0.3, fy + 1.45, fy + 2.0, rev=rev, mull=0, lit=lit,
                                  bars=k == 0, bar_mat=bar_mat))
                    continue
                ww = min(bay - 0.5, bay * r.uniform(0.5, 0.65))
                sill = max(fy + 0.9, P.gmax(uc - ww / 2, uc + ww / 2) + 0.4)
                if fy + 2.2 - sill < 0.5 or ww < 0.5:
                    continue
                ops.append(Op("win", uc - ww / 2, uc + ww / 2, sill, fy + 2.2, rev=rev, lit=lit,
                              bars=(k == 0 and r.random() < 0.85) or (k == 1 and r.random() < 0.3), bar_mat=bar_mat))
                if role != "front" and r.random() < 0.25:
                    ua = uc + (ww / 2 + 0.5) * (1 if r.random() < 0.5 else -1)
                    if 0.45 < ua < W - 0.45:
                        if k == 0:
                            D.ac_unit(o, P, ua, P.ground(ua) + 0.05, w_back=0.04, bracket=False)
                        else:
                            D.ac_unit(o, P, ua, fy + 0.3)
        wall_grid_all(o, P, bands, [op.hole() for op in ops], main)
        emit_openings(o, P, ops)
        if role in ("front", "street", "yard"):
            add_anchor(o, P, 0.3 if r.random() < 0.5 else W - 0.3, y_body_top - 0.4)
    # 지붕
    bx0, bx1, bz0, bz1 = body
    Fb = Frame(*F.p((bx0 + bx1) / 2, 0, (bz0 + bz1) / 2)[0::2], F.yaw)
    hw, hd = (bx1 - bx0) / 2, (bz1 - bz0) / 2
    if roof_kind == "slab":
        slab_roof(o, Fb, hw, hd, y_body_top, over=0.22, t=0.18, parapet=0.25 if r.random() < 0.6 else 0.0)
        ytop = y_body_top + 0.18
        if r.random() < 0.55:
            D.water_tank(o, Fb, (hw - 0.8) * (1 if r.random() < 0.5 else -1), (hd - 0.8) * (1 if r.random() < 0.5 else -1),
                         ytop, r=0.45, h=0.9)
        if r.random() < 0.4:
            for k in range(r.randint(2, 5)):
                D.pot(o, Fb.p(-hw + 0.5 + 0.55 * k, ytop, hd - 0.5), 0.18, 0.25, "styro", r)
        if r.random() < 0.3:
            D.laundry_rack(o, Fb, 0.0, 0.0, ytop, yaw_x=hw > hd, r=r)
    else:
        if hw >= hd:
            g = Giwa(Fb, hw + 0.5, hd + 0.5, y_body_top, style="hip" if hw - hd > 0.4 else "pyramid" if abs(hw - hd) < 0.1 else "hip",
                     ov_x=0.5, ov_z=0.5, pitch=0.5, lift=0.1, flare=0.06, te=0.18, rafters=False,
                     soffit_mat="M_Plaster_White", gable_mat=main)
        else:
            Fr = Frame(Fb.cx, Fb.cz, F.yaw + 90.0)
            g = Giwa(Fr, hd + 0.5, hw + 0.5, y_body_top, style="hip", ov_x=0.5, ov_z=0.5, pitch=0.5, lift=0.1, flare=0.06,
                     te=0.18, rafters=False, soffit_mat="M_Plaster_White", gable_mat=main)
        g.build(o)


def _wall_top(o, P, u0, u1, y, thick, r, old, ends=True):
    """담장 윗면: 콘크리트 갓돌(또는 오래된 담이면 기와 갓)."""
    if old and r.random() < 0.5:
        wall_giwa_cap(o, P, u0, u1, y, thick, over=0.12, rise=0.16)
        return
    for L in (0, 1, 2):
        pbox(o.lod[L], P, u0 - (0.03 if ends else 0.0), u1 + (0.03 if ends else 0.0), y - 0.07, y, -thick - 0.03, 0.04,
             "M_Concrete", skip=("-y",) if L > 0 else ())


# ================================================================ 한옥
def hanok(zone, b, o):
    r = rng(b.key, "hanok")
    F = b.F
    ff = front_face(b)
    empty = "빈" in b.name or "빈" in b.use
    rect = block_rect(b)
    gmax = max(max(y for _, y in b.ground_faces[f]) for f in FACES)
    y_pod = gmax + 0.45
    y_w = y_pod + 2.55
    o.meta = dict(variant="empty" if empty else "lived", y_podium=round(y_pod, 2), y_wall=round(y_w, 2))
    bands = [(b.bottom - 1, y_pod, "M_Granite"), (y_pod, y_pod + 0.95, "M_Stone_Base"),
             (y_pod + 0.9, y_w - 0.32, "M_Plaster_White"), (y_w - 0.32, y_w + 1, "M_Wood_Red")]
    for f in FACES:
        role = b.roles[f]
        P = make_panel(zone, F, rect, f, role, b.bottom, y_w)
        W = P.W
        nb = max(1, int(round(W / 2.4)))
        bay = W / nb
        ops = []
        gate_bay = (nb // 2) if f == ff else None
        if role != "party":
            for i in range(nb):
                uc = (i + 0.5) * bay
                if i == gate_bay:
                    g = P.ground(uc)
                    gw = min(1.6, bay - 0.5)
                    ops.append(Op("gate", uc - gw / 2, uc + gw / 2, g + 0.12, y_pod + 1.95, rev="M_Wood_Red",
                                  leaf="M_Wood_Dark", frame="M_Wood_Red", depth=0.22))
                    # 문턱돌·문패
                    for L in (0, 1):
                        pbox(o.lod[L], P, uc - gw / 2 - 0.1, uc + gw / 2 + 0.1, g - 0.2, g + 0.12, -0.2, 0.3, "M_Granite")
                    sign_band(o, P, uc + gw / 2 + 0.2, uc + gw / 2 + 0.55, y_pod + 1.25, y_pod + 1.45, "문패", b, depth=0.03,
                              kind="plate")
                    if empty:
                        pbox(o.det, P, uc - 0.08, uc + 0.08, y_pod + 0.9, y_pod + 1.05, -0.2, -0.12, "M_Metal_Rust")
                    continue
                if role == "back" and r.random() < 0.5:
                    continue
                ww = min(bay - 0.6, r.uniform(0.9, 1.25))
                ops.append(Op("hanji", uc - ww / 2, uc + ww / 2, y_pod + 1.12, y_pod + 1.9, rev="M_Wood_Red",
                              frame="M_Wood_Red", depth=0.08, lit=None if empty else lit_val(r)))
        wall_grid_all(o, P, bands, [op.hole() for op in ops], "M_Plaster_White")
        emit_openings(o, P, ops)
        # 기둥(주칠)·중방·창방
        for L in (0, 1, 2):
            mb = o.lod[L]
            for k in range(nb + 1):
                u = min(W - 0.13, max(0.13, k * bay))
                ytop = y_w + (0.12 if k in (0, nb) else 0.0)
                pbox(mb, P, u - 0.13, u + 0.13, y_pod, ytop, -0.08, 0.06, "M_Wood_Red", skip=("-z",))
            if L < 2:
                pbox(mb, P, 0.0, W, y_w - 0.34, y_w, 0.0, 0.05, "M_Wood_Red", skip=("-z",))
                for a, c in _gaps(0.0, W, [op.hole()[:2] for op in ops if op.kind == "gate"]):
                    pbox(mb, P, a, c, y_pod + 0.86, y_pod + 0.98, 0.0, 0.035, "M_Wood_Red", skip=("-z",))
            # 기단 갓돌
            pbox(mb, P, 0.0, W, y_pod - 0.06, y_pod, 0.0, 0.08, "M_Granite_Dark" if L < 2 else "M_Granite", skip=("-z",))
        if role == "front" and r.random() < 0.85:
            for a, c in _gaps(0.3, W - 0.3, [op.hole()[:2] for op in ops if op.kind == "gate"], pad=0.3):
                if c - a > 0.5 and gap_ok(zone, P, (a + c) / 2, 0.2):
                    D.pot_row(o, P, a, c, P.ground, 0.17, r, 0.6)
        if role in ("front", "street"):
            add_anchor(o, P, 0.3 if r.random() < 0.5 else W - 0.3, y_w - 0.5)
    # 지붕
    style = "gable" if empty else "paljak"
    if b.w >= b.d:
        g = Giwa(F, b.w / 2 + 0.9, b.d / 2 + 1.0, y_w, style=style, ov_x=0.9, ov_z=1.0, pitch=0.62)
    else:
        Fr = Frame(b.cx, b.cz, b.yaw + 90.0)
        g = Giwa(Fr, b.d / 2 + 0.9, b.w / 2 + 1.0, y_w, style=style, ov_x=0.9, ov_z=1.0, pitch=0.62)
    g.build(o)
    o.meta["roof"] = style
    o.meta["ridge_y"] = round(g.y_ridge, 2)


def _gaps(a, c, holes, pad=0.0):
    out = []
    u = a
    for h0, h1 in sorted(holes):
        if h0 - pad > u:
            out.append((u, h0 - pad))
        u = max(u, h1 + pad)
    if c > u:
        out.append((u, c))
    return out


# ================================================================ 학교 건물(캠퍼스)
def school(zone, b, o):
    r = rng(b.key, "school")
    F = b.F
    ff = front_face(b)
    FH = 3.6
    par = 0.9
    fl = max(2, int((b.h - 1.0) / FH))
    top = b.top
    y0 = top - par - FH * fl
    y_roof = y0 + FH * fl
    rect = block_rect(b)
    main = "M_Plaster_Cream"
    bands = [(b.bottom - 1, y0 + 0.4, "M_Granite"), (y_roof - 0.05, top + 1, "M_Concrete"), (y0 + 0.4, y_roof, main)]
    o.meta = dict(variant="school", floors=fl, y0=round(y0, 2))
    for f in FACES:
        P = make_panel(zone, F, rect, f, b.roles[f], b.bottom, top - 0.07)
        role = P.role
        W = P.W
        ops = []
        nb = max(1, int(round(W / 4.2)))
        bay = W / nb
        core = (nb - 2) if (role in ("front", "street") and nb >= 4) else None
        for k in range(fl):
            fy = y0 + FH * k
            for i in range(nb):
                uc = (i + 0.5) * bay
                if i == core:
                    if fy + 2.3 + 1.6 < y_roof:
                        ops.append(Op("stair", uc - 0.7, uc + 0.7, fy + 2.3, fy + 3.9, rev="M_Concrete", mull=1, lit=lit_val(r)))
                    if k == 0:
                        g = P.ground(uc)
                        ops.append(Op("door", uc - 0.9, uc + 0.9, max(g, y0), max(g, y0) + 2.4, rev="M_Concrete", leaf="glass"))
                        canopy(o, P, uc - 1.3, uc + 1.3, max(g, y0) + 2.6, 1.2)
                    continue
                if W < 16 and i not in (0, nb - 1) and nb > 2:
                    continue
                ww = bay - 0.95
                sill = max(fy + 0.9, P.gmax(uc - ww / 2, uc + ww / 2) + 0.5)
                if fy + 2.9 - sill < 0.6:
                    continue
                ops.append(Op("win", uc - ww / 2, uc + ww / 2, sill, fy + 2.9, rev="M_Concrete", mull=3,
                              lit=round(0.25 + 0.75 * r.random(), 3)))
        wall_grid_all(o, P, bands, [op.hole() for op in ops], main)
        emit_openings(o, P, ops)
        # 기둥형 띠(필라스터)
        if role != "party":
            for L in (0, 1, 2):
                for k in range(nb + 1):
                    u = min(W - 0.25, max(0.25, k * bay))
                    pbox(o.lod[L], P, u - 0.25, u + 0.25, y0 + 0.4, y_roof, 0.0, 0.14, "M_Concrete", skip=("-z",))
                if L < 2:
                    for k in range(1, fl):
                        fy = y0 + FH * k
                        pbox(o.lod[L], P, 0, W, fy - 0.15, fy + 0.15, 0.0, 0.08, "M_Concrete", skip=("-z",))
            if role in ("front", "street"):
                for k in range(1, fl):
                    if r.random() < 0.5:
                        uu = (r.randrange(nb) + 0.5) * bay + 1.2
                        if uu < W - 0.6:
                            D.ac_unit(o, P, uu, y0 + FH * k + 0.2)
    hw, hd = b.w / 2, b.d / 2
    flat_roof(o, F, hw, hd, y_roof, top, t=0.25, floor_mat="M_Concrete")
    # 옥상: 계단탑 + 물탱크실 + 실외기 줄 + 안전 난간
    box(o.lod[0], F, -hw + 1.0, -hw + 5.0, y_roof, y_roof + 3.0, -hd + 1.0, -hd + 5.5, "M_Concrete", skip=("-y",))
    for L in (1, 2):
        box(o.lod[L], F, -hw + 1.0, -hw + 5.0, y_roof, y_roof + 3.0, -hd + 1.0, -hd + 5.5, "M_Concrete", skip=("-y",))
    for L in (0, 1, 2):
        box(o.lod[L], F, hw - 4.5, hw - 1.2, y_roof, y_roof + 2.0, -1.2, 1.2, "M_Concrete", skip=("-y",))
    for k in range(8):
        x = -hw + 7.0 + k * 1.1
        Pr = Panel(F.p(x - 0.4, 0, hd - 1.0)[0::2], (F.ux[0], F.ux[2]), (-F.uz[0], -F.uz[2]), 0.8, "back", [], y_roof, y_roof + 1)
        D.ac_unit(o, Pr, 0.4, y_roof + 0.1, w_back=0.0, bracket=False)
    Ppar = make_panel(zone, F, rect, "+z", "back", y_roof, top)
    for f in FACES:
        P = make_panel(zone, F, rect, f, "back", y_roof, top)
        n = max(2, int(P.W / 2.0))
        for k in range(n + 1):
            u = 0.3 + (P.W - 0.6) * k / n
            pbox(o.lod[0], P, u - 0.025, u + 0.025, top, top + 1.0, -0.3, -0.25, "M_Metal_Grey")
        pbox(o.lod[0], P, 0.3, P.W - 0.3, top + 0.95, top + 1.0, -0.3, -0.25, "M_Metal_Grey")
        pbox(o.lod[1], P, 0.3, P.W - 0.3, top + 0.95, top + 1.0, -0.3, -0.25, "M_Metal_Grey")


# ================================================================ 경비실
def booth(zone, b, o):
    r = rng(b.key, "booth")
    F = b.F
    ff = front_face(b)
    rect = block_rect(b)
    y0 = b.top - 3.1
    ytop = y0 + 2.75
    bands = [(b.bottom - 1, y0 + 0.2, "M_Granite"), (y0 + 0.2, ytop + 1, "M_Plaster_White")]
    right, left = neighbors(ff)
    for f in FACES:
        P = make_panel(zone, F, rect, f, b.roles[f], b.bottom, ytop)
        ops = []
        if f in (ff, right, left):
            ops.append(Op("win", 0.3, P.W - 0.3, y0 + 1.0, y0 + 2.25, rev="M_Concrete", mull=1, lit=round(0.05 + 0.2 * r.random(), 3)))
        if f == right:
            ops = [Op("door", 0.4, 1.3, y0, y0 + 2.1, rev="M_Concrete", leaf="M_Metal_Grey"),
                   Op("win", 1.55, P.W - 0.3, y0 + 1.0, y0 + 2.25, rev="M_Concrete", mull=0, lit=0.1)]
        wall_grid_all(o, P, bands, [op.hole() for op in ops], "M_Plaster_White")
        emit_openings(o, P, ops)
        if f == ff:
            sign_band(o, P, 0.5, P.W - 0.5, y0 + 2.35, y0 + 2.62, "경비실", b, depth=0.05, kind="plate")
        if b.roles[f] == "back":
            D.ac_unit(o, P, P.W / 2, P.ground(P.W / 2) + 0.05, bracket=False)
    slab_roof(o, F, b.w / 2, b.d / 2, ytop, over=0.35, t=0.2, mat="M_Metal_Grey", top_mat="M_Roof_Green")


# ================================================================ 후문 기둥
def gatepost(zone, b, o):
    F = b.F
    ff = front_face(b)
    g = min(min(y for _, y in b.ground_faces[f]) for f in FACES)
    top = b.top
    hw, hd = b.w / 2, b.d / 2
    for L in (0, 1, 2):
        mb = o.lod[L]
        box(mb, F, -hw - 0.06, hw + 0.06, b.bottom, g + 0.35, -hd - 0.06, hd + 0.06, "M_Granite_Dark")
        box(mb, F, -hw + 0.04, hw - 0.04, g + 0.35, top - 0.35, -hd + 0.04, hd - 0.04, "M_Granite", skip=("-y",))
        box(mb, F, -hw - 0.1, hw + 0.1, top - 0.35, top - 0.12, -hd - 0.1, hd + 0.1, "M_Granite_Dark")
        box(mb, F, -hw + 0.1, hw - 0.1, top - 0.12, top, -hd + 0.1, hd - 0.1, "M_Granite_Dark", skip=("-y",))
        cyl(mb, F.p(0, top, 0), UP, 0.16, 0.16, 0.32, (10, 8, 6)[L], "M_AC_White", caps=(False, True))
    P = make_panel(zone, F, (-hw + 0.04, hw - 0.04, -hd + 0.04, hd - 0.04), ff, "front", g, top)
    sign_band(o, P, 0.18, P.W - 0.18, g + 1.0, g + 2.4, "성대 후문(기둥 현판)", b, depth=0.04, kind="plate")
    o.sockets.append(dict(kind="lamp_globe", pos=F.p(0, top + 0.16, 0), n=(0, 1, 0)))


# ================================================================ 정자(사모정)
def pavilion(zone, b, o):
    F = b.F
    hw, hd = b.w / 2, b.d / 2
    deck = b.top - 4.2 if b.h > 4.5 else min(y for _, y in b.ground_faces["-z"])
    y_floor = deck + 0.5
    y_w = y_floor + 2.35
    cols = [(-hw + 0.3, -hd + 0.3), (hw - 0.3, -hd + 0.3), (hw - 0.3, hd - 0.3), (-hw + 0.3, hd - 0.3)]
    for L in (0, 1, 2):
        mb = o.lod[L]
        n = (10, 8, 6)[L]
        box(mb, F, -hw + 0.1, hw - 0.1, y_floor - 0.12, y_floor, -hd + 0.1, hd - 0.1, "M_Wood_Dark")
        for x, z in cols:
            g = zone.ground(*F.p(x, 0, z)[0::2])
            gb = min(g, deck) - 0.25
            cyl(mb, F.p(x, gb, z), UP, 0.2, 0.17, (min(g, deck) + 0.18) - gb, n, "M_Granite", caps=(False, True))
            y_c = min(g, deck) + 0.18
            cyl(mb, F.p(x, y_c, z), UP, 0.13, 0.12, y_w - y_c + 0.1, n, "M_Wood_Red", caps=(False, False))
        if L < 2:   # 마루 밑 귀틀(가로 들보)
            for sx in (-1, 1):
                box(mb, F, -hw + 0.2, hw - 0.2, y_floor - 0.3, y_floor - 0.12, sx * (hd - 0.3) - 0.08, sx * (hd - 0.3) + 0.08, "M_Wood_Dark")
                box(mb, F, sx * (hw - 0.3) - 0.08, sx * (hw - 0.3) + 0.08, y_floor - 0.3, y_floor - 0.12, -hd + 0.2, hd - 0.2, "M_Wood_Dark")
        if L < 2:
            for sx in (-1, 1):
                box(mb, F, -hw + 0.2, hw - 0.2, y_w - 0.3, y_w, sx * (hd - 0.3) - 0.1, sx * (hd - 0.3) + 0.1, "M_Wood_Red")
                box(mb, F, sx * (hw - 0.3) - 0.1, sx * (hw - 0.3) + 0.1, y_w - 0.3, y_w, -hd + 0.2, hd - 0.2, "M_Wood_Red")
    # 난간(계자난간 단순형): 입구(남쪽 -z) 빼고 세 면
    for L in (0, 1):
        mb = o.lod[L]
        for (x0, x1, z0, z1) in ((-hw + 0.3, hw - 0.3, hd - 0.35, hd - 0.25), (hw - 0.35, hw - 0.25, -hd + 0.3, hd - 0.3),
                                 (-hw + 0.25, -hw + 0.35, -hd + 0.3, hd - 0.3)):
            box(mb, F, x0, x1, y_floor + 0.5, y_floor + 0.58, z0, z1, "M_Wood_Red")
            box(mb, F, x0, x1, y_floor, y_floor + 0.12, z0, z1, "M_Wood_Red")
            if L == 0:
                n = int(max(x1 - x0, z1 - z0) / 0.3)
                for k in range(1, n):
                    if x1 - x0 > z1 - z0:
                        x = x0 + (x1 - x0) * k / n
                        box(o.det, F, x - 0.02, x + 0.02, y_floor + 0.12, y_floor + 0.5, z0 + 0.02, z1 - 0.02, "M_Wood_Red", skip=("+y", "-y"))
                    else:
                        z = z0 + (z1 - z0) * k / n
                        box(o.det, F, x0 + 0.02, x1 - 0.02, y_floor + 0.12, y_floor + 0.5, z - 0.02, z + 0.02, "M_Wood_Red", skip=("+y", "-y"))
    g = Giwa(F, hw + 0.75, hd + 0.75, y_w, style="pyramid", ov_x=1.05, ov_z=1.05, pitch=0.78, lift=0.3, flare=0.22, te=0.2)
    g.build(o)
    o.meta = dict(variant="pyramid", y_floor=round(y_floor, 2), ridge=round(g.y_ridge, 2))


# ================================================================ 담·옹벽·난간
def _len_frame(b):
    """긴 쪽을 로컬 x 로: (틀, 반 길이, 반 두께)."""
    if b.w >= b.d:
        return b.F, b.w / 2, b.d / 2
    return Frame(b.cx, b.cz, b.yaw + 90.0), b.d / 2, b.w / 2


def fence_wall(zone, b, o):
    r = rng(b.key, "fence")
    F, hl, ht = _len_frame(b)
    mat = r.choice(["M_Brick_Red", "M_Plaster_Cream", "M_Concrete", "M_Brick_Brown", "M_Brick_Red"])
    top = b.top
    old = r.random() < 0.3
    giwa_cap = old and mat != "M_Concrete" and r.random() < 0.6
    yb = top - (0.0 if giwa_cap else 0.07)
    for L in (0, 1, 2):
        box(o.lod[L], F, -hl, hl, b.bottom, yb, -ht, ht, mat, skip=("+y",) if giwa_cap else ())
        if not giwa_cap:
            box(o.lod[L], F, -hl - 0.02, hl + 0.02, yb, top, -ht - 0.03, ht + 0.03, "M_Concrete", skip=("-y",) if L else ())
    # 걷는 쪽 면
    best = None
    for f in ("+z", "-z"):
        O, t, n, W = rect_face(F, -hl, hl, -ht, ht, f)
        x, z = O[0] + t[0] * W / 2 + n[0] * 0.8, O[1] + t[1] * W / 2 + n[1] * 0.8
        d = zone.walk_dist(x, z)
        if best is None or d < best[0]:
            best = (d, f)
    P = make_panel(zone, F, (-hl, hl, -ht, ht), best[1], "front", b.bottom, top)
    if giwa_cap:
        wall_giwa_cap(o, P, 0.0, P.W, top, 2 * ht, over=0.12, rise=0.16)
    barbed = r.random() < 0.6 and not giwa_cap
    if barbed:
        D.barbed(o.det, P, 0.05, P.W - 0.05, top, -ht)
    if r.random() < 0.45 and gap_ok(zone, P, P.W / 2, 0.2):
        D.pot_row(o, P, 0.2, P.W - 0.2, P.ground, 0.17, r, 0.55)
    from . import dense
    fn = dense.fence_clutter(zone, b, o, P, rng(b.key, "fence5"))   # 5차: 담장 가스관·계량기·주소판
    o.meta = dict(variant="giwa_cap" if giwa_cap else "coping", main=mat, barbed=barbed, **({"clutter": fn} if any(fn.values()) else {}))


def fence_grille(zone, b, o):
    F, hl, ht = _len_frame(b)
    top = b.top
    gb = min(y for _, y in b.ground_faces["-z"] + b.ground_faces["+z"])
    yb = gb + 0.6
    mg = "M_Metal_Green"
    n_post = max(1, int(math.ceil(2 * hl / 2.6)))
    for L in (0, 1, 2):
        mb = o.lod[L]
        box(mb, F, -hl, hl, b.bottom, yb, -ht, ht, "M_Concrete")
        for k in range(n_post + 1):
            x = -hl + 2 * hl * k / n_post
            x = max(-hl + 0.15, min(hl - 0.15, x))
            box(mb, F, x - 0.15, x + 0.15, yb, top - 0.05, -0.15, 0.15, "M_Concrete", skip=("-y",))
            box(mb, F, x - 0.18, x + 0.18, top - 0.05, top + 0.03, -0.18, 0.18, "M_Concrete")
        for yy in (yb + 0.15, top - 0.2):
            box(mb, F, -hl + 0.15, hl - 0.15, yy - 0.03, yy + 0.03, -0.03, 0.03, mg)
        if L < 2:
            step = 0.15 if L == 0 else 0.3
            n = int(2 * hl / step)
            for k in range(1, n):
                x = -hl + 2 * hl * k / n
                box(mb, F, x - 0.013, x + 0.013, yb, top - 0.1, -0.013, 0.013, mg, skip=("+y", "-y"))
    o.meta = dict(variant="grille")


def retaining(zone, b, o, stone=False):
    r = rng(b.key, "ret")
    F, hl, ht = _len_frame(b)
    top = b.top
    mat = "M_Stone_Rubble" if stone else "M_Concrete"
    for L in (0, 1, 2):
        box(o.lod[L], F, -hl, hl, b.bottom, top - 0.08, -ht, ht, mat)
        box(o.lod[L], F, -hl - 0.02, hl + 0.02, top - 0.08, top, -ht - 0.04, ht + 0.04,
            "M_Granite_Dark" if stone else "M_Concrete", skip=("-y",) if L else ())
    best = None
    for f in ("+z", "-z"):
        O, t, n, W = rect_face(F, -hl, hl, -ht, ht, f)
        x, z = O[0] + t[0] * W / 2 + n[0] * 0.8, O[1] + t[1] * W / 2 + n[1] * 0.8
        d = zone.walk_dist(x, z)
        if best is None or d < best[0]:
            best = (d, f)
    P = make_panel(zone, F, (-hl, hl, -ht, ht), best[1], "front", b.bottom, top - 0.08)
    if not stone:
        n = int(P.W / 1.8)
        for k in range(n):
            u = 0.9 + 1.8 * k
            g = P.ground(u)
            if top - g > 0.9:
                cyl(o.det, P.p(u, g + 0.45, -0.05), P.n3, 0.05, 0.05, 0.14, 8, "M_Metal_Grey", caps=(False, False))
    o.meta = dict(variant="stone" if stone else "concrete")


def railing(zone, b, o):
    F, hl, ht = _len_frame(b)
    top = b.top
    g = min(y for _, y in b.ground_faces["-z"] + b.ground_faces["+z"])
    mg = "M_Metal_Green"
    n = max(1, int(math.ceil(2 * hl / 1.5)))
    for L in (0, 1, 2):
        mb = o.lod[L]
        sides = (8, 6, 4)[L]
        for k in range(n + 1):
            x = -hl + 0.05 + (2 * hl - 0.1) * k / n
            gg = zone.ground(*F.p(x, 0, 0)[0::2])
            cyl(mb, F.p(x, gg - 0.15, 0), UP, 0.03, 0.03, top - gg + 0.15, sides, mg, caps=(False, True))
        for yy in ((top - 0.04,) if L == 2 else (top - 0.04, g + 0.5)):
            cyl(mb, F.p(-hl, yy, 0), F.ux, 0.025, 0.025, 2 * hl, sides, mg, caps=(True, True))
    o.meta = dict(variant="pipe")


# ================================================================ 돌 쌓기(성곽·축대 공통)
def stone_face(mb, P, u0, u1, ylo, yhi, course, lmin, lmax, rr, w=0.0, prot=0.04, joint=0.012, mat="M_Granite_Block",
               hole=None):
    """돌 켜쌓기(LOD0): 켜(course)는 수평, 돌 길이 lmin~lmax, 켜마다 엇갈림. 돌 = 벽 면(뒤는 짙은 줄눈 M_Granite_Dark)
    앞으로 0.6~1.2 × prot 튀어나온 평평한 덩어리, 가장자리는 직각. 줄눈 폭 = 2 × joint.
    → 줄눈이 짙은 선 하나로 읽힌다(모서리 깎기는 외곽선이 세 줄 생겨 지저분했음 — 2026-10-06).
    ylo(u)·yhi(u) 는 직선(양 끝 값으로 자름). hole = (u0, u1, y0, y1) 안 돌은 뺌."""
    lo0, lo1 = ylo(u0), ylo(u1)
    hi0, hi1 = yhi(u0), yhi(u1)
    span = max(1e-6, u1 - u0)
    blo, bhi = (lo1 - lo0) / span, (hi1 - hi0) / span
    y = math.floor(min(lo0, lo1) / course) * course
    ymax = max(hi0, hi1)
    while y < ymax:
        ya, yb = y, y + course
        u = u0 - rr.uniform(0.0, lmax)
        while u < u1:
            L = rr.uniform(lmin, lmax)
            a, c = max(u, u0), min(u + L, u1)
            u += L
            pr = prot * rr.uniform(0.6, 1.2)
            if c - a < 0.06:
                continue
            if hole and not (c <= hole[0] or a >= hole[1] or yb <= hole[2] or ya >= hole[3]):
                continue
            la, lc = lo0 + blo * (a - u0), lo0 + blo * (c - u0)
            ha, hc = hi0 + bhi * (a - u0), hi0 + bhi * (c - u0)
            if yb <= min(la, lc) or ya >= max(ha, hc):
                continue
            if ya >= max(la, lc) and yb <= min(ha, hc):
                _stone(mb, P, a, c, ya, yb, w, pr, joint, mat)
            else:
                poly = [(a + joint, ya + joint), (c - joint, ya + joint), (c - joint, yb - joint), (a + joint, yb - joint)]
                poly = clip_poly(poly, -blo, 1.0, -(lo0 - blo * u0))
                if len(poly) >= 3:
                    poly = clip_poly(poly, bhi, -1.0, hi0 - bhi * u0)
                if len(poly) >= 3:
                    mb.face([P.p(pu, pv, w + pr * 0.5) for pu, pv in poly], mat, out=P.out)
        y += course


def _stone(mb, P, a, c, ya, yb, w, pr, j, mat):
    A, C, YA, YB = a + j, c - j, ya + j, yb - j
    f = [P.p(A, YA, w + pr), P.p(C, YA, w + pr), P.p(C, YB, w + pr), P.p(A, YB, w + pr)]
    b = [P.p(A, YA, w), P.p(C, YA, w), P.p(C, YB, w), P.p(A, YB, w)]
    mb.face(f, mat, out=P.out)
    for k in range(4):
        k2 = (k + 1) % 4
        mid = mul(add(f[k], f[k2]), 0.5)
        cen = mul(add(f[0], f[2]), 0.5)
        mb.face([b[k], b[k2], f[k2], f[k]], mat, out=sub(mid, cen))


# ================================================================ 한양도성 성곽
def wall_offsets(pts, off):
    """폴리라인을 진행 방향 오른쪽(+off) 으로 평행 이동(마이터). pts = [(x, y, z)]"""
    out = []
    n = len(pts)
    for i in range(n):
        ns = []
        for j in (i - 1, i):
            if 0 <= j < n - 1:
                a, b_ = pts[j], pts[j + 1]
                dx, dz = b_[0] - a[0], b_[2] - a[2]
                L = math.hypot(dx, dz)
                ns.append((dz / L, -dx / L))   # 오른쪽 법선
        mx, mz = sum(v[0] for v in ns), sum(v[1] for v in ns)
        L = math.hypot(mx, mz)
        mx, mz = mx / L, mz / L
        k = off / max(0.5, mx * ns[0][0] + mz * ns[0][1])
        out.append((pts[i][0] + mx * k, pts[i][1], pts[i][2] + mz * k))
    return out


def castle_wall(zone, wall, i0, i1, o, gate=None):
    """성곽 한 덩어리(점 i0..i1). 안쪽(남) 면 = 돌 켜쌓기(LOD0), 바깥(북) 면 = 평면(텍스처).
    체성 → 미석 → 여장(타·타구·총안) → 옥개석. 06 문서 6장 값 그대로."""
    pts = wall["points"]
    H, T = wall["height"], wall["thickness"]
    body = wall.get("body", {})
    brow, par, cap = wall["brow"], wall["parapet"], wall["cap"]
    # 켜·돌 길이: zone1.json 값(0.5, 0.5~0.98)은 옛 생성 텍스처 기준 → 아트 텍스처 M_Granite(3m 에 성돌 5단 = 켜 0.6,
    # 돌 길이 0.7~1.25)에 맞춤. LOD1·2 평면(텍스처)과 LOD0 돌덩이의 줄눈 간격이 같게
    course = 0.6
    smin, smax = 0.7, 1.25
    inner = wall_offsets(pts, T / 2)     # 오른쪽 = 성 안(남)
    outer = wall_offsets(pts, -T / 2)
    r = rng("castle", i0, i1)
    sink = 1.5
    for i in range(i0, i1):
        a, b_ = pts[i], pts[i + 1]
        ia, ib = inner[i], inner[i + 1]
        oa, ob = outer[i], outer[i + 1]
        ya, yb = a[1], b_[1]
        # 안쪽 면 Panel: 바깥(남)에서 볼 때 오른쪽 = 진행 방향
        dx, dz = ib[0] - ia[0], ib[2] - ia[2]
        Wi = math.hypot(dx, dz)
        t = (dx / Wi, dz / Wi)
        n_in = (t[1], -t[0])
        Pi = Panel((ia[0], ia[2]), t, n_in, Wi, "front", [], min(ya, yb) - sink, max(ya, yb) + H)
        ylo = lambda u, ya=ya, yb=yb, Wi=Wi: ya + (yb - ya) * u / Wi - sink
        yhi = lambda u, ya=ya, yb=yb, Wi=Wi: ya + (yb - ya) * u / Wi + H
        gh = None
        if gate and gate[0] == i:
            gu = gate[1] * Wi
            gh = (gu - 0.85, gu + 0.85, ya + (yb - ya) * gate[1] - 0.3, ya + (yb - ya) * gate[1] + 2.3)
        for L in (0, 1, 2):
            mb = o.lod[L]
            _wall_quad(mb, Pi, Wi, ylo, yhi, "M_Granite_Dark" if L == 0 else "M_Granite", gh)
            # 바깥 면(북): 반대 방향 Panel
            dxo, dzo = oa[0] - ob[0], oa[2] - ob[2]
            Wo = math.hypot(dxo, dzo)
            to = (dxo / Wo, dzo / Wo)
            Po = Panel((ob[0], ob[2]), to, (to[1], -to[0]), Wo, "back", [], 0, 0)
            ylo_o = lambda u, ya=ya, yb=yb, Wo=Wo: yb + (ya - yb) * u / Wo - sink - 3.0
            yhi_o = lambda u, ya=ya, yb=yb, Wo=Wo: yb + (ya - yb) * u / Wo + H
            _wall_quad(mb, Po, Wo, ylo_o, yhi_o, "M_Granite", None)
            # 윗면
            mb.face([(ia[0], ya + H, ia[2]), (ib[0], yb + H, ib[2]), (ob[0], yb + H, ob[2]), (oa[0], ya + H, oa[2])],
                    "M_Granite", out=UP)
        if gh:
            _amun(o, Pi, gh)
        stone_face(o.lod[0], Pi, 0.0, Wi, lambda u, f=ylo: f(u) + sink - 0.4, yhi, course, smin, smax, r,
                   hole=(gh[0] - 0.25, gh[1] + 0.25, gh[2], gh[3] + 0.5) if gh else None)
        # 미석(눈썹돌 띠)
        bi = wall_offsets([pts[i], pts[i + 1]], T / 2 + brow["overhang"])
        bo = wall_offsets([pts[i], pts[i + 1]], -T / 2 - brow["overhang"])
        for L in (0, 1, 2):
            _ribbon(o.lod[L], bi, bo, ya + H, yb + H, brow["height"], "M_Granite")
        # 여장: 타(merlon) 마다 수평
        L_seg = math.hypot(b_[0] - a[0], b_[2] - a[2])
        nm = max(1, int(round(L_seg / (par["merlon"] + par["crenel"]))))
        pitch = L_seg / nm
        mlen = max(0.6, pitch - par["crenel"])
        tt = ((b_[0] - a[0]) / L_seg, (b_[2] - a[2]) / L_seg)
        outN = (-tt[1], tt[0])     # 왼쪽 = 성 밖(북)
        slope = abs(yb - ya) / L_seg
        for m in range(nm):
            s = (m + 0.5) * pitch
            yTop = ya + (yb - ya) * s / L_seg + H + brow["height"]
            sk = 0.25 + slope * mlen / 2
            cx = a[0] + tt[0] * s + outN[0] * (T / 2 - par["thickness"] / 2)
            cz = a[2] + tt[1] * s + outN[1] * (T / 2 - par["thickness"] / 2)
            # 여장 Panel(안쪽 면): 바깥(남)에서 볼 때 오른쪽 = 진행 방향
            pin = (n_in[0], n_in[1])
            Oc = (cx + pin[0] * par["thickness"] / 2 - tt[0] * mlen / 2, cz + pin[1] * par["thickness"] / 2 - tt[1] * mlen / 2)
            Pm = Panel(Oc, tt, pin, mlen, "front", [], yTop - sk, yTop + par["height"])
            emb = par.get("embrasure", [0.22, 0.3])
            eh = (mlen / 2 - emb[0] / 2, mlen / 2 + emb[0] / 2, yTop + 0.45, yTop + 0.45 + emb[1])
            for L in (0, 1, 2):
                mb = o.lod[L]
                ax3, nz3 = (tt[0], 0, tt[1]), (pin[0], 0, pin[1])
                c3 = (cx, yTop - sk + (par["height"] + sk) / 2, cz)
                obox(mb, c3, ax3, UP, nz3, mlen / 2, (par["height"] + sk) / 2, par["thickness"] / 2, "M_Granite",
                     skip=("+z",) if L < 2 else ())
                if L < 2:
                    pm = "M_Granite_Dark" if L == 0 else "M_Granite"
                    wall_grid(mb, Pm, [(Pm.bot, Pm.top, pm)], [eh], default=pm)
                    from .facade import reveal
                    reveal(mb, Pm, *eh, 0.25, "M_Granite_Dark")
                    pquad(mb, Pm, eh[0], eh[1], eh[2], eh[3], -0.25, "M_Wire_Black")
                # 옥개석
                cc = (cx, yTop + par["height"] + cap["height"] / 2, cz)
                obox(mb, cc, ax3, UP, nz3, mlen / 2 + cap["overhang"], cap["height"] / 2, par["thickness"] / 2 + cap["overhang"],
                     "M_Granite")
                if L == 0:
                    # 옥개석 지붕 모양(가운데가 솟은 낮은 맞배)
                    yy = yTop + par["height"] + cap["height"]
                    hw_ = par["thickness"] / 2 + cap["overhang"]
                    ridge_a = (cx - tt[0] * (mlen / 2 + cap["overhang"]), yy + 0.1, cz - tt[1] * (mlen / 2 + cap["overhang"]))
                    ridge_b = (cx + tt[0] * (mlen / 2 + cap["overhang"]), yy + 0.1, cz + tt[1] * (mlen / 2 + cap["overhang"]))
                    for sg in (1, -1):
                        ea = (ridge_a[0] + pin[0] * hw_ * sg, yy, ridge_a[2] + pin[1] * hw_ * sg)
                        eb = (ridge_b[0] + pin[0] * hw_ * sg, yy, ridge_b[2] + pin[1] * hw_ * sg)
                        mb.face([ridge_a, ridge_b, eb, ea], "M_Granite", out=(pin[0] * sg, 1.0, pin[1] * sg))
                    for e, sg in ((ridge_a, -1), (ridge_b, 1)):
                        mb.face([e, (e[0] + pin[0] * hw_, yy, e[2] + pin[1] * hw_), (e[0] - pin[0] * hw_, yy, e[2] - pin[1] * hw_)],
                                "M_Granite", out=(tt[0] * sg, 0, tt[1] * sg))
            stone_face(o.lod[0], Pm, 0.0, mlen, lambda u, y=yTop: y - 0.05, lambda u, y=yTop: y + par["height"],
                       0.29, 0.3, 0.55, r, hole=(eh[0] - 0.05, eh[1] + 0.05, eh[2] - 0.05, eh[3] + 0.05), prot=0.03, joint=0.01)
    # 양 끝 마구리
    for idx, sg in ((i0, -1), (i1, 1)):
        if idx not in (0, len(pts) - 1):
            continue
        p = pts[idx]
        ia_, oa_ = inner[idx], outer[idx]
        for L in (0, 1, 2):
            o.lod[L].face([(ia_[0], p[1] - sink, ia_[2]), (oa_[0], p[1] - sink - 3, oa_[2]), (oa_[0], p[1] + H, oa_[2]),
                           (ia_[0], p[1] + H, ia_[2])], "M_Granite", out=(sg * (pts[min(idx + 1, len(pts) - 1)][0] - pts[max(idx - 1, 0)][0]), 0,
                                                                         sg * (pts[min(idx + 1, len(pts) - 1)][2] - pts[max(idx - 1, 0)][2])))


def _wall_quad(mb, P, W, ylo, yhi, mat, hole):
    if hole:
        wall_grid(mb, Panel(P.O, P.t, P.n, W, "front", [], min(ylo(0), ylo(W)), max(yhi(0), yhi(W))),
                  [(-1e9, 1e9, mat)], [hole], default=mat, y_hi=min(yhi(0), yhi(W)))
        # 위쪽 비스듬한 부분
        ytop0, ytop1 = yhi(0), yhi(W)
        ymin = min(ytop0, ytop1)
        mb.face([P.p(0, ymin), P.p(W, ymin), P.p(W, ytop1), P.p(0, ytop0)], mat, out=P.out)
        return
    mb.face([P.p(0, ylo(0)), P.p(W, ylo(W)), P.p(W, yhi(W)), P.p(0, yhi(0))], mat, out=P.out)


def _ribbon(mb, bi, bo, ya, yb, h, mat):
    (ia, ib), (oa, ob) = bi, bo
    A = [(ia[0], ya, ia[2]), (ib[0], yb, ib[2]), (ob[0], yb, ob[2]), (oa[0], ya, oa[2])]
    B = [(p[0], p[1] + h, p[2]) for p in A]
    mb.face(B, mat, out=UP)
    mb.face(A[::-1], mat, out=(0, -1, 0))
    for k in range(4):
        k2 = (k + 1) % 4
        c = mul(add(A[k], A[k2]), 0.5)
        cen = mul(add(add(A[0], A[1]), add(A[2], A[3])), 0.25)
        mb.face([A[k], A[k2], B[k2], B[k]], mat, out=sub(c, cen))


def _amun(o, P, gh):
    """암문(닫힌 문): 짙은 화강석 문틀 + 안으로 들어간 나무 문짝."""
    u0, u1, y0, y1 = gh
    for L in (0, 1, 2):
        mb = o.lod[L]
        from .facade import reveal
        reveal(mb, P, u0, u1, y0, y1, 0.6, "M_Granite_Dark")
        pquad(mb, P, u0, u1, y0, y1, -0.6, "M_Wood_Dark")
        pbox(mb, P, u0 - 0.25, u1 + 0.25, y1, y1 + 0.45, 0.0, 0.06, "M_Granite_Dark", skip=("-z",))
        for uu in (u0 - 0.25, u1):
            pbox(mb, P, uu, uu + 0.25, y0, y1, 0.0, 0.06, "M_Granite_Dark", skip=("-z",))
    if True:
        mb = o.lod[0]
        for k in range(1, 6):
            u = u0 + (u1 - u0) * k / 6
            pbox(mb, P, u - 0.015, u + 0.015, y0, y1, -0.6, -0.57, "M_Metal_Rust", skip=("-z",))
        for yy in (y0 + 0.5, y1 - 0.5):
            pbox(mb, P, u0, u1, yy - 0.04, yy + 0.04, -0.6, -0.56, "M_Metal_Rust", skip=("-z",))


GENERATORS = {
    "villa": villa, "shop": shop, "house": house, "hanok": hanok, "school": school, "booth": booth,
    "gatepost": gatepost, "pavilion": pavilion, "fence_wall": fence_wall, "fence_grille": fence_grille,
    "retaining_concrete": lambda z, b, o: retaining(z, b, o, False),
    "retaining_stone": lambda z, b, o: retaining(z, b, o, True), "railing": railing,
}
