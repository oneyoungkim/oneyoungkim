# head_swap.py 용 텍스처 한 장(6144×4096): 왼쪽 4096² = 몸(v2 GLB 안 JPEG), 오른쪽 위 2048² = 새 머리(Tripo 머리+상반신 GLB 안 텍스처)
# 사용: python make_atlas.py <body.glb> <bust.glb> <out.jpg> [r g b 배율]
import sys, json, struct, io
from PIL import Image
def glb_img(p):
    b = open(p, 'rb').read(); off, js, binc = 12, None, None
    while off < len(b):
        ln, typ = struct.unpack_from('<II', b, off); ch = b[off + 8: off + 8 + ln]
        if typ == 0x4E4F534A: js = json.loads(ch)
        elif typ == 0x004E4942: binc = ch
        off += 8 + ln
    ti = js['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['index']
    bv = js['bufferViews'][js['images'][js['textures'][ti]['source']]['bufferView']]
    return Image.open(io.BytesIO(binc[bv.get('byteOffset', 0): bv.get('byteOffset', 0) + bv['byteLength']])).convert('RGB')
body, head = glb_img(sys.argv[1]), glb_img(sys.argv[2])
print('body', body.size, 'head', head.size)
at = Image.new('RGB', (6144, 4096), (128, 120, 112))
at.paste(body.resize((4096, 4096)) if body.size != (4096, 4096) else body, (0, 0))
hd = head.resize((2048, 2048), Image.LANCZOS)
if len(sys.argv) > 6:                                              # 머리 피부색을 몸 피부색에 맞추는 채널 배율(r g b)
    import numpy as np
    g = np.array([float(x) for x in sys.argv[4:7]]); a = np.asarray(hd, np.float32)
    opts = dict(o.split('=', 1) for o in sys.argv[7:])              # skin=r,g,b(이 색 가까운 화소에만 배율) · hair=r,g,b:R,G,B(이 색 가까운 화소를 목표색으로)
    w = np.ones(a.shape[:2], np.float32)
    if 'skin' in opts:
        sk = np.array([float(x) for x in opts['skin'].split(',')]); w = np.clip(1 - (np.linalg.norm(a - sk, axis=2) - 40) / 40, 0, 1)
    out = a * (1 + (g - 1) * w[..., None])
    if 'hair' in opts:
        src, tgt = opts['hair'].split(':'); hs = np.array([float(x) for x in src.split(',')]); ht = np.array([float(x) for x in tgt.split(',')])
        wh = np.clip(1 - (np.linalg.norm(a - hs, axis=2) - 35) / 35, 0, 1) * (a.mean(2) > 80) * (np.linalg.norm(a - (sk if 'skin' in opts else hs + 999), axis=2) > 45)
        out = out * (1 - wh[..., None]) + (a * (ht / hs)) * wh[..., None]
        print('hair texels', int((wh > 0.5).sum()))
    hd = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
at.paste(hd, (4096, 0))
at.save(sys.argv[3], quality=94)
print('ATLAS', sys.argv[3])
