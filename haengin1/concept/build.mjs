// Inlines src/*.js into src/page.html -> style_drafts_3d.html (single-file artifact)
// Test mode: node build.mjs --parts 01,02,03,07 --extra path/test.js --out path/test.html [--bare]
//   --parts : numeric prefixes of src files to include (default: all)
//   --extra : extra script appended after the parts (a unit test / scene)
//   --bare  : minimal full-window page instead of the real page shell
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
const argv = process.argv.slice(2), opt = k => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : null; };
const dir = new URL('./src/', import.meta.url);
let parts = readdirSync(dir).filter(f => /^\d\d_.*\.js$/.test(f)).sort();
if (opt('--parts')) { const want = opt('--parts').split(','); parts = parts.filter(f => want.includes(f.slice(0, 2))); }
const extra = opt('--extra');
const js = ["import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js';"]
  .concat(parts.map(f => `// ===== ${f} =====\n` + readFileSync(new URL(f, dir), 'utf8')))
  .concat(extra ? [`// ===== extra: ${extra} =====\n` + readFileSync(extra, 'utf8')] : []).join('\n');
const shell = argv.includes('--bare')
  ? '<title>test</title><style>html,body{margin:0;height:100%;background:#fff}canvas{display:block;width:100vw;height:100vh}</style><canvas id="gl"></canvas>\n<script type="module">\n/*__JS__*/\n</script>\n'
  : readFileSync(new URL('page.html', dir), 'utf8');
const out = opt('--out') || new URL('./style_drafts_3d.html', import.meta.url);
writeFileSync(out, shell.replace('/*__JS__*/', () => js));
console.log('built', parts.length, 'parts' + (extra ? ' + extra' : '') + ' ->', String(out));
