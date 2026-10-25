# 룩데브 합격 검사(docs/14 4-10) — Unity 사진 한 장을 스타일 프레임과 재서 기준 통과 여부를 찍는다(크레딧 0, Unity 불필요)
# 사용: python tools/lookdev/measure.py <Unity png> <프레임 png> [--indoor] [--json 출력.json]
#   Unity 사진 = EnvSetup.Shots / ZBikeShots 같은 배치 사진(같은 카메라). 프레임은 Unity 해상도로 가운데 잘라 맞춤.
# 기준(14 문서 4-10): 팔레트 ΔE ≤ 5.0 · 그늘 ΔE ≤ 6.0 · 먹선 배율 0.8~1.25 · 결 배율 0.7~1.4 · 먼 대비 |Unity − 프레임| ≤ 0.15
import sys, os, json
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ld_core as ld
from PIL import Image

u, f = sys.argv[1], sys.argv[2]
indoor = '--indoor' in sys.argv
with Image.open(u) as im:
    size = im.size
mu = ld.metrics(ld.load(u, size), indoor)
mf = ld.metrics(ld.load(f, size), indoor)
c = ld.compare(mu, mf)
checks = [
    ('팔레트 ΔE', c['palette_de'], c['palette_de'] <= 5.0, '≤ 5.0'),
    ('그늘 ΔE', c['shadow_de'], c['shadow_de'] <= 6.0, '≤ 6.0'),
    ('먹선 배율', c['line_ratio'], 0.8 <= c['line_ratio'] <= 1.25, '0.8~1.25'),
    ('결 배율', c['grain_ratio'], 0.7 <= c['grain_ratio'] <= 1.4, '0.7~1.4'),
]
if 'far_contrast_u' in c:
    dfc = abs(c['far_contrast_u'] - c['far_contrast_f'])
    checks.append(('먼 대비 차', dfc, dfc <= 0.15, '≤ 0.15'))
ok = all(x[2] for x in checks)
for nm, v, p, crit in checks:
    print(f'{"통과" if p else "미달"}  {nm:8s} {v:6.2f}  (기준 {crit})')
print('합격' if ok else '불합격')
if '--json' in sys.argv:
    with open(sys.argv[sys.argv.index('--json') + 1], 'w', encoding='utf-8') as fo:
        json.dump({'cmp': c, 'pass': ok, 'unity': {k: v for k, v in mu.items() if k != '_pal'}, 'frame': {k: v for k, v in mf.items() if k != '_pal'}}, fo, ensure_ascii=False, indent=1, default=float)
