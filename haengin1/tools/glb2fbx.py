# GLB(Tripo 리깅 + 클립 1개) → FBX (Unity Humanoid 용, 2026-10-06)
# 사용: "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tools/glb2fbx.py -- <src.glb> <dst.fbx> <클립 이름> [메시 1|0]
#   예) ... -- concept/art/3d/anim/siwoo_walk.glb unity/HaenginMainEvent/Assets/_Project/Art/Characters/Siwoo/SiwooWalk.fbx Walk
# 메시는 기본 1(넣음): Unity CharSetup 이 아바타 기준 자세를 스킨 바인드 포즈에서 읽는다(메시 없는 FBX 는 기준 자세를 알 수 없음).
# 배율 = FBX_SCALE_ALL(Unity 에서 루트 배율 1, 키 1.74m), 정면 = glTF +Z 그대로(Unity +Z). 텍스처는 넣지 않는다(GLB 의 JPEG 를 따로 꺼내 씀).
import bpy, sys
argv = sys.argv[sys.argv.index('--')+1:]
src, dst, clip = argv[0], argv[1], argv[2]
with_mesh = len(argv) < 4 or argv[3] != '0'
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30
sc.render.fps_base = 1.0
bpy.ops.import_scene.gltf(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
mesh = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm]
# 뼈 표시용 Icosphere 등 나머지는 지운다
for o in list(bpy.data.objects):
    if o != arm and o not in mesh:
        bpy.data.objects.remove(o, do_unlink=True)
if not with_mesh:
    for o in mesh: bpy.data.objects.remove(o, do_unlink=True)
    mesh = []

act = arm.animation_data.action
act.name = clip
def fcurves(a):
    for L in a.layers:
        for st in L.strips:
            for cb in st.channelbags:
                for fc in cb.fcurves: yield fc
fcs = list(fcurves(act))
f0, f1 = (round(x) for x in act.frame_range)
# 루프 이음새: 클립 끝 프레임이 첫 프레임과 같으면(Meshy/Tripo 새 클립 — 끝이 처음과 같은 '닫힌 루프') 그대로 둔다.
# 다르면(예전 Casual_Walk·Run_02: 마지막 → 처음이 한 프레임 움직임) 마지막 다음 프레임(f1+1)에 첫 프레임 값을 넣는다
# → 어느 쪽이든 Unity 에서 길이가 정확히 한 주기(이음새에서 한 칸이 빠지거나 한 칸 멈추지 않음)
quat = {}
for fc in fcs:
    if fc.data_path.endswith('rotation_quaternion'):
        quat.setdefault(fc.data_path, {})[fc.array_index] = fc
def same(fc):
    a, b = fc.evaluate(f0), fc.evaluate(f1)
    if fc.data_path.endswith('rotation_quaternion'):
        g = quat[fc.data_path]
        if sum(g[i].evaluate(f0) * g[i].evaluate(f1) for i in range(4)) < 0: b = -b
        return abs(a - b) < 2e-3
    return abs(a - b) < 2e-3 * max(1.0, abs(a))
closed = all(same(fc) for fc in fcs)
if not closed:
    for fc in fcs:
        v = fc.evaluate(f0)
        if fc.data_path.endswith('rotation_quaternion'):
            g = quat[fc.data_path]
            if sum(g[i].evaluate(f0) * g[i].evaluate(f1) for i in range(4)) < 0: v = -v
        fc.keyframe_points.insert(f1 + 1, v, options={'FAST'})
    for fc in fcs: fc.update()
sc.frame_start, sc.frame_end = int(f0), int(f1) + (0 if closed else 1)
print('LOOP', 'closed(끝 = 처음, 그대로)' if closed else 'open(이음새 프레임 추가)')
print('CLIP', clip, 'range', tuple(act.frame_range), 'frames', sc.frame_start, sc.frame_end, 'fcurves', len(fcs))

# 참고: FBX 뼈 노드의 기본 자세(Lcl 값)는 내보낼 때의 '현재 자세'(클립 한 프레임)다(액션을 떼고 휴지 자세로 돌려도 바뀌지 않음 — 2026-10-06 확인).
# 그래서 Unity CharSetup 이 아바타 기준 자세를 노드 기본값이 아니라 스킨 바인드 포즈에서 계산한다. 메시를 빼면 안 되는 이유.
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for o in mesh: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)
print('WROTE', dst)
