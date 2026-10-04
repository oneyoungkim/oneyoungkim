// Inlines src/*.js into src/page.html -> style_drafts_3d.html (single-file artifact)
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
const dir = new URL('./src/', import.meta.url);
const parts = readdirSync(dir).filter(f => /^\d\d_.*\.js$/.test(f)).sort();
const js = ["import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js';"]
  .concat(parts.map(f => `// ===== ${f} =====\n` + readFileSync(new URL(f, dir), 'utf8'))).join('\n');
const page = readFileSync(new URL('page.html', dir), 'utf8').replace('/*__JS__*/', () => js);
writeFileSync(new URL('./style_drafts_3d.html', import.meta.url), page);
console.log('built', parts.length, 'parts,', page.length, 'chars');
