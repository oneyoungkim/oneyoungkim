# 정면 얼굴 그림을 실물 비율로 맞추는 2D 휘기(0cr) — 2026-10-30 코치 v3
#   YuNet 5점(눈 둘·코끝·입꼬리 둘)으로 지금 비율을 재고, 얼굴 상자는 그대로 둔 채 눈 사이(상자 폭 ÷ 눈 사이)·코(눈 중점→코끝 ÷ 눈 사이)·
#   입(눈 중점→입 ÷ 눈 사이)을 목표값으로 옮긴다. 얼굴 둘레(이마·턱·뺨·모서리)는 고정점, 사이는 가우스 RBF 로 부드럽게.
# 사용: python face_warp2d.py <in.png> <out.png> <boxw_ipd> <en_ipd> <em_ipd> <yunet.py 경로>
import sys, json, subprocess
import numpy as np, cv2
src, dst, t_box, t_en, t_em, yn = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5]), sys.argv[6]
f = json.loads(subprocess.run(['python', '-I', yn, src], capture_output=True, text=True, encoding='utf-8').stdout.splitlines()[0])['faces'][0]
(lx, ly), (rx, ry) = f['eyes']; nx, ny = f['nose']; mx, my = f['mouth']; bx, by, bw, bh = f['box']
ex, ey = (lx + rx) / 2, (ly + ry) / 2; ipd = rx - lx
ipd_t = bw / t_box
L = [(lx, ly), (rx, ry), (nx, ny), (mx, my)]
T = [(ex - ipd_t / 2, ey), (ex + ipd_t / 2, ey), (nx, ey + t_en * ipd_t), (mx, ey + t_em * ipd_t)]
fixed = [(bx + bw / 2, by - 0.1 * bh), (bx + bw / 2, by + 1.05 * bh), (bx - 0.05 * bw, ey), (bx + 1.05 * bw, ey),
         (bx - 0.05 * bw, by + 0.8 * bh), (bx + 1.05 * bw, by + 0.8 * bh), (bx, by), (bx + bw, by)]
ctrl = np.array([*T, *fixed], np.float64)                               # 목표(출력) 자리
disp = np.array([np.subtract(L[i], T[i]) for i in range(4)] + [(0, 0)] * len(fixed), np.float64)   # 출력 → 원본으로 가는 이동
sig = 0.22 * bw
K = np.exp(-((ctrl[:, None, :] - ctrl[None, :, :]) ** 2).sum(-1) / (2 * sig ** 2)) + 1e-6 * np.eye(len(ctrl))
W = np.linalg.solve(K, disp)
im = cv2.imdecode(np.fromfile(src, np.uint8), cv2.IMREAD_UNCHANGED)
h, w = im.shape[:2]
gx, gy = np.meshgrid(np.arange(w, dtype=np.float64), np.arange(h, dtype=np.float64))
G = np.stack([gx.ravel(), gy.ravel()], 1)
D = np.zeros_like(G)
for i in range(0, len(G), 400000):
    g = G[i:i + 400000]
    k = np.exp(-((g[:, None, :] - ctrl[None, :, :]) ** 2).sum(-1) / (2 * sig ** 2))
    D[i:i + 400000] = k @ W
mapx = (gx + D[:, 0].reshape(h, w)).astype(np.float32); mapy = (gy + D[:, 1].reshape(h, w)).astype(np.float32)
out = cv2.remap(im, mapx, mapy, cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
cv2.imencode('.png', out)[1].tofile(dst)
print('WARP2D ipd %.1f→%.1f, nose dy %.1f, mouth dy %.1f px' % (ipd, ipd_t, T[2][1] - ny, T[3][1] - my))
