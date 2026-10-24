# -*- coding: utf-8 -*-
"""키트 삼각형 예산 보고: 전체 LOD 별 합 + 미리 보기 카메라 자리마다 '화면 안 삼각형'(Unity LODGroup 식으로 LOD 고름)
+ 그림자 캐스케이드가 다시 그리는 몫(디테일·창문 불·간판은 그림자 끔 가정).

실행: python tools/env/perf_report.py [shots.json] [--zone zone1.json]
  shots.json = preview_env.py 가 남긴 카메라 자리(없으면 전체 합만).
"""
import json
import math
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from envkit import build  # noqa: E402


def main():
    zone_path = os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json")
    if "--zone" in sys.argv:
        zone_path = sys.argv[sys.argv.index("--zone") + 1]
    shots_path = next((a for a in sys.argv[1:] if a.endswith(".json") and "zone" not in os.path.basename(a)), None)
    zone, objs, lit = build.generate(zone_path)
    st = build.stats(objs)
    tot = {k: sum(s[k] for s in st.values()) for k in ("lod0", "lod1", "lod2", "detail", "winlit", "signs")}
    print("| 구역 | LOD0 | LOD1 | LOD2 | 디테일(LOD0) | 창문 불 | 간판 |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    for d, s in st.items():
        print("| %s | %s | %s | %s | %s | %s | %s |" % (d, f(s["lod0"]), f(s["lod1"]), f(s["lod2"]), f(s["detail"]), f(s["winlit"]), f(s["signs"])))
    print("| **합** | **%s** | **%s** | **%s** | **%s** | %s | %s |" % tuple(f(tot[k]) for k in ("lod0", "lod1", "lod2", "detail", "winlit", "signs")))
    out = dict(totals=tot, districts=st, views=[])
    if shots_path and os.path.exists(shots_path):
        sh = json.load(open(shots_path, encoding="utf-8"))["shots"]
        print()
        props = load_props()
        print("| 자리 | 화면 안(키트) | LOD0·1·2 몫 | 그림자 2단(60m) | 그림자 4단(추정) | 소품 GLB(docs/10 LOD) | 합(키트 + 그림자 2단 + 소품) |")
        print("|---|---:|---|---:|---:|---:|---:|")
        for s in sh:
            if "yaw" not in s:
                continue
            a, p = math.radians(s["yaw"]), math.radians(s["pitch"])
            fwd = (math.sin(a) * math.cos(p), -math.sin(p), math.cos(a) * math.cos(p))
            v = build.view_tris(objs, tuple(s["cam"]), fwd, fov=s.get("fov", 45))
            v4 = v["shadow_2casc"] * 2
            pt = prop_tris(objs, props, tuple(s["cam"]), fwd, s.get("fov", 45))
            out["views"].append(dict(name=s["name"], **v, shadow_4casc=v4, props=pt))
            print("| %s | %s | %s | %s | %s | %s | %s |" % (s["name"], f(v["view"]), " / ".join(f(x) for x in v["by_lod"][:3]),
                                                      f(v["shadow_2casc"]), f(v4), f(pt), f(v["view"] + v["shadow_2casc"] + pt)))
    with open(os.path.join(ROOT, "concept", "art", "env", "buildings", "perf_report.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)


def load_props():
    p = os.path.join(ROOT, "concept", "art", "env", "props", "props_report.json")
    try:
        d = json.load(open(p, encoding="utf-8"))
    except OSError:
        return {}
    items = d if isinstance(d, dict) else {x["name"]: x for x in d}
    return {k: v for k, v in items.items() if isinstance(v, dict) and "tris" in v}


def prop_tris(objs, props, cam, fwd, fov, aspect=16 / 9):
    """docs/10 10장: LOD0 그대로 ≤12m, LOD1 50% ≤30m, LOD2 20%, 1m 미만은 60m 에서 컬링(큰 것은 120m)."""
    tan_h = math.tan(math.radians(fov / 2)) * aspect
    tot = 0
    for ob in objs:
        if ob.kind != "prop":
            continue
        info = props.get(ob.info["prop"])
        if not info:
            continue
        p = ob.empty[0]
        v = (p[0] - cam[0], p[1] - cam[1], p[2] - cam[2])
        d = math.sqrt(sum(x * x for x in v))
        z = v[0] * fwd[0] + v[1] * fwd[1] + v[2] * fwd[2]
        lat = math.sqrt(max(0.0, d * d - z * z))
        if z < -1 or lat - 1 > (abs(z) + 1) * tan_h * 1.05:
            continue
        big = max(info.get("w", 0), info.get("d", 0), info.get("h", 0)) >= 1.0
        if d > (120 if big else 60):
            continue
        k = 1.0 if d <= 12 else (0.5 if d <= 30 else 0.2)
        tot += info["tris"] * k
    return tot


def f(n):
    return "{:,}".format(int(n))


if __name__ == "__main__":
    main()
