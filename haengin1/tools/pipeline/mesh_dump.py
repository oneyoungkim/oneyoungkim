# Blender 배치: 리깅 GLB 몸 메시의 정점 위치·법선·삼각형 UV 를 npz 로(머리카락 마스크를 UV 공간에서 다시 굽기용, hair_blond.py)
# 사용: blender -b --factory-startup --python mesh_dump.py -- <rigged.glb> <out.npz>
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
if arm and arm.animation_data:
    arm.animation_data.action = None
    for t in list(arm.animation_data.nla_tracks): arm.animation_data.nla_tracks.remove(t)
bpy.context.view_layer.update()
body = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
me = body.data; mw = body.matrix_world; n3 = mw.to_3x3()
me.calc_loop_triangles()
P = np.array([tuple(mw @ v.co) for v in me.vertices], np.float32)
Nn = np.array([tuple((n3 @ v.normal).normalized()) for v in me.vertices], np.float32)
uvl = me.uv_layers.active.data
T = np.array([list(t.vertices) for t in me.loop_triangles], np.int32)
UV = np.array([[tuple(uvl[li].uv) for li in t.loops] for t in me.loop_triangles], np.float32)
np.savez(a[1], P=P, N=Nn, T=T, UV=UV)
print('DUMP', len(P), len(T), flush=True)
