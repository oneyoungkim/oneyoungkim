# -*- coding: utf-8 -*-
"""건물 키트 미리 보기: 게임 카메라 높이에서 장소별 '지금(회색 상자) / 키트' 를 같은 조명·같은 카메라로 렌더.

실행(저장소 haengin1/ 에서):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/preview_env.py -- \
      --fbx concept/art/env/buildings --out <폴더> [--res 1600x900] [--shots 1,2,3] [--state both|after|before] [--night 1]
그다음 시스템 파이썬으로 외곽선·안개·나란히 붙이기:  python tools/env/compose_shots.py <폴더>

렌더 3장/컷: color(평면 셀 2단 — 고유색 / 고유색 × (.78,.76,.74), 그림자 포함), normal(월드 법선), depth(카메라 거리).
외곽선은 compose_shots.py 가 깊이·법선 차이로 그린다(09 문서 3-5 '화면 공간 외곽선' 흉내: 근경 1.5px → 30m 0.75px → 80m 부터 0).
지형·길·계단·나무는 zone1.json 으로 Zone1Builder 와 같은 식으로 다시 만든다(키트 밖, 두 상태 같음).
"""
import json
import math
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import mathutils  # noqa: E402

from envkit.zone import Zone, classify, poly_closest  # noqa: E402
from envkit.mats import MATS, hex_rgb, srgb_to_linear, texture_path  # noqa: E402
from envkit.geom import MB, Frame, box, cyl, UP, quad, tube, catenary  # noqa: E402
from envkit import blend  # noqa: E402

KIT_KINDS = ("shop", "house", "hanok", "campus", "pavilion", "gate", "fence", "retaining", "railing")
SHADOW_MUL = (0.78, 0.76, 0.74)
SUN_UNITY = (-0.283, -0.743, 0.606)   # Zone1Builder 해: Euler(48, 335) 의 앞 방향(빛이 가는 쪽)


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(zone=os.path.join(ROOT, "unity", "HaenginMainEvent", "Assets", "_Project", "Data", "zone1.json"),
             fbx=os.path.join(ROOT, "concept", "art", "env", "buildings"), out=None, res="1600x900", shots=None,
             state="both", night="1", scale="2")
    i = 0
    while i < len(a):
        o[a[i].lstrip("-")] = a[i + 1]
        i += 2
    return o


# ---------------------------------------------------------------- 재질(셀 2단)
def toon_mat(name, hexcol, emissive=False, strength=1.0, tex=None, scale=1.0):
    """셀 2단: 고유색(텍스처 또는 평면 색) / 고유색 × (.78, .76, .74). 끝에 밤 색조 곱셈 노드(PV_Tint)."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    col = srgb_to_linear(hex_rgb(hexcol))
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], out.inputs[0])
    tint = nt.nodes.new("ShaderNodeMix")
    tint.name = "PV_Tint"
    tint.data_type = "RGBA"
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1.0
    tint.inputs[7].default_value = (1, 1, 1, 1)
    nt.links.new(tint.outputs[2], em.inputs[0])
    if tex:
        img = bpy.data.images.load(tex, check_existing=True)
        ti = nt.nodes.new("ShaderNodeTexImage")
        ti.image = img
        ti.interpolation = "Linear"
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (scale, scale, 1.0)
        nt.links.new(tc.outputs["UV"], mp.inputs[0])
        nt.links.new(mp.outputs[0], ti.inputs[0])
        base = ti.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (*col, 1)
        base = rgb.outputs[0]
    if emissive:
        nt.links.new(base, tint.inputs[6])
        em.inputs[1].default_value = strength
        return m
    dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    dif.inputs[0].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].position = 0.12
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(s2r.outputs[0], bw.inputs[0])
    nt.links.new(bw.outputs[0], ramp.inputs[0])
    shd = nt.nodes.new("ShaderNodeMix")
    shd.data_type = "RGBA"
    shd.blend_type = "MULTIPLY"
    shd.inputs[0].default_value = 1.0
    nt.links.new(base, shd.inputs[6])
    shd.inputs[7].default_value = (*SHADOW_MUL, 1)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(ramp.outputs[0], mix.inputs[0])
    nt.links.new(shd.outputs[2], mix.inputs[6])
    nt.links.new(base, mix.inputs[7])
    nt.links.new(mix.outputs[2], tint.inputs[6])
    return m


def toon_from_gltf(m):
    """소품 GLB 재질 → 셀 재질(기본색 텍스처 그대로)."""
    img = None
    if m.use_nodes:
        for nd in m.node_tree.nodes:
            if nd.type == "TEX_IMAGE" and nd.image is not None:
                img = nd.image
                break
    nm = m.name
    if img is None:
        toon_mat(nm, "#B0A898")
        return
    path = img.filepath_raw or img.filepath
    toon_mat(nm, "#B0A898")
    nt = m.node_tree
    ti = nt.nodes.new("ShaderNodeTexImage")
    ti.image = img
    shd = [n for n in nt.nodes if n.type == "MIX" and n.blend_type == "MULTIPLY" and n.name != "PV_Tint"][0]
    mix = [n for n in nt.nodes if n.type == "MIX" and n.blend_type == "MIX"][0]
    nt.links.new(ti.outputs["Color"], shd.inputs[6])
    nt.links.new(ti.outputs["Color"], mix.inputs[7])


def data_mats():
    nm = bpy.data.materials.new("PV_Normal")
    nm.use_nodes = True
    nt = nm.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    vm = nt.nodes.new("ShaderNodeVectorMath")
    vm.operation = "MULTIPLY_ADD"
    vm.inputs[1].default_value = (0.5, 0.5, 0.5)
    vm.inputs[2].default_value = (0.5, 0.5, 0.5)
    nt.links.new(geo.outputs["Normal"], vm.inputs[0])
    nt.links.new(vm.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    dm = bpy.data.materials.new("PV_Depth")
    dm.use_nodes = True
    nt = dm.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    cam = nt.nodes.new("ShaderNodeCameraData")
    mth = nt.nodes.new("ShaderNodeMath")
    mth.operation = "DIVIDE"
    mth.inputs[1].default_value = 600.0
    nt.links.new(cam.outputs["View Distance"], mth.inputs[0])
    cmb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(mth.outputs[0], cmb.inputs[0])
    nt.links.new(mth.outputs[0], cmb.inputs[1])
    nt.links.new(mth.outputs[0], cmb.inputs[2])
    nt.links.new(cmb.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    return nm, dm


# ---------------------------------------------------------------- 장면 바탕(키트 밖)
def mb_obj(name, mb, coll, mat_map=None):
    if mb.empty():
        return None
    me = blend.mesh_from_mbs(name, [mb], (0.0, 0.0, 0.0))
    if mat_map:
        for i, m in enumerate(me.materials):
            if m.name in mat_map:
                me.materials[i] = mat_map[m.name]
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


def extrude_ribbon(mb, pts, width, below, lift, mat):
    """Zone1Builder.Extrude 와 같은 식(점 높이 그대로, 아래로 below 만큼 치마)."""
    n = len(pts)
    L, R = [], []
    for i in range(n):
        ns = []
        for j in (i - 1, i):
            if 0 <= j < n - 1:
                a, b = pts[j], pts[j + 1]
                dx, dz = b[0] - a[0], b[2] - a[2]
                l = math.hypot(dx, dz)
                ns.append((-dz / l, dx / l))
        mx, mz = sum(v[0] for v in ns), sum(v[1] for v in ns)
        l = math.hypot(mx, mz)
        mx, mz = mx / l, mz / l
        k = (width / 2) / max(0.5, mx * ns[0][0] + mz * ns[0][1])
        x, y, z = pts[i]
        L.append((x + mx * k, y + lift, z + mz * k))
        R.append((x - mx * k, y + lift, z - mz * k))
    for i in range(n - 1):
        mb.face([L[i], L[i + 1], R[i + 1], R[i]], mat, out=UP)
        for A, B in ((L[i], L[i + 1]), (R[i + 1], R[i])):
            mb.face([A, B, (B[0], B[1] - below, B[2]), (A[0], A[1] - below, A[2])], mat)
    for P0, P1 in ((L[0], R[0]), (R[-1], L[-1])):
        mb.face([P0, P1, (P1[0], P1[1] - below, P1[2]), (P0[0], P0[1] - below, P0[2])], mat)


def base_scene(zone, coll):
    C = zone.colors
    mats = {}

    def M(key):
        if key not in mats:
            mats[key] = toon_mat("G_" + key, C.get(key, "#FF00FF"))
        return "G_" + key
    mb = MB()
    # 지형(깎은 격자) + 바깥 띠
    nx, nz = zone.nx, zone.nz
    V = [[(zone.gx0 + i * zone.cell, zone.HC[j][i], zone.gz0 + j * zone.cell) for i in range(nx)] for j in range(nz)]
    for j in range(nz - 1):
        for i in range(nx - 1):
            a, b, c, d = V[j][i], V[j][i + 1], V[j + 1][i], V[j + 1][i + 1]
            mb.face([a, c, d], M("ground"), out=UP, smooth=True)
            mb.face([a, d, b], M("ground"), out=UP, smooth=True)
    per = [(i, 0) for i in range(nx)] + [(nx - 1, j) for j in range(1, nz)] + [(i, nz - 1) for i in range(nx - 2, -1, -1)] + \
          [(0, j) for j in range(nz - 2, 0, -1)]
    rings = []
    for dist, drop in ((0, 0), (40, 6), (420, 70)):
        rr = []
        for i, j in per:
            dx = -1 if i == 0 else (1 if i == nx - 1 else 0)
            dz = -1 if j == 0 else (1 if j == nz - 1 else 0)
            l = math.hypot(dx, dz) or 1
            p = V[j][i]
            rr.append((p[0] + dx / l * dist, p[1] - drop, p[2] + dz / l * dist))
        rings.append(rr)
    P = len(per)
    for r_ in range(2):
        for k in range(P):
            a, b = rings[r_][k], rings[r_][(k + 1) % P]
            c, d = rings[r_ + 1][(k + 1) % P], rings[r_ + 1][k]
            mb.face([a, b, c, d], M("ground"), out=UP, smooth=True)
    mb_obj("Terrain", mb, coll)
    # 길·패드·계단
    mb = MB()
    for k, r in enumerate(zone.roads):
        lift = {"road": 0.0, "sidewalk": 0.005, "driveway": 0.02}.get(r["kind"], 0.015) + k * 0.002
        extrude_ribbon(mb, r["points"], r["width"], 3.0, lift + 0.01, M(r["kind"]))
    for p in zone.pads:
        top = p["center"][1] + 0.02
        key = "pad_" + p["surface"]
        if p["shape"] == "circle":
            cyl(mb, (p["center"][0], top - 3, p["center"][2]), UP, p["size"][0] / 2, p["size"][0] / 2, 3, 40, M(key), caps=(False, True), smooth=False)
        else:
            F = Frame(p["center"][0], p["center"][2], p["yaw"])
            box(mb, F, -p["size"][0] / 2, p["size"][0] / 2, top - 3, top, -p["size"][1] / 2, p["size"][1] / 2, M(key))
    for s in zone.stairs:
        a, b = s["from"], s["to"]
        dx, dz = b[0] - a[0], b[2] - a[2]
        L = math.hypot(dx, dz)
        yaw = math.degrees(math.atan2(-dz, dx))
        F = Frame(a[0], a[2], yaw)
        n = s["steps"]
        rise = b[1] - a[1]
        for i in range(n):
            top = a[1] + rise * (i + 0.5) / n
            box(mb, F, L * i / n, L * (i + 1) / n, top - abs(rise) / n - 2.0, top, -s["width"] / 2, s["width"] / 2, M("stairs"))
    mb_obj("Roads", mb, coll)
    # 키트 밖 블록(나무·풀숲·소품·차단물)
    mb = MB()
    for b in zone.blocks:
        if b.kind in KIT_KINDS:
            continue
        F = Frame(b.cx, b.cz, b.yaw)
        if b.kind == "tree":
            H = b.h
            bot = b.bottom
            box(mb, F, -0.25, 0.25, bot, bot + H * 0.45, -0.25, 0.25, M("tree"))
            box(mb, F, -b.w / 2, b.w / 2, bot + H * 0.38, bot + H, -b.d / 2, b.d / 2, M("tree"))
        else:
            box(mb, F, -b.w / 2, b.w / 2, b.bottom, b.top, -b.d / 2, b.d / 2, M(b.kind))
    # 랜드마크(키트 밖: 가로등·표지판·정류장)
    for lm in zone.landmarks:
        x, y, z = lm["pos"]
        g = zone.ground(x, z)
        h = lm.get("height") or 2.0
        if lm["kind"] == "lamp":
            cyl(mb, (x, g - 0.3, z), UP, 0.09, 0.09, h + 0.3, 8, M("lamp"))
            box(mb, Frame(x, z, 0), -0.3, 0.3, g + h - 0.12, g + h, -0.18, 0.18, M("lamp"))
        elif lm["kind"] in ("sign", "bus_stop") and y - g < 1.0:
            cyl(mb, (x, g - 0.3, z), UP, 0.05, 0.05, h + 0.3, 6, M(lm["kind"]))
            box(mb, Frame(x, z, 0), -0.5, 0.5, g + h - 0.7, g + h, -0.03, 0.03, M(lm["kind"]))
    mb_obj("Props", mb, coll)
    return mats


def before_scene(zone, coll):
    """지금 모습: 키트 블록 = 회색 상자(zone1 colors), 성곽 = Zone1Builder 와 같은 단면, 전봇대 = 원기둥 + 곧은 전선."""
    C = zone.colors

    def M(key):
        n = "G_" + key
        if bpy.data.materials.get(n) is None:
            toon_mat(n, C.get(key, "#FF00FF"))
        return n
    mb = MB()
    for b in zone.blocks:
        if b.kind not in KIT_KINDS:
            continue
        F = Frame(b.cx, b.cz, b.yaw)
        box(mb, F, -b.w / 2, b.w / 2, b.bottom, b.top, -b.d / 2, b.d / 2, M(b.kind))
    for w in zone.walls:
        pts = w["points"]
        from envkit.btypes import wall_offsets
        inner, outer = wall_offsets(pts, w["thickness"] / 2), wall_offsets(pts, -w["thickness"] / 2)
        for i in range(len(pts) - 1):
            a, b_ = pts[i], pts[i + 1]
            for P0, P1 in ((inner[i], inner[i + 1]), (outer[i + 1], outer[i])):
                mb.face([(P0[0], P0[1] - 1.5, P0[2]), (P1[0], P1[1] - 1.5, P1[2]), (P1[0], P1[1] + w["height"], P1[2]),
                         (P0[0], P0[1] + w["height"], P0[2])], M("wall"))
            mb.face([(inner[i][0], a[1] + w["height"], inner[i][2]), (inner[i + 1][0], b_[1] + w["height"], inner[i + 1][2]),
                     (outer[i + 1][0], b_[1] + w["height"], outer[i + 1][2]), (outer[i][0], a[1] + w["height"], outer[i][2])],
                    M("wall"), out=UP)
            L = math.hypot(b_[0] - a[0], b_[2] - a[2])
            par = w["parapet"]
            nm = max(1, int(round(L / (par["merlon"] + par["crenel"]))))
            pitch = L / nm
            yaw = math.degrees(math.atan2(-(b_[2] - a[2]), b_[0] - a[0]))
            tx, tz = (b_[0] - a[0]) / L, (b_[2] - a[2]) / L
            onx, onz = -tz, tx
            for m in range(nm):
                s = (m + 0.5) * pitch
                yTop = a[1] + (b_[1] - a[1]) * s / L + w["height"] + w["brow"]["height"]
                cx = a[0] + tx * s + onx * (w["thickness"] / 2 - par["thickness"] / 2)
                cz = a[2] + tz * s + onz * (w["thickness"] / 2 - par["thickness"] / 2)
                F = Frame(cx, cz, yaw)
                ml = max(0.6, pitch - par["crenel"])
                box(mb, F, -ml / 2, ml / 2, yTop - 0.3, yTop + par["height"], -par["thickness"] / 2, par["thickness"] / 2, M("wall"))
                box(mb, F, -ml / 2 - 0.08, ml / 2 + 0.08, yTop + par["height"], yTop + par["height"] + 0.2,
                    -par["thickness"] / 2 - 0.08, par["thickness"] / 2 + 0.08, M("wallcap"))
            F = Frame((a[0] + b_[0]) / 2, (a[2] + b_[2]) / 2, yaw)
            ym = (a[1] + b_[1]) / 2 + w["height"]
            box(mb, F, -L / 2, L / 2, ym, ym + 0.16, -w["thickness"] / 2 - 0.14, w["thickness"] / 2 + 0.14, M("wallcap"))
    tops = {}
    for lm in zone.landmarks:
        if lm["kind"] in ("pole", "board"):
            x, y, z = lm["pos"]
            g = zone.ground(x, z)
            h = lm.get("height") or 9.0
            cyl(mb, (x, g - 0.3, z), UP, 0.15, 0.15, h + 0.3, 10, M("pole"))
            import re
            m = re.match(r"전봇대\s*(\d+)\s*\((.+)\)", lm["name"])
            if m:
                tops.setdefault(m.group(2), []).append((int(m.group(1)), (x, g + h, z)))
    for grp, lst in tops.items():
        lst.sort()
        for i in range(len(lst) - 1):
            a, b_ = lst[i][1], lst[i + 1][1]
            for dy in (-0.5, -1.1):
                tube(mb, [(a[0], a[1] + dy, a[2]), (b_[0], b_[1] + dy, b_[2])], 0.035, 4, M("pole"))
    mb_obj("Greybox", mb, coll)


def mannequin(coll, feet, yaw):
    """시우 대신 1.74m 인형(짙은 남색 교복 덩어리 — 크기 비교용)."""
    mat = toon_mat("PV_Siwoo", "#2A2E3A")
    skin = toon_mat("PV_Skin", "#D9B79A")
    mb = MB()
    F = Frame(feet[0], feet[2], yaw)
    y = feet[1]
    box(mb, F, -0.09, -0.01, y, y + 0.82, -0.08, 0.08, "PV_Siwoo")
    box(mb, F, 0.01, 0.09, y, y + 0.82, -0.08, 0.08, "PV_Siwoo")
    box(mb, F, -0.2, 0.2, y + 0.82, y + 1.42, -0.12, 0.12, "PV_Siwoo")
    box(mb, F, -0.27, -0.2, y + 0.78, y + 1.4, -0.06, 0.06, "PV_Siwoo")
    box(mb, F, 0.2, 0.27, y + 0.78, y + 1.4, -0.06, 0.06, "PV_Siwoo")
    cyl(mb, F.p(0, y + 1.44, 0), UP, 0.1, 0.1, 0.3, 10, "PV_Skin")
    me = blend.mesh_from_mbs("Siwoo", [mb], (0, 0, 0))
    me.materials.clear()
    me.materials.append(mat)
    me.materials.append(skin)
    # 마지막 면(머리)만 피부색
    n = len(me.polygons)
    for k, p in enumerate(me.polygons):
        p.material_index = 1 if k >= n - 11 else 0
    ob = bpy.data.objects.new("Siwoo", me)
    coll.objects.link(ob)
    return ob


# ---------------------------------------------------------------- 키트 불러오기
def import_kit(fbx_dir, coll):
    before = set(bpy.data.objects)
    for fn in sorted(os.listdir(fbx_dir)):
        if fn.lower().endswith(".fbx"):
            bpy.ops.import_scene.fbx(filepath=os.path.join(fbx_dir, fn), axis_forward="-Z", axis_up="Y",
                                     use_custom_props=True, bake_space_transform=True)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
    # 재질: 같은 이름 M_* 를 셀 재질로(concept/art/env/textures/<이름>.png 가 있으면 텍스처, UV 배율 1/타일)
    for m in list(bpy.data.materials):
        base = m.name.split(".")[0]
        if base in MATS:
            info = MATS[base]
            tex = texture_path(base)
            if base == "M_Granite_Block":
                tex = None          # 줄눈 없는 돌 면 — 텍스처(줄눈 있음) 대신 평면 색
            sc = 1.0 if info["uv"] == "unit" else 1.0 / info["tile"]
            if base == "M_Glass_Lit":
                toon_mat(m.name, info["color"], emissive=True, strength=1.0, tex=tex)
            else:
                toon_mat(m.name, info["color"], tex=tex, scale=sc)
    # 간판: Sign 메시 UV(0~1)를 sign_atlas.json 사각형으로
    atlas_path = os.path.join(ROOT, "concept", "art", "env", "signs", "sign_atlas.json")
    if os.path.exists(atlas_path):
        at = json.load(open(atlas_path, encoding="utf-8"))
        S = float(at["size"][0])
        for ob in new:
            if ob.type == "MESH" and "atlas" in ob.keys() and ob["atlas"] in at["rects"]:
                x, y, w, h = at["rects"][ob["atlas"]]
                uvl = ob.data.uv_layers.active
                for lp in uvl.data:
                    u, v = lp.uv
                    lp.uv = ((x + u * w) / S, 1.0 - (y + (1.0 - v) * h) / S)
    return new


def place_props(new, coll, cam_u=None, radius=60.0):
    """Prop_<소품>_<n> 자리 표시에 concept/art/env/props/<소품>.glb 를 같은 메시로 여러 번 놓는다(카메라 radius 안만)."""
    pdir = os.path.join(ROOT, "concept", "art", "env", "props")
    meshes = {}
    made = []
    for e in new:
        if e.type != "EMPTY" or not e.name.startswith("Prop_"):
            continue
        name = e.get("prop") or e.name[5:].rsplit("_", 1)[0]
        if cam_u is not None:
            p = e.matrix_world.translation
            pu = (-p.x, p.z, -p.y)
            if math.dist(pu, cam_u) > radius:
                continue
        if name not in meshes:
            path = os.path.join(pdir, name + ".glb")
            if not os.path.exists(path):
                meshes[name] = None
                continue
            before = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=path)
            objs = [o for o in bpy.data.objects if o not in before]
            ms = [o for o in objs if o.type == "MESH"]
            meshes[name] = [(o.data, o.matrix_world.copy()) for o in ms]
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
            for me, _ in meshes[name]:
                for m in me.materials:
                    if m is not None and not m.name.startswith("PVP_"):
                        m.name = "PVP_" + m.name
                        toon_from_gltf(m)
        if not meshes[name]:
            continue
        for me, mw in meshes[name]:
            ob = bpy.data.objects.new("pv_" + e.name, me)
            coll.objects.link(ob)
            ob.matrix_world = e.matrix_world @ mw
            made.append(ob)
    return made


def check_positions(new, manifest_path):
    """FBX 를 다시 읽은 위치가 manifest(Unity 좌표)와 같은지: 블렌더 (-X, -Z, Y) 로 바꿔 비교."""
    try:
        man = json.load(open(manifest_path, encoding="utf-8"))
    except OSError:
        return None
    want = {o["name"] + "_LOD0": o["origin"] for o in man["objects"] if o["kind"] in ("building", "wall", "pole", "fences")}
    worst = 0.0
    n = 0
    for ob in new:
        if ob.name.split(".")[0] in want:
            X, Y, Z = want[ob.name.split(".")[0]]
            p = ob.matrix_world.translation
            u = (-p.x, p.z, -p.y)
            err = math.sqrt((u[0] - X) ** 2 + (u[1] - Y) ** 2 + (u[2] - Z) ** 2)
            rot = max(abs(v) for v in ob.matrix_world.to_euler())
            worst = max(worst, err, rot)
            n += 1
    return n, worst


def set_lods(new, cam_pos_u, fov=45.0, bias=1.0):
    """Unity LODGroup 처럼: 화면 높이 비율로 LOD 하나만 보이게."""
    tan_v = math.tan(math.radians(fov / 2))
    groups = {}
    for o in new:
        n = o.name.split(".")[0]
        if "_LOD" in n:
            groups.setdefault(o.parent, []).append(o)
    counts = [0, 0, 0, 0]
    for g, kids in groups.items():
        if g is None or "lod_screen_heights" not in g.keys():
            continue
        t0, t1, tc = [float(v) for v in str(g["lod_screen_heights"]).split(",")]
        lo = mathutils.Vector((1e9, 1e9, 1e9))
        hi = -lo
        for k in kids:
            for c in k.bound_box:
                w = k.matrix_world @ mathutils.Vector(c)
                lo = mathutils.Vector(map(min, lo, w))
                hi = mathutils.Vector(map(max, hi, w))
        size = max(hi - lo)
        c = (lo + hi) / 2
        cu = (-c.x, c.z, -c.y)
        dist = math.sqrt(sum((cu[i] - cam_pos_u[i]) ** 2 for i in range(3)))
        rel = size / (2 * max(0.1, dist) * tan_v) * bias
        L = 0 if rel >= t0 else (1 if rel >= t1 else (2 if rel >= tc else 3))
        counts[L] += 1
        for k in kids:
            nm = k.name.split(".")[0]
            lv = int(nm[nm.rindex("_LOD") + 4:])
            k.hide_render = (lv != L)
    return counts


# ---------------------------------------------------------------- 카메라 자리
def shots(zone):
    """(이름, 발 위치(Unity), 바라보는 yaw(도, Unity: 0=+Z 북, 90=+X 동), 피치(아래 +), 거리, FOV)"""
    R = {r["name"]: r["points"] for r in zone.roads}
    S = {s["name"]: s for s in zone.stairs}

    def on(name, s_frac=None, at=None):
        pts = R[name]
        if at is not None:
            return at
        tot = sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][2] - pts[i][2]) for i in range(len(pts) - 1))
        s = tot * s_frac
        for i in range(len(pts) - 1):
            L = math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][2] - pts[i][2])
            if s <= L:
                t = s / L
                return tuple(pts[i][k] + (pts[i + 1][k] - pts[i][k]) * t for k in range(3))
            s -= L
        return tuple(pts[-1])

    def yaw_to(a, b):
        return math.degrees(math.atan2(b[0] - a[0], b[2] - a[2]))
    out = []
    # 1 후문 상가거리: 북쪽 인도, 후문 쪽에서 동쪽(상가 A·B동, 하루편의점 모서리)
    # 1 후문 상가거리: 성균관로 북쪽 차선(x = -193, 가운데선에서 북으로 1.6m)에서 동북동 — 상가 A·B동·하루편의점 모서리·다세대
    mr = zone.roads[0]["points"]
    for i in range(len(mr) - 1):
        if mr[i][0] <= -193.0 <= mr[i + 1][0]:
            t = (-193.0 - mr[i][0]) / (mr[i + 1][0] - mr[i][0])
            p = (-193.0, mr[i][1] + (mr[i + 1][1] - mr[i][1]) * t, mr[i][2] + (mr[i + 1][2] - mr[i][2]) * t + 1.6)
    out.append(("1_후문상가거리", p, 72.0, 4.0, 4.0, 45))
    # 2 뒷골목 주차장: 진입로에서 남서쪽(옹벽·주차장 뒤 다세대·상가 뒷면)
    pk = [g for g in zone.pads if "주차장" in g["name"]][0]["center"]
    dw = R["주차장 진입로"]
    p = (dw[0][0], pk[1], dw[0][2])
    out.append(("2_뒷골목주차장", p, yaw_to(p, (pk[0] - 7, 0, pk[2] - 4)), 5.0, 3.8, 45))
    # 3 주택가 골목 계단: 시우네 앞에서 꼭대기 계단 올려다봄
    st = [s for n, s in S.items() if "꼭대기 계단" in n][0]
    lo, hi = (st["from"], st["to"]) if st["from"][1] <= st["to"][1] else (st["to"], st["from"])
    d = (hi[0] - lo[0], hi[2] - lo[2])
    L = math.hypot(*d)
    feet = (lo[0] - d[0] / L * 2.0, lo[1], lo[2] - d[1] / L * 2.0)
    feet = (feet[0], zone.ground(feet[0], feet[2]), feet[2])
    out.append(("3_골목계단", feet, yaw_to(lo, hi), -4.0, 4.0, 50))
    # 4 와룡 오르막: 후문 옆 공원길을 오르며(북)
    p = on("후문 옆 공원길(와룡공원 오르막)", 0.15)
    out.append(("4_와룡오르막", p, yaw_to(p, on("후문 옆 공원길(와룡공원 오르막)", 1.0)) + 12, 2.0, 3.8, 45))
    # 5 성곽길: 암문 앞에서 동쪽(공터·마을 지붕)
    p = on("와룡공원 성곽길", 0.33)
    out.append(("5_성곽길", p, yaw_to(p, on("와룡공원 성곽길", 0.0)) + 8, 3.0, 3.8, 45))
    # 6 한옥 골목: 명륜3가 골목 동쪽 가지에서 동남(기와 단독주택)
    p = on("명륜3가 골목 동쪽(명륜1가 방면)", 0.18)
    out.append(("6_한옥골목", p, yaw_to(p, on("명륜3가 골목 동쪽(명륜1가 방면)", 0.62)) + 14, 4.0, 3.6, 48))
    # 7 명륜3가 골목(체크포인트 4 → 시우네)
    p = on("명륜3가 골목(시우네 골목)", 0.04)
    out.append(("7_명륜3가골목", p, yaw_to(p, on("명륜3가 골목(시우네 골목)", 0.75)), 3.0, 3.4, 50))
    # 8 성곽 아래 빈집·꼭대기 다세대(숲길에서)
    p = on("성곽 안쪽 숲길(혜성고 방면)", 0.42)
    out.append(("8_성곽아래빈집", p, yaw_to(p, (-142.5, 0, 145.0)) - 10, 3.0, 4.0, 45))
    return out


def place_camera(cam, feet, yaw, pitch, dist, fov):
    """Zone1Builder.Follow 근사: 따라가는 점 = 발 + 1.40m, 피치(아래 +), 거리 dist. 반환 = 카메라 Unity 위치."""
    a, p = math.radians(yaw), math.radians(pitch)
    fwd = (math.sin(a) * math.cos(p), -math.sin(p), math.cos(a) * math.cos(p))
    tgt = (feet[0], feet[1] + 1.40, feet[2])
    c = (tgt[0] - fwd[0] * dist, tgt[1] - fwd[1] * dist, tgt[2] - fwd[2] * dist)
    cb = mathutils.Vector(blend.to_bl(c, (0, 0, 0)))
    tb = mathutils.Vector(blend.to_bl(tgt, (0, 0, 0)))
    cam.location = cb
    cam.rotation_euler = (tb - cb).to_track_quat("-Z", "Y").to_euler()
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(fov)
    cam.data.clip_start = 0.1
    cam.data.clip_end = 900
    return c, fwd


# ---------------------------------------------------------------- 렌더
def setup_render(sc, w, h):
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.dither_intensity = 0.0
    sc.eevee.taa_render_samples = 8
    sc.eevee.use_shadows = True
    sc.eevee.shadow_resolution_scale = 1.0
    world = bpy.data.worlds.new("PV_World")
    sc.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg_cam = nt.nodes.new("ShaderNodeBackground")
    bg_cam.inputs[0].default_value = (*srgb_to_linear(hex_rgb("#EEE3D1")), 1)
    bg_dark = nt.nodes.new("ShaderNodeBackground")
    bg_dark.inputs[0].default_value = (0, 0, 0, 1)
    lp = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(bg_dark.outputs[0], mix.inputs[1])
    nt.links.new(bg_cam.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    return world, bg_cam


def sun(coll, night=False):
    ld = bpy.data.lights.new("PV_Sun", "SUN")
    ld.energy = 3.2 if not night else 0.6
    ld.angle = math.radians(0.6)
    ld.use_shadow = True
    so = bpy.data.objects.new("PV_Sun", ld)
    coll.objects.link(so)
    d = mathutils.Vector(blend.to_bl(SUN_UNITY, (0, 0, 0))).normalized()
    so.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return so


def render(sc, path, override=None, samples=8, mode="color"):
    """mode: color(8비트 RGB, Standard) / normal(8비트 RGBA, Raw, 하늘 투명) / depth(16비트 흑백, Raw, 하늘 0)."""
    sc.view_layers[0].material_override = override
    sc.render.film_transparent = mode != "color"
    sc.eevee.taa_render_samples = samples
    sc.render.filter_size = 1.5 if mode == "color" else 0.0
    sc.view_settings.view_transform = "Standard" if mode == "color" else "Raw"
    st = sc.render.image_settings
    st.file_format = "PNG"
    st.color_mode = {"color": "RGB", "normal": "RGBA", "depth": "BW"}[mode]
    st.color_depth = "16" if mode == "depth" else "8"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def tint_all(f):
    """밤: 모든 셀 재질 끝 PV_Tint 곱셈 값을 f 로(None 이면 1). 창문 불(발광)은 강도를 따로 올림."""
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        nd = m.node_tree.nodes.get("PV_Tint")
        if nd is None:
            continue
        if m.name.split(".")[0] == "M_Glass_Lit":
            continue
        nd.inputs[7].default_value = (1, 1, 1, 1) if f is None else (f[0], f[1], f[2], 1)
        m.node_tree.update_tag()      # 안 하면 EEVEE 가 두 번째 밤 컷에서 옛 값을 씀(2026-10-06 확인)
        m.update_tag()
    bpy.context.view_layer.update()


def main():
    o = args()
    out = o["out"] or os.path.join(ROOT, "_env_shots")
    os.makedirs(out, exist_ok=True)
    W, H = [int(v) for v in o["res"].split("x")]
    S = int(o["scale"])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    zone = Zone(o["zone"])
    classify(zone)
    base = bpy.data.collections.new("PV_Base")
    sc.collection.children.link(base)
    base_scene(zone, base)
    bcoll = bpy.data.collections.new("PV_Before")
    sc.collection.children.link(bcoll)
    before_scene(zone, bcoll)
    acoll = bpy.data.collections.new("PV_After")
    sc.collection.children.link(acoll)
    new = import_kit(o["fbx"], acoll)
    chk = check_positions(new, os.path.join(o["fbx"], "zone1_buildings_manifest.json"))
    print("[pv] 키트 오브젝트 %d · 위치 확인(블록·성곽·전봇대 %s개 최대 오차 %.4fm)" % (len(new), chk[0] if chk else "?", chk[1] if chk else -1))
    world, bg = setup_render(sc, W * S, H * S)
    sun(base)
    camd = bpy.data.cameras.new("PV_Cam")
    cam = bpy.data.objects.new("PV_Cam", camd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    nmat, dmat = data_mats()
    lit_objs = [x for x in new if x.name.startswith("WinLit")]
    sel = None if not o["shots"] else set(o["shots"].split(","))
    shot_list = shots(zone)
    if o.get("look"):
        # 점검용: --look 이름:x,y,z,yaw,pitch,dist,fov (발 위치 Unity 좌표) — 여러 개는 ; 로
        shot_list = []
        for item in o["look"].split(";"):
            nm, vals = item.split(":")
            v = [float(x) for x in vals.split(",")]
            shot_list.append((nm, (v[0], v[1], v[2]), v[3], v[4], v[5], v[6] if len(v) > 6 else 45))
        sel = None
    log = []
    for (name, feet, yaw, pitch, dist, fov) in shot_list:
        if sel and name.split("_")[0] not in sel:
            continue
        man = mannequin(base, feet, yaw) if not o.get("look") else None
        camu, fwd = place_camera(cam, feet, yaw, pitch, dist, fov)
        counts = set_lods(new, camu, fov)
        props_here = place_props(new, acoll, camu)
        for x in lit_objs:
            x.hide_render = True
        states = ["before", "after"] if o["state"] == "both" else [o["state"]]
        for stt in states:
            bcoll.hide_render = stt != "before"
            acoll.hide_render = stt != "after"
            stem = os.path.join(out, "%s_%s" % (name, stt))
            render(sc, stem + "_color.png", None, samples=16)
            render(sc, stem + "_normal.png", nmat, samples=1, mode="normal")
            render(sc, stem + "_depth.png", dmat, samples=1, mode="depth")
        log.append(dict(name=name, feet=feet, yaw=yaw, pitch=pitch, dist=dist, fov=fov, cam=camu, lod_counts=counts))
        if man is not None:
            bpy.data.objects.remove(man, do_unlink=True)
        for x in props_here:
            bpy.data.objects.remove(x, do_unlink=True)
    # 밤: 1번 컷만, 창문 불 40%(문턱 < 0.4 묶음)
    if o["night"] == "1" and not o.get("look") and (not sel or "1" in sel or "7" in sel):
        for (name, feet, yaw, pitch, dist, fov) in shots(zone):
            if not name.startswith(("1_", "7_")):
                continue
            man = mannequin(base, feet, yaw)
            camu, fwd = place_camera(cam, feet, yaw, pitch, dist, fov)
            set_lods(new, camu, fov)
            props_here = place_props(new, acoll, camu)
            for x in lit_objs:
                x.hide_render = float(x.get("window_threshold", 1.0)) > 0.4
            bcoll.hide_render, acoll.hide_render = True, False
            sunobj = bpy.data.objects["PV_Sun"]
            sunobj.data.energy = 0.0
            bg.inputs[0].default_value = (*srgb_to_linear(hex_rgb("#222A44")), 1)   # docs/10 9장 밤 하늘
            # 밤 그림자 곱셈(.55, .58, .75) = 낮 곱셈(.78, .76, .74) × 이 값(달빛뿐이라 모든 면이 그림자 쪽)
            tint_all((0.55 / 0.78, 0.58 / 0.76, 0.75 / 0.74))
            stem = os.path.join(out, "%s_night" % name)
            render(sc, stem + "_color.png", None, samples=16)
            render(sc, stem + "_normal.png", nmat, samples=1, mode="normal")
            render(sc, stem + "_depth.png", dmat, samples=1, mode="depth")
            tint_all(None)
            sunobj.data.energy = 3.2
            bg.inputs[0].default_value = (*srgb_to_linear(hex_rgb("#EEE3D1")), 1)
            bpy.data.objects.remove(man, do_unlink=True)
            for x in props_here:
                bpy.data.objects.remove(x, do_unlink=True)
            log.append(dict(name=name + "_night", cam=camu))
    with open(os.path.join(out, "shots.json"), "w", encoding="utf-8") as f:
        json.dump(dict(shots=log, check=chk), f, ensure_ascii=False, indent=1)
    print("[pv] 끝:", out)


main()
