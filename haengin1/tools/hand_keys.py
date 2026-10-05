# 손 셰이프 키(주먹·잡기 손) 만들기 — docs/08_M2_전투_설계.md 9장 A′ (2026-10-06)
# 리깅 모델(손가락 뼈 없음)의 손 정점을 손가락 다섯 묶음으로 나눠, 손가락마다 3마디(밑·가운데·끝) 관절을 두고 손바닥 쪽으로 굽힌 모양을
# 셰이프 키 Fist_L · Fist_R · Grip_L · Grip_R 로 굽는다. 9장 A′ 의 '임시 손가락 뼈 + 자동 가중치 → 셰이프 키로 적용'과 같은 결과를
# 뼈 대신 마디마다 회전을 직접 계산해서 만든다(관절 앞뒤 1.2cm 를 부드럽게 섞음 — 자동 가중치의 이음매 역할).
#   주먹: 마디 85° · 90° · 70°, 엄지 = 손바닥 쪽으로 40° · 50° 접음(검지·중지 위로 감는 모으기는 아직 — 왼손에서 바깥으로 돌아 뺌)
#   잡기 손(옷깃을 쥔 손): 55° · 60° · 40°, 엄지 25° · 30°
# 네 손가락은 손가락마다 나누지 않고 '벙어리장갑'처럼 손 넓이 방향 관절선 3개로 굽힌다(손가락별로 나누면 따로 모델링된 손가락 조각·테이프가
# 찢어져 가시가 생겼다 — 2026-10-06 시험). 관절 위치는 손끝 길이 윤곽에 비례.
# 모델별 값(PROFILES, 2026-10-06 덩치 손 수정): 원본 GLB 파일 이름 앞부분(naengjanggo·scrum)으로 고른다. 프로필이 없는 모델(시우·깐족이·석 달)은
#   위 기본값 그대로 — 계산 경로도 예전과 한 비트도 다르지 않다(프로필 키가 없으면 기본 상수·기본 판정을 씀).
#   덩치 둘이 갈퀴 손이 된 까닭: 손가락이 벌어진 넓은 손이라 엄지 쪽 판정(손바닥 가운데 높이에서 더 튀어나온 쪽)이 새끼 쪽 손날을 엄지로 잡았다
#   → 진짜 엄지는 '새끼' 묶음으로 들어가 밑동만 조금 굽고 옆으로 뻗은 채, 새끼·손날은 엄지처럼 40°·50° 만 접힘, 손끝 윤곽도 엄지 정점에 끌려
#   틀어짐(왼·오른손 모두). 엄지 쪽을 바로잡아도 ① 손 넓이 바깥 20% 엄지 묶음에 검지 바깥 가장자리가 섞여 검지가 덜 굽고 ② 넓이 칸별 손끝 윤곽이
#   비스듬한 약지 끝을 새끼 칸에 넣어 새끼 끝마디가 안 굽었다(갈고리).
#   프로필: 엄지 쪽 = 가장 멀리 뻗은 쪽, 손바닥 쪽 = 왼손·오른손 해부학, 엄지 경계 = 검지 가장자리를 지나는 엄지 축 나란한 비스듬한 선,
#   손끝 = 손가락별(물갈퀴 위 조각 묶음) 중심선에서, 손가락 조금 모으기, 엄지는 맞은편(손바닥 앞)으로 돌려 검지·중지 위로 감싼다.
#   이 판정 문제(엄지 쪽 반대)는 깐족이 양손·석 달 왼손에도 있지만 엄지가 짧고 손가락이 붙어 있어 티가 덜 난다 — 기본값은 건드리지 않음.
# 쓰는 곳: glb2fbx.py 의 5번째 인자 hands=1(이 파일을 불러 씀, 원본 경로를 src 로 넘김). 단독 점검: blender -b --python tools/hand_keys.py -- <src.glb> <out_dir>
#   → 손 정점 묶음·주먹 모양을 위에서·옆에서 본 점 그림(PNG, PIL 없이 Blender 이미지로) 을 out_dir 에 남긴다.
import bpy, sys, math, os
import numpy as np

FIST = (85.0, 90.0, 70.0)
GRIP = (55.0, 60.0, 40.0)
THUMB_FIST = (0.0, 40.0, 50.0)      # 모으기(손 길이 축 둘레 — 0: 시험해 보니 왼손 엄지가 바깥으로 돌아 끔) · 밑·가운데 마디 · 끝 마디
THUMB_GRIP = (0.0, 25.0, 30.0)
JOINTS = (0.0, 0.45, 0.73)     # 손가락 길이 비율로 밑·가운데·끝 마디
BLEND = 0.010                  # 가운데·끝 관절 앞뒤 섞는 폭(m)
RAMP = 0.015                   # 밑 마디: 손가락 시작에서 이만큼 들어가며 0 → 1(m)

# 모델별 값. 없는 키는 위 기본값. 키 뜻:
#   u_k: 손가락 밑 마디선 = u_k × L(손목 → 손끝 길이), joints: 밑·가운데·끝 마디(밑 마디선 → 손끝 비율), ramp/blend: 섞는 폭(m)
#   fist/grip: 네 손가락 세 마디 각도, thumb_fist/thumb_grip: 엄지 (맞은편 돌리기, 밑, 가운데, 끝 마디) — 4개면 밑 마디 따로, 3개면 기본처럼 밑=가운데
#   thumb_side 'reach': 엄지 쪽 = 손 넓이 방향으로 가장 멀리 뻗은 쪽, palm 'anatomy': 손바닥 쪽 = 왼손·오른손과 엄지 쪽에서 정함
#   thumb_cut 'line': 엄지 경계 = 비스듬한 선(edge_u: 검지 가장자리를 재는 높이 비율, web_du: 선이 지나는 높이 = u_k + web_du)
#   thumb_across(°): 엄지 마디 굽힘을 손바닥 쪽에서 새끼 쪽으로 기울임, tip_mode 'finger': 손가락별 손끝, fan(1/m): 손가락 모으기 세기
_BIG_HAND = dict(thumb_side='reach', palm='anatomy', thumb_cut='line', tip_mode='finger', fan=3.0,
                 fist=(85.0, 100.0, 80.0), grip=(55.0, 60.0, 40.0),
                 thumb_fist=(75.0, 5.0, 50.0, 40.0), thumb_grip=(45.0, 5.0, 30.0, 25.0), thumb_across=80.0)
PROFILES = {
    'naengjanggo': dict(_BIG_HAND),      # 냉장고 1.84m — 큰 손, 엄지 38° 벌어짐, 새끼 짧고 바깥으로 기욺
    'scrum': dict(_BIG_HAND),            # 스크럼 1.88m — 손가락 사이가 1~2cm 벌어진 손
}


def profile_for(src):
    """원본 경로(파일 이름 앞부분)로 모델별 값 고르기 — 없으면 None(기본값)"""
    if not src:
        return None
    base = os.path.basename(str(src)).lower()
    for k, v in PROFILES.items():
        if base.startswith(k):
            return v
    return None


def _hand_verts(mesh, side):
    vg = {g.index: g.name for g in mesh.vertex_groups}
    out = []
    for v in mesh.data.vertices:
        w = 0.0
        best = None
        for g in v.groups:
            if g.weight > w:
                w, best = g.weight, vg.get(g.group)
        if best == side:
            out.append(v.index)
    return np.array(out, dtype=np.int64)


def _kmeans1d(x, k, iters=40):
    qs = np.quantile(x, np.linspace(0.08, 0.92, k))
    c = qs.copy()
    for _ in range(iters):
        lab = np.argmin(np.abs(x[:, None] - c[None, :]), axis=1)
        for j in range(k):
            if np.any(lab == j):
                c[j] = x[lab == j].mean()
    order = np.argsort(c)
    remap = np.empty(k, dtype=np.int64)
    remap[order] = np.arange(k)
    return remap[lab], c[order]


def analyze(mesh, arm, side, prof=None):
    """손 정점 → (인덱스, 손가락 번호 −1 손바닥 · 0 엄지 · 1~4 검지~새끼, 손 틀)"""
    P = prof or {}
    idx = _hand_verts(mesh, side)
    co = np.array([tuple(mesh.matrix_world @ mesh.data.vertices[i].co) for i in idx])
    hb = arm.data.bones[side]
    wrist = np.array(tuple(arm.matrix_world @ hb.head_local))
    fore = arm.data.bones[side.replace('Hand', 'ForeArm')]
    elbow = np.array(tuple(arm.matrix_world @ fore.head_local))
    # 손 길이 방향 a = 팔꿈치 → 손목 연장(손이 팔 방향으로 뻗은 A포즈), 손 정점 무게중심 쪽으로 다듬음
    a0 = wrist - elbow; a0 /= np.linalg.norm(a0)
    c = co.mean(0)
    a = (c - wrist); a /= np.linalg.norm(a)
    a = a0 * 0.5 + a * 0.5; a /= np.linalg.norm(a)
    # 손 넓이 방향 w = 손 정점을 a 에 수직인 면에 투영한 PCA 첫 축, 손바닥 법선 n = a × w
    rel = co - wrist
    perp = rel - np.outer(rel @ a, a)
    _, _, vt = np.linalg.svd(perp - perp.mean(0), full_matrices=False)
    w = vt[0] - (vt[0] @ a) * a; w /= np.linalg.norm(w)
    n = np.cross(a, w); n /= np.linalg.norm(n)
    u = rel @ a; ww = rel @ w; nn = rel @ n
    L = np.quantile(u, 0.995)
    if P.get('thumb_side') == 'reach':
        # 엄지 쪽 = 손 넓이 방향으로 가장 멀리 뻗은 쪽(벌린 엄지 끝). 손가락이 벌어진 넓은 손은 아래 손바닥 가운데 판정이 손날 쪽을 잡는다
        med = np.median(ww)
        thumb_sign = 1.0 if (np.quantile(ww, 0.995) - med) > (med - np.quantile(ww, 0.005)) else -1.0
    else:
        # 엄지: 손 넓이 한쪽 끝에서 손목 가까이 시작하는 묶음. 엄지 쪽 = 손바닥 가운데 높이(0.35L)에서 정점이 더 바깥으로 뻗은 쪽
        mid = (u > 0.2 * L) & (u < 0.5 * L)
        wl, wh = np.quantile(ww[mid], 0.02), np.quantile(ww[mid], 0.98)
        span_lo = np.median(ww[mid]) - wl
        span_hi = wh - np.median(ww[mid])
        thumb_sign = 1.0 if span_hi > span_lo else -1.0
    # 손가락 영역: 손가락 밑 마디선(u_k) 너머
    u_k = P.get('u_k', 0.52) * L
    label = np.full(len(idx), -1, dtype=np.int64)
    # 엄지 후보: 엄지 쪽 바깥 20% 이면서 0.22L ~ 0.9L. 경계는 부드럽게(thumb_w 0..1 — 이음매가 찢어지지 않게)
    wn = ww * thumb_sign
    wthr = np.quantile(wn, 0.80)
    if P.get('thumb_cut') == 'line':
        # 엄지 경계 = 비스듬한 선(손 넓이 바깥 20% 로 자르면 벌린 엄지 옆 검지 바깥 가장자리까지 엄지로 잡혀 검지가 덜 굽는다).
        # 선은 검지 바깥 가장자리(엄지 끝보다 위 손가락 영역의 엄지 쪽 99.5% + 4mm)·밑 마디선 근처(u_k + web_du)를 지나고
        # 엄지 축(확실한 엄지 정점의 u·wn 평면 PCA)과 나란하다. ts = 선에서 엄지 쪽으로 잰 거리
        up = u > P.get('edge_u', 0.66) * L
        w0 = np.quantile(wn[up], 0.995) + 0.004
        core = (wn > w0 + 0.01) & (u > 0.2 * L) & (u < 0.9 * L)
        X2 = np.c_[u[core], wn[core]]
        _, _, v2 = np.linalg.svd(X2 - X2.mean(0), full_matrices=False)
        d2 = v2[0] if v2[0][0] > 0 else -v2[0]
        u0 = u_k + P.get('web_du', -0.015)
        ts = -(u - u0) * d2[1] + (wn - w0) * d2[0]
        thumb = (ts > 0) & (u > 0.22 * L) & (u < 0.9 * L)
        info_thumb = 'w0 %.3f u0 %.3f deg %.1f' % (w0, u0, math.degrees(math.atan2(d2[1], d2[0])))
    else:
        thumb = (wn > wthr) & (u > 0.22 * L) & (u < 0.9 * L)
    label[thumb] = 0
    fing = (~thumb) & (u > u_k)
    lab4, cent = _kmeans1d(wn[fing], 4)
    # 엄지 쪽(가장 큰 wn)이 검지(1) → 새끼(4)
    lab4 = 3 - lab4
    label[np.where(fing)[0]] = lab4 + 1
    # 모든 손 정점의 '가까운 손가락'(손바닥 정점도 — 밑 마디 근처는 그 손가락과 같이 조금 굽어 이음매가 늘어나지 않게)
    cent_sorted = np.sort(cent)[::-1]          # 검지(가장 큰 wn) → 새끼
    near = np.argmin(np.abs(wn[:, None] - cent_sorted[None, :]), axis=1) + 1
    if P.get('thumb_cut') == 'line':
        tw = np.clip((ts + 0.006) / 0.012, 0.0, 1.0)
    else:
        tw = np.clip((wn - (wthr - 0.006)) / 0.012, 0.0, 1.0)
    tw = tw * tw * (3 - 2 * tw)
    tw *= np.clip((u - 0.12 * L) / (0.1 * L), 0.0, 1.0) * np.clip((0.98 * L - u) / (0.08 * L), 0.0, 1.0)
    frame = dict(wrist=wrist, a=a, w=w, n=n, L=L, u_k=u_k, thumb_sign=thumb_sign, near=near, thumb_w=tw)
    if P.get('thumb_cut') == 'line':
        frame['info'] = ['thumb_line ' + info_thumb]
    if P.get('palm') == 'anatomy':
        # 손바닥 쪽: 오른손은 (손가락 방향 a) × (손바닥 법선 p) = 엄지 쪽 t, 왼손은 p × a = t → p 를 거꾸로 풀어 n 과 부호 비교
        t = w * thumb_sign
        p = np.cross(a, t) if side.startswith('Left') else np.cross(t, a)
        frame['palm_fixed'] = 1.0 if p @ n > 0 else -1.0
    if P.get('fan'):
        frame['cent'] = cent_sorted              # 손가락 묶음 중심(엄지 쪽 부호 wn, 검지 → 새끼) — bend 의 손가락 모으기
    if P.get('tip_mode') == 'finger':
        # 손가락별 길이(모델별 값): 넓이 칸마다 손끝을 재면 비스듬한 약지 끝이 새끼 안쪽 칸에 들어가 새끼 관절이 약지 길이로 잡혔다(새끼 끝마디가
        # 안 굽어 갈고리). 물갈퀴 위(u > u_k + 2cm)를 정점 이음 + 4mm 안 이웃으로 묶은 손가락 조각마다 u·w 평면 중심선과 손끝(98% u)을 재고,
        # 중심선이 8mm 안으로 겹치는 조각(따로 모델링된 손가락 반쪽)은 합친다. bend 가 정점마다 가까운 중심선의 손끝을 부드럽게 섞어 쓴다
        cut = (u > u_k + 0.02) & (label >= 1)
        ci = np.where(cut)[0]
        loc = -np.ones(len(mesh.data.vertices), dtype=np.int64)
        loc[idx[ci]] = np.arange(len(ci))
        ev = np.empty(len(mesh.data.edges) * 2, dtype=np.int64)
        mesh.data.edges.foreach_get('vertices', ev)
        e = loc[ev.reshape(-1, 2)]
        e = e[(e[:, 0] >= 0) & (e[:, 1] >= 0)]
        cc = co[ci]
        dist = np.linalg.norm(cc[:, None, :] - cc[None, :, :], axis=2)
        e = np.r_[e, np.argwhere(np.triu(dist < 0.004, 1))]
        par = np.arange(len(ci))

        def root(x):
            while par[x] != x:
                par[x] = par[par[x]]
                x = par[x]
            return x
        for i0, i1 in e:
            r0, r1 = root(i0), root(i1)
            if r0 != r1:
                par[r0] = r1
        roots = np.array([root(i) for i in range(len(ci))])
        fl = []
        for r in np.unique(roots):
            m = ci[roots == r]
            if len(m) < 12 or np.ptp(u[m]) < 0.015:
                continue
            X2 = np.c_[ww[m], u[m]]          # bend 의 wn(부호 없는 rel·w)과 같은 좌표
            mu = X2.mean(0)
            _, _, v2 = np.linalg.svd(X2 - mu, full_matrices=False)
            dv = v2[0] if v2[0][1] > 0 else -v2[0]
            if abs(dv[1]) < 0.7:                  # 손 길이 축에서 45° 넘게 누운 조각은 손가락이 아님
                continue
            fl.append([mu, dv, float(np.quantile(u[m], 0.98)), len(m)])
        merged = True
        while merged:
            merged = False
            for i in range(len(fl)):
                for j in range(i + 1, len(fl)):
                    um_ = 0.5 * (fl[i][0][1] + fl[j][0][1])
                    wi = fl[i][0][0] + fl[i][1][0] / fl[i][1][1] * (um_ - fl[i][0][1])
                    wj = fl[j][0][0] + fl[j][1][0] / fl[j][1][1] * (um_ - fl[j][0][1])
                    if abs(wi - wj) < 0.008:
                        tot = fl[i][3] + fl[j][3]
                        mu = (fl[i][0] * fl[i][3] + fl[j][0] * fl[j][3]) / tot
                        dv = fl[i][1] * fl[i][3] + fl[j][1] * fl[j][3]; dv /= np.linalg.norm(dv)
                        fl[i] = [mu, dv, max(fl[i][2], fl[j][2]), tot]
                        del fl[j]
                        merged = True
                        break
                if merged:
                    break
        frame['fingers'] = [(float(f[0][0]), float(f[0][1]), float(f[1][0]), float(f[1][1]), f[2]) for f in fl]
        frame.setdefault('info', []).append('fingers(w, u, 기울기, 손끝) ' + str([(round(f[0], 3), round(f[1], 3), round(f[2] / f[3], 2), round(f[4], 3)) for f in frame['fingers']]))
    return idx, co, label, frame


def _rot(axis, ang):
    axis = axis / np.linalg.norm(axis)
    x, y, z = axis
    c, s = math.cos(ang), math.sin(ang)
    C = 1 - c
    return np.array([[c + x * x * C, x * y * C - z * s, x * z * C + y * s],
                     [y * x * C + z * s, c + y * y * C, y * z * C - x * s],
                     [z * x * C - y * s, z * y * C + x * s, c + z * z * C]])


def _smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _curl(P, d, base, length, axis, angs, first_blend, joints=JOINTS, blend=BLEND):
    """P 를 base 에서 d 방향으로 뻗은 손가락으로 보고 3마디를 axis 둘레로 굽힘"""
    Q = P.copy()
    jpos = [base + d * (length * j) for j in joints]
    dd = d.copy()
    for ji in range(len(joints)):
        R = _rot(axis, math.radians(angs[ji]))
        s = (Q - jpos[ji]) @ dd
        if ji == 0:
            # 밑 마디: 손가락이 시작하는 곳(s = 0)에서 0 → first_blend 안쪽에서 1 — 손바닥과 맞닿은 이음매가 늘어나지 않게
            wgt = _smooth(s / first_blend)
        else:
            wgt = _smooth((s + blend) / (2 * blend))
        rotated = (Q - jpos[ji]) @ R.T + jpos[ji]
        Q = Q + (rotated - Q) * wgt[:, None]
        for k in range(ji + 1, len(jpos)):
            jpos[k] = (jpos[k] - jpos[ji]) @ R.T + jpos[ji]
        dd = dd @ R.T
        axis = axis @ R.T
    return Q


def _finger_axis(P, wrist):
    cP = P.mean(0)
    _, _, vt = np.linalg.svd(P - cP, full_matrices=False)
    d = vt[0]
    if (cP - wrist) @ d < 0:
        d = -d
    t = (P - cP) @ d
    t0, t1 = np.quantile(t, 0.01), np.quantile(t, 0.995)
    return d, cP + d * t0, t1 - t0


def bend(co, label, frame, angles, thumb_angles, palm_sign, prof=None):
    """'벙어리장갑 굽힘': 네 손가락을 손가락마다 나누지 않고, 손 넓이 방향(w) 축의 관절선 3개로 손바닥 쪽(palm_sign × n)으로 굽힌다.
    관절 위치는 그 정점의 w 에서 손끝 길이(가운데 손가락이 길고 새끼가 짧음)에 비례 — 위치의 매끄러운 함수라 손가락 사이가 찢어지지 않는다.
    엄지는 따로(엄지 묶음 PCA 축) 굽혀 경계 가중치(thumb_w)로 섞는다."""
    P_ = prof or {}
    joints = P_.get('joints', JOINTS)
    ramp = P_.get('ramp', RAMP)
    blend = P_.get('blend', BLEND)
    out = co.copy()
    a, w, n, wrist = frame['a'], frame['w'], frame['n'], frame['wrist']
    tw = frame['thumb_w']
    L, u_k = frame['L'], frame['u_k']
    rel = co - wrist
    u = rel @ a
    wn = rel @ w
    nn = rel @ n
    fing = label >= 1
    # 손끝 길이 윤곽 tip(w): w 를 10칸으로 나눠 각 칸 98% u, 칸 사이는 선형
    lo, hi = np.quantile(wn[fing], 0.01), np.quantile(wn[fing], 0.99)
    edges = np.linspace(lo, hi, 11)
    mids, tips = [], []
    for i in range(10):
        m = fing & (wn >= edges[i]) & (wn <= edges[i + 1])
        if m.sum() >= 5:
            mids.append(0.5 * (edges[i] + edges[i + 1]))
            tips.append(np.quantile(u[m], 0.98))
    mids, tips = np.array(mids), np.array(tips)
    if len(tips) >= 3:
        tips = np.convolve(np.pad(tips, 1, mode='edge'), np.ones(3) / 3, mode='valid')
    tip = np.interp(wn, mids, tips) if len(mids) >= 2 else np.full_like(u, L)
    if P_.get('tip_mode') == 'finger' and len(frame.get('fingers', [])) >= 2:
        # 손가락별 손끝: (w, u) 평면에서 손가락 중심선까지 거리로 가중(가우스 6mm) 평균 — 손가락 안은 제 손끝, 사이는 부드럽게
        F = np.array(frame['fingers'])
        dw = wn[:, None] - (F[None, :, 0] + F[None, :, 2] / F[None, :, 3] * (u[:, None] - F[None, :, 1]))
        dist_ = np.abs(dw) * F[None, :, 3]       # 비스듬한 중심선까지 수직 거리
        wt = np.exp(-(dist_ / 0.006) ** 2) + 1e-12
        tip = (wt * F[None, :, 4]).sum(1) / wt.sum(1)
    tip = np.maximum(tip, u_k + 0.03)
    n0 = np.median(nn[fing]) if fing.any() else 0.0
    axis = np.cross(a, palm_sign * n)          # 손가락 끝이 손바닥 쪽(palm_sign·n)으로 가는 회전축(axis × a = palm_sign·n)
    axis /= np.linalg.norm(axis)
    fan = P_.get('fan', 0.0) if 'cent' in frame else 0.0
    if fan:
        # 벌어진 손가락 모으기(모델별 값): 덩치 둘은 손가락이 밑동부터 벌어져(검지~새끼 중심 12cm) 그대로 굽히면 새끼가 주먹 옆으로 갈고리처럼
        # 삐져나온다. 손가락마다 기울기 = −fan × (중심 − 가운데·약지 사이) — 밑 마디선 너머를 손가락 통째로 w 방향으로 밀어(층밀림, 폭은 그대로)
        # 손끝이 손 가운데로 모이게. 손가락 사이(묶음 중심의 가운데)에서 6mm 폭으로 부드럽게 바뀜
        ts_ = frame['thumb_sign']
        c = frame['cent']
        wc = 0.5 * (c[1] + c[2])
        sf = -fan * (c - wc)
        ws_ = wn * ts_
        slope = np.full_like(wn, sf[3])
        for j in range(3):
            slope = slope + (sf[j] - sf[j + 1]) * _smooth((ws_ - 0.5 * (c[j] + c[j + 1])) / 0.006 + 0.5)
        slope = slope * ts_
    # 정점마다: 관절 3개(그 정점 w 에서) — 앞 관절 회전이 뒤 관절도 옮긴다
    P = co.copy()
    sel = np.where(u > u_k)[0]
    for k in sel:
        q = P[k].copy()
        if fan:
            du = u[k] - u_k
            q = q + w * (slope[k] * du * _smooth(du / ramp))
        ax = axis.copy()
        dd = a.copy()
        piv = [wrist + a * (u_k + (tip[k] - u_k) * j) + n * n0 + w * wn[k] for j in joints]
        for ji in range(3):
            s_ = (q - piv[ji]) @ dd
            g = _smooth(s_ / ramp) if ji == 0 else _smooth((s_ + blend) / (2 * blend))
            if g <= 0.0:
                continue
            R = _rot(ax, math.radians(angles[ji]) * g)
            q = (q - piv[ji]) @ R.T + piv[ji]
            Rf = _rot(ax, math.radians(angles[ji]))
            for m_ in range(ji + 1, 3):
                piv[m_] = (piv[m_] - piv[ji]) @ Rf.T + piv[ji]
            dd = dd @ Rf.T
        P[k] = q
    fw = 1.0 - tw
    out = co * (1 - fw[:, None]) + P * fw[:, None]
    tsel = np.where(label == 0)[0]
    if len(tsel) >= 8:
        d, base, length = _finger_axis(co[tsel], wrist)
        sel2 = np.where(tw > 1e-3)[0]
        Q0 = co[sel2]
        # 엄지 모으기: 손 길이 축(a) 둘레로 손바닥 쪽으로 thumb_angles[0] 만큼(엄지 끝이 검지·중지 앞으로) — 엄지 밑동 기준, 엄지 묶음 안에서 부드럽게
        add = math.radians(thumb_angles[0]) * palm_sign * frame['thumb_sign']
        Ra = _rot(a, add)
        s0 = (Q0 - base) @ d
        g0 = _smooth(s0 / ramp)
        Q1 = Q0 + (((Q0 - base) @ Ra.T + base) - Q0) * g0[:, None]
        d1 = d @ Ra.T
        if 'thumb_across' in P_:
            # 엄지 마디 굽힘 방향: 손바닥 쪽(p)에서 새끼 쪽(−t)으로 thumb_across 만큼 기울임 — 손바닥 앞으로 돌린 엄지가 검지·중지를 가로질러 감김
            k_ = math.radians(P_['thumb_across'])
            f = palm_sign * n * math.cos(k_) - w * frame['thumb_sign'] * math.sin(k_)
            tax = np.cross(d1, f); tax /= np.linalg.norm(tax)
        else:
            tax = np.cross(d1, palm_sign * n); tax /= np.linalg.norm(tax)
        # 엄지 세 마디 각도: 기본 (밑·가운데 같은 값, 끝). 모델별 값은 4개(모으기, 밑, 가운데, 끝)로 밑 마디를 따로 줄 수 있다
        tangs = (thumb_angles[1], thumb_angles[2], thumb_angles[3]) if len(thumb_angles) == 4 else (thumb_angles[1], thumb_angles[1], thumb_angles[2])
        Qt = _curl(Q1, d1, base, length, tax, tangs, ramp, joints, blend)
        out[sel2] = out[sel2] * (1 - tw[sel2, None]) + Qt * tw[sel2, None]
    return out


def palm_side(co, label, frame):
    """손바닥 쪽 부호: 손가락 끝이 굽어 들어갈 면 = 엄지 끝이 놓인 쪽(엄지는 손바닥 앞으로 나와 있음)"""
    if 'palm_fixed' in frame:
        return frame['palm_fixed']
    n, wrist = frame['n'], frame['wrist']
    th = co[label == 0]
    fi = co[label >= 1]
    if len(th) == 0 or len(fi) == 0:
        return 1.0
    return 1.0 if ((th.mean(0) - wrist) @ n) > ((fi.mean(0) - wrist) @ n) else -1.0


def add_hand_keys(mesh, arm, log=print, src=None):
    """메시에 Basis + Fist_L/R + Grip_L/R 셰이프 키를 더한다(아마추어 휴지 자세 기준). src = 원본 GLB 경로(모델별 값 고르기)"""
    prof = profile_for(src)
    P = prof or {}
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    if mesh.data.shape_keys is None:
        mesh.shape_key_add(name='Basis', from_mix=False)
    inv = np.array(mesh.matrix_world.inverted())
    report = {}
    for side, tag in (('LeftHand', 'L'), ('RightHand', 'R')):
        idx, co, label, frame = analyze(mesh, arm, side, prof)
        ps = palm_side(co, label, frame)
        counts = [int((label == f).sum()) for f in range(-1, 5)]
        for name, ang, tang in (('Fist', P.get('fist', FIST), P.get('thumb_fist', THUMB_FIST)),
                                ('Grip', P.get('grip', GRIP), P.get('thumb_grip', THUMB_GRIP))):
            new = bend(co, label, frame, ang, tang, ps, prof)
            key = mesh.shape_key_add(name=f'{name}_{tag}', from_mix=False)
            data = key.data
            homo = np.c_[new, np.ones(len(new))] @ inv.T
            for k, vi in enumerate(idx):
                data[int(vi)].co = tuple(homo[k, :3])
            moved = np.linalg.norm(new - co, axis=1)
            report[f'{name}_{tag}'] = (len(idx), float(moved.max()), float(np.median(moved[label >= 0])) if np.any(label >= 0) else 0.0)
        report[f'labels_{tag}'] = counts
        report[f'palm_{tag}'] = ps
        report[f'L_{tag}'] = float(frame['L'])
        report[f'thumb_{tag}'] = float(frame['thumb_sign'])
        for i_, line in enumerate(frame.get('info', [])):
            report[f'info{i_}_{tag}'] = line
    report['profile'] = next((k for k, v in PROFILES.items() if v is prof), 'default')
    arm.data.pose_position = 'POSE'
    for k, v in report.items():
        log(f'HANDKEY {k} {v}')
    return report


def _debug(src, out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    mesh = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm][0]
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    os.makedirs(out_dir, exist_ok=True)
    prof = profile_for(src)
    P = prof or {}
    for side, tag in (('LeftHand', 'L'), ('RightHand', 'R')):
        idx, co, label, frame = analyze(mesh, arm, side, prof)
        ps = palm_side(co, label, frame)
        fist = bend(co, label, frame, P.get('fist', FIST), P.get('thumb_fist', THUMB_FIST), ps, prof)
        grip = bend(co, label, frame, P.get('grip', GRIP), P.get('thumb_grip', THUMB_GRIP), ps, prof)
        a, w, n, wr = frame['a'], frame['w'], frame['n'], frame['wrist']
        cols = [(0.5, 0.5, 0.5), (0.9, 0.3, 0.1), (0.1, 0.5, 0.9), (0.1, 0.7, 0.2), (0.8, 0.7, 0.1), (0.6, 0.2, 0.7)]
        W, H = 1200, 400
        px = np.ones((H, W, 4), dtype=np.float32)
        def plot(P, ox, ax1, ax2):
            r = P - wr
            x = r @ ax1; y = r @ ax2
            for k in range(len(P)):
                X = int(ox + 200 + x[k] * 1100); Y = int(200 + y[k] * 1100)
                if 0 <= X < W and 0 <= Y < H:
                    px[Y, X, :3] = cols[label[k] + 1]
        # 위: 손바닥 면(a·w) / 옆: a·n — 원래 · 주먹 · 잡기
        plot(co, 0, a, n); plot(fist, 400, a, n); plot(grip, 800, a, n)
        img = bpy.data.images.new(f'hand_{tag}', W, H)
        img.pixels = px[::-1].ravel()
        img.filepath_raw = os.path.join(out_dir, f'hand_{tag}_side.png')
        img.file_format = 'PNG'
        img.save()
        px[:] = 1.0
        plot(co, 0, a, w); plot(fist, 400, a, w); plot(grip, 800, a, w)
        img2 = bpy.data.images.new(f'hand2_{tag}', W, H)
        img2.pixels = px[::-1].ravel()
        img2.filepath_raw = os.path.join(out_dir, f'hand_{tag}_top.png')
        img2.file_format = 'PNG'
        img2.save()
        print('HANDDBG', tag, 'n', len(idx), 'labels', [int((label == f).sum()) for f in range(-1, 5)], 'palm', ps, 'L', round(float(frame['L']), 3), 'thumb_sign', frame['thumb_sign'])


if __name__ == '__main__' and '--' in sys.argv:
    args = sys.argv[sys.argv.index('--') + 1:]
    if len(args) >= 2:
        _debug(args[0], args[1])
