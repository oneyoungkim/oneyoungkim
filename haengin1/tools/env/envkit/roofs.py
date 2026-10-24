# -*- coding: utf-8 -*-
"""지붕: 평지붕(난간·녹색 방수 바닥·갓돌) / 기와 지붕(맞배 gable · 우진각 hip · 사모 pyramid · 팔작 paljak).

기와 지붕은 평면 좌표 (x = 용마루 방향, z = 가로) 의 패치로 만든다. 높이 = 용마루 - 물매 곡선(오목: 위는 가파르고 처마 쪽은 완만)
+ 처마 귀 솟음(앙곡), 평면은 귀 쪽을 바깥으로 밀어(안허리곡) 처마선이 곡선이 되게 한다. 두 함수 모두 평면 위치만의 함수라
패치끼리 추녀선에서 어긋나지 않는다. 우진각·팔작은 a_r = A - B(모든 면 물매 같음).
LOD0 = 면 + 수키와 줄 + 막새(흰 점) + 서까래 끝 + 용마루·추녀마루 / LOD1 = 면(거칠게) + 마루 / LOD2 = 면(아주 거칠게) + 용마루.
"""
import math

from .geom import madd, UP, obox, quad, cyl, norm, sub, cross, length, add, mul
from .facade import pbox

GIWA = "M_Giwa_Dark"


# ---------------------------------------------------------------- 평지붕
def flat_roof(o, F, hw, hd, y_roof, y_top, t=0.2, floor_mat="M_Roof_Green", par_mat="M_Concrete",
              cope_mat="M_Concrete", cope=0.07, over=0.03, levels=(0, 1, 2)):
    """F 틀 중심, 바깥 반 크기 hw·hd. 바닥(y_roof) + 난간 안쪽 면 + 갓돌(y_top - cope ~ y_top)."""
    for L in levels:
        mb = o.lod[L]
        ix, iz = hw - t, hd - t
        quad(mb, F.p(-ix, y_roof, -iz), F.p(ix, y_roof, -iz), F.p(ix, y_roof, iz), F.p(-ix, y_roof, iz),
             floor_mat, out=UP)
        yc = y_top - cope
        if yc > y_roof + 0.01:
            # 안쪽 면 4개(안쪽을 봄)
            quad(mb, F.p(-ix, y_roof, iz), F.p(ix, y_roof, iz), F.p(ix, yc, iz), F.p(-ix, yc, iz), par_mat, out=F.d(0, 0, -1))
            quad(mb, F.p(-ix, y_roof, -iz), F.p(ix, y_roof, -iz), F.p(ix, yc, -iz), F.p(-ix, yc, -iz), par_mat, out=F.d(0, 0, 1))
            quad(mb, F.p(ix, y_roof, -iz), F.p(ix, y_roof, iz), F.p(ix, yc, iz), F.p(ix, yc, -iz), par_mat, out=F.d(-1, 0, 0))
            quad(mb, F.p(-ix, y_roof, -iz), F.p(-ix, y_roof, iz), F.p(-ix, yc, iz), F.p(-ix, yc, -iz), par_mat, out=F.d(1, 0, 0))
        # 갓돌: 바깥으로 over 만큼 내밈
        ox, oz = hw + over, hd + over
        tx, tz = ix - 0.02, iz - 0.02
        from .geom import box
        box(mb, F, -ox, ox, yc, y_top, tz, oz, cope_mat, skip=("-y",))
        box(mb, F, -ox, ox, yc, y_top, -oz, -tz, cope_mat, skip=("-y",))
        box(mb, F, tx, ox, yc, y_top, -tz, tz, cope_mat, skip=("-y", "+z", "-z"))
        box(mb, F, -ox, -tx, yc, y_top, -tz, tz, cope_mat, skip=("-y", "+z", "-z"))
        # 갓돌 아랫면(내민 부분)
        for sz in (1, -1):
            quad(mb, F.p(-ox, yc, sz * hd), F.p(ox, yc, sz * hd), F.p(ox, yc, sz * oz), F.p(-ox, yc, sz * oz),
                 cope_mat, out=(0, -1, 0))
        for sx in (1, -1):
            quad(mb, F.p(sx * hw, yc, -hd), F.p(sx * ox, yc, -hd), F.p(sx * ox, yc, hd), F.p(sx * hw, yc, hd),
                 cope_mat, out=(0, -1, 0))


def slab_roof(o, F, hw, hd, y, over=0.25, t=0.18, mat="M_Concrete", top_mat="M_Roof_Green", parapet=0.0, levels=(0, 1, 2)):
    """단독주택 슬래브 지붕: 처마처럼 내민 판 + (선택) 낮은 난간."""
    from .geom import box
    for L in levels:
        mb = o.lod[L]
        box(mb, F, -hw - over, hw + over, y, y + t, -hd - over, hd + over, mat, mats={"+y": top_mat})
        if parapet > 0:
            pt = 0.12
            X, Z = hw + over, hd + over
            y1 = y + t
            box(mb, F, -X, X, y1, y1 + parapet, Z - pt, Z, mat, skip=("-y",))
            box(mb, F, -X, X, y1, y1 + parapet, -Z, -Z + pt, mat, skip=("-y",))
            box(mb, F, X - pt, X, y1, y1 + parapet, -Z + pt, Z - pt, mat, skip=("-y", "+z", "-z"))
            box(mb, F, -X, -X + pt, y1, y1 + parapet, -Z + pt, Z - pt, mat, skip=("-y", "+z", "-z"))


# ---------------------------------------------------------------- 기와 지붕
class Giwa:
    def __init__(self, F, A, B, y_wall, style="gable", ov_x=0.9, ov_z=1.0, pitch=0.62, prof_c=0.45,
                 lift=0.28, flare=0.22, s_k=0.5, te=0.22, ox=0.0, oz=0.0, gable_mat="M_Wood_Dark",
                 soffit_mat="M_Wood_Dark", rafter_mat="M_Wood_Red", tiles=True, makse=True, rafters=True):
        """A, B = 처마 끝까지의 반 길이(용마루 방향)·반 폭. y_wall = 벽 윗면(처마 안쪽 아랫면이 여기에 닿게)."""
        if style in ("hip", "paljak", "pyramid") and A < B:
            raise ValueError("A >= B 이어야 함(용마루를 긴 쪽으로)")
        self.F, self.A, self.B, self.style = F, A, B, style
        self.ov_x, self.ov_z = ov_x, ov_z
        self.prof_c, self.Lc, self.Fl, self.te = prof_c, lift, flare, te
        self.ox, self.oz = ox, oz
        self.a_r = {"gable": A, "hip": A - B, "paljak": A - B, "pyramid": 0.0}[style]
        self.s_k = s_k if style == "paljak" else 0.0
        self.Xg = self.a_r + B * self.s_k
        self.rise = pitch * B
        self.s_w = 1.0 - ov_z / B
        self.y_ridge = y_wall + te + self.rise * self.prof(self.s_w) - 0.02
        self.y_wall = y_wall
        self.gable_mat, self.soffit_mat, self.rafter_mat = gable_mat, soffit_mat, rafter_mat
        self.tiles, self.makse, self.rafters = tiles, makse, rafters

    def prof(self, s):
        c = self.prof_c
        return s + c * s * (1 - s)

    def lift(self, x, z):
        u, v = min(1.0, abs(x) / self.A), min(1.0, abs(z) / self.B)
        if self.style == "gable":
            return self.Lc * u ** 4 * v ** 2
        return self.Lc * (u * v) ** 2.2 * max(u, v) ** 2

    def warp(self, x, z):
        u, v = min(1.0, abs(x) / self.A), min(1.0, abs(z) / self.B)
        if self.style == "gable":
            k = self.Fl * u ** 4 * v ** 2
            return x, z + math.copysign(k, z)
        k = self.Fl * (u * v) ** 3
        return x + math.copysign(k, x), z + math.copysign(k, z)

    def Y(self, s):
        return self.y_ridge - self.rise * self.prof(s)

    def pt(self, x, z, s, dy=0.0):
        y = self.Y(s) + self.lift(x, z) + dy
        wx, wz = self.warp(x, z)
        return self.F.p(self.ox + wx, y, self.oz + wz)

    # 패치 경계
    def X_long(self, s):
        if self.style == "gable":
            return self.A
        if self.style == "paljak" and s < self.s_k:
            return self.Xg
        return self.a_r + self.B * s

    def long_xz(self, s, t, sz):
        X = self.X_long(s)
        return -X + 2 * X * t, sz * self.B * s

    def end_xz(self, s, t, sx):
        Z = self.B * s
        return sx * (self.a_r + self.B * s), -Z + 2 * Z * t

    def s_list(self, n, s0=0.0):
        S = [s0 + (1 - s0) * k / n for k in range(n + 1)]
        if self.style == "paljak" and s0 < self.s_k:
            S.append(self.s_k)
        if s0 < self.s_w < 1:
            S.append(self.s_w)
        return sorted(set(round(s, 6) for s in S))

    def build(self, o, levels=(0, 1, 2)):
        F = self.F
        for L in levels:
            mb = o.lod[L]
            ns = (7, 4, 2)[L]
            nt_per = (1.2, 2.0, 4.0)[L]
            self._surface(mb, ns, nt_per)
            self._edges(mb, ns, nt_per, L)
            self._ridges(mb, L)
        if 0 in levels:
            if self.tiles:
                self._tiles(o.lod[0])
            self._eave_rows(o.lod[0])

    # 면(지붕 윗면)
    def _patches(self):
        """(이름, s 시작, 평면 함수(s,t)→(x,z), 바깥 방향 로컬)"""
        out = []
        for sz in (1, -1):
            out.append(("long", 0.0, (lambda s, t, sz=sz: self.long_xz(s, t, sz)), (0, 1, sz), sz))
        if self.style in ("hip", "pyramid", "paljak"):
            s0 = self.s_k if self.style == "paljak" else 0.0
            for sx in (1, -1):
                out.append(("end", s0, (lambda s, t, sx=sx: self.end_xz(s, t, sx)), (sx, 1, 0), sx))
        return out

    def _surface(self, mb, ns, nt_per):
        F = self.F
        for kind, s0, fn, outl, sg in self._patches():
            S = self.s_list(ns, s0)
            span = 2 * (self.A if kind == "long" else self.B)
            nt = max(2, int(math.ceil(span / nt_per)))
            rows = []
            for s in S:
                rows.append([self.pt(*fn(s, k / nt), s) for k in range(nt + 1)])
            out = F.d(*outl)
            for i in range(len(S) - 1):
                for k in range(nt):
                    mb.face([rows[i][k], rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]], GIWA, out=out)

    def _edges(self, mb, ns, nt_per, L):
        """처마 앞면(두께 te) + 처마 아랫면(벽선까지) + 맞배 박공 끝."""
        F, te = self.F, self.te
        for kind, s0, fn, outl, sg in self._patches():
            span = 2 * (self.A if kind == "long" else self.B)
            nt = max(2, int(math.ceil(span / nt_per)))
            top = [self.pt(*fn(1.0, k / nt), 1.0) for k in range(nt + 1)]
            bot = [self.pt(*fn(1.0, k / nt), 1.0, -te) for k in range(nt + 1)]
            hor = F.d(outl[0], 0, outl[2])
            for k in range(nt):
                mb.face([bot[k], bot[k + 1], top[k + 1], top[k]], GIWA, out=hor)
            # 처마 아랫면: s_w..1
            sw = max(s0, self.s_w if kind == "long" else 1.0 - self.ov_x / self.B)
            S = [sw + (1 - sw) * j / 2 for j in range(3)]
            rows = [[self.pt(*fn(s, k / nt), s, -te) for k in range(nt + 1)] for s in S]
            for i in range(len(S) - 1):
                for k in range(nt):
                    mb.face([rows[i][k], rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]], self.soffit_mat, out=(0, -1, 0))
        if self.style == "gable":
            self._gable_ends(mb, ns)
        if self.style == "paljak":
            self._hapgak(mb, ns)

    def _gable_ends(self, mb, ns):
        F, te, A, B = self.F, self.te, self.A, self.B
        Aw = A - self.ov_x
        S = [k / ns for k in range(ns + 1)]
        for sx in (1, -1):
            out = F.d(sx, 0, 0)
            for sz in (1, -1):
                tops = [self.pt(sx * A, sz * B * s, s) for s in S]
                bots = [self.pt(sx * A, sz * B * s, s, -te) for s in S]
                for i in range(ns):
                    mb.face([bots[i], bots[i + 1], tops[i + 1], tops[i]], GIWA, out=out)
                # 박공 아랫면(벽선 밖으로 내민 부분)
                inner = [self.pt(sx * Aw, sz * B * s, s, -te) for s in S]
                for i in range(ns):
                    mb.face([bots[i], bots[i + 1], inner[i + 1], inner[i]], self.soffit_mat, out=(0, -1, 0))
            # 박공벽(풍판): 벽선 x = Aw, 아래 = 벽 윗면, 위 = 지붕 아랫면
            n = 8
            Bw = B * self.s_w
            pts = []
            for k in range(n + 1):
                z = -Bw + 2 * Bw * k / n
                s = abs(z) / B
                pts.append(self.pt(sx * Aw, z, s, -te))
            pts.append(F.p(self.ox + sx * Aw, self.y_wall, self.oz + Bw))
            pts.append(F.p(self.ox + sx * Aw, self.y_wall, self.oz - Bw))
            mb.face(pts, self.gable_mat, out=out)

    def _hapgak(self, mb, ns):
        """팔작 합각벽: x = ±Xg 의 세모(긴 면 박공선 아래)."""
        F, B = self.F, self.B
        S = [self.s_k * k / max(2, ns) for k in range(max(2, ns) + 1)]
        for sx in (1, -1):
            pts = [self.pt(sx * self.Xg, B * s, s, -0.01) for s in reversed(S)]
            pts += [self.pt(sx * self.Xg, -B * s, s, -0.01) for s in S[1:]]
            mb.face(pts, self.gable_mat, out=F.d(sx, 0, 0))

    def _ridges(self, mb, L):
        F = self.F
        X0 = self.X_long(0.0)
        y = self.y_ridge
        h, w = (0.36, 0.42) if L < 2 else (0.3, 0.4)
        if X0 > 0.05:
            a = self.pt(-X0, 0.0, 0.0)
            b = self.pt(X0, 0.0, 0.0)
            ext = 0.25 if self.style in ("gable", "paljak") else 0.0
            c = mul(add(a, b), 0.5)
            obox(mb, add(c, (0, h / 2 - 0.08, 0)), F.ux, UP, F.uz, X0 + ext, h / 2, w / 2, GIWA)
            if L < 2:   # 망와(용마루 양 끝을 들어 올린 덩어리)
                for sx in (1, -1):
                    e = self.pt(sx * (X0 + ext - 0.1), 0.0, 0.0)
                    obox(mb, add(e, (0, h + 0.05, 0)), F.ux, UP, F.uz, 0.14, 0.2, w / 2 + 0.02, GIWA)
        if self.style in ("hip", "pyramid", "paljak") and L < 2:
            s0 = self.s_k if self.style == "paljak" else 0.0
            n = 5
            for sx in (1, -1):
                for sz in (1, -1):
                    S = [s0 + (1 - s0) * k / n for k in range(n + 1)]
                    pts = [self.pt(sx * (self.a_r + self.B * s), sz * self.B * s, s, 0.06) for s in S]
                    for i in range(n):
                        _ridge_seg(mb, pts[i], pts[i + 1], 0.26, 0.2)
            if self.style == "paljak":   # 합각 박공 위 마루(내림마루)
                S = [self.s_k * k / 4 for k in range(5)]
                for sx in (1, -1):
                    for sz in (1, -1):
                        pts = [self.pt(sx * self.Xg, sz * self.B * s, s, 0.06) for s in S]
                        for i in range(4):
                            _ridge_seg(mb, pts[i], pts[i + 1], 0.24, 0.18)
        if self.style == "pyramid":   # 절병통
            top = self.pt(0.0, 0.0, 0.0)
            cyl(mb, add(top, (0, -0.05, 0)), UP, 0.22, 0.16, 0.35, 8, GIWA, caps=(False, True))
            cyl(mb, add(top, (0, 0.3, 0)), UP, 0.14, 0.04, 0.35, 8, GIWA, caps=(False, False))
        if self.style == "gable" and L < 2:   # 박공 끝 내림마루(얇게)
            for sx in (1, -1):
                for sz in (1, -1):
                    S = [k / 4 for k in range(5)]
                    pts = [self.pt(sx * (self.A - 0.12), sz * self.B * s, s, 0.05) for s in S]
                    for i in range(4):
                        _ridge_seg(mb, pts[i], pts[i + 1], 0.24, 0.16)

    def _tiles(self, mb):
        """수키와 줄: 물매 방향으로 볼록한 세모 단면 띠, 0.25m 간격(막새와 같은 간격)."""
        gap = 0.25
        n = 6
        for kind, s0, fn, outl, sg in self._patches():
            if kind == "long":
                Xe = self.X_long(1.0)
                k0 = int(Xe / gap)
                for k in range(-k0, k0 + 1):
                    x = k * gap
                    # 이 줄이 시작하는 s(추녀선에 닿는 곳). 맞배 = 용마루부터, 팔작 = 합각선 안쪽은 용마루부터
                    inner = self.A if self.style == "gable" else (self.Xg if self.style == "paljak" else self.a_r)
                    smin = 0.0 if abs(x) <= inner + 1e-6 else (abs(x) - self.a_r) / self.B
                    if smin > 0.96:
                        continue
                    S = [smin + (1 - smin) * j / n for j in range(n + 1)]
                    line = [(x, sg * self.B * s, s) for s in S]
                    _tile_row(self, mb, line, along_x=False)
            else:
                k0 = int(self.B / gap)
                for k in range(-k0, k0 + 1):
                    z = k * gap
                    smin = max(s0, abs(z) / self.B)
                    if smin > 0.96:
                        continue
                    S = [smin + (1 - smin) * j / n for j in range(n + 1)]
                    line = [(sg * (self.a_r + self.B * s), z, s) for s in S]
                    _tile_row(self, mb, line, along_x=True)

    def _eave_rows(self, mb):
        """처마 끝: 막새 흰 점 띠(지름 0.12 흰 원판, 0.25 간격 — docs/10 3-4, 먹 테는 외곽선이 그림) 와 그 아래 서까래 끝."""
        gap = 0.25
        te = self.te
        for kind, s0, fn, outl, sg in self._patches():
            if kind == "long":
                Xe = self.X_long(1.0)
                xs = [k * gap for k in range(-int(Xe / gap), int(Xe / gap) + 1)]
                pts = [(x, sg * self.B) for x in xs]
                hor_l = (0, 0, sg)
            else:
                zs = [k * gap for k in range(-int(self.B / gap), int(self.B / gap) + 1)]
                pts = [(sg * (self.a_r + self.B), z) for z in zs]
                hor_l = (sg, 0, 0)
            hor = self.F.d(*hor_l)
            for i, (x, z) in enumerate(pts):
                c = self.pt(x, z, 1.0, -0.07)
                c = madd(c, (hor, 0.02))
                if self.makse:
                    _disc(mb, madd(c, (hor, 0.006)), hor, 0.06, 8, "M_Plaster_White")
                if self.rafters:
                    # 서까래 끝: 막새 사이, 처마 앞면 아래
                    x2, z2 = (x + gap / 2, z) if kind == "long" else (x, z + gap / 2)
                    if abs(x2) <= (self.X_long(1.0) if kind == "long" else 99) and abs(z2) <= self.B:
                        r = self.pt(x2, z2, 1.0, -te - 0.07)
                        r = madd(r, (hor, -0.12))
                        cyl(mb, r, hor, 0.055, 0.055, 0.2, 6, self.rafter_mat, caps=(False, True), smooth=True)


def _ridge_seg(mb, a, b, w, h):
    d = sub(b, a)
    L = length(d)
    if L < 1e-4:
        return
    t = mul(d, 1 / L)
    s = norm(cross(UP, t))
    if length(s) < 1e-6:
        s = (1.0, 0.0, 0.0)
    u = norm(cross(t, s))
    obox(mb, mul(add(a, b), 0.5), s, u, t, w / 2, h / 2, L / 2 + w / 4, GIWA)


def _tile_row(g, mb, line, along_x):
    """line = [(x, z, s)] 줄 가운데. 단면: 지붕 면 위 ±0.09 → 꼭대기 +0.075."""
    hw, hh = 0.09, 0.075
    L, R, T = [], [], []
    for x, z, s in line:
        if along_x:
            l, r = (x, z - hw), (x, z + hw)
        else:
            l, r = (x - hw, z), (x + hw, z)
        L.append(g.pt(l[0], l[1], s, 0.005))
        R.append(g.pt(r[0], r[1], s, 0.005))
        T.append(g.pt(x, z, s, hh))
    for i in range(len(line) - 1):
        up = (0, 1, 0)
        sd = sub(R[i], L[i])
        mb.face([L[i], L[i + 1], T[i + 1], T[i]], GIWA, out=add(up, mul(norm(sd), -1.0)))
        mb.face([T[i], T[i + 1], R[i + 1], R[i]], GIWA, out=add(up, norm(sd)))
    # 끝 막음(처마 쪽)
    mb.face([L[-1], R[-1], T[-1]], GIWA, out=sub(T[-1], T[-2]))


def _disc(mb, c, n, r, k, mat):
    from .geom import ring
    pts = ring(c, n, r, k, ref=(0.0, 1.0, 0.0) if abs(n[1]) < 0.9 else (1.0, 0.0, 0.0))
    mb.face(pts, mat, out=n)


# ---------------------------------------------------------------- 담장 기와 갓(작은 맞배)
def wall_giwa_cap(o, P, u0, u1, y, thick, over=0.18, rise=0.22, levels=(0, 1, 2), rows=True):
    """담장 위 기와 지붕 띠(Panel 위, 벽 두께 thick 의 가운데에 용마루)."""
    wc = -thick / 2
    hb = thick / 2 + over
    for L in levels:
        mb = o.lod[L]
        for sw in (1, -1):
            a0, a1 = P.p(u0 - 0.1, y + rise, wc), P.p(u1 + 0.1, y + rise, wc)
            b0, b1 = P.p(u0 - 0.1, y, wc + sw * hb), P.p(u1 + 0.1, y, wc + sw * hb)
            mb.face([a0, a1, b1, b0], GIWA, out=add(UP, mul(P.n3, sw)))
            c0, c1 = P.p(u0 - 0.1, y - 0.08, wc + sw * hb), P.p(u1 + 0.1, y - 0.08, wc + sw * hb)
            mb.face([c0, c1, b1, b0], GIWA, out=mul(P.n3, sw))
            d0, d1 = P.p(u0 - 0.1, y - 0.08, wc + sw * thick / 2), P.p(u1 + 0.1, y - 0.08, wc + sw * thick / 2)
            mb.face([c0, c1, d1, d0], GIWA, out=(0, -1, 0))
        for uu, sg in ((u0 - 0.1, -1), (u1 + 0.1, 1)):
            mb.face([P.p(uu, y + rise, wc), P.p(uu, y, wc + hb), P.p(uu, y - 0.08, wc + hb),
                     P.p(uu, y - 0.08, wc - hb), P.p(uu, y, wc - hb)], GIWA, out=mul(P.t3, sg))
        obox(mb, P.p((u0 + u1) / 2, y + rise + 0.06, wc), P.t3, UP, P.n3, (u1 - u0) / 2 + 0.14, 0.09, 0.12, GIWA)
    if rows and 0 in levels:
        mb = o.lod[0]
        n = int((u1 - u0 + 0.2) / 0.25)
        for k in range(n + 1):
            u = u0 - 0.05 + (u1 - u0 + 0.1) * k / max(1, n)
            for sw in (1, -1):
                c = P.p(u, y - 0.03, wc + sw * (hb + 0.01))
                _disc(mb, madd(c, (mul(P.n3, sw), 0.005)), mul(P.n3, sw), 0.05, 6, "M_Plaster_White")
