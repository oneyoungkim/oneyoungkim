# -*- coding: utf-8 -*-
"""순수 파이썬 메시 쌓기. 블렌더 없이 돌아간다(통계·검사는 시스템 파이썬으로).

좌표는 Unity 숫자 그대로: X = 동, Y = 위, Z = 북(zone1.json 과 같음). 면의 바깥 방향은 이 숫자 공간의
오른손 규칙(Newell 법선)으로 정한다. 블렌더로 옮길 때(blend.py) (X, Y, Z) → (-X, -Z, Y) 로 바꾸면서 감기 방향을
뒤집는다 — 그러면 블렌더 → FBX/glTF(+Y 위, +Z 정면) → Unity(X 뒤집음) 를 거쳐 다시 같은 숫자·같은 바깥 방향이 된다.

UV 는 따로 주지 않으면 '월드 미터': 벽은 u = 바깥에서 볼 때 오른쪽으로 잰 거리, v = 높이(경사 지붕은 오르막 거리),
윗면은 (x, z). 그래서 어느 건물이든 1 UV = 1m 로 같은 밀도다(재질 Tiling = 1 / 텍스처 한 장의 실제 크기 — mats.py).
"""
import math
import random
import zlib

SEED = 20261006   # 키트 전체 씨앗. 바꾸면 모든 건물의 창 배치·색·소품이 바뀐다(같은 값이면 같은 결과)


def rng(*keys):
    """이름·좌표 같은 키로 만든 고정 난수(파이썬 해시 무작위화와 무관 — crc32)."""
    s = "|".join(str(k) for k in keys)
    return random.Random(zlib.crc32(s.encode("utf-8")) ^ SEED)


# ---------------------------------------------------------------- 벡터
def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def mul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def length(a):
    return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])


def norm(a):
    L = length(a)
    return (0.0, 0.0, 0.0) if L < 1e-12 else (a[0] / L, a[1] / L, a[2] / L)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def madd(p, *terms):
    """p + Σ v·s  (terms = (v, s), (v, s) ...)"""
    x, y, z = p
    for v, s in terms:
        x += v[0] * s
        y += v[1] * s
        z += v[2] * s
    return (x, y, z)


UP = (0.0, 1.0, 0.0)


def newell(pts):
    nx = ny = nz = 0.0
    n = len(pts)
    for i in range(n):
        c, d = pts[i], pts[(i + 1) % n]
        nx += (c[1] - d[1]) * (c[2] + d[2])
        ny += (c[2] - d[2]) * (c[0] + d[0])
        nz += (c[0] - d[0]) * (c[1] + d[1])
    L = math.sqrt(nx * nx + ny * ny + nz * nz)
    if L < 1e-12:
        return None, 0.0
    return (nx / L, ny / L, nz / L), L / 2.0


def world_uv(pts, n):
    nx, ny, nz = n
    if abs(ny) > 0.97:
        if ny > 0:
            return [(p[0], p[2]) for p in pts]
        return [(-p[0], p[2]) for p in pts]
    tx, tz = -nz, nx
    L = math.hypot(tx, tz)
    t = (tx / L, 0.0, tz / L)
    b = cross(t, n)
    return [(dot(p, t), dot(p, b)) for p in pts]


# ---------------------------------------------------------------- 메시 묶음
class MB:
    """면 목록(정점은 면마다 따로 — 블렌더로 옮길 때 같은 자리 정점을 합친다)."""

    __slots__ = ("V", "F", "M", "UV", "S")

    def __init__(self):
        self.V = []
        self.F = []
        self.M = []
        self.UV = []
        self.S = []

    def face(self, pts, mat, out=None, uv=None, smooth=False):
        """pts 다각형 하나. out(바깥 방향 벡터)을 주면 감기 방향을 그쪽으로 맞춘다. 넓이 0 이면 건너뜀."""
        if len(pts) > 3:   # 겹친 꼭짓점(뾰족한 끝·지붕 꼭대기) 빼기
            keep, kuv = [], []
            for i, p in enumerate(pts):
                q = pts[i - 1]
                if abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2]) > 1e-6:
                    keep.append(p)
                    if uv is not None:
                        kuv.append(uv[i])
            if len(keep) < 3:
                return False
            pts = keep
            if uv is not None:
                uv = kuv
        n, area = newell(pts)
        if n is None or area < 1e-6:
            return False
        if out is not None and dot(n, out) < 0:
            pts = pts[::-1]
            n = (-n[0], -n[1], -n[2])
            if uv is not None:
                uv = uv[::-1]
        if uv is None:
            uv = world_uv(pts, n)
        base = len(self.V)
        self.V.extend(pts)
        self.F.append(tuple(range(base, base + len(pts))))
        self.M.append(mat)
        self.UV.append(list(uv))
        self.S.append(smooth)
        return True

    def tris(self):
        return sum(len(f) - 2 for f in self.F)

    def append(self, o):
        base = len(self.V)
        self.V.extend(o.V)
        self.F.extend(tuple(i + base for i in f) for f in o.F)
        self.M.extend(o.M)
        self.UV.extend(o.UV)
        self.S.extend(o.S)

    def empty(self):
        return not self.F

    def bounds(self):
        if not self.V:
            return None
        xs = [p[0] for p in self.V]
        ys = [p[1] for p in self.V]
        zs = [p[2] for p in self.V]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

    def mats(self):
        out = {}
        for f, m in zip(self.F, self.M):
            out[m] = out.get(m, 0) + len(f) - 2
        return out


# ---------------------------------------------------------------- 틀(로컬 좌표)
class Frame:
    """zone1 블록 yaw 규약 그대로: 로컬 +X → 월드 (cos, -sin), 로컬 +Z → 월드 (sin, cos). 위에서 볼 때 시계 방향 +."""

    __slots__ = ("cx", "cz", "yaw", "ux", "uz")

    def __init__(self, cx, cz, yaw=0.0):
        a = math.radians(yaw)
        self.cx, self.cz, self.yaw = cx, cz, yaw
        self.ux = (math.cos(a), 0.0, -math.sin(a))
        self.uz = (math.sin(a), 0.0, math.cos(a))

    def p(self, x, y, z):
        return (self.cx + self.ux[0] * x + self.uz[0] * z, y, self.cz + self.ux[2] * x + self.uz[2] * z)

    def d(self, x, y, z):
        return (self.ux[0] * x + self.uz[0] * z, y, self.ux[2] * x + self.uz[2] * z)

    def local(self, X, Z):
        dx, dz = X - self.cx, Z - self.cz
        return dx * self.ux[0] + dz * self.ux[2], dx * self.uz[0] + dz * self.uz[2]


WORLD = Frame(0.0, 0.0, 0.0)

_BOX_FACES = {  # 이름: (바깥 축, 모서리 4개(축 부호 x,y,z))
    "+x": ((1, 0, 0), ((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1))),
    "-x": ((-1, 0, 0), ((-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1))),
    "+y": ((0, 1, 0), ((-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1))),
    "-y": ((0, -1, 0), ((-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1))),
    "+z": ((0, 0, 1), ((1, -1, 1), (1, 1, 1), (-1, 1, 1), (-1, -1, 1))),
    "-z": ((0, 0, -1), ((-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1))),
}


def obox(mb, c, ax, ay, az, hx, hy, hz, mat, skip=(), mats=None):
    """중심 c, 축 ax·ay·az(단위 벡터), 반 크기 hx·hy·hz 인 상자. skip = 빼는 면 이름, mats = 면별 재질."""
    for name, (o, cs) in _BOX_FACES.items():
        if name in skip:
            continue
        pts = [madd(c, (ax, sx * hx), (ay, sy * hy), (az, sz * hz)) for sx, sy, sz in cs]
        out = madd((0.0, 0.0, 0.0), (ax, o[0]), (ay, o[1]), (az, o[2]))
        mb.face(pts, (mats or {}).get(name, mat), out=out)


def box(mb, F, x0, x1, y0, y1, z0, z1, mat, skip=(), mats=None):
    """틀 F 안의 축 정렬 상자(로컬 x0..x1, 월드 높이 y0..y1, 로컬 z0..z1)."""
    if x1 - x0 < 1e-4 or y1 - y0 < 1e-4 or z1 - z0 < 1e-4:
        return
    c = F.p((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    obox(mb, c, F.ux, UP, F.uz, (x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2, mat, skip, mats)


def seg_box(mb, a, b, w, h, mat, up=UP, ext=0.0, skip=()):
    """a→b 를 잇는 각진 막대(폭 w, 높이 h). up 은 단면의 '위' 기준. ext = 양 끝을 늘리는 길이(이음 겹치기)."""
    d = sub(b, a)
    L = length(d)
    if L < 1e-5:
        return
    t = mul(d, 1.0 / L)
    s = norm(cross(up, t))
    if length(s) < 1e-6:
        s = norm(cross((1.0, 0.0, 0.0), t))
    u = cross(t, s)
    c = mul(add(a, b), 0.5)
    obox(mb, c, s, u, t, w / 2, h / 2, L / 2 + ext, mat, skip=skip)


def quad(mb, a, b, c, d, mat, out=None, uv=None, smooth=False):
    return mb.face([a, b, c, d], mat, out=out, uv=uv, smooth=smooth)


def ring(c, axis, r, n, ref=None, phase=0.0):
    axis = norm(axis)
    if ref is None:
        ref = (0.0, 0.0, 1.0) if abs(axis[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = norm(cross(axis, ref))
    v = cross(axis, u)
    out = []
    for k in range(n):
        a = phase + 2 * math.pi * k / n
        out.append(madd(c, (u, r * math.cos(a)), (v, r * math.sin(a))))
    return out


def cyl(mb, base, axis, r0, r1, h, n, mat, caps=(False, True), smooth=True, cap_mat=None, ref=None, phase=None):
    """base 에서 axis 방향으로 높이 h 인 원기둥(원뿔대). 옆면 매끈(smooth)."""
    axis = norm(axis)
    if phase is None:
        phase = math.pi / n
    top = madd(base, (axis, h))
    A = ring(base, axis, r0, n, ref, phase)
    B = ring(top, axis, r1, n, ref, phase)
    for k in range(n):
        k2 = (k + 1) % n
        mid = mul(add(add(A[k], A[k2]), add(B[k], B[k2])), 0.25)
        out = sub(mid, madd(base, (axis, dot(sub(mid, base), axis))))
        mb.face([A[k], A[k2], B[k2], B[k]], mat, out=out, smooth=smooth)
    if caps[0]:
        mb.face(A[::-1], cap_mat or mat, out=mul(axis, -1.0))
    if caps[1]:
        mb.face(B, cap_mat or mat, out=axis)


def tube(mb, pts, r, n, mat, smooth=True):
    """폴리라인을 따라가는 관(점마다 평행 이동 틀 — 전선·완만한 곡선용). 끝 막음 없음."""
    if len(pts) < 2:
        return
    tans = []
    for i in range(len(pts)):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        tans.append(norm(sub(b, a)))
    ref = (0.0, 1.0, 0.0) if abs(tans[0][1]) < 0.9 else (1.0, 0.0, 0.0)
    u = norm(cross(tans[0], ref))
    rings = []
    for i, p in enumerate(pts):
        t = tans[i]
        u = norm(sub(u, mul(t, dot(u, t))))
        if length(u) < 1e-6:
            u = norm(cross(t, ref))
        v = cross(t, u)
        rings.append([madd(p, (u, r * math.cos(2 * math.pi * k / n)), (v, r * math.sin(2 * math.pi * k / n))) for k in range(n)])
    for i in range(len(pts) - 1):
        A, B = rings[i], rings[i + 1]
        c = mul(add(pts[i], pts[i + 1]), 0.5)
        for k in range(n):
            k2 = (k + 1) % n
            mid = mul(add(add(A[k], A[k2]), add(B[k], B[k2])), 0.25)
            mb.face([A[k], A[k2], B[k2], B[k]], mat, out=sub(mid, c), smooth=smooth)


def pipe(mb, pts, w, mat, n=6):
    """꺾이는 관(가스관): 마디마다 따로 각기둥, 끝을 반지름만큼 늘려 꺾인 자리가 비지 않게."""
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        d = norm(sub(b, a))
        if length(d) < 1e-6:
            continue
        a2 = madd(a, (d, -w / 2 if i > 0 else 0.0))
        b2 = madd(b, (d, w / 2 if i < len(pts) - 2 else 0.0))
        cyl(mb, a2, d, w / 2, w / 2, length(sub(b2, a2)), n, mat, caps=(i == 0, i == len(pts) - 2), smooth=True,
            ref=(0.0, 1.0, 0.0) if abs(d[1]) < 0.9 else (1.0, 0.0, 0.0))


def catenary(a, b, sag, n):
    """a→b 처진 줄(포물선 근사). sag = 가운데가 직선보다 내려가는 길이."""
    out = []
    for k in range(n + 1):
        t = k / n
        p = lerp(a, b, t)
        out.append((p[0], p[1] - sag * 4 * t * (1 - t), p[2]))
    return out


def clip_poly(poly, a, b, c):
    """2D 다각형을 a·x + b·y + c >= 0 쪽만 남긴다(Sutherland–Hodgman 한 평면)."""
    out = []
    n = len(poly)
    for i in range(n):
        P, Q = poly[i], poly[(i + 1) % n]
        fp, fq = a * P[0] + b * P[1] + c, a * Q[0] + b * Q[1] + c
        if fp >= 0:
            out.append(P)
        if (fp >= 0) != (fq >= 0):
            t = fp / (fp - fq)
            out.append((P[0] + (Q[0] - P[0]) * t, P[1] + (Q[1] - P[1]) * t))
    return out


def poly_area2(poly):
    s = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        s += x0 * y1 - x1 * y0
    return s / 2
