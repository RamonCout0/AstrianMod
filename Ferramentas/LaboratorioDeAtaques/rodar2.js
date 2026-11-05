const lab = require('./lab.js');
const INI = [{ x: 300, y: 1250 }, { x: 1200, y: 1250 }, { x: 2100, y: 1250 }, { x: 700, y: 800 }, { x: 1700, y: 800 }, { x: 1200, y: 400 }, { x: 400, y: 200 }, { x: 2000, y: 300 }];
for (const nome of (process.argv[2] ? process.argv.slice(2) : Object.keys(lab.ATAQUES))) {
  let n = 0, hit = 0; for (const i of INI) for (let s = 1; s <= 5; s++) { n++; if (lab.parado(nome, i, s * 31)) hit++; }
  console.log(String(Math.round(hit / n * 100)).padStart(3) + '% parado é atingido   ' + nome);
}
