// Headless screenshot harness (WebGL via SwiftShader). Usage:
//   PAGE=path/to/page.html OUT=dir STEPS='[...]' NODE_PATH=/opt/node22/lib/node_modules node tools/shot.cjs
// Env:
//   THREE  path to three.module.min.js (r170) served for cdn.jsdelivr.net (default: scratchpad copy)
//   READY  JS expression that becomes true when the scene is ready (default: window.__ready || window.__hy?.app?.S)
//   VW,VH  viewport (default 1200x900)   SCHEME light|dark
// STEPS: array of {wait:ms} {eval:"js"} {until:"js expr"} {style:"A"} {act:["siwoo","onetwo"]} {shot:"file.png", sel:"#stage"|"canvas", full:true}
// Console errors and page errors are written to OUT/logs.txt (always check it).
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const PAGE = process.env.PAGE, OUT = process.env.OUT || '.';
const THREE_PATH = process.env.THREE || '/tmp/claude-0/-home-user-oneyoungkim/77dfc82b-fac7-5f26-9c5b-0b5f07a51bad/scratchpad/three170/package/build/three.module.min.js';
const READY = process.env.READY || '(window.__ready || (window.__hy && window.__hy.app && window.__hy.app.S))';
const three = fs.readFileSync(THREE_PATH);
fs.mkdirSync(OUT, { recursive: true });
(async () => {
  const b = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const p = await b.newPage({ viewport: { width: +(process.env.VW || 1200), height: +(process.env.VH || 900) }, colorScheme: process.env.SCHEME || 'light' });
  const logs = [];
  p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') { const t = m.text(); if (!/ERR_CERT|fonts\.g/.test(t)) logs.push(m.type() + ': ' + t); } });
  p.on('pageerror', e => logs.push('PAGEERROR: ' + e.message));
  await p.route('https://cdn.jsdelivr.net/**', r => r.fulfill({ body: three, contentType: 'application/javascript' }));
  const html = '<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head><body>' + fs.readFileSync(PAGE, 'utf8') + '</body></html>';
  await p.route('http://local.test/', r => r.fulfill({ body: html, contentType: 'text/html; charset=utf-8' }));
  await p.goto('http://local.test/');
  await p.waitForFunction(READY, null, { timeout: 90000 }).catch(e => logs.push('READY TIMEOUT: ' + READY));
  const steps = JSON.parse(process.env.STEPS || '[{"wait":2500,"shot":"shot.png","full":true}]');
  for (const s of steps) {
    if (s.style) await p.evaluate(k => window.__hy.setStyle(k), s.style);
    if (s.act) await p.evaluate(([w, m]) => window.__hy.director.request(w, m), s.act);
    if (s.eval) await p.evaluate(s.eval).catch(e => logs.push('EVAL ERROR: ' + e.message));
    if (s.until) await p.waitForFunction(s.until, null, { timeout: 60000 }).catch(() => logs.push('UNTIL TIMEOUT: ' + s.until));
    if (s.wait) await p.waitForTimeout(s.wait);
    if (s.shot) {
      if (s.full || !s.sel) await p.screenshot({ path: path.join(OUT, s.shot), fullPage: !!s.full });
      else { const el = await p.$(s.sel); await (el || p).screenshot({ path: path.join(OUT, s.shot) }); }
    }
  }
  fs.writeFileSync(path.join(OUT, 'logs.txt'), logs.join('\n'));
  console.log('done; log lines:', logs.length);
  await b.close();
})();
