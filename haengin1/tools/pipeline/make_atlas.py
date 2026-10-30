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
    g = np.array([float(x) for x in sys.argv[4:7]]); hd = Image.fromarray(np.clip(np.asarray(hd, np.float32) * g, 0, 255).astype(np.uint8))
at.paste(hd, (4096, 0))
at.save(sys.argv[3], quality=94)
print('ATLAS', sys.argv[3])
