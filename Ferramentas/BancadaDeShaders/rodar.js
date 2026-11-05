// uso: node rodar.js arquivo.fx modo saida.png [json de parametros] [tempo]
const { chromium } = require('playwright'); const fs = require('fs'); const path = require('path');
(async () => {
  const [fx, modo, saida, params, t] = process.argv.slice(2);
  const b = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const p = await b.newPage({ viewport: { width: 1400, height: 900 } });
  p.on('pageerror', e => console.log('ERRO JS:', e.message));
  await p.goto('file://' + path.resolve(__dirname, 'teste.html'));
  await p.waitForFunction(() => window.bancadaPronta, null, { timeout: 30000 });
  const fonte = fs.readFileSync(fx, 'utf8');
  const r = await p.evaluate(([f, m]) => window.bancada.carregar(f, m), [fonte, modo || 'sprite']);
  console.log(JSON.stringify(r));
  if (!r.ok) { const g = await p.evaluate(() => document.getElementById('glsl').textContent); console.log(g); }
  const extras = params ? JSON.parse(params) : {};
  await p.evaluate(e => { for (const [k, v] of Object.entries(e)) { if (k.startsWith('tex')) window.bancada.textura(parseInt(k.slice(3)), v); else if (k === 'fundo') window.bancada.fundo(v); else if (k === 'area') window.bancada.area(v); else window.bancada.definir(k, v); } }, extras);
  await p.evaluate(tt => window.bancada.tempo(tt), parseFloat(t || '3'));
  const url = await p.evaluate(() => window.bancada.foto());
  fs.writeFileSync(saida, Buffer.from(url.split(',')[1], 'base64'));
  await b.close();
})();
