# -*- coding: utf-8 -*-
"""블렌더 쪽: envkit 메시 묶음 → 블렌더 오브젝트(재질 슬롯 = M_* 이름) → 구역별 FBX(또는 GLB).

좌표 변환: Unity (X, Y, Z) → 블렌더 (-X, -Z, Y), 감기 방향 뒤집음. FBX 는 glb2fbx.py 와 같은 축(-Z 앞, +Y 위,
FBX_SCALE_ALL)에 bake_space_transform=True(축 회전을 메시에 구워 Unity 오브젝트 회전이 0 이 되게).
→ Unity 에서 프리팹을 원점(0,0,0)·회전 0·배율 1 로 놓으면 zone1.json 좌표 그대로 맞는다.
"""
import bpy

from .mats import MATS, hex_rgb, srgb_to_linear


def to_bl(p, o):
    return (-(p[0] - o[0]), -(p[2] - o[2]), p[1] - o[1])


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    info = MATS.get(name, dict(color="#FF00FF"))
    lin = srgb_to_linear(hex_rgb(info["color"]))
    m.diffuse_color = (*lin, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
        bsdf.inputs["Roughness"].default_value = 1.0
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.0
        if name == "M_Glass_Lit" and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*lin, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 0.0
    return m


def mesh_from_mbs(name, mbs, origin, weld=1e-4):
    """MB 여러 개를 메시 하나로(같은 자리 정점 합침, 재질 슬롯 = 이름 정렬 순)."""
    verts, vmap, faces, fmats, fuvs, fsm = [], {}, [], [], [], []
    inv = 1.0 / weld
    for mb in mbs:
        if mb is None:
            continue
        for f, m, uv, s in zip(mb.F, mb.M, mb.UV, mb.S):
            idx = []
            for vi in f:
                p = to_bl(mb.V[vi], origin)
                key = (round(p[0] * inv), round(p[1] * inv), round(p[2] * inv))
                j = vmap.get(key)
                if j is None:
                    j = len(verts)
                    vmap[key] = j
                    verts.append(p)
                idx.append(j)
            # 감기 뒤집기(좌표 변환이 거울이라)
            idx = idx[::-1]
            uvr = list(uv)[::-1]
            # 같은 정점이 겹친 면(합친 뒤 퇴화) 정리
            clean, cuv = [], []
            for k, j in enumerate(idx):
                if clean and clean[-1] == j:
                    continue
                clean.append(j)
                cuv.append(uvr[k])
            if len(clean) > 1 and clean[0] == clean[-1]:
                clean.pop()
                cuv.pop()
            if len(set(clean)) < 3 or len(set(clean)) != len(clean):
                continue
            faces.append(clean)
            fmats.append(m)
            fuvs.append(cuv)
            fsm.append(s)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    names = sorted(set(fmats))
    for n in names:
        me.materials.append(get_mat(n))
    slot = {n: i for i, n in enumerate(names)}
    mi = [slot[m] for m in fmats]
    me.polygons.foreach_set("material_index", mi)
    me.polygons.foreach_set("use_smooth", [bool(s) for s in fsm])
    uvl = me.uv_layers.new(name="UVMap")
    flat = []
    for poly, cuv in zip(me.polygons, fuvs):
        flat.extend(cuv)
    data = [c for uv in flat for c in uv]
    uvl.data.foreach_set("uv", data)
    me.validate(clean_customdata=False)
    me.update()
    return me


def new_obj(name, me, parent, loc, coll):
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    ob.location = loc
    return ob


def build_district(objs, district, coll, props=True):
    """구역 하나의 오브젝트 트리. 맨 위 노드 = 건물 묶음(빈 오브젝트, 원점)·간판·창문 불·자리 표시.
    뿌리 노드를 따로 두지 않는다: Blender 5.2 FBX 내보내기는 bake_space_transform 일 때 '뿌리의 손자' 메시 변환을
    틀리게 쓴다(회전 90°·축 안 바뀐 위치 — fbx_inspect.py 로 확인, 2026-10-06). Unity 에서는 파일 자체가 프리팹 뿌리가 되어
    Z1_<구역>/<건물>/<건물>_LOD0 구조가 그대로 나온다."""
    root = None
    made = []
    for ob in objs:
        if ob.district != district:
            continue
        o = ob.origin()
        if ob.kind in ("socket", "prop"):
            pos = ob.empty[0]
            e = bpy.data.objects.new(ob.name, None)
            e.empty_display_type = "SINGLE_ARROW"
            e.empty_display_size = 0.4
            coll.objects.link(e)
            e.parent = root
            e.location = to_bl(pos, (0.0, 0.0, 0.0))
            n = ob.empty[1]
            import mathutils
            d = mathutils.Vector(to_bl(n, (0.0, 0.0, 0.0)))
            if d.length > 1e-6:
                e.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
            if ob.kind == "prop":
                # Unity +Z(정면) = 블렌더 -Y → Z 축 회전 θ: R·(0,-1,0) = 정면
                import math as _m
                nb = to_bl(ob.empty[1], (0.0, 0.0, 0.0))
                e.rotation_euler = (0.0, 0.0, _m.atan2(nb[0], -nb[1]))
                e.empty_display_type = "ARROWS"
                if props:
                    e["prop"] = ob.info["prop"]
                    e["glb"] = ob.info["glb"]
                made.append(e)
                continue
            if props:
                e["sock_kind"] = ob.info.get("sock_kind", "")
                if ob.info.get("tenant"):
                    e["tenant"] = ob.info["tenant"]
            made.append(e)
            continue
        loc = to_bl(o, (0.0, 0.0, 0.0))
        if ob.lods:
            grp = bpy.data.objects.new(ob.name, None)
            grp.empty_display_size = 1.0
            coll.objects.link(grp)
            grp.parent = root
            grp.location = (0.0, 0.0, 0.0)   # 부모는 항상 원점(FBX bake_space_transform 이 원점 아닌 빈 부모 밑 메시를 틀어지게 함)
            if props:
                t = ob.info.get("lod_screen_heights")
                if t:
                    grp["lod_screen_heights"] = ",".join("%.4f" % v for v in t)
                if ob.info.get("block"):
                    grp["zone1_block"] = ob.info["block"]
                    grp["zone1_index"] = int(ob.info.get("index", -1))
            for L, mb in enumerate(ob.lods):
                if mb is None or mb.empty():
                    continue
                me = mesh_from_mbs("%s_LOD%d" % (ob.name, L), [mb], o)
                made.append(new_obj("%s_LOD%d" % (ob.name, L), me, grp, loc, coll))
            if ob.det is not None and not ob.det.empty():
                me = mesh_from_mbs("%s_Detail_LOD0" % ob.name, [ob.det], o)
                made.append(new_obj("%s_Detail_LOD0" % ob.name, me, grp, loc, coll))
            made.append(grp)
        elif ob.single is not None and not ob.single.empty():
            me = mesh_from_mbs(ob.name, [ob.single], o)
            x = new_obj(ob.name, me, root, loc, coll)
            if props:
                if ob.kind == "winlit":
                    x["window_threshold"] = float(ob.info["threshold"])
                if ob.kind == "sign":
                    x["sign_size_m"] = "%.3f,%.3f" % tuple(ob.info["size_m"])
                    x["sign_kind"] = ob.info["sign_kind"]
                    x["atlas"] = ob.info.get("atlas", "")
                    x["tenant"] = ob.info.get("tenant") or ""
            made.append(x)
    return root, made


def make_fbx_deterministic():
    """같은 입력 → 같은 FBX 바이트(git 에 바이너리 변경이 쌓이지 않게).
    Blender 5.2 FBX 내보내기는 ① 머리말에 지금 시각 ② 노드 UID 에 파이썬 hash(문자열)(프로세스마다 무작위)를 쓴다.
    ② 를 SHA-1 기반으로, ① 을 고정 시각으로 바꾼다(실행 중에만, 애드온 파일은 그대로)."""
    import datetime
    import hashlib
    from io_scene_fbx import fbx_utils, export_fbx_bin
    if getattr(fbx_utils, "_haengin_patched", False):
        return
    orig_key_to_uuid = fbx_utils._key_to_uuid

    def key_to_uuid(uuids, key):
        if isinstance(key, int) and 0 <= key < 2 ** 63:
            return orig_key_to_uuid(uuids, key)
        h = int.from_bytes(hashlib.sha1(repr(key).encode("utf-8")).digest()[:8], "little") % (2 ** 62)
        return orig_key_to_uuid(uuids, h)
    fbx_utils._key_to_uuid = key_to_uuid
    orig_header = export_fbx_bin.fbx_header_elements

    def header(root, scene_data, time=None):
        return orig_header(root, scene_data, time=datetime.datetime(2026, 1, 1, 0, 0, 0))
    export_fbx_bin.fbx_header_elements = header
    fbx_utils._haengin_patched = True


def export_fbx(path, objs_to_export):
    make_fbx_deterministic()
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs_to_export:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs_to_export[-1]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=False, use_custom_props=True,
        add_leaf_bones=False, bake_anim=False, path_mode="STRIP", embed_textures=False,
        use_mesh_edges=False, use_triangles=False)


def export_glb(path, objs_to_export):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs_to_export:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_yup=True,
                              export_apply=False, export_extras=True, export_materials="EXPORT",
                              export_image_format="NONE" if False else "AUTO")
