const { chromium } = require('playwright'); const fs = require('fs'); const path = require('path');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage();
  await p.setContent('<body style="margin:0;background:#000"></body>');
  await p.addScriptTag({ content: fs.readFileSync(path.join(__dirname, 'lab.js'), 'utf8').replace("if (typeof module !== 'undefined') module.exports = { ATAQUES, avaliarTudo, simular, desenharQuadro, W, H };", 'window.LAB={ATAQUES,desenharQuadro,W,H};').replace("if (typeof module !== 'undefined') module.exports.parado = parado;", '') });
  const url = await p.evaluate(() => {
    const nomes = Object.keys(LAB.ATAQUES), cw = 480, ch = 260, cols = 4, linhas = Math.ceil(nomes.length / cols);
    const C = document.createElement('canvas'); C.width = cols * cw; C.height = linhas * (ch + 22); const g = C.getContext('2d');
    g.fillStyle = '#000'; g.fillRect(0, 0, C.width, C.height);
    nomes.forEach((n, i) => {
      const c = document.createElement('canvas'); c.width = cw; c.height = ch; const x = c.getContext('2d');
      const dur = LAB.ATAQUES[n](() => 0.5, () => ({ x: 1200, y: 1100 })).dur;
      LAB.desenharQuadro(x, n, 7, Math.floor(Math.min(dur * 0.4, 160)), { x: 900, y: 1100 });
      const ox = (i % cols) * cw, oy = Math.floor(i / cols) * (ch + 22);
      g.drawImage(c, ox, oy + 22); g.fillStyle = '#e8e0ff'; g.font = '13px sans-serif'; g.fillText(n, ox + 6, oy + 15);
    });
    return C.toDataURL();
  });
  fs.writeFileSync(path.join(__dirname, 'folha.png'), Buffer.from(url.split(',')[1], 'base64')); await b.close();
})();
