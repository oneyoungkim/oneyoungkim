# face_masks.py 결과로 UTS 그림자·외곽선 지도를 만든다(face_tex.py 의 FUJIMOTO 6-3 · 7-4 값과 같음, 노이즈는 뺌)
#   <이름>Shade.jpg = 몸 텍스처 × 곱셈값: 얼굴 앞면 .93·.88·.88 / 그 밖의 피부 .80·.72·.72 / 나머지 .78·.71·.74(지금 _1st_ShadeColor)
#   <이름>OutW.png  = 1024² RGB, 실루엣 1.0 · 머리카락 .45 · 얼굴 앞면 0
# 사용: python face_shade_outw.py <tex.jpg> <masks.npz> <out_prefix(…/Yunjun_v2)>
import sys
import numpy as np
from PIL import Image
texp, mp, pre = sys.argv[1], sys.argv[2], sys.argv[3]
tex = np.asarray(Image.open(texp).convert('RGB')).astype(np.float32) / 255
H, W = tex.shape[:2]
m = np.load(mp); wf, wh, cov = m['wf'].copy(), m['wh'].copy(), m['cov'].copy()
# UV 섬 가장자리 덧칠(패딩): 덮이지 않은 텍셀을 이웃 최댓값으로 6칸 — 정점이 섬 경계에서 읽어도 값이 맞게(얼굴에 흰 금이 생기던 것)
for _ in range(6):
    pw = np.pad(wf, 1, mode='edge'); ph = np.pad(wh, 1, mode='edge'); pc = np.pad(cov, 1, mode='edge')
    nf = np.zeros_like(wf); nh = np.zeros_like(wh); nc = np.zeros_like(cov)
    for dy in (0, 1, 2):
        for dx in (0, 1, 2):
            c = pc[dy:dy + wf.shape[0], dx:dx + wf.shape[1]]
            nf = np.maximum(nf, np.where(c, pw[dy:dy + wf.shape[0], dx:dx + wf.shape[1]], 0))
            nh = np.maximum(nh, np.where(c, ph[dy:dy + wf.shape[0], dx:dx + wf.shape[1]], 0))
            nc |= c
    grow = ~cov & nc
    wf[grow] = nf[grow]; wh[grow] = nh[grow]; cov |= grow
wf4 = np.asarray(Image.fromarray((wf * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32) / 255
face_px = tex[wf4 > 0.8]
skin = np.median(face_px, 0) if len(face_px) else np.array([0.72, 0.57, 0.48])
dist = np.linalg.norm(tex - skin, axis=2)
is_skin = np.clip((0.20 - dist) / 0.06, 0, 1)[..., None]
base = np.array([0.78, 0.71, 0.74]) * (1 - is_skin) + np.array([0.80, 0.72, 0.72]) * is_skin
mul = base * (1 - wf4[..., None]) + np.array([0.93, 0.88, 0.88]) * wf4[..., None]
if len(sys.argv) > 4:                                              # 밝은 머리카락(금발)은 그늘에서도 덜 어둡게: 머리카락 곱셈값
    hm = float(sys.argv[4]); wh4 = np.asarray(Image.fromarray((wh * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32)[..., None] / 255
    mul = mul * (1 - wh4) + hm * wh4
Image.fromarray((np.clip(tex * mul, 0, 1) * 255).astype(np.uint8)).save(pre + 'Shade.jpg', quality=92)
ow = np.ones_like(wf)
ow = ow * (1 - wh) + 0.45 * wh
ow = ow * (1 - wf)
Image.fromarray((np.clip(ow, 0, 1) * 255).astype(np.uint8)).convert('RGB').save(pre + 'OutW.png')
print('SHADE skin', np.round(skin * 255).astype(int).tolist(), 'face', int((wf4 > 0.5).sum()), 'OutW face0', int((wf > 0.5).sum()), 'hair', int((wh > 0.5).sum()))
