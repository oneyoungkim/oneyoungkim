# GLB 안의 baseColor 이미지(첫 재질)만 다른 JPEG 로 바꾼다 — 메시·뼈·가중치·동작 바이트는 그대로(Blender 를 거치지 않음)
# 사용: python glb_swap_tex.py <in.glb> <new.jpg> <out.glb>
import json, struct, sys
src, newimg, dst = sys.argv[1], sys.argv[2], sys.argv[3]
b = open(src, 'rb').read()
assert b[:4] == b'glTF'
off, js, binc = 12, None, None
while off < len(b):
    ln, typ = struct.unpack_from('<II', b, off)
    chunk = b[off + 8: off + 8 + ln]
    if typ == 0x4E4F534A: js = json.loads(chunk)
    elif typ == 0x004E4942: binc = chunk
    off += 8 + ln
ti = js['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['index']
ii = js['textures'][ti]['source']
target = js['images'][ii]['bufferView']
data = open(newimg, 'rb').read()
assert data[:3] == b'\xff\xd8\xff', 'JPEG 만'
js['images'][ii]['mimeType'] = 'image/jpeg'
# 버퍼 뷰를 순서대로 다시 이어 붙인다(4바이트 정렬)
views = js['bufferViews']
order = sorted(range(len(views)), key=lambda i: views[i].get('byteOffset', 0))
out = bytearray()
for i in order:
    v = views[i]
    chunk = data if i == target else binc[v.get('byteOffset', 0): v.get('byteOffset', 0) + v['byteLength']]
    while len(out) % 4: out += b'\0'
    v['byteOffset'] = len(out); v['byteLength'] = len(chunk)
    out += chunk
while len(out) % 4: out += b'\0'
js['buffers'][0]['byteLength'] = len(out)
jb = json.dumps(js, separators=(',', ':')).encode()
while len(jb) % 4: jb += b' '
total = 12 + 8 + len(jb) + 8 + len(out)
with open(dst, 'wb') as f:
    f.write(struct.pack('<III', 0x46546C67, 2, total))
    f.write(struct.pack('<II', len(jb), 0x4E4F534A)); f.write(jb)
    f.write(struct.pack('<II', len(out), 0x004E4942)); f.write(out)
print('SWAPPED image', ii, 'bufferView', target, len(data), 'bytes ->', dst)
