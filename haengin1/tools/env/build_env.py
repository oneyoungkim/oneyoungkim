# -*- coding: utf-8 -*-
"""Zone1 건물 키트 생성 → 구역별 FBX + manifest. (사용법: docs/11_건물생성기.md)

실행(저장소 haengin1/ 에서):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/build_env.py -- [옵션]
옵션:
  --zone <zone1.json>     기본 unity/HaenginMainEvent/Assets/_Project/Data/zone1.json
  --out <폴더>            기본 concept/art/env/buildings
  --format fbx|glb|both   기본 fbx (Unity 는 FBX — _LOD0/1/2 이름으로 LOD Group 자동)
  --only HoomunN,Alley    이 구역만
  --blend <파일.blend>    확인용 블렌더 파일도 저장(커밋하지 않음)
  --prop-geometry 1       소품(실외기·계량기·물탱크·건조대·화분·장독·우편함) 자리에 키트의 저면 수 대용품도 넣기
                          (기본 0 = 자리 표시 Prop_<소품>_<n> 만 — 소품 GLB 는 Unity 에서 인스턴싱, docs/10 4장 담당 표)
같은 zone1.json·같은 envkit/geom.SEED 면 같은 메시가 나온다(manifest 의 geometry_sha1 로 확인).
"""
import hashlib
import json
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))   # haengin1/
sys.path.insert(0, HERE)

import bpy  # noqa: E402

from envkit import build, blend  # noqa: E402
from envkit.mats import MATS  # noqa: E402
from envkit.zone import DISTRICTS  # noqa: E402


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(zone=os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json"),
             out=os.path.join(ROOT, "concept", "art", "env", "buildings"), format="fbx", only=None, blend=None,
             **{"prop-geometry": "0"})
    i = 0
    while i < len(a):
        k = a[i].lstrip("-")
        o[k] = a[i + 1]
        i += 2
    if o["only"]:
        o["only"] = o["only"].split(",")
    return o


def geom_hash(objs):
    h = hashlib.sha1()
    for ob in objs:
        h.update(ob.name.encode())
        for mb in ob.all_mbs():
            for p in mb.V:
                h.update(("%.4f,%.4f,%.4f;" % p).encode())
            for m in mb.M:
                h.update(m.encode())
    return h.hexdigest()


def main():
    o = args()
    from envkit import details
    details.PROP_GEOMETRY = o["prop-geometry"] == "1"
    t0 = time.time()
    zone, objs, lit = build.generate(o["zone"])
    print("[env] 생성 %.1fs · 오브젝트 %d" % (time.time() - t0, len(objs)))
    os.makedirs(o["out"], exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    files = {}
    for dist in DISTRICTS:
        if o["only"] and dist not in o["only"]:
            continue
        here = [ob for ob in objs if ob.district == dist]
        if not here:
            continue
        coll = bpy.data.collections.new("Z1_" + dist)
        bpy.context.scene.collection.children.link(coll)
        root, made = blend.build_district(objs, dist, coll)
        base = os.path.join(o["out"], "Z1_%s" % dist)
        if o["format"] in ("fbx", "both"):
            blend.export_fbx(base + ".fbx", made)
            files[dist + ".fbx"] = os.path.getsize(base + ".fbx")
        if o["format"] in ("glb", "both"):
            blend.export_glb(base + ".glb", made)
            files[dist + ".glb"] = os.path.getsize(base + ".glb")
        print("[env] %s: %d 오브젝트 → %s" % (dist, len(made), base))
    print("[env] 6차 먼 LOD 재질 줄임: 서브메시 %d → %d" % (blend.REDUCE_STATS["before"], blend.REDUCE_STATS["after"]))
    if o.get("blend"):
        bpy.ops.wm.save_as_mainfile(filepath=o["blend"])
    st = build.stats(objs)
    man = dict(
        generator="haengin1/tools/env/build_env.py", doc="haengin1/docs/11_건물생성기.md",
        zone=os.path.relpath(o["zone"], ROOT).replace("\\", "/"), zone_version=zone.meta.get("version"),
        seed=__import__("envkit.geom", fromlist=["SEED"]).SEED, geometry_sha1=geom_hash(objs),
        axes="Unity 좌표 그대로(X 동, Y 위, Z 북). 프리팹을 원점·회전 0·배율 1 로 놓음",
        lod=dict(distances_m=list(build.LOD_DIST), cull_screen_height=build.CULL_FRAC, camera_fov_deg=build.FOV,
                 note="lod_screen_heights = [LOD0→1, LOD1→2, 안 그림] Unity LODGroup screenRelativeTransitionHeight(LOD Bias 1 기준)"),
        districts={k: dict(desc=v, stats=st.get(k), lit_panes=lit.get(k, 0)) for k, v in DISTRICTS.items()},
        files_bytes=files,
        materials={k: dict(v, tiling=round(1.0 / v["tile"], 4)) for k, v in MATS.items()},
        objects=[dict(name=ob.name, district=ob.district, kind=ob.kind, info=_jsonable(ob.info),
                      tris=[m.tris() for m in ob.lods] if ob.lods else (ob.single.tris() if ob.single else 0),
                      detail_tris=ob.det.tris() if ob.det is not None else 0,
                      origin=[round(v, 3) for v in ob.origin()]) for ob in objs],
    )
    with open(os.path.join(o["out"], "zone1_buildings_manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(man, f, ensure_ascii=False, indent=1)
    tot = [sum((s or {}).get(k, 0) for s in st.values()) for k in ("lod0", "lod1", "lod2", "detail")]
    print("[env] 삼각형 LOD0 %d · LOD1 %d · LOD2 %d · 디테일 %d · 파일 %s" % (*tot, files))
    print("[env] 끝 %.1fs" % (time.time() - t0))


def _jsonable(d):
    out = {}
    for k, v in d.items():
        if isinstance(v, tuple):
            v = list(v)
        out[k] = v
    return out


main()
