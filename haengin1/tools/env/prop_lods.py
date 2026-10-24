# 소품 GLB → LOD 3단이 든 GLB (docs/10_맵_아트패스.md 10장: LOD1 50% · LOD2 20%)
# 사용: blender -b --factory-startup -P tools/env/prop_lods.py -- --src concept/art/env/props --out unity/HaenginMainEvent/Assets/_Project/Art/Env/Props [--only a,b]
# 결과: <out>/<이름>.glb — 오브젝트 <이름>_LOD0 / _LOD1 / _LOD2 (같은 재질·텍스처, 정면 +Z · 바닥 0 그대로). Unity 쪽 EnvSetup 이 LODGroup 프리팹으로 만든다.
import bpy, os, sys, glob, json

def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {"src": "concept/art/env/props", "out": "unity/HaenginMainEvent/Assets/_Project/Art/Env/Props", "only": ""}
    i = 0
    while i < len(a):
        k = a[i].lstrip("-")
        if k in o and i + 1 < len(a):
            o[k] = a[i + 1]; i += 2
        else:
            i += 1
    return o

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def one(src, out, name):
    reset()
    bpy.ops.import_scene.gltf(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        return None
    for o in bpy.context.scene.objects:
        o.select_set(o.type == "MESH")
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    base = bpy.context.view_layer.objects.active
    # 부모(빈 노드)에서 떼어 월드 변환 그대로 적용
    bpy.ops.object.select_all(action="DESELECT")
    base.select_set(True)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o is not base:
            bpy.data.objects.remove(o, do_unlink=True)
    base.name = name + "_LOD0"
    base.data.name = name + "_LOD0"
    tris0 = sum(len(p.vertices) - 2 for p in base.data.polygons)
    out_objs = [base]
    for lod, ratio in ((1, 0.5), (2, 0.2)):
        d = base.copy(); d.data = base.data.copy()
        d.name = name + "_LOD%d" % lod; d.data.name = d.name
        bpy.context.scene.collection.objects.link(d)
        m = d.modifiers.new("dec", "DECIMATE")
        m.ratio = ratio
        m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = d
        bpy.ops.object.modifier_apply(modifier=m.name)
        out_objs.append(d)
    tris = [sum(len(p.vertices) - 2 for p in o.data.polygons) for o in out_objs]
    bpy.ops.object.select_all(action="DESELECT")
    for o in out_objs:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_image_format="AUTO", export_texcoords=True, export_normals=True,
                              export_materials="EXPORT", export_animations=False, export_skins=False, export_morph=False)
    return tris

def main():
    o = args()
    root = os.getcwd()
    src = os.path.join(root, o["src"]); out = os.path.join(root, o["out"])
    os.makedirs(out, exist_ok=True)
    only = [s for s in o["only"].split(",") if s]
    report = {}
    for f in sorted(glob.glob(os.path.join(src, "*.glb"))):
        name = os.path.splitext(os.path.basename(f))[0]
        if only and name not in only:
            continue
        tris = one(f, os.path.join(out, name + ".glb"), name)
        report[name] = tris
        print("[prop_lods] %s: %s" % (name, tris))
    with open(os.path.join(out, "prop_lods_report.json"), "w", encoding="utf-8") as fp:
        json.dump(report, fp, ensure_ascii=False, indent=1)
    print("[prop_lods] 끝: %d개 → %s" % (len(report), out))

main()
