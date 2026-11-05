const lab = require('./lab.js');
const t0 = Date.now();
const r = lab.avaliarTudo(process.argv[2] ? process.argv.slice(2) : undefined);
for (const x of r) console.log(`${x.veredito.padEnd(14)} sobrevive ${String(x.sobrevive).padStart(3)}%  livre mín ${String(x.menorLivre).padStart(3)}%  média ${String(x.mediaLivre).padStart(3)}%   ${x.nome}`);
console.log('tempo', (Date.now() - t0) / 1000, 's');
