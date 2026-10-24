# -*- coding: utf-8 -*-
"""zone1.json → 구역(district)별 오브젝트 목록(LOD 메시 묶음). 블렌더 없이 돌아간다(통계·화면 삼각형 추정 포함).

오브젝트 이름(ASCII, FBX·Unity 안전):
  <유형>_<zone1 블록 번호 3자리>_LOD0|1|2          건물 하나 = 블록 하나(재질별 서브메시)
  <유형>_<번호>_Detail_LOD0                        같은 건물의 작은 디테일(그림자 끔 권장)
  Fences_<구역>_LOD*, Fences_<구역>_Detail_LOD0     담·옹벽·난간 조각을 구역마다 하나로
  Wall_<k>_LOD*                                    성곽 덩어리(점 3칸씩 ≈ 24m)
  Pole_<k>_LOD*                                    전봇대 + 거기서 나가는 전선
  WinLit_<구역>_t<00..09>                          창문 불 판(문턱 0.05·0.15·…·0.95 묶음, LOD 밖)
  Sign_<nnn>                                       간판 자리(0~1 UV, LOD 밖)
  Sock_<종류>_<nnn>                                자리 표시(빈 오브젝트: 전선 받는 곳·상가 불빛·천장 등)
"""
import math

from .geom import MB, rng, catenary, tube, UP, norm, sub, add, mul, length, madd
from .zone import Zone, classify, DISTRICTS, poly_closest
from .facade import Out
from .btypes import GENERATORS, castle_wall
from . import details as D
from . import dense

FENCE_TYPES = ("fence_wall", "fence_grille", "retaining_concrete", "retaining_stone", "railing")
LOD_DIST = (40.0, 110.0)     # LOD0→1, LOD1→2 바뀌는 거리(m) 목표 — 게임 카메라 세로 FOV 45° 기준
CULL_FRAC = 0.006            # 화면 높이 0.6% 아래는 그리지 않음
FOV = 45.0
LIT_BUCKETS = 10


class Obj:
    """내보낼 오브젝트 하나(LOD 묶음 또는 단일 메시 또는 빈 오브젝트)."""

    def __init__(self, name, district, kind, lods=None, det=None, single=None, empty=None, info=None):
        self.name, self.district, self.kind = name, district, kind
        self.lods = lods            # [MB, MB, MB] 또는 None
        self.det = det              # MB 또는 None (LOD0 디테일)
        self.single = single        # MB (LOD 없는 메시)
        self.empty = empty          # (pos, normal) 빈 오브젝트
        self.info = info or {}

    def all_mbs(self):
        out = []
        if self.lods:
            out += [m for m in self.lods if m is not None]
        if self.det is not None:
            out.append(self.det)
        if self.single is not None:
            out.append(self.single)
        return out

    def bounds(self):
        bs = [m.bounds() for m in self.all_mbs() if m is not None and not m.empty()]
        if not bs:
            return None
        lo = tuple(min(b[0][i] for b in bs) for i in range(3))
        hi = tuple(max(b[1][i] for b in bs) for i in range(3))
        return lo, hi

    def origin(self):
        b = self.bounds()
        if b is None:
            return self.empty[0] if self.empty else (0.0, 0.0, 0.0)
        lo, hi = b
        return ((lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2)


def merge_outs(outs):
    m = Out(None)
    for o in outs:
        for L in range(3):
            m.lod[L].append(o.lod[L])
        m.det.append(o.det)
        m.lit += o.lit
        m.signs += o.signs
        m.sockets += o.sockets
    return m


def lod_thresholds(size):
    k = 2 * math.tan(math.radians(FOV / 2))
    t0 = size / (k * LOD_DIST[0])
    t1 = size / (k * LOD_DIST[1])
    t0 = min(0.95, max(0.08, t0))
    t1 = min(t0 * 0.6, max(0.03, t1))
    return round(t0, 4), round(t1, 4), CULL_FRAC


def generate(zone_path, log=print):
    zone = Zone(zone_path)
    classify(zone)
    objs = []
    outs_by_block = {}
    fence_groups = {}
    for b in zone.blocks:
        if not b.btype:
            continue
        o = Out(b)
        o.meta = {}
        o._pending_holes = []
        GENERATORS[b.btype](zone, b, o)
        if b.btype in dense.DENSE_TYPES:
            dense.densify(zone, b, o)   # 4차: 벽면 밀도 규칙 배치
        outs_by_block[b.idx] = o
        if b.btype in FENCE_TYPES:
            fence_groups.setdefault(b.district, []).append((b, o))
            continue
        name = "%s_%03d" % (b.btype, b.idx)
        info = dict(block=b.name, index=b.idx, btype=b.btype, kind=b.kind, center=[b.cx, b.cy, b.cz],
                    size=[b.w, b.h, b.d], yaw=b.yaw, floors=b.floors, use=b.use, roles=b.roles, meta=o.meta)
        objs.append((Obj(name, b.district, "building", lods=o.lod, det=o.det, info=info), o))
    tot = {}
    for o in outs_by_block.values():
        for k, v in (getattr(o, "dense", None) or {}).items():
            tot[k] = tot.get(k, 0) + v
    if tot:
        log("4차 벽면 밀도: " + " · ".join("%s %d" % kv for kv in sorted(tot.items())))
    for dist, lst in sorted(fence_groups.items()):
        m = merge_outs([o for _, o in lst])
        info = dict(blocks=[dict(block=b.name, index=b.idx, btype=b.btype, center=[b.cx, b.cy, b.cz], size=[b.w, b.h, b.d],
                                 yaw=b.yaw, meta=o.meta) for b, o in lst])
        objs.append((Obj("Fences_%s" % dist, dist, "fences", lods=m.lod, det=m.det, info=info), m))
    # 성곽: 점 3칸씩(약 24m — LOD 가 덩어리 가운데 거리로 바뀌므로 짧게)
    for wi, wall in enumerate(zone.walls):
        pts = wall["points"]
        gate = None
        for lm in zone.landmarks:
            if lm["kind"] == "wall_gate":
                d, y, i, t = poly_closest(pts, lm["pos"][0], lm["pos"][2])
                gate = (i, t)
        n = len(pts) - 1
        step = 3
        k = 0
        for i0 in range(0, n, step):
            i1 = min(n, i0 + step)
            o = Out(None)
            castle_wall(zone, wall, i0, i1, o, gate)
            objs.append((Obj("Wall_%d" % k, "Wall", "wall", lods=o.lod, det=o.det,
                             info=dict(wall=wall["name"], points=[i0, i1], gate=bool(gate and i0 <= gate[0] < i1))), o))
            k += 1
    # 전봇대·전선
    anchors = []
    for obj, o in objs:
        for s in o.sockets:
            if s["kind"] == "wire_anchor":
                anchors.append(s)
    for obj, o in _poles_and_wires(zone, anchors):
        objs.append((obj, o))
    # 구역별: 창문 불 묶음·간판·자리 표시
    final = []
    sign_no = 0
    sock_no = 0
    prop_no = 0
    lit_stats = {}
    for dist in list(DISTRICTS.keys()):
        here = [(ob, o) for ob, o in objs if ob.district == dist]
        if not here:
            continue
        buckets = [MB() for _ in range(LIT_BUCKETS)]
        nlit = 0
        for ob, o in here:
            final.append(ob)
            size = _size(ob)
            ob.info["lod_size"] = round(size, 2)
            ob.info["lod_screen_heights"] = lod_thresholds(size)
            for pts, th, out in o.lit:
                k = min(LIT_BUCKETS - 1, int(th * LIT_BUCKETS))
                buckets[k].face(pts, "M_Glass_Lit", out=out, uv=[(0, 0), (1, 0), (1, 1), (0, 1)])
                nlit += 1
            for s in o.signs:
                sign_no += 1
                mb = MB()
                for f in s["faces"]:
                    mb.face(f, "M_Sign_Atlas", uv=[(0, 0), (1, 0), (1, 1), (0, 1)])
                c = mul(add(add(s["faces"][0][0], s["faces"][0][1]), add(s["faces"][0][2], s["faces"][0][3])), 0.25)
                final.append(Obj("Sign_%03d" % sign_no, dist, "sign", single=mb,
                                 info=dict(sign_kind=s["kind"], size_m=list(s["size"]), center=[round(v, 3) for v in c],
                                           normal=[round(v, 4) for v in s["normal"]], tenant=s["tenant"], block=s["block"],
                                           faces=len(s["faces"]), atlas=atlas_hint(s["kind"], s["tenant"], sign_no))))
            for s in o.sockets:
                if s["kind"] == "wire_anchor":
                    continue
                if s["kind"] == "prop":
                    prop_no += 1
                    yaw = math.degrees(math.atan2(s["n"][0], s["n"][2]))
                    final.append(Obj("Prop_%s_%03d" % (s["prop"], prop_no), dist, "prop", empty=(s["pos"], s["n"]),
                                     info=dict(prop=s["prop"], glb="concept/art/env/props/%s.glb" % s["prop"],
                                               pos=[round(v, 3) for v in s["pos"]], yaw_deg=round(yaw, 2), host=s.get("host"))))
                    continue
                sock_no += 1
                final.append(Obj("Sock_%s_%03d" % (s["kind"], sock_no), dist, "socket", empty=(s["pos"], s["n"]),
                                 info=dict(sock_kind=s["kind"], pos=[round(v, 3) for v in s["pos"]],
                                           normal=[round(v, 4) for v in s["n"]], tenant=s.get("tenant"),
                                           **({"size": list(s["size"])} if s.get("size") else {}), **({"mull": s["mull"]} if "mull" in s else {}))))
        for k, mb in enumerate(buckets):
            if mb.empty():
                continue
            th = (k + 0.5) / LIT_BUCKETS
            final.append(Obj("WinLit_%s_t%02d" % (dist, k), dist, "winlit", single=mb,
                             info=dict(threshold=round(th, 3), panes=len(mb.F),
                                       note="LightSpot(Kind=Window, Threshold=%.2f): 켜짐 비율 > %.2f 이면 켬" % (th, th))))
        lit_stats[dist] = nlit
    return zone, final, lit_stats


# 간판 바탕(sign_atlas.json 사각형 이름) — docs/10 7장 표의 가상 상호 ↔ 띠
ATLAS_BANDS = [("국밥", "band_wood_gukbap"), ("하루편의점", "band_haru_conv"), ("번개배달", "band_mustard_bungae"),
               ("문구", "band_navy_alu"), ("세탁", "band_white_laundry"), ("철물", "band_rusty_hardware"),
               ("PC", "band_cream_lightbox"), ("제본", "band_cream_lightbox"), ("분식", "band_redbrown_bunsik")]
ATLAS_VERT = ["vert_navy", "vert_cream", "vert_slate", "vert_redbrown", "vert_white", "vert_green"]


def atlas_hint(kind, tenant, n):
    t = tenant or ""
    if kind == "band":
        for key, name in ATLAS_BANDS:
            if key in t:
                return name
        return "band_cream_lightbox"
    if kind == "projecting":
        for key, name in (("하루편의점", "vert_slate"), ("번개배달", "vert_navy"), ("국밥", "vert_redbrown")):
            if key in t:
                return name
        return ATLAS_VERT[n % len(ATLAS_VERT)]
    return "hanging_wood" if ("문패" in t or "경비실" in t) else "vert_white"


def _size(ob):
    b = ob.bounds()
    if b is None:
        return 0.0
    lo, hi = b
    return max(hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2])


# ---------------------------------------------------------------- 전봇대·전선
def _poles_and_wires(zone, anchors):
    poles = []
    for lm in zone.landmarks:
        if lm["kind"] in ("pole", "board"):
            x, y, z = lm["pos"]
            poles.append(dict(name=lm["name"], x=x, z=z, y=zone.ground(x, z), h=lm.get("height", 9.0) or 9.0))
    if not poles:
        return []
    # 줄 나누기: 성균관로(인도 위) / 골목
    street = sorted([p for p in poles if "성균관로" in p["name"] or "전단지" in p["name"]], key=lambda p: p["x"])
    alley = [p for p in poles if p not in street]
    alley.sort(key=lambda p: (p["name"]))
    seqs = [street, alley]
    res = []
    arms_of = {}
    outs = {}
    for seq in seqs:
        for i, p in enumerate(seq):
            a = seq[max(0, i - 1)]
            c = seq[min(len(seq) - 1, i + 1)]
            toward = (c["x"] - a["x"], 0.0, c["z"] - a["z"])
            if length(toward) < 1e-3:
                toward = (1.0, 0.0, 0.0)
            o = Out(None)
            r = rng("pole", p["name"])
            arms = D.utility_pole(o, (p["x"], p["y"], p["z"]), p["h"], toward, r, transformer=r.random() < 0.4)
            arms_of[p["name"]] = arms
            outs[p["name"]] = (o, p)
    # 같은 줄 이웃끼리: 위 3가닥 + 아래 2가닥 + 통신선 1
    def span(o, A, B, sag_k=0.03, n_seg=14):
        L = length(sub(B, A))
        sag = 0.15 + sag_k * L
        D.wire(o.lod[0], A, B, sag, r=0.013, n=4, seg=n_seg)
        D.wire(o.lod[1], A, B, sag, r=0.016, n=3, seg=max(4, n_seg // 2))
        D.wire(o.lod[2], A, B, sag, r=0.02, n=3, seg=4)
    for seq in seqs:
        for i in range(len(seq) - 1):
            pa, pb = seq[i], seq[i + 1]
            o, _ = outs[pa["name"]]
            A, B = arms_of[pa["name"]], arms_of[pb["name"]]
            for key in A:
                if key in B or True:
                    la = A[key]
                    lb = B[min(B.keys(), key=lambda kk: abs(kk - key))]
                    for k in range(min(len(la), len(lb))):
                        span(o, la[k], lb[k])
            # 통신선 다발(조금 더 처짐, 굵게)
            ya = pa["y"] + pa["h"] - 2.6
            yb = pb["y"] + pb["h"] - 2.6
            Aa, Bb = (pa["x"], ya, pa["z"]), (pb["x"], yb, pb["z"])
            Lab = length(sub(Bb, Aa))
            D.wire(o.lod[0], Aa, Bb, 0.3 + 0.05 * Lab, r=0.03, n=5, seg=14)
            D.wire(o.lod[1], Aa, Bb, 0.3 + 0.05 * Lab, r=0.035, n=3, seg=6)
    # 골목 첫 전봇대 ↔ 가장 가까운 길가 전봇대
    if street and alley:
        pa = alley[0]
        pb = min(street, key=lambda p: math.hypot(p["x"] - pa["x"], p["z"] - pa["z"]))
        o, _ = outs[pa["name"]]
        la, lb = arms_of[pa["name"]], arms_of[pb["name"]]
        k1, k2 = min(la), min(lb)
        for k in range(2):
            span(o, la[k1][k], lb[k2][k])
    # 인입선: 전봇대 → 가까운 건물 받는 자리(최대 3), 각 받는 자리는 한 번만
    used = set()
    for name, (o, p) in sorted(outs.items()):
        arms = arms_of[name]
        low = arms[min(arms)]
        cand = []
        for i, s in enumerate(anchors):
            if i in used:
                continue
            d = math.hypot(s["pos"][0] - p["x"], s["pos"][2] - p["z"])
            if 3.0 < d < 20.0:   # 4차: 17 → 20m
                cand.append((d, i))
        cand.sort()
        for d, i in cand[:5]:   # 4차: 전봇대마다 인입선 3 → 5
            used.add(i)
            A = low[i % len(low)]
            B = anchors[i]["pos"]
            L = length(sub(B, A))
            D.wire(o.lod[0], A, B, 0.1 + 0.035 * L, r=0.01, n=4, seg=10)
            D.wire(o.lod[1], A, B, 0.1 + 0.035 * L, r=0.014, n=3, seg=5)
            # 받는 자리 애자(벽 고리)
            D.cyl(o.det, B, anchors[i]["n"], 0.02, 0.02, 0.12, 6, "M_Metal_Grey", caps=(False, True))
    # 집과 집 사이(골목 건너편 받는 자리끼리)
    pairs = []
    for i in range(len(anchors)):
        for j in range(i + 1, len(anchors)):
            a, b = anchors[i], anchors[j]
            dn = a["n"][0] * b["n"][0] + a["n"][2] * b["n"][2]
            d = math.hypot(a["pos"][0] - b["pos"][0], a["pos"][2] - b["pos"][2])
            if dn < -0.6 and 2.8 < d < 11.0:
                # 사이에 건물이 없어야
                ok = True
                for t in (0.3, 0.5, 0.7):
                    x = a["pos"][0] + (b["pos"][0] - a["pos"][0]) * t
                    z = a["pos"][2] + (b["pos"][2] - a["pos"][2]) * t
                    if zone.solid_at(x, z, kinds=("shop", "house", "hanok", "campus")):
                        ok = False
                if ok:
                    pairs.append((d, i, j))
    pairs.sort()
    r = rng("housewires")
    taken = set()
    n_pairs = 0
    for d, i, j in pairs:
        if i in taken or j in taken or n_pairs >= 24:   # 4차: 집과 집 사이 10 → 24
            continue
        taken.add(i)
        taken.add(j)
        n_pairs += 1
        a, b = anchors[i]["pos"], anchors[j]["pos"]
        near = min(outs.values(), key=lambda op: math.hypot(op[1]["x"] - a[0], op[1]["z"] - a[2]))[0]
        for k in range(1 + (r.random() < 0.5)):
            dy = -0.25 * k
            A, B = (a[0], a[1] + dy, a[2]), (b[0], b[1] + dy - 0.2 * r.random(), b[2])
            D.wire(near.lod[0], A, B, 0.15 + 0.03 * d + 0.1 * r.random(), r=0.01, n=4, seg=10)
            D.wire(near.lod[1], A, B, 0.15 + 0.03 * d, r=0.014, n=3, seg=5)
    for k, (name, (o, p)) in enumerate(sorted(outs.items(), key=lambda kv: (kv[1][1]["x"], kv[1][1]["z"]))):
        res.append((Obj("Pole_%d" % k, "Wires", "pole", lods=o.lod, det=o.det,
                        info=dict(pole=name, pos=[p["x"], p["y"], p["z"]], height=p["h"])), o))
    return res


# ---------------------------------------------------------------- 통계·화면 삼각형 추정
def stats(objs):
    out = {}
    for ob in objs:
        d = out.setdefault(ob.district, dict(lod0=0, lod1=0, lod2=0, detail=0, winlit=0, signs=0, objects=0, sockets=0))
        if ob.lods:
            d["lod0"] += ob.lods[0].tris()
            d["lod1"] += ob.lods[1].tris()
            d["lod2"] += ob.lods[2].tris()
            d["objects"] += 1
        if ob.det is not None:
            d["detail"] += ob.det.tris()
        if ob.kind == "winlit":
            d["winlit"] += ob.single.tris()
        if ob.kind == "sign":
            d["signs"] += ob.single.tris()
        if ob.kind == "socket":
            d["sockets"] += 1
    return out


def view_tris(objs, cam, look, fov=FOV, aspect=16 / 9, shadow_dist=60.0, lod_bias=1.0):
    """게임 카메라에서 보이는(시야 원뿔 안) 오브젝트의 LOD 를 Unity 식으로 골라 삼각형을 센다.
    shadow = 그림자 캐스케이드(2단, 60m) 에서 다시 그리는 몫(디테일·창문 불·간판은 그림자 끔 가정)."""
    f = norm(look)
    tan_v = math.tan(math.radians(fov / 2))
    tan_h = tan_v * aspect
    total, shadow, by_lod = 0, 0, [0, 0, 0, 0]
    for ob in objs:
        b = ob.bounds()
        if b is None:
            continue
        lo, hi = b
        c = ((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2)
        rad = length(sub(hi, lo)) / 2
        v = sub(c, cam)
        dist = max(0.1, length(v))
        z = v[0] * f[0] + v[1] * f[1] + v[2] * f[2]
        lat = math.sqrt(max(0.0, dist * dist - z * z))
        if z < -rad or lat - rad > (abs(z) + rad) * tan_h * 1.05:
            # 화면 밖: 그림자만(카메라 60m 안)
            vis = False
        else:
            vis = True
        if ob.lods:
            size = max(hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2])
            rel = size / (2 * dist * tan_v) * lod_bias
            t0, t1, tc = lod_thresholds(size)
            L = 0 if rel >= t0 else (1 if rel >= t1 else (2 if rel >= tc else 3))
            if L < 3:
                tri = ob.lods[L].tris() + (ob.det.tris() if (L == 0 and ob.det is not None) else 0)
                main = ob.lods[L].tris()
                if vis:
                    total += tri
                    by_lod[L] += tri
                if dist - rad < shadow_dist:
                    shadow += main * 2
        elif ob.single is not None and vis:
            total += ob.single.tris()
    return dict(view=total, shadow_2casc=shadow, by_lod=by_lod)
