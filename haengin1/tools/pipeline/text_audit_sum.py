import sys, os, re, collections
sys.stdout.reconfigure(encoding='utf-8')
d = sys.argv[1]
mode = sys.argv[2] if len(sys.argv) > 2 else 'bad'
def rows(n):
    p = os.path.join(d, n)
    if not os.path.exists(p): return []
    return [l.rstrip('\n').split('\t') for l in open(p, encoding='utf-8', errors='replace')]
if mode == 'bad':
    c = collections.Counter(); ex = {}; sec = collections.defaultdict(set)
    for r in rows('text_bad.tsv'):
        if len(r) < 5: continue
        s, f, kind, path, txt = r[0], r[1], r[2], r[3], r[4]
        k0 = kind.split(' ')[0]
        root = '/'.join(path.split('/')[-3:])
        key = (k0, root)
        c[key] += 1
        v = re.findall(r'[\d.]+', kind)
        val = float(v[0]) if v else 0
        if key not in ex or (v and val < ex[key][0]): ex[key] = (val, txt[:34], f[:40])
        sec[key].add(s.split(' ')[0])
    for (k, root), n in sorted(c.items(), key=lambda x: (x[0][0], -x[1])):
        e = ex[(k, root)]
        print(f"{k}\t{n}\t{root[:60]}\t{e[0]}\t{e[1]}\t{','.join(sorted(sec[(k, root)]))[:40]}")
    print('합계', sum(c.values()), '곳', len(c))
elif mode == 'world':
    c = collections.Counter(); ex = {}
    for r in rows('text_world.tsv'):
        if len(r) < 6: continue
        key = ('/'.join(r[2].split('/')[-2:]), r[3][:24], r[4], r[5])
        c[key] += 1; ex[key] = r[0].split(' ')[0]
    for k, n in sorted(c.items(), key=lambda x: (x[0][2], -x[1])):
        print(f"{k[2]}\t{k[3]}\t{n}\t{k[0][:40]}\t{k[1]}\t{ex[k]}")
    print('합계', len(c))
elif mode == 'inv':
    c = collections.defaultdict(lambda: [0, 9, 99, set(), ''])
    for r in rows('text_inv.tsv'):
        if len(r) < 9: continue
        key = '/'.join(r[2].split('/')[-3:])
        e = c[key]; e[0] += 1; e[1] = min(e[1], float(r[5])); e[2] = min(e[2], float(r[7])); e[3].add(r[0].split(' ')[0]); e[4] = r[3][:24]
    for k, e in sorted(c.items()):
        print(f"{k[:58]}\t{e[0]}\t판 {e[1]:.2f}\t대비 {e[2]:.1f}\t{','.join(sorted(e[3]))[:30]}\t{e[4]}")
    print('합계', len(c))
