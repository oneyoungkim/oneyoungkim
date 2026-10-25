# 행인1의 메인이벤트 — 시우 자전거 페달질 동작 4종(docs/14 B, 2026-10-26 — 크레딧 0)
# 사용:
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tools/bike_anim.py -- \
#       <Siwoo.fbx(정식 모델, 손가락 뼈)> <DeliveryBike.fbx(tools/bike_split.py 결과)> <출력 폴더> \
#       [--render <렌더 폴더>] [--tex <SiwooTex.jpg>] [--clips cruise,climb,coast,startstop] [--bike-scale 1.0] [--no-glb]
#   예) ... -- unity/HaenginMainEvent/Assets/_Project/Art/Characters/Siwoo/Siwoo.fbx unity/HaenginMainEvent/Assets/_Project/Art/Bike/DeliveryBike.fbx concept/art/3d/anim/bike
# 결과(출력 폴더):
#   siwoo_bike_{cruise,climb,coast,startstop}.fbx — Unity Humanoid 용(아마추어 + 대리 메시: 뼈마다 1cm 삼각형, glb2fbx.py 'p' 와 같음 — 바인드 포즈 보존)
#   siwoo_bike_*.glb — 미리보기(시우 메시 + 자전거, 크랭크·바퀴·기울기까지 움직임)
#   bike_curves.json — 클립마다 프레임별 Unity 크랭크 각 θ·Unity 기울기 Lean·구간·FootDown·실측 오차
# 좌표 약속(이 스크립트 = 자전거 좌표: 자전거 고정, 원점 = 두 바퀴 접지선 가운데 땅, 정면 −Y, 위 +Z, 라이더 오른쪽 = −X)
#   Unity (x, y, z) = (−Bx, Bz, −By). 앞으로 밟기 = Blender +X 축 양의 회전 = Unity Crank.localRotation = Euler(θ,0,0) 의 θ 증가.
#   Unity θ = 0 = FBX 쉬는 자세(오른쪽 페달이 꼭대기에서 약 25.6° 뒤). 오른쪽 페달 각 φR(꼭대기 0°, 앞으로 +) = φR0 + θ.
#   기울기 Lean(도) = Unity LeanRoot.localRotation = Euler(0,0,Lean) 값 그대로: + = 라이더 왼쪽으로 기욺(BikeVisual 과 같은 부호).
#   동작은 '자전거에 대한' 몸 자세다: 자전거가 기울면(댄싱·정차) 몸은 자전거 좌표에서 반대로 기울어 세상 기준으로 거의 서 있게 했다.
#   → Unity 에서는 시우를 자전거와 같은 LeanRoot 아래에 두고 Lean 곡선을 LeanRoot 에 더하면 된다.
# 방식(제약·베이크 대신 해석적 FK — 같은 결과를 프레임마다 정확히):
#   프레임마다 목표(페달 → 발볼, 그립 → 손목, 안장 → 골반)를 계산 → 다리·팔은 2관절 IK(무릎 = 앞·조금 바깥, 팔꿈치 = 바깥·뒤),
#   발은 발목 각 곡선(ankling)대로, 손은 그립을 감싸게 손바닥 아래·손가락 굽힘, 척추 3마디 + 골반에 숙임을 나눔, 머리는 앞을 봄.
#   뼈마다 '쉬는 자세 대비 세상 회전 Q' 를 정하고 부모부터 머리 위치를 FK 로 이어 붙인 뒤 Bone.convert_local_to_pose 로 기본 행렬에 키.
#   앉은 높이 = 시우 메시(Body_LOD0)를 실제로 변형해 엉덩이 아랫면이 안장 윗면보다 4mm 눌리게 맞춤(배달통 앞면과 2.7cm 띄움).
#   손가락 = 손가락 뿌리 줄을 손잡이 축과 나란히 두고, 마디마다 다음 관절이 손잡이 축에서 25·24·21mm 에 처음 닿는 각까지 굽힘.
# 함정(2026-10-26 실측):
#   ① DeliveryBike.fbx 의 PedalL/R 빈 점은 Crank 메시 자식이라 Blender FBX 임포트가 위치를 잘못 푼다 → 같은 폴더 DeliveryBike.json 의 BB·페달 값을 씀.
#   ② GripL/GripR 빈 점은 레버·종 정점까지 섞인 무게중심이라 봉보다 약 2.5cm 뒤·1.5cm 위 → 봉 메시(|x| 0.29~0.375m)의 가운데값으로 다시 잼.
#   ③ 이름 PedalL/GripL 은 x<0(라이더 오른쪽)이다 — 여기서는 x 부호로 좌우를 고른다.
#   ④ 정식 모델 FBX 는 Idle 클립(cm 배율 키)을 품고 있어 배율을 적용하기 전에 animation_data_clear 해야 메시가 제자리.
# 클립(30fps, 페달 1회전 = 1주기, 닫힌 루프 = 끝 프레임이 첫 프레임과 같음 — Unity 클립 길이가 정확히 한 주기):
#   cruise    앉아서 평지 순항   30프레임 = 1회전(기준 60rpm) · 골반 좌우 흔들림·롤 · 숨(작게)
#   climb     일어서서 밟기(댄싱) 40프레임 = 1회전(45rpm) · 미는 다리 반대로 자전거 ±8° · 몸통은 세상 기준 거의 수직 · 한 바퀴 두 번 오르내림
#   coast     내리막 관성        60프레임 · 크랭크 멈춤(오른발 앞 수평) · 몸 낮춤 · 숨
#   startstop 출발·정지          150프레임 · 왼발 땅(자전거 왼쪽 6°) → 오른발 밀어 출발·안장 앉기 → 2회전 → 감속·왼발 땅 → 정지(끝 = 처음)
import bpy, sys, os, json, math
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index('--') + 1:]
SIWOO, BIKE, OUT = argv[0], argv[1], argv[2]
def opt(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
RENDER = opt('--render')
TEX = opt('--tex')
CLIPS = opt('--clips', 'cruise,climb,coast,startstop').split(',')
BIKE_SCALE = float(opt('--bike-scale', '1.0'))
NO_GLB = '--no-glb' in argv
FPS = 30
os.makedirs(OUT, exist_ok=True)
Z = Vector((0, 0, 1)); X = Vector((1, 0, 0)); Y = Vector((0, 1, 0))
SIDES = (('Left', 1.0), ('Right', -1.0))      # 라이더 왼쪽 = +X

# ───────────────────────── 불러오기
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps, sc.render.fps_base = FPS, 1.0
bpy.ops.import_scene.fbx(filepath=SIWOO)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body_meshes = [o for o in bpy.data.objects if o.type == 'MESH']
arm.animation_data_clear()                      # 정식 모델에 들어 있는 Idle 클립(cm 배율 키)은 버린다
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
bpy.ops.object.select_all(action='DESELECT')
for o in [arm] + body_meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)   # FBX cm → 미터·배율 1
bpy.ops.object.select_all(action='DESELECT')
siwoo_objs = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=BIKE)
bike_objs = [o for o in bpy.data.objects if o not in siwoo_objs]
bike_root = bpy.data.objects['Bike']
bike_root.scale = (BIKE_SCALE,) * 3
bpy.context.view_layer.update()
def W(name):
    return bpy.data.objects[name].matrix_world.translation.copy()
# 페달 빈 점은 Crank(메시) 자식이라 Blender FBX 임포트가 위치를 잘못 푼다 → bike_split.py 가 남긴 JSON(같은 좌표) 값을 쓴다
_bj = json.load(open(os.path.splitext(BIKE)[0] + '.json', encoding='utf-8'))
BB = Vector((0.0, _bj['BB']['y'], _bj['BB']['z'])) * BIKE_SCALE
_p = sorted([Vector(_bj['BB']['pedalL']) * BIKE_SCALE, Vector(_bj['BB']['pedalR']) * BIKE_SCALE], key=lambda v: v.x)
PED_X = {'Right': _p[0].x, 'Left': _p[1].x}
CR = math.hypot(_p[0].y - BB.y, _p[0].z - BB.z)
PHI0 = math.degrees(math.atan2(-(_p[0].y - BB.y), _p[0].z - BB.z))     # 쉬는 자세 오른쪽 페달 각
_g = sorted([W('GripL'), W('GripR')], key=lambda v: v.x)
GRIP_EMPTY = {'Right': _g[0], 'Left': _g[1]}
SADDLE = W('Saddle')
frame_obj = bpy.data.objects['Frame']
_fco = [frame_obj.matrix_world @ v.co for v in frame_obj.data.vertices]
_k = BIKE_SCALE
# 손잡이 축: 빈 점 Grip* 은 브레이크 레버·종까지 섞인 무게중심이라 실제 봉보다 2cm 뒤·1.7cm 위 → 봉 메시(|x| 0.29~0.375)로 다시 잼
GRIP = {}
for _sd, _e in GRIP_EMPTY.items():
    _v = [c for c in _fco if 0.29 * _k < abs(c.x) < 0.375 * _k and (c.x > 0) == (_e.x > 0) and abs(c.y - _e.y) < 0.045 * _k and abs(c.z - _e.z) < 0.045 * _k]
    _xs = [c.x for c in _v]
    _md = lambda a: sorted(a)[len(a) // 2]       # 가운데값(레버·종 정점에 덜 끌리게)
    GRIP[_sd] = Vector(((min(_xs) + max(_xs)) / 2, _md([c.y for c in _v]), _md([c.z for c in _v])))
HAND_X_IN = 0.018 * _k     # 가운데 손가락 뿌리가 손 가운데보다 검지 쪽 — 손 가운데를 봉 가운데에 두려고 안쪽으로
SADDLE_TOP = max(c.z for c in _fco if abs(c.x) < 0.07 * _k and 0.12 * _k < c.y < 0.36 * _k and 0.7 * _k < c.z < 0.95 * _k)
_box = [c for c in _fco if c.z > 0.9 * _k and c.y > 0.25 * _k]
BOX_FRONT = min(c.y for c in _box) if _box else 9.0
BOX_FRONT_Z = min(c.z for c in _box) if _box else 9.0
print('GRIP bar', {k: tuple(round(x, 4) for x in v) for k, v in GRIP.items()}, 'empty', {k: tuple(round(x, 4) for x in v) for k, v in GRIP_EMPTY.items()})
print('BIKE BB', tuple(round(v, 3) for v in BB), 'crank', round(CR, 4), 'phi0', round(PHI0, 2), 'saddle top', round(SADDLE_TOP, 4), 'box front', round(BOX_FRONT, 3), round(BOX_FRONT_Z, 3))

# ───────────────────────── 쉬는 자세 자료
bones = arm.data.bones
REST = {}
for b in bones:
    REST[b.name] = dict(head=b.head_local.copy(), tail=b.tail_local.copy(), R=b.matrix_local.to_3x3(), parent=b.parent.name if b.parent else None)
ORDER = []
def _walk(b):
    ORDER.append(b.name)
    for c in b.children:
        _walk(c)
for b in bones:
    if b.parent is None:
        _walk(b)
def rh(n): return REST[n]['head']
def rdir(a, b): return (rh(b) - rh(a)).normalized()
ARM0 = {s: rdir(s + 'Arm', s + 'ForeArm') for s, _ in SIDES}
FORE0 = {s: rdir(s + 'ForeArm', s + 'Hand') for s, _ in SIDES}
LA = {s: (rh(s + 'ForeArm') - rh(s + 'Arm')).length for s, _ in SIDES}
LF = {s: (rh(s + 'Hand') - rh(s + 'ForeArm')).length for s, _ in SIDES}
HINGE0 = {s: Y.cross(ARM0[s]).normalized() for s, _ in SIDES}          # 쉬는 자세 팔꿈치 경첩(팔꿈치 끝이 뒤 +Y)
F0 = {s: rdir(s + 'Hand', s + 'HandMiddle1') for s, _ in SIDES}         # 손목 → 가운데 손가락 뿌리
K0 = {s: rdir(s + 'HandPinky1', s + 'HandIndex1') for s, _ in SIDES}    # 손가락 뿌리 줄(새끼 → 검지) — 그립과 나란히 놓을 축
N0 = {s: (REST[s + 'HandPalmN']['tail'] - REST[s + 'HandPalmN']['head']).normalized() for s, _ in SIDES}   # 손바닥 법선
THIGH0 = {s: rdir(s + 'UpLeg', s + 'Leg') for s, _ in SIDES}
SHIN0 = {s: rdir(s + 'Leg', s + 'Foot') for s, _ in SIDES}
LT = {s: (rh(s + 'Leg') - rh(s + 'UpLeg')).length for s, _ in SIDES}
LS = {s: (rh(s + 'Foot') - rh(s + 'Leg')).length for s, _ in SIDES}
V0 = {s: rh(s + 'ToeBase') - rh(s + 'Foot') for s, _ in SIDES}         # 발목 → 발볼
FOOT_PITCH0 = math.degrees(math.asin(-V0['Left'].normalized().z))      # 평평하게 선 발의 발목→발볼 내림각(약 40°)
SOLE_BALL = rh('LeftToeBase').z                                         # 발볼 관절 ~ 신발 바닥(약 0.038)
print('REST thigh %.3f shin %.3f arm %.3f fore %.3f foot pitch0 %.1f ball h %.3f' % (LT['Left'], LS['Left'], LA['Left'], LF['Left'], FOOT_PITCH0, SOLE_BALL))

PEDAL_UP = 0.012          # 페달 몸통 위 면(가운데 빈 점에서)
HAND_F, HAND_N = 0.072, 0.031
THUMB_WRAP = 0.8    # 손목 → 손잡이 중심: 손가락 방향 · 손바닥 법선 방향
FINGERS = {'Index': (95, 105, 80), 'Middle': (95, 105, 80), 'Ring': (95, 105, 80), 'Pinky': (95, 105, 80), 'Thumb': (40, 60, 60)}   # 마디별 최대 굽힘(도)
FINGER_R = (0.025, 0.024, 0.021)     # 손잡이 축 → 가운데·끝 마디 관절·손끝(손잡이 반지름 16mm + 손가락 두께)
THUMB_R = (0.027, 0.025, 0.022)
TWIST_K = 0.6             # 손 비틀기 중 아래팔 몫(Humanoid 가 다시 나눔)

# ───────────────────────── 수학
def R(axis, deg): return Matrix.Rotation(math.radians(deg), 3, axis)
def RX(d): return Matrix.Rotation(math.radians(d), 3, 'X')
def RY(d): return Matrix.Rotation(math.radians(d), 3, 'Y')
def RZ(d): return Matrix.Rotation(math.radians(d), 3, 'Z')
def basis(t, s):
    x = t.normalized(); y = (s - s.dot(x) * x).normalized(); z = x.cross(y)
    return Matrix((x, y, z)).transposed()
def align(t0, s0, t1, s1): return basis(t1, s1) @ basis(t0, s0).transposed()
def ik2(S, T, a, b, pole):
    """2관절: 뿌리 S · 목표 T · 길이 a, b · 가운데 관절이 향할 점 pole → (가운데 관절, 끝, 못 닿은 길이, 경첩 축)"""
    dv = T - S; d0 = dv.length; u = dv.normalized()
    d = min(max(d0, abs(a - b) + 1e-4), (a + b) * 0.9995)
    ca = (a * a + d * d - b * b) / (2 * a * d); sa = math.sqrt(max(0.0, 1 - ca * ca))
    v = pole - S; v = (v - v.dot(u) * u).normalized()
    M = S + u * (a * ca) + v * (a * sa)
    return M, S + u * d, max(0.0, d0 - d), v.cross(u).normalized()
def twist_deg(q, axis):
    a = axis.normalized(); p = a * Vector((q.x, q.y, q.z)).dot(a)
    t = Quaternion((q.w, p.x, p.y, p.z))
    if t.magnitude < 1e-9: return 0.0
    t.normalize()
    ang = 2 * math.atan2(Vector((t.x, t.y, t.z)).dot(a), t.w)
    return math.degrees((ang + math.pi) % (2 * math.pi) - math.pi)
def smooth(e0, e1, x):
    if e1 == e0: return 1.0 if x >= e1 else 0.0
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
def lerp(a, b, t): return a + (b - a) * t
def cs(d): return math.cos(math.radians(d))
def sn(d): return math.sin(math.radians(d))
def pedal(side, phiR):
    phi = phiR + (0.0 if side == 'Right' else 180.0)
    return Vector((PED_X[side], BB.y - CR * sn(phi), BB.z + CR * cs(phi)))
def ankling(phi, base, amp):
    """발끝 내림각(+ = 발끝 아래): 밟을 때(90° 근처) 뒤꿈치 살짝 내림, 아래·올라올 때(260° 근처) 발끝 내림"""
    return base + amp * cs(phi - 260.0)
def foot_dir(side, toe_down, toe_out=7.0, tilt=0.0):
    """발목→발볼 방향. tilt = 세상 땅이 자전거 좌표에서 기운 각(자전거 기울기 ψ) — 세상 기준 방향을 자전거 좌표로"""
    s = 1.0 if side == 'Left' else -1.0
    p = FOOT_PITCH0 + toe_down
    a = Vector((s * sn(toe_out) * cs(p), -cs(toe_out) * cs(p), -sn(p)))
    return (RY(-tilt) @ a).normalized()

# ───────────────────────── 자세 풀기
def solve(p):
    Q, H, err = {}, {}, {}
    Q['Hips'] = RZ(p['pyaw']) @ RY(p['proll']) @ RX(p['tilt'])
    H['Hips'] = p['pel'].copy()
    def head(n):
        if n not in H:
            par = REST[n]['parent']
            H[n] = head(par) + Q[par] @ (rh(n) - rh(par))
        return H[n]
    for i, n in enumerate(('Spine02', 'Spine01', 'Spine', 'neck', 'Head')):
        Q[n] = RZ(p['yaw'][i]) @ RY(p['roll'][i]) @ RX(p['lean'][i])
    for side, s in SIDES:
        Q[side + 'Shoulder'] = Q['Spine'] @ RZ(-s * p['clav']) @ RY(-s * p['shrug'])     # clav = 앞으로 모음, shrug + = 어깨 올림
    # 팔
    for side, s in SIDES:
        S = head(side + 'Arm')
        G = GRIP[side] - X * (s * HAND_X_IN)
        f1 = Vector((s * 0.14, -1.0, -p['wrist_down'])).normalized()
        n1 = X.cross(f1).normalized()                                  # 손바닥 = 아래·조금 뒤
        Wt = G - f1 * HAND_F - n1 * HAND_N
        pole = (S + Wt) * 0.5 + Vector((s * p['elbow_out'], 0.22, -0.18))
        E, Wa, short, hn = ik2(S, Wt, LA[side], LF[side], pole)
        Q[side + 'Arm'] = align(ARM0[side], HINGE0[side], E - S, hn)
        qf = align(FORE0[side], HINGE0[side], Wa - E, hn)
        qh = align(F0[side], K0[side], f1, -s * X)                       # 손가락 뿌리 줄 = 손잡이 축(검지가 안쪽)
        tw = twist_deg((qf.inverted() @ qh).to_quaternion(), FORE0[side])
        Q[side + 'ForeArm'] = qf @ R(FORE0[side], tw * TWIST_K)
        Q[side + 'Hand'] = qh
        for fn, angs in FINGERS.items():
            names = [side + 'Hand' + fn + str(i) for i in (1, 2, 3)]
            d1 = (REST[names[0]]['tail'] - rh(names[0])).normalized()
            ax = d1.cross(N0[side]).normalized()
            kx = K0[side] if K0[side].dot(ax) > 0 else -K0[side]
            if fn != 'Thumb':      # 네 손가락은 손가락 뿌리 줄을 축으로 굽힘(손잡이와 나란히 — 옆으로 새지 않게)
                ax = kx
            else:                  # 엄지는 뒤에서 손잡이 아래로 감기(손가락과 반대 방향으로 돎) — 굽힘 축과 손잡이 축을 섞음
                ax = (ax - kx * THUMB_WRAP).normalized()
            # 마디마다 다음 관절(끝 마디는 손끝)이 손잡이 축에서 FINGER_R 거리에 처음 닿는 각까지 굽힘(손가락 길이가 좌우 달라도 감김)
            J = head(names[0]); cum = 0.0
            ends = [rh(names[1]), rh(names[2]), REST[names[2]]['tail']]
            rt = THUMB_R if fn == 'Thumb' else FINGER_R
            for i, nm in enumerate(names):
                seg = ends[i] - rh(nm)
                best, bd = None, None
                for c in range(int(round(cum)) - 10, int(round(cum)) + angs[i] + 1):
                    nx = J + qh @ R(ax, c) @ seg
                    d = math.hypot(nx.y - G.y, nx.z - G.z)
                    if d <= rt[i]:
                        best = c; break
                    if bd is None or abs(d - rt[i]) < bd:
                        bd, best = abs(d - rt[i]), c
                cum = float(best) * p['curl']
                Q[nm] = qh @ R(ax, cum)
                J = J + Q[nm] @ seg
                err.setdefault(side + 'Curl', []).append(cum)
        err[side + 'Hand'] = short
        err[side + 'Elbow'] = math.degrees((E - S).angle(Wa - E))
        err[side + 'WristT'] = Wt
    # 다리
    for side, s in SIDES:
        S = head(side + 'UpLeg')
        ft = p['feet'][side]          # dict(ball=발볼 닿는 점(페달 위 면/땅), a=발목→발볼 방향, up=발바닥 법선 기준)
        a1, up = ft['a'], ft['up']
        a0 = V0[side].normalized()
        qf = align(a0, Z.cross(a0), a1, up.cross(a1))
        sole_n = qf @ Z
        ball_t = ft['ball'] + sole_n * SOLE_BALL
        A = ball_t - qf @ V0[side]
        pole = (S + A) * 0.5 + Vector((s * p['knee_out'], -0.6, 0.0))
        K, Ae, short, hn = ik2(S, A, LT[side], LS[side], pole)
        Q[side + 'UpLeg'] = align(THIGH0[side], X, K - S, hn)
        Q[side + 'Leg'] = align(SHIN0[side], X, Ae - K, hn)
        Q[side + 'Foot'] = qf
        Q[side + 'ToeBase'] = qf
        err[side + 'Foot'] = short
        err[side + 'Knee'] = math.degrees((K - S).angle(Ae - K))
        err[side + 'BallT'] = ball_t
        err[side + 'Reach'] = (A - S).length / (LT[side] + LS[side])
    for n in ORDER:
        if n not in Q:
            Q[n] = Q[REST[n]['parent']]
    for n in ORDER:
        head(n)
    return Q, H, err

PREV = {}
def apply(Q, H, frame=None):
    M = {}
    for n in ORDER:
        M[n] = Matrix.Translation(H[n]) @ (Q[n] @ REST[n]['R']).to_4x4()
        pb = arm.pose.bones[n]; b = pb.bone
        if b.parent:
            bm = b.convert_local_to_pose(M[n], b.matrix_local, parent_matrix=M[b.parent.name], parent_matrix_local=b.parent.matrix_local, invert=True)
        else:
            bm = b.convert_local_to_pose(M[n], b.matrix_local, invert=True)
        loc, rot, _ = bm.decompose()
        if n in PREV:
            rot.make_compatible(PREV[n])
        PREV[n] = rot.copy()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = rot
        pb.location = loc if n == 'Hips' else Vector()
        if frame is not None:
            pb.keyframe_insert('rotation_quaternion', frame=frame)
            if n == 'Hips':
                pb.keyframe_insert('location', frame=frame)

# ───────────────────────── 엉덩이 ↔ 안장(메시로 잼)
body = bpy.data.objects['Body_LOD0']
def butt_gap():
    """(엉덩이 아랫면 − 안장 윗면, 배달통 앞면 − 몸 뒷면) 미터"""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg); me = ev.to_mesh()
    mw = body.matrix_world
    zmin, ymax_box = 9.0, -9.0
    for v in me.vertices:
        c = mw @ v.co
        if abs(c.x) < 0.075 and SADDLE.y - 0.06 < c.y < SADDLE.y + 0.10 and c.z > SADDLE_TOP - 0.10:   # 종아리·뒤꿈치 제외
            zmin = min(zmin, c.z)
        if abs(c.x) < 0.2 and c.z > BOX_FRONT_Z:
            ymax_box = max(ymax_box, c.y)
    ev.to_mesh_clear()
    return zmin - SADDLE_TOP, BOX_FRONT - ymax_box

# ───────────────────────── 자세 매개변수(클립별)
SEAT = dict(y=0.19, z=1.05)     # 앉은 골반 머리(안장 맞춤으로 z 를 고침)
def seated(phi, breath=0.0, mode='cruise', comp=0.0):
    """앉은 자세. phi = 오른쪽 페달 각. comp = 자전거 기울기(도, + 왼쪽) — 몸을 세상 기준으로 세우는 반대 롤"""
    beta = 2.5 * cs(phi - 20.0)                    # 골반 롤(+ = 왼쪽 아래): 아래로 내려간 다리 쪽이 내려감
    low = mode == 'coast'
    tilt = 22.0 if low else 18.0
    lean = [31, 39, 46, 27, 4] if low else [22, 27, 30, 18, 7]
    lean = [l - (breath * (0.0, 0.6, 1.0, 0.3, 0.0)[i]) for i, l in enumerate(lean)]
    k = (0.55, 0.7, 0.8, 0.9, 1.0)
    p = dict(pel=Vector((0.008 * cs(phi - 20.0), SEAT['y'] + (0.012 if low else 0.0), SEAT['z'] - (0.004 if low else 0.0))),
             tilt=tilt, proll=beta - 0.55 * comp, pyaw=1.5 * sn(phi),
             lean=lean, roll=[0.45 * beta - k[0] * comp, 0.15 * beta - k[1] * comp, -k[2] * comp, -k[3] * comp, -k[4] * comp],
             yaw=[0.8 * sn(phi), 0.3 * sn(phi), -0.6 * sn(phi), 0.0, 0.0],
             clav=6.0, shrug=-2.0 + 0.6 * breath, wrist_down=0.55 if low else 0.42, elbow_out=0.38 if low else 0.30,
             curl=1.0, knee_out=0.07)
    feet = {}
    for side, s in SIDES:
        ph = phi + (0.0 if side == 'Right' else 180.0)
        td = 2.0 if low else ankling(ph, 8.0, 12.0)
        feet[side] = dict(ball=pedal(side, phi) + Z * PEDAL_UP, a=foot_dir(side, td), up=Z.copy())
    p['feet'] = feet
    return p

def standing(phi, psi):
    """일어서서 밟기. psi = 자전거 기울기(+ 왼쪽). 몸통은 세상 기준 0.3ψ 만 따라감 → 자전거 좌표에선 −0.7ψ"""
    bob = 0.022 * cs(2 * (phi - 60.0))
    k = (0.6, 0.7, 0.78, 0.9, 1.0)
    p = dict(pel=Vector((-0.045 * sn(phi - 10.0), 0.10, 1.17 + bob)),
             tilt=32.0, proll=-0.6 * psi + 3.0 * cs(phi - 30.0), pyaw=5.0 * sn(phi),
             lean=[40, 49, 55, 36, 14], roll=[-k[0] * psi, -k[1] * psi, -k[2] * psi, -k[3] * psi, -k[4] * psi],
             yaw=[2.0 * sn(phi), 1.0 * sn(phi), -2.5 * sn(phi), -1.0 * sn(phi), 0.0],
             clav=9.0, shrug=-4.0, wrist_down=0.36, elbow_out=0.26, curl=1.0, knee_out=0.06)
    feet = {}
    for side, s in SIDES:
        ph = phi + (0.0 if side == 'Right' else 180.0)
        feet[side] = dict(ball=pedal(side, phi) + Z * PEDAL_UP, a=foot_dir(side, ankling(ph, 4.0, 15.0)), up=Z.copy())
    p['feet'] = feet
    return p

FOOT_DOWN_W = Vector((0.285, -0.045, 0.0))      # 정차 때 왼발볼이 닿는 땅(세상 좌표, 자전거 원점 기준)
STOP_LEAN = 6.0
def stopped(phi, psi):
    """정차: 안장 앞으로 내려선 채 왼발 땅, 오른발 페달(φR ≈ 50°), 자전거 왼쪽 ψ"""
    upw = RY(-psi) @ Z                       # 자전거 좌표에서 본 세상 위
    k = (0.6, 0.75, 0.85, 0.95, 1.0)
    p = dict(pel=Vector((0.03, -0.02, 0.95)), tilt=8.0, proll=-0.6 * psi + 2.0, pyaw=-4.0,
             lean=[10, 13, 15, 10, 4], roll=[-k[0] * psi, -k[1] * psi, -k[2] * psi, -k[3] * psi, -k[4] * psi],
             yaw=[-2.0, -1.0, 0.0, 0.0, 0.0], clav=4.0, shrug=-2.0, wrist_down=0.55, elbow_out=0.16, curl=1.0, knee_out=0.10)
    p['feet'] = {'Left': dict(ball=RY(-psi) @ FOOT_DOWN_W, a=foot_dir('Left', 0.0, 14.0, tilt=psi), up=upw),
                 'Right': dict(ball=pedal('Right', phi) + Z * PEDAL_UP, a=foot_dir('Right', ankling(phi, 6.0, 10.0)), up=Z.copy())}
    # 왼다리가 땅에 닿게 골반 높이: 고관절 → 발목 거리 = 다리 길이 × 0.975
    lo, hi = 0.6, 1.3
    for _ in range(28):
        mid = (lo + hi) / 2; p['pel'].z = mid
        _, _, e = solve(p)
        if e['LeftReach'] > 0.975: hi = mid
        else: lo = mid
    p['pel'].z = lo
    return p

def blend(pa, pb, t):
    out = {}
    for k_, va in pa.items():
        vb = pb[k_]
        if k_ == 'feet':
            out[k_] = {s: dict(ball=lerp(va[s]['ball'], vb[s]['ball'], t), a=va[s]['a'].lerp(vb[s]['a'], t).normalized(),
                               up=va[s]['up'].lerp(vb[s]['up'], t).normalized()) for s in va}
        elif isinstance(va, list):
            out[k_] = [lerp(a, b, t) for a, b in zip(va, vb)]
        else:
            out[k_] = lerp(va, vb, t)
    return out

# ───────────────────────── 앉은 높이 맞추기(엉덩이 아랫면 = 안장 윗면 − 4mm)
SINK = 0.004
for it in range(4):
    Q, H, e = solve(seated(90.0)); apply(Q, H)
    gap, boxc = butt_gap()
    SEAT['z'] -= gap + SINK
    print('SEAT fit', it, 'gap %.4f box clearance %.3f -> z %.4f' % (gap, boxc, SEAT['z']))

# ───────────────────────── 클립 정의
def startstop_crank():
    """프레임별 오른쪽 페달 각: 0~20 정지, 20~40 가속, 40~100 일정, 100~122 감속, 끝 = 처음 + 3회전"""
    om = [smooth(20, 40, f) * (1 - smooth(100, 122, f)) for f in range(151)]
    tot = sum((om[f] + om[f + 1]) / 2 for f in range(150))
    sc_ = 1080.0 / tot
    ph, out = 50.0, [50.0]
    for f in range(150):
        ph += (om[f] + om[f + 1]) / 2 * sc_
        out.append(ph)
    return out, sc_, om
SS_PHI, SS_OMEGA, SS_OM = startstop_crank()

CLIP = {
    'cruise': dict(n=30, loop=True, rpm=60.0),
    'climb': dict(n=40, loop=True, rpm=45.0),
    'coast': dict(n=60, loop=True, rpm=0.0),
    'startstop': dict(n=150, loop=False, rpm=SS_OMEGA * FPS / 360.0 * 60.0,
                      segments={'start': [0, 45], 'ride': [45, 100], 'stop': [100, 150]}),
}
def clip_frame(name, f):
    """→ (자세 매개변수, 오른쪽 페달 각 φR, 기울기 ψ(+ 왼쪽), 크랭크 속도 비(순항 = 1))"""
    c = CLIP[name]; n = c['n']
    if name == 'cruise':
        phi = PHI0 + 360.0 * f / n
        return seated(phi, breath=0.6 * sn(360.0 * f / n)), phi, 0.0, 1.0
    if name == 'climb':
        phi = PHI0 + 360.0 * f / n
        psi = 8.0 * sn(phi - 10.0)                  # 오른발이 밟을 때(φR 90°) 자전거는 왼쪽(+)으로
        return standing(phi, psi), phi, psi, 0.75
    if name == 'coast':
        phi = 90.0
        t = 360.0 * f / n
        p = seated(phi, breath=1.4 * sn(t), mode='coast')
        p['pel'] = p['pel'] + Vector((0.005 * sn(t + 40), 0.0, 0.003 * sn(2 * t)))
        p['proll'] += 1.2 * sn(t + 40)
        return p, phi, 0.0, 1.2
    if name == 'startstop':
        phi = SS_PHI[f]
        psi = STOP_LEAN * (1 - smooth(22, 40, f)) + STOP_LEAN * smooth(108, 128, f)
        ws = smooth(24, 42, f) * (1 - smooth(104, 126, f))      # 0 = 정차 자세, 1 = 앉음
        wl = smooth(26, 38, f) * (1 - smooth(106, 124, f))      # 왼발: 0 = 땅, 1 = 페달
        pst = stopped(phi, psi)
        pse = seated(phi, comp=psi)
        p = blend(pst, pse, ws)
        p['pel'] = p['pel'] + Vector((0, 0, 0.05 * math.sin(math.pi * ws)))     # 안장 코를 넘어가게 위로 살짝 둘러감
        fl_g, fl_p = pst['feet']['Left'], pse['feet']['Left']
        p['feet']['Left'] = dict(ball=lerp(fl_g['ball'], fl_p['ball'], wl) + Z * (0.07 * math.sin(math.pi * wl)),
                                 a=fl_g['a'].lerp(fl_p['a'], wl).normalized(), up=fl_g['up'].lerp(fl_p['up'], wl).normalized())
        return p, phi, psi, SS_OMEGA / 12.0 * SS_OM[f]
    raise KeyError(name)

def theta_unity(phi): return (phi - PHI0) % 360.0

# ───────────────────────── 대리 메시(glb2fbx 'p' 와 같음)
def make_proxy():
    verts, faces, groups = [], [], []
    for b in arm.data.bones:
        h = b.head_local; k = len(verts)
        verts += [(h.x, h.y, h.z), (h.x + 0.01, h.y, h.z), (h.x, h.y, h.z + 0.01)]
        faces.append((k, k + 1, k + 2)); groups.append(b.name)
    me = bpy.data.meshes.new('Proxy'); me.from_pydata(verts, [], faces)
    pm = bpy.data.objects.new('Proxy', me); sc.collection.objects.link(pm)
    for i, nm in enumerate(groups):
        g = pm.vertex_groups.new(name=nm); g.add([3 * i, 3 * i + 1, 3 * i + 2], 1.0, 'REPLACE')
    pm.parent = arm
    md = pm.modifiers.new('Armature', 'ARMATURE'); md.object = arm
    return pm
proxy = make_proxy()

curves = dict(
    note='행인1 자전거 동작 곡선(tools/bike_anim.py). theta = Unity Crank.localRotation = Euler(theta,0,0) 의 각(도, 0 = FBX 쉬는 자세, 앞으로 밟으면 증가). '
         'lean = Unity LeanRoot.localRotation = Euler(0,0,lean) 의 각(도, + = 라이더 왼쪽). 동작은 자전거 좌표(자전거 기울기 없이) 기준 — Unity 에서 Lean 을 시우·자전거 공통 부모에 더한다. '
         'crank_speed = 크랭크 각속도 / 순항(12°/프레임). 좌표: Unity (x,y,z) = (-Bx, Bz, -By), 자전거 원점 = 접지선 가운데 땅. 루프 클립(cruise·climb)은 정규화 시간 = theta/360.',
    fps=FPS, bike_scale=BIKE_SCALE, crank_len=round(CR, 4), phi0_right_pedal_deg=round(PHI0, 2),
    bb_unity=[round(-BB.x, 4), round(BB.z, 4), round(-BB.y, 4)], saddle_top=round(SADDLE_TOP, 4),
    grip_bar_unity={sd: [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)] for sd, v in GRIP.items()},
    grip_note='손잡이 봉 가운데(메시로 잰 값). 자전거 FBX 의 GripL/GripR 빈 점은 레버·종이 섞여 봉보다 약 2.5cm 뒤·1.5cm 위(Unity 기준 z +0.025, y +0.015) — 손 IK 보정에는 이 값이나 wrist_target_unity_f0 를 쓸 것. 손잡이 이름 Left/Right = 라이더 기준.',
    foot_down_unity=dict(ball_ground=[round(-FOOT_DOWN_W.x, 4), round(FOOT_DOWN_W.z, 4), round(-FOOT_DOWN_W.y, 4)], side='left', bike_lean=STOP_LEAN,
                         note='왼발볼이 닿는 땅 자리(세상 기준, 자전거 원점·기울기 전). 자전거가 Lean 만큼 기울어 있을 때.'),
    clips={})
ACTIONS = {}
for name in CLIPS:
    c = CLIP[name]; n = c['n']
    arm.animation_data_clear(); PREV.clear()
    rec = dict(frames=n + 1, length_s=round(n / FPS, 4), fps=FPS, loop=c['loop'], ref_rpm=round(c['rpm'], 2), theta=[], lean=[], crank_speed=[])
    errs = dict(foot=[], hand=[], knee=[], elbow=[], reach=[])
    for f in range(n + 1):
        p, phi, psi, wsp = clip_frame(name, f)
        Q, H, e = solve(p)
        apply(Q, H, frame=f)
        rec['theta'].append(round(theta_unity(phi), 3)); rec['lean'].append(round(psi, 3)); rec['crank_speed'].append(round(wsp, 3))
        errs['foot'].append(max(e['LeftFoot'], e['RightFoot']) * 1000); errs['hand'].append(max(e['LeftHand'], e['RightHand']) * 1000)
        errs['knee'] += [e['LeftKnee'], e['RightKnee']]; errs['elbow'] += [e['LeftElbow'], e['RightElbow']]
        errs['reach'] += [e['LeftReach'], e['RightReach']]
    Q0, H0_, e0 = solve(clip_frame(name, 0)[0])
    U = lambda v: [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]
    rec['wrist_target_unity_f0'] = {sd: U(e0[sd + 'WristT']) for sd, _ in SIDES}
    rec['hips_unity_f0'] = U(H0_['Hips'])
    if 'segments' in c:
        rec['segments'] = c['segments']
        rec['segment_theta'] = {k: [rec['theta'][v[0]], rec['theta'][v[1]]] for k, v in c['segments'].items()}
    if name == 'coast':
        rec['note'] = '크랭크 고정(오른발 앞 수평, theta 일정). 왼발 앞이 필요하면 Unity 상태의 Mirror 를 켜고 theta + 180.'
    act = arm.animation_data.action
    act.name = 'Bike_' + name
    act.use_fake_user = True
    ACTIONS[name] = act
    # 실측(뼈 실제 위치로): 발볼–목표, 손목–목표, 엉덩이 간격(앉은 프레임)
    meas = dict(ball_mm=[], wrist_mm=[], butt_mm=[], box_mm=[], finger_mm=[])
    for f in range(0, n + 1, 1 if n <= 40 else 3):
        sc.frame_set(f)
        p, phi, psi, _ = clip_frame(name, f)
        Q, H, e = solve(p)
        for side, s in SIDES:
            ball = arm.matrix_world @ arm.pose.bones[side + 'ToeBase'].head
            meas['ball_mm'].append((ball - e[side + 'BallT']).length * 1000)
            wr = arm.matrix_world @ arm.pose.bones[side + 'Hand'].head
            meas['wrist_mm'].append((wr - e[side + 'WristT']).length * 1000)
            g = GRIP[side]
            for fn in ('Index', 'Middle', 'Ring', 'Pinky'):
                for j in (2, 3):
                    h = arm.matrix_world @ arm.pose.bones['%sHand%s%d' % (side, fn, j)].head
                    meas['finger_mm'].append(math.hypot(h.y - g.y, h.z - g.z) * 1000)
        if name in ('cruise', 'coast', 'climb') or (name == 'startstop' and 45 <= f <= 100):
            g, bx = butt_gap(); meas['butt_mm'].append(g * 1000); meas['box_mm'].append(bx * 1000)
    seam = 0.0
    sc.frame_set(0); q0 = {pb.name: pb.matrix.to_quaternion() for pb in arm.pose.bones}
    sc.frame_set(n); q1 = {pb.name: pb.matrix.to_quaternion() for pb in arm.pose.bones}
    for k_ in q0:
        seam = max(seam, math.degrees(q0[k_].rotation_difference(q1[k_]).angle))
    def st(v): return dict(max=round(max(v), 2), mean=round(sum(v) / len(v), 2), min=round(min(v), 2)) if v else None
    rec['check'] = dict(ik_short_foot_mm=st(errs['foot']), ik_short_hand_mm=st(errs['hand']), knee_bend_deg=st(errs['knee']), elbow_bend_deg=st(errs['elbow']),
                        leg_reach_ratio=st(errs['reach']), ball_to_target_mm=st(meas['ball_mm']), wrist_to_target_mm=st(meas['wrist_mm']),
                        butt_gap_mm=st(meas['butt_mm']), box_clear_mm=st(meas['box_mm']), finger_joint_to_bar_axis_mm=st(meas['finger_mm']), loop_seam_deg=round(seam, 4))
    curves['clips'][name] = rec
    print('CLIP', name, json.dumps(rec['check'], ensure_ascii=False))
    sc.frame_start, sc.frame_end = 0, n
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); proxy.select_set(True)
    bpy.context.view_layer.objects.active = arm
    dst = os.path.join(OUT, 'siwoo_bike_%s.fbx' % name)
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode='STRIP', embed_textures=False)
    print('WROTE', dst, os.path.getsize(dst))

with open(os.path.join(OUT, 'bike_curves.json'), 'w', encoding='utf-8') as fp:
    json.dump(curves, fp, ensure_ascii=False, indent=1)
print('WROTE', os.path.join(OUT, 'bike_curves.json'))

# ───────────────────────── 미리보기 장면(자전거 기울기 루트 · 크랭크 · 바퀴) — GLB · 렌더 공용
bpy.data.objects.remove(proxy, do_unlink=True)
lean_root = bpy.data.objects.new('LeanRoot', None); sc.collection.objects.link(lean_root)
def piv(name, at):
    e = bpy.data.objects.new(name, None); sc.collection.objects.link(e)
    e.location = at; e.parent = lean_root; return e
wf, wr = bpy.data.objects['WheelF'], bpy.data.objects['WheelR']
crank_p = piv('CrankPivot', BB)
wheelF_p = piv('WheelFPivot', wf.matrix_world.translation.copy())
wheelR_p = piv('WheelRPivot', wr.matrix_world.translation.copy())
bpy.context.view_layer.update()
def reparent(o, parent):
    mw = o.matrix_world.copy(); o.parent = parent; o.matrix_parent_inverse.identity()
    bpy.context.view_layer.update(); o.matrix_world = mw
reparent(bpy.data.objects['Crank'], crank_p)
reparent(wf, wheelF_p); reparent(wr, wheelR_p)
reparent(bike_root, lean_root)
reparent(arm, lean_root)
for o in body_meshes:
    if o.name not in ('Body_LOD0', 'Face_LOD0'):
        o.hide_render = True; o.hide_viewport = True
if TEX and os.path.exists(TEX):
    img = bpy.data.images.load(os.path.abspath(TEX))
    for mat in bpy.data.objects['Body_LOD0'].data.materials:
        nt = mat.node_tree
        bsdf = [nd for nd in nt.nodes if nd.type == 'BSDF_PRINCIPLED'][0]
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
    TEX_NODES = [nd for mat in bpy.data.objects['Body_LOD0'].data.materials for nd in mat.node_tree.nodes if nd.type == 'TEX_IMAGE']
    IMG_FULL = img
    IMG_SMALL = img.copy(); IMG_SMALL.name = 'SiwooTex_1k'; IMG_SMALL.scale(1024, 1024)    # GLB 미리보기는 1024
else:
    TEX_NODES, IMG_FULL, IMG_SMALL = [], None, None
bmat = bpy.data.materials.new('BikePreview'); bmat.diffuse_color = (0.30, 0.42, 0.36, 1)
bmat.use_nodes = True
_bs = [nd for nd in bmat.node_tree.nodes if nd.type == 'BSDF_PRINCIPLED'][0]; _bs.inputs['Base Color'].default_value = (0.30, 0.42, 0.36, 1)
for o in bike_objs:
    if o.type == 'MESH':
        o.data.materials.clear(); o.data.materials.append(bmat)
fmat = bpy.data.materials.new('FacePreview'); fmat.diffuse_color = (0.93, 0.78, 0.66, 1)
fo = bpy.data.objects['Face_LOD0']; fo.data.materials.clear(); fo.data.materials.append(fmat)

def fcurves_of(o):
    a = o.animation_data.action
    for L in a.layers:
        for st_ in L.strips:
            for cb in st_.channelbags:
                for fc in cb.fcurves:
                    yield fc
def key_scene(name):
    """크랭크·바퀴·기울기 키(클립 길이만큼) — 미리보기용"""
    n = CLIP[name]['n']; rec = curves['clips'][name]
    for o in (crank_p, wheelF_p, wheelR_p, lean_root):
        o.animation_data_clear(); o.rotation_mode = 'XYZ'
    spin = 0.0
    for f in range(n + 1):
        th = (SS_PHI[f] - PHI0) if name == 'startstop' else (rec['theta'][f] if name != 'coast' else rec['theta'][0])
        if name in ('cruise', 'climb'):
            th = 360.0 * f / n
        crank_p.rotation_euler = (math.radians(th), 0, 0)
        crank_p.keyframe_insert('rotation_euler', frame=f)
        wheelF_p.rotation_euler = (spin, 0, 0); wheelR_p.rotation_euler = (spin, 0, 0)
        wheelF_p.keyframe_insert('rotation_euler', frame=f); wheelR_p.keyframe_insert('rotation_euler', frame=f)
        spin += math.radians(12.0 * 2.2) * rec['crank_speed'][f]
        lean_root.rotation_euler = (0, math.radians(rec['lean'][f]), 0)      # Blender +Y 축 양의 회전 = 위가 +X(라이더 왼쪽)로
        lean_root.keyframe_insert('rotation_euler', frame=f)
    for o in (crank_p, wheelF_p, wheelR_p, lean_root):
        for fc in fcurves_of(o):
            for kp in fc.keyframe_points:
                kp.interpolation = 'LINEAR'
    arm.animation_data.action = ACTIONS[name]
    sc.frame_start, sc.frame_end = 0, n

if not NO_GLB:
    # 미리보기 GLB: 시우 LOD2(삼각형 20%) + 텍스처 1024 + 자전거(크랭크·바퀴·기울기 움직임) — 파일을 작게
    lod2 = bpy.data.objects['Body_LOD2']
    lod2.hide_viewport = False; lod2.hide_render = False
    for nd in TEX_NODES: nd.image = IMG_SMALL
    for name in CLIPS:
        key_scene(name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in [arm, lean_root, crank_p, wheelF_p, wheelR_p, lod2] + bike_objs:
            o.select_set(True)
        dst = os.path.join(OUT, 'siwoo_bike_%s.glb' % name)
        bpy.ops.export_scene.gltf(filepath=dst, use_selection=True, export_format='GLB', export_animation_mode='ACTIVE_ACTIONS',
                                  export_force_sampling=True, export_frame_range=True, export_image_format='JPEG', export_image_quality=75,
                                  export_apply=False, export_yup=True)
        print('WROTE', dst, os.path.getsize(dst))
    for nd in TEX_NODES: nd.image = IMG_FULL
    lod2.hide_viewport = True; lod2.hide_render = True

# ───────────────────────── 렌더(검수용)
if RENDER:
    os.makedirs(RENDER, exist_ok=True)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_cavity = True; sh.cavity_type = 'WORLD'
    sh.show_object_outline = True; sh.object_outline_color = (0.1, 0.08, 0.09)
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.93, 0.92, 0.89)
    sc.render.resolution_x = sc.render.resolution_y = 480
    sc.render.image_settings.file_format = 'PNG'
    gm = bpy.data.meshes.new('Ground'); gm.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
    go = bpy.data.objects.new('Ground', gm); sc.collection.objects.link(go)
    gmat = bpy.data.materials.new('G'); gmat.diffuse_color = (0.80, 0.78, 0.74, 1); gm.materials.append(gmat)
    def cam(name, loc, rot, scale):
        cd = bpy.data.cameras.new(name); cd.type = 'ORTHO'; cd.ortho_scale = scale
        co = bpy.data.objects.new(name, cd); sc.collection.objects.link(co)
        co.location = loc; co.rotation_euler = [math.radians(a) for a in rot]
        return co
    cams = {'side': cam('CamSide', (-4.0, 0.0, 0.92), (90, 0, -90), 2.05), 'front': cam('CamFront', (0.0, -4.0, 0.92), (90, 0, 0), 2.05)}
    for name in CLIPS:
        key_scene(name)
        n = CLIP[name]['n']
        for f in range(n + 1):
            sc.frame_set(f)
            for cn, co in cams.items():
                sc.camera = co
                sc.render.filepath = os.path.join(RENDER, '%s_%s_%03d.png' % (name, cn, f))
                bpy.ops.render.render(write_still=True)
        print('RENDERED', name, n + 1)
    # 손·발 확대(첫 프레임, 검수용): 오른손 그립 측면·정면, 오른발 페달 측면
    gr = GRIP['Right']
    close = {'hand': cam('CamHand', (-2.0, gr.y, gr.z), (90, 0, -90), 0.32), 'handf': cam('CamHandF', (gr.x, gr.y - 2.0, gr.z), (90, 0, 0), 0.32),
             'handt': cam('CamHandT', (gr.x, gr.y, gr.z + 2.0), (0, 0, 0), 0.32)}
    for name in CLIPS:
        key_scene(name)
        sc.frame_set(0)
        for cn, co in close.items():
            sc.camera = co
            sc.render.filepath = os.path.join(RENDER, '%s_%s_000.png' % (name, cn))
            bpy.ops.render.render(write_still=True)
print('DONE')
