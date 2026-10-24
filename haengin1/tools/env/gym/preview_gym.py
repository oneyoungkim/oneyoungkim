# -*- coding: utf-8 -*-
"""체육관 키트 + 소품 미리 보기: 스타일 프레임 6 앵글(Sock_Cam_g01~g06)을 EEVEE 2톤 셀 + Freestyle 먹선으로 렌더.

실행(저장소 haengin1/ 에서):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/gym/preview_gym.py -- \
      [--out <폴더>] [--res 1280x720] [--shots g01,g02] [--samples 16] [--props 1]
그다음 시스템 파이썬으로 나란히 붙이기:  python tools/env/gym/compose_compare.py <폴더>

재질: MATS 색 또는 gym/textures/<이름>.png(월드 m UV → Mapping 1/tile), 고유색 / 고유색 × (.78,.76,.74) 2톤(Shader to RGB),
형광등·타이머 판은 발광, 철망·유리는 알파. 소품은 Prop_ 자리에 concept/art/env/gym/props/<이름>.glb 를 끼움(없으면 건너뜀).
밤(g06)은 형광등 1개 + 케이지 스포트만. 사람은 없음(프레임의 인물 자리에 1.74m 회색 대역만).
"""
import json
import math
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(ENV))
sys.path.insert(0, ENV)
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import mathutils  # noqa: E402

import build_gym as G  # noqa: E402
from envkit import blend  # noqa: E402
from envkit.mats import MATS, hex_rgb, srgb_to_linear  # noqa: E402

SHADOW = (0.78, 0.76, 0.74)
INK = (0x1A / 255, 0x14 / 255, 0x17 / 255)
EMISSIVE = {"M_Fluorescent": 1.6, "M_Timer_Red": 1.4, "M_Glass_Lit": 1.2}
ALPHA_TEX = {"M_Cage_Mesh"}
ALPHA_CONST = {"M_Glass_Clear": 0.18}


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(out=os.path.join(ROOT, "concept", "art", "env", "gym", "_preview"), res="1280x720", shots=None, samples="16", props="1")
    i = 0
    while i < len(a):
        k = a[i].lstrip("-")
        if k in o and i + 1 < len(a):
            o[k] = a[i + 1]
            i += 2
        else:
            i += 1
    return o


def to_bl(p):
    return blend.to_bl(p, (0.0, 0.0, 0.0))


def cel_material(mat, base_rgb=None, image=None, tile=1.0, unit=False, alpha_tex=False, alpha_const=None, emissive=0.0):
    """재질 노드를 2톤 셀 발광으로 바꿈."""
    if isinstance(base_rgb, str):
        base_rgb = hex_rgb(base_rgb)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emis = nt.nodes.new("ShaderNodeEmission")
    base_node = None
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Closest" if alpha_tex else "Linear"
        if not unit:
            co = nt.nodes.new("ShaderNodeTexCoord")
            mp = nt.nodes.new("ShaderNodeMapping")
            mp.inputs["Scale"].default_value = (1.0 / tile, 1.0 / tile, 1.0)
            nt.links.new(co.outputs["UV"], mp.inputs["Vector"])
            nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        base_node = tex
        base_out = tex.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (*srgb_to_linear(base_rgb), 1.0)
        base_out = rgb.outputs[0]
    if emissive > 0.0:
        nt.links.new(base_out, emis.inputs["Color"])
        emis.inputs["Strength"].default_value = emissive
    else:
        diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        nt.links.new(diff.outputs[0], s2r.inputs[0])
        thr = nt.nodes.new("ShaderNodeMath")
        thr.operation = "GREATER_THAN"
        thr.inputs[1].default_value = 0.42
        nt.links.new(s2r.outputs["Color"], thr.inputs[0])
        dark = nt.nodes.new("ShaderNodeMix")
        dark.data_type = "RGBA"
        dark.blend_type = "MULTIPLY"
        dark.inputs["Factor"].default_value = 1.0
        dark.inputs[7].default_value = (*SHADOW, 1.0)      # B
        nt.links.new(base_out, dark.inputs[6])              # A
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(thr.outputs[0], mix.inputs["Factor"])
        nt.links.new(dark.outputs[2], mix.inputs[6])        # 그늘
        nt.links.new(base_out, mix.inputs[7])               # 빛
        nt.links.new(mix.outputs[2], emis.inputs["Color"])
        emis.inputs["Strength"].default_value = 1.0
    shader_out = emis.outputs[0]
    if alpha_tex and base_node is not None:
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mx = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(base_node.outputs["Alpha"], mx.inputs["Fac"])
        nt.links.new(tr.outputs[0], mx.inputs[1])
        nt.links.new(shader_out, mx.inputs[2])
        shader_out = mx.outputs[0]
        mat.surface_render_method = "DITHERED"
    elif alpha_const is not None:
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mx = nt.nodes.new("ShaderNodeMixShader")
        mx.inputs["Fac"].default_value = alpha_const
        nt.links.new(tr.outputs[0], mx.inputs[1])
        nt.links.new(shader_out, mx.inputs[2])
        shader_out = mx.outputs[0]
        mat.surface_render_method = "BLENDED"
    nt.links.new(shader_out, out.inputs["Surface"])
    mat.use_backface_culling = False


def setup_kit_materials():
    for mat in bpy.data.materials:
        info = MATS.get(mat.name)
        if info is None:
            continue
        img = None
        if info.get("texture"):
            p = os.path.join(ROOT, info["texture"])
            if os.path.exists(p):
                img = bpy.data.images.load(p, check_existing=True)
                if mat.name in ALPHA_TEX or mat.name == "M_Glass_Clear":
                    img.alpha_mode = "STRAIGHT"
        cel_material(mat, base_rgb=hex_rgb(info["color"]), image=img, tile=float(info.get("tile", 1.0)), unit=info.get("uv") == "unit",
                     alpha_tex=mat.name in ALPHA_TEX, alpha_const=ALPHA_CONST.get(mat.name), emissive=EMISSIVE.get(mat.name, 0.0))


def convert_prop_material(mat):
    """소품 GLB 재질(Principled + 텍스처) → 같은 2톤 셀."""
    if not mat.use_nodes:
        mat.use_nodes = True
    img, col = None, (0.6, 0.6, 0.6)
    for nd in mat.node_tree.nodes:
        if nd.type == "BSDF_PRINCIPLED":
            c = nd.inputs["Base Color"].default_value
            col = (c[0], c[1], c[2])
            for l in nd.inputs["Base Color"].links:
                if l.from_node.type == "TEX_IMAGE":
                    img = l.from_node.image
    cel_material(mat, base_rgb=None if img else "#999999", image=img, unit=True)
    if img is None:
        for nd in mat.node_tree.nodes:
            if nd.type == "RGB":
                nd.outputs[0].default_value = (*col, 1.0)


def import_props(sockets, coll):
    n = 0
    for s in sockets:
        if s["kind"] != "prop":
            continue
        p = os.path.join(ROOT, s["info"]["glb"])
        if not os.path.exists(p):
            continue
        e = bpy.data.objects.get(s["name"])
        if e is None:
            continue
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=p)
        new = [o for o in bpy.data.objects if o not in before]
        for o in new:
            if o.parent is None or o.parent not in new:
                o.parent = e
                o.matrix_parent_inverse = mathutils.Matrix.Identity(4)
            if o.type == "MESH":
                for slot in o.material_slots:
                    if slot.material and not slot.material.get("_cel"):
                        convert_prop_material(slot.material)
                        slot.material["_cel"] = 1
        n += 1
    return n


def add_standin(loc, coll, name):
    """1.74m 회색 대역(플레이어 자리)."""
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.5, location=(loc[0], loc[1], 0.75))
    body = bpy.context.active_object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.12, location=(loc[0], loc[1], 1.62), segments=12, ring_count=8)
    head = bpy.context.active_object
    m = bpy.data.materials.new(name + "_mat")
    cel_material(m, base_rgb="#4A4A48")
    for o in (body, head):
        o.data.materials.append(m)
        o.name = name + ("_body" if o is body else "_head")
    return body, head


def lights_for(sockets, shot):
    made = []
    night = shot == "g06"
    for s in sockets:
        if s["kind"] != "light":
            continue
        kind = s["info"].get("light")
        pos = to_bl(s["pos"])
        if kind == "fluor":
            if night and s["name"] != "Sock_Light_Fluor_08":
                continue
            L = bpy.data.lights.new(s["name"], "AREA")
            L.shape = "RECTANGLE"
            L.size, L.size_y = 1.2, 0.3
            L.energy = 45.0 if not night else 60.0
            L.color = (0.92, 0.97, 0.93)
        elif kind == "spot":
            L = bpy.data.lights.new(s["name"], "SPOT")
            L.energy = 160.0 if not night else 260.0
            L.spot_size = math.radians(55)
            L.spot_blend = 0.4
            L.color = (1.0, 0.93, 0.82)
            L.shadow_soft_size = 0.2
        elif kind == "area":
            if night:
                continue
            L = bpy.data.lights.new(s["name"], "AREA")
            L.size, L.size_y = 1.2, 0.3
            L.energy = 40.0
        else:
            continue
        o = bpy.data.objects.new(s["name"] + "_L", L)
        bpy.context.scene.collection.objects.link(o)
        o.location = pos
        d = mathutils.Vector(to_bl(s["fwd"]))
        o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        made.append(o)
    return made


def main():
    o = args()
    rx, ry = (int(v) for v in o["res"].lower().split("x"))
    shots = o["shots"].split(",") if o["shots"] else ["g01", "g02", "g03", "g04", "g05", "g06"]
    os.makedirs(o["out"], exist_ok=True)
    objs = G.generate()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    coll = bpy.data.collections.new("Gym_Hyehwa")
    sc.collection.children.link(coll)
    root, made = blend.build_district(objs, "Gym", coll)
    setup_kit_materials()
    nprops = import_props(G.SOCKETS, coll) if o["props"] == "1" else 0
    # 렌더 설정
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = int(o["samples"])
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.55, 0.55, 0.55, 1.0)
    bg.inputs[1].default_value = 0.35
    # Freestyle 먹선
    sc.render.use_freestyle = True
    sc.render.line_thickness_mode = "ABSOLUTE"
    sc.render.line_thickness = 1.4
    vl = bpy.context.view_layer
    fs = vl.freestyle_settings
    fs.crease_angle = math.radians(125)
    while fs.linesets:
        fs.linesets.remove(fs.linesets[0])
    ls = fs.linesets.new("ink")
    ls.select_silhouette = ls.select_border = ls.select_crease = True
    ls.select_contour = False
    ls.select_edge_mark = False
    ls.linestyle.color = INK
    ls.linestyle.thickness = 1.4
    cams = {s["name"].split("_")[-1]: s for s in G.SOCKETS if s["kind"] == "camera"}
    for shot in shots:
        s = cams.get(shot)
        if s is None:
            print("[preview] 카메라 없음", shot)
            continue
        lights = lights_for(G.SOCKETS, shot)
        cam_d = bpy.data.cameras.new("Cam_" + shot)
        cam_d.sensor_fit = "VERTICAL"
        cam_d.angle_y = math.radians(float(s["info"].get("fov_v", 50)))
        cam_d.clip_start = 0.1
        cam = bpy.data.objects.new("Cam_" + shot, cam_d)
        sc.collection.objects.link(cam)
        loc = mathutils.Vector(to_bl(s["pos"]))
        tgt = mathutils.Vector(to_bl(s["info"]["look_at"]))
        cam.location = loc
        cam.rotation_euler = (tgt - loc).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        # 플레이어 대역: 카메라 앞 2.2m, 왼쪽 0.55m
        fwd = (tgt - loc)
        fwd.z = 0.0
        fwd.normalize()
        left = mathutils.Vector((-fwd.y, fwd.x, 0.0))
        sp = loc + fwd * 2.4 + left * 0.95
        standin = add_standin((sp.x, sp.y), coll, "Standin_" + shot)
        sc.render.filepath = os.path.join(o["out"], shot + ".png")
        bpy.ops.render.render(write_still=True)
        print("[preview] 렌더", shot, sc.render.filepath)
        for ob in standin:
            bpy.data.objects.remove(ob, do_unlink=True)
        for ob in lights:
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.objects.remove(cam, do_unlink=True)
    with open(os.path.join(o["out"], "preview_info.json"), "w", encoding="utf-8") as f:
        json.dump(dict(shots=shots, props_imported=nprops, res=[rx, ry]), f, ensure_ascii=False, indent=1)
    print("[preview] 끝 소품 %d" % nprops)


if __name__ == "__main__":
    main()
