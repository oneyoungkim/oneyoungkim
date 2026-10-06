"""Normalize raw image-to-3D props into concept/art/env/props/<name>.glb and render previews.

usage: python process_props.py [names...] [--render] [--force] [--jobs 4]
Reads specs/props_meta.json (dims, face_limit, tier) and specs/props_final.json (chosen model, yaw fix, fit).
Writes concept/art/env/props/props_report.json (faces, size, texture per prop).
"""
import json, subprocess, sys, pathlib, concurrent.futures as cf

HERE = pathlib.Path(__file__).parent
ROOT = HERE.parent.parent                                  # haengin1/
BLENDER = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
PROPS = ROOT / 'concept/art/env/props'
RAW = PROPS / 'raw'
meta = json.load(open(HERE / 'specs/props_meta.json', encoding='utf-8'))
final = json.load(open(HERE / 'specs/props_final.json', encoding='utf-8'))
args = [a for a in sys.argv[1:] if not a.startswith('--')]
jobs = int(sys.argv[sys.argv.index('--jobs') + 1]) if '--jobs' in sys.argv else 4
if '--jobs' in sys.argv:
    args = [a for a in args if a != str(jobs)]
names = args or list(meta)


def one(n):
    m = meta[n]; f = final.get(n, {})
    model = f.get('model', 'hunyuan')
    src = RAW / f'{n}__{model}.glb'
    dst = PROPS / f'{n}.glb'
    if not src.exists():
        return n, None, 'no raw'
    if dst.exists() and '--force' not in sys.argv:
        return n, None, 'exists'
    w, d, h = f.get('dims', m['dims'])
    tex = 2048 if m['tier'] == 'H' else 1024
    cmd = [BLENDER, '-b', '--python', str(HERE / 'normalize_prop.py'), '--', str(src), str(dst), n, model,
           str(w), str(d), str(h), str(m['face_limit']), str(f.get('yaw', 0)), str(tex), f.get('fit', 'h'), str(PROPS / 'ref' / f'{n}.png')]
    out = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    line = [l for l in out.splitlines() if l.startswith('NORM ')]
    return n, json.loads(line[0][5:]) if line else None, '' if line else out[-600:]


rep_path = PROPS / 'props_report.json'
report = json.load(open(rep_path, encoding='utf-8')) if rep_path.exists() else {}
with cf.ThreadPoolExecutor(jobs) as ex:
    for n, r, err in ex.map(one, names):
        if r:
            r['bytes'] = (PROPS / f'{n}.glb').stat().st_size
            report[n] = r
            print('OK  ', n, r['tris'], 'tris', r['w'], r['d'], r['h'], 'want', r['want'], 'tex', r['tex'], round(r['bytes'] / 1e6, 1), 'MB')
        else:
            print('SKIP', n, err)
json.dump(dict(sorted(report.items())), open(rep_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
if '--render' in sys.argv:
    glbs = [str(PROPS / f'{n}.glb') for n in names if (PROPS / f'{n}.glb').exists()]
    outdir = HERE / 'logs' / 'renders'
    subprocess.run([BLENDER, '-b', '--python', str(HERE / 'render_props.py'), '--', str(outdir)] + glbs, capture_output=True)
    print('rendered', len(glbs), '->', outdir)
