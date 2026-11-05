// LABORATÓRIO DE ATAQUES — reproduz a geometria e o tempo de cada ataque (números tirados do C#),
// simula um jogador voando (7 px/tick) e mede se sempre existe um lugar seguro alcançável.
// Arena: 2400 × 1300 px (y para baixo, chão em y = 1300).
'use strict';
const W = 2400, H = 1300, CEL = 20, GX = W / CEL, GY = H / CEL, PASSO = 3, VEL = 7;
const PI = Math.PI, TAU = PI * 2;
const lerp = (a, b, t) => a + (b - a) * t, clamp = (x, a, b) => Math.max(a, Math.min(b, x));
const getLerp = (a, b, x) => clamp((x - a) / (b - a), 0, 1);
const wrap = a => { while (a > PI) a -= TAU; while (a < -PI) a += TAU; return a; };
function rng(seed) { let s = seed >>> 0 || 1; return () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296; }
// distância de ponto a segmento
function distSeg(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay, l = dx * dx + dy * dy || 1;
  const t = clamp(((px - ax) * dx + (py - ay) * dy) / l, 0, 1);
  return Math.hypot(px - ax - dx * t, py - ay - dy * t);
}
const PX = 10, PY = 21; // meia caixa do jogador (20 × 42)

// ─── Primitivas de perigo ─── cada uma: {ativo(t), bate(x,y,t), desenhar(ctx,t,escala)}
function Ret(cx, cy, w, h, t0, aviso, cor = '#ff5050', ativo = 24) {
  return { cor, ini: t0, fim: t0 + aviso + ativo,
    bate: (x, y, t) => t >= t0 + aviso && t < t0 + aviso + ativo && Math.abs(x - cx) < w / 2 + PX && Math.abs(y - cy) < h / 2 + PY,
    aviso: t => t >= t0 && t < t0 + aviso, forma: () => ({ tipo: 'ret', cx, cy, w, h }) };
}
function Feixe(cx, cy, a0, giro, t0, cor = '#a070ff') {
  const AV = 60, GI = 150, L = 1700;
  const ang = t => { const k = getLerp(t0 + AV, t0 + AV + GI, t); return a0 + giro * k * k * (3 - 2 * k); };
  return { cor, ini: t0, fim: t0 + AV + GI,
    bate: (x, y, t) => { if (t < t0 + AV || t >= t0 + AV + GI) return false; const a = ang(t);
      return distSeg(x, y, cx, cy, cx + Math.cos(a) * L, cy + Math.sin(a) * L) < 17 + PX; },
    aviso: t => t >= t0 && t < t0 + AV, forma: t => ({ tipo: 'linha', ax: cx, ay: cy, bx: cx + Math.cos(ang(t)) * L, by: cy + Math.sin(ang(t)) * L, larg: 34 }) };
}
// projéteis: integrados por tick num "mundo"; mira = função que dá o alvo atual
function Proj(x, y, vx, vy, t0, opc) {
  const o = Object.assign({ raio: 9, acel: 1.015, max: 14, persegue: 0, tempoPers: 60, atraso: 0, vida: 300, cor: '#80d0ff' }, opc);
  return { cor: o.cor, ini: t0, fim: t0 + o.vida, proj: true, x, y, vx, vy, o, vivo: true, idade: 0,
    passo(t, alvo) {
      if (t < t0 || !this.vivo) return;
      this.idade++;
      const o = this.o, sp = Math.hypot(this.vx, this.vy);
      if (o.tipo === 'pena') { const c = Math.cos(o.curva), sn = Math.sin(o.curva), vx = this.vx * c - this.vy * sn; this.vy = this.vx * sn + this.vy * c; this.vx = vx; if (Math.hypot(this.vx, this.vy) < 12) { this.vx *= 1.01; this.vy *= 1.01; } }
      else if (o.tipo === 'lamina') {
        if (this.idade < o.atraso) { this.vx *= 0.93; this.vy *= 0.93; }
        else if (this.idade < o.atraso + 75) { const d = norm(alvo.x - this.x, alvo.y - this.y); this.vx = lerp(this.vx, d[0] * 13, 0.06); this.vy = lerp(this.vy, d[1] * 13, 0.06); }
        else if (sp < 20) { this.vx *= 1.03; this.vy *= 1.03; }
      } else {
        if (o.persegue > 0 && this.idade < o.tempoPers) { const d = norm(alvo.x - this.x, alvo.y - this.y); this.vx = lerp(this.vx, d[0] * sp, 0.04 * o.persegue); this.vy = lerp(this.vy, d[1] * sp, 0.04 * o.persegue); }
        if (Math.hypot(this.vx, this.vy) < o.max) { this.vx *= o.acel; this.vy *= o.acel; }
      }
      this.x += this.vx; this.y += this.vy;
      if (this.idade > o.vida || this.x < -400 || this.x > W + 400 || this.y < -400 || this.y > H + 400) this.vivo = false;
    },
    bate(x, y, t) { return this.vivo && t >= t0 && Math.abs(x - this.x) < this.o.raio + PX && Math.abs(y - this.y) < this.o.raio + PY; },
    aviso: () => false, forma() { return { tipo: 'bola', x: this.x, y: this.y, r: this.o.raio }; } };
}
function norm(x, y) { const l = Math.hypot(x, y) || 1; return [x / l, y / l]; }
function Queda(x, t0, atraso, cor = '#a070ff') { // QuedaEstelar
  const tc = t0 + atraso;
  const yEm = t => { if (t < tc) return 10; let y = 10, v = 8; for (let k = tc; k < t; k++) { v = Math.min(24, v + 0.9); y += v; } return y; };
  return { cor, ini: t0, fim: tc + 70, bate: (px, py, t) => t >= tc && t < tc + 70 && Math.abs(px - x) < 11 + PX && Math.abs(py - yEm(t)) < 11 + PY,
    aviso: t => t >= t0 && t < tc, forma: t => t < tc ? { tipo: 'ret', cx: x, cy: H / 2, w: 20, h: H } : { tipo: 'bola', x, y: yEm(t), r: 11 } };
}
function Linha(a, b, t0, atraso) { // risco que explode
  return { cor: '#ffd080', ini: t0, fim: t0 + atraso + 26, bate: (x, y, t) => t >= t0 + atraso && t < t0 + atraso + 26 && distSeg(x, y, a.x, a.y, b.x, b.y) < 30 + PX,
    aviso: t => t < t0 + atraso, forma: () => ({ tipo: 'linha', ax: a.x, ay: a.y, bx: b.x, by: b.y, larg: 60 }) };
}
function Espinho(x, t0, atraso, alt) {
  const tc = t0 + atraso;
  const h = t => t < tc ? 0 : alt * getLerp(0, 6, t - tc) * getLerp(0, 10, tc + 40 - t);
  return { cor: '#90dcff', ini: t0, fim: tc + 40, bate: (px, py, t) => t >= tc && t < tc + 32 && Math.abs(px - x) < 14 + PX && py + PY > H - h(t),
    aviso: t => t >= t0 && t < tc, forma: t => ({ tipo: 'ret', cx: x, cy: H - (t < tc ? alt : h(t)) / 2, w: 28, h: t < tc ? alt : h(t) }) };
}
function Anel(cx, cy, brecha, rot, t0, rmax = 1500) {
  const R = t => lerp(40, rmax, getLerp(t0 + 20, t0 + 160, t)), B = t => brecha + rot * (t - t0);
  return { cor: '#90dcff', ini: t0, fim: t0 + 160, bate: (x, y, t) => { if (t < t0 + 20 || t >= t0 + 160) return false;
      const d = Math.hypot(x - cx, y - cy); if (Math.abs(d - R(t)) > 38 + PX) return false; return Math.abs(wrap(Math.atan2(y - cy, x - cx) - B(t))) > 0.55; },
    aviso: t => t >= t0 && t < t0 + 20, forma: t => ({ tipo: 'anel', cx, cy, r: R(t), brecha: B(t), meia: 0.55 }) };
}
function Prisao(x, y, t0) {
  return { cor: '#90dcff', ini: t0, fim: t0 + 60, bate: (px, py, t) => t >= t0 + 50 && t < t0 + 53 && Math.hypot(px - x, py - y) < 70,
    aviso: t => t >= t0 && t < t0 + 50, forma: () => ({ tipo: 'circulo', x, y, r: 70 }) };
}
function Cone(cx, cy, a0, giro, t0) {
  const AV = 50, DU = 110, L = 1000, AB = 0.36, ang = t => a0 + giro * getLerp(t0 + AV, t0 + AV + DU, t);
  return { cor: '#90dcff', ini: t0, fim: t0 + AV + DU, bate: (x, y, t) => { if (t < t0 + AV || t >= t0 + AV + DU) return false;
      const d = Math.hypot(x - cx, y - cy); return d < L && Math.abs(wrap(Math.atan2(y - cy, x - cx) - ang(t))) < AB + 14 / Math.max(d, 1); },
    aviso: t => t >= t0 && t < t0 + AV, forma: t => ({ tipo: 'cone', cx, cy, a: ang(t), ab: AB, L }) };
}
function Caixa(f, ini, fim, w, h, cor = '#ffb060') { // chefe (corpo) se movendo: f(t) → {x,y}
  return { cor, ini, fim, bate: (x, y, t) => { if (t < ini || t >= fim) return false; const p = f(t); return Math.abs(x - p.x) < w / 2 + PX && Math.abs(y - p.y) < h / 2 + PY; },
    aviso: () => false, forma: t => { const p = f(t); return { tipo: 'ret', cx: p.x, cy: p.y, w, h }; } };
}
function LancaCeu(x, t0) {
  const tq = t0 + 75, yEm = t => -60 + (t - tq) * 30;
  return { cor: '#90dcff', ini: t0 + 30, fim: tq + 50, bate: (px, py, t) => t >= tq && yEm(t) < H + 20 && Math.abs(px - x) < 11 + PX && py + PY > yEm(t) - 110 && py - PY < yEm(t) + 20,
    aviso: t => t >= t0 + 30 && t < tq, forma: t => t < tq ? { tipo: 'ret', cx: x, cy: H / 2, w: 28, h: H } : { tipo: 'ret', cx: x, cy: yEm(t) - 45, w: 12, h: 130 } };
}
function Dragao(t0, dur, alvo) { // persegue + bote (corpo de 14 segmentos)
  const s = { x: -200, y: 200, vx: 0, vy: 0, ciclo: 0, corpo: Array.from({ length: 14 }, (_, i) => [-200, 200 + i * 38]) };
  return { cor: '#c080ff', ini: t0, fim: t0 + dur, proj: true, vivo: true, s,
    passo(t, a) {
      if (t < t0 || t >= t0 + dur) return;
      s.ciclo = (s.ciclo + 1) % 300; const d = norm(a.x - s.x, a.y - s.y);
      if (s.ciclo < 180) { s.vx = lerp(s.vx, d[0] * 8, 0.03); s.vy = lerp(s.vy, d[1] * 8, 0.03); }
      else if (s.ciclo < 240) { s.vx = lerp(s.vx, d[0] * 3, 0.15); s.vy = lerp(s.vy, d[1] * 3, 0.15); }
      else if (s.ciclo === 240) { s.vx = d[0] * 23; s.vy = d[1] * 23; }
      else if (s.ciclo > 280) { s.vx *= 0.94; s.vy *= 0.94; }
      s.x += s.vx; s.y += s.vy;
      let ax = s.x, ay = s.y;
      for (const c of s.corpo) { const dx = c[0] - ax, dy = c[1] - ay, l = Math.hypot(dx, dy); if (l > 38) { c[0] = ax + dx / l * 38; c[1] = ay + dy / l * 38; } ax = c[0]; ay = c[1]; }
    },
    bate(x, y, t) { if (t < t0) return false; if (Math.abs(x - s.x) < 27 + PX && Math.abs(y - s.y) < 27 + PY) return true;
      return s.corpo.some(c => Math.abs(x - c[0]) < 20 + PX && Math.abs(y - c[1]) < 20 + PY); },
    aviso: () => false, forma: () => ({ tipo: 'dragao', x: s.x, y: s.y, corpo: s.corpo }) };
}

// ─── OS ATAQUES (números do C#) ─── cada um: (r, alvo) → { dur, perigos[], chefe(t)? }
// alvo(t) = onde está "o jogador" naquele instante (centro da área alcançável)
const ATAQUES = {
  // ═══ ASTHAROTH ═══
  'Astharoth · Corte em cruz': (r, alvo) => { const P = []; for (let k = 0; k < 3; k++) { const t0 = 1 + k * 65, a = alvo(t0);
      P.push(Ret(a.x, H / 2, 90, H, t0, 44), Ret(W / 2, a.y, W, 80, t0, 44)); } return { dur: 220, perigos: P }; },
  'Astharoth · Duelo de cortes': (r, alvo) => { const P = []; for (let k = 0; k < 4; k++) { const t0 = 1 + k * 50, a = alvo(t0);
      P.push(Ret(a.x, H / 2, 90, H, t0, 36), Ret(W / 2, a.y, W, 80, t0, 36)); } return { dur: 230, perigos: P }; },
  'Astharoth · Rajadas': (r, alvo) => { const P = [], b = { x: W / 2, y: H - 64 };
      for (const t0 of [20, 45, 70]) { const a = alvo(t0), d = norm(a.x - b.x, a.y - b.y), ang = Math.atan2(d[1], d[0]);
        for (let k = -2; k <= 2; k++) P.push(Proj(b.x, b.y, Math.cos(ang + k * 0.22) * 9, Math.sin(ang + k * 0.22) * 9, t0, { persegue: 1, cor: '#a070ff' })); }
      return { dur: 200, perigos: P }; },
  'Astharoth · Lâminas guiadas': (r, alvo) => { const P = [], b = { x: W / 2, y: H - 220 };
      for (const t0 of [20, 60, 100, 140]) for (let k = -1; k <= 1; k++) { const ang = -PI / 2 + k * 0.6;
        P.push(Proj(b.x, b.y, Math.cos(ang) * 11, Math.sin(ang) * 11, t0, { tipo: 'lamina', atraso: 30 + Math.abs(k) * 8, raio: 10, vida: 240, cor: '#c080ff' })); }
      return { dur: 330, perigos: P }; },
  'Astharoth · Chuva estelar': (r) => { const P = []; for (const t0 of [20, 80, 140, 200]) { const desl = r() * 380;
      for (let x = 20; x < W; x += 40) { const np = ((x + desl) % 380 + 380) % 380; if (Math.abs(np - 190) < 65) continue; P.push(Queda(x, t0, 46 + Math.floor(x / 120))); } }
      return { dur: 360, perigos: P }; },
  'Astharoth · Investida do duelo': (r, alvo) => { const lado = alvo(1).x > W / 2 ? -1 : 1, x0 = lado === 1 ? 70 : W - 70, x1 = lado === 1 ? W - 70 : 70;
      return { dur: 170, perigos: [Caixa(t => ({ x: clamp(x0 + lado * 26 * (t - 60), Math.min(x0, x1), Math.max(x0, x1)), y: H - 64 }), 60, 60 + Math.abs(x1 - x0) / 26, 77, 132)] }; },
  'Astharoth · Espiral': () => { const P = [], b = { x: W / 2, y: 170 };
      for (let t = 30; t <= 150; t += 20) { const giro = (Math.floor((t + 10) / 20) % 2 === 0 ? 1 : -1) * (t + 10) * 0.045; for (let k = 0; k < 17; k++) { const a = giro + k * TAU / 17; P.push(Proj(b.x, b.y, Math.cos(a) * 3.8, Math.sin(a) * 3.8, t + 10, { cor: '#a070ff' })); } }
      return { dur: 420, perigos: P }; },
  'Astharoth · Feixe giratório': (r, alvo) => { const c = { x: W / 2, y: H / 2 }, a = alvo(45), s = r() < 0.5 ? 1 : -1, ang = Math.atan2(a.y - c.y, a.x - c.x) - s * 0.5;
      return { dur: 280, perigos: [Feixe(c.x, c.y, ang, s * 2.4, 45), Feixe(c.x, c.y, ang + PI, s * 2.4, 45)] }; },
  'Arcanjo · Feixe triplo': (r, alvo) => { const c = { x: W / 2, y: H / 2 }, a = alvo(45), s = r() < 0.5 ? 1 : -1, ang = Math.atan2(a.y - c.y, a.x - c.x) - s * 0.5;
      return { dur: 280, perigos: [0, 1, 2].map(k => Feixe(c.x, c.y, ang + k * TAU / 3, s * 2.4, 45, '#ffd080')) }; },
  'Arcanjo · Julgamento solar': (r, alvo) => { const P = []; let prev = alvo(0);
      for (let k = 0; k < 10; k++) { const t0 = 30 + k * 18, a = alvo(t0), vx = (a.x - prev.x) / 18; prev = a;
        P.push(Ret(a.x, H / 2, 110, H, t0, 34, '#ffd080', 22)); const pv = a.x + vx * 32; if (Math.abs(pv - a.x) > 140) P.push(Ret(clamp(pv, 60, W - 60), H / 2, 110, H, t0, 34, '#ffd080', 22)); }
      return { dur: 300, perigos: P }; },
  'Arcanjo · Asas de luz': () => { const P = [], b = { x: W / 2, y: 220 };
      for (let t = 40; t <= 160; t += 30) { const curva = (Math.floor((t - 40) / 30) % 2 === 0 ? 1 : -1) * 0.009;
        for (let k = 0; k < 16; k++) { const a = lerp(0.15, PI - 0.15, k / 15); P.push(Proj(b.x, b.y, Math.cos(a) * 8, Math.sin(a) * 8, t, { tipo: 'pena', curva, cor: '#ffd080', vida: 220, raio: 8 })); } }
      return { dur: 380, perigos: P }; },
  'Arcanjo · Cruz de luz': (r, alvo) => { const P = [];
      const linhas = [[{ x: 120, y: 120 }, { x: W - 120, y: H - 90 }], [{ x: W - 120, y: 120 }, { x: 120, y: H - 90 }]];
      linhas.forEach(([a, b], i) => P.push(Linha(a, b, i * 50, 55)));
      const y = clamp(alvo(100).y, 100, H - 80); P.push(Linha({ x: 120, y }, { x: W - 120, y }, 100, 55));
      return { dur: 230, perigos: P }; },
  'Astharoth · Dragão perseguindo (12 s)': (r, alvo) => ({ dur: 720, perigos: [Dragao(1, 720, alvo)] }),

  // ═══ SILVANNA / ARMOTHEUS ═══
  'Armotheus · Cortes (colunas)': (r) => { const P = [], larg = W / 7, br = Math.floor(r() * 7);
      for (let c = 0; c < 7; c++) if (c !== br) P.push(Ret(larg * (c + 0.5), H / 2, larg * 0.86, H, 10, 55)); return { dur: 100, perigos: P }; },
  'Armotheus · Cortes (faixas)': (r) => { const P = [], n = 8, al = Array.from({ length: n }, (_, k) => H - 40 - k * ((H - 80) / (n - 1))), b1 = Math.floor(r() * n), b2 = (b1 + 3 + Math.floor(r() * 3)) % n;
      al.forEach((y, k) => { if (k !== b1 && k !== b2) P.push(Ret(W / 2, y, W, 90, 10, 55)); }); return { dur: 100, perigos: P }; },
  'Armotheus · Varredura do chão': (r) => { const lado = r() < 0.5 ? 1 : -1, ini = 70, dur = 100, seg = 90;
      return { dur: 200, perigos: [{ cor: '#90dcff', ini, fim: ini + dur, aviso: t => t < ini, forma: () => ({ tipo: 'ret', cx: W / 2, cy: H - 25, w: W - seg, h: 50 }),
        bate: (x, y, t) => { if (t < ini || t >= ini + dur) return false; const frente = lerp(0, W - seg, (t - ini) / dur); const dentro = lado === 1 ? x < frente : x > W - frente; return dentro && y + PY > H - 50; } }] }; },
  'Armotheus · Rajada tripla': (r, alvo) => { const P = [], b = { x: W / 2, y: H - 170 };
      for (const t0 of [25, 45, 65]) { const a = alvo(t0), ang = Math.atan2(a.y - b.y, a.x - b.x); for (let k = -1; k <= 1; k++) P.push(Proj(b.x, b.y, Math.cos(ang + k * 0.3) * 8.5, Math.sin(ang + k * 0.3) * 8.5, t0, { persegue: 1 })); }
      return { dur: 220, perigos: P }; },
  'Armotheus · Salto (3×)': (r, alvo) => { const P = []; let ini = { x: W / 2, y: H - 62 };
      for (let c = 0; c < 3; c++) { const t0 = c * 70, al = { x: clamp(alvo(t0).x, 60, W - 60), y: H - 62 }, de = { ...ini };
        P.push(Caixa(t => { const s = getLerp(t0 + 20, t0 + 50, t); return { x: lerp(de.x, al.x, s), y: lerp(de.y, al.y, s) - Math.sin(s * PI) * 240 }; }, t0 + 20, t0 + 52, 100, 120));
        ini = al; } return { dur: 220, perigos: P }; },
  'Cavaleiro de gelo · Lança dupla': (r, alvo) => { const P = [], b = { x: W / 2, y: H - 62 };
      for (const t0 of [35, 85, 135]) { const a = alvo(t0), ang = Math.atan2(a.y - b.y, a.x - b.x); for (const k of [-1, 0, 1]) P.push(Proj(b.x, b.y, Math.cos(ang + k * 0.16) * 19, Math.sin(ang + k * 0.12) * 19, t0, { acel: 1, max: 19, vida: 150, raio: 12 })); }
      return { dur: 180, perigos: P }; },
  'Cavaleiro de gelo · Espinhos': (r, alvo) => { const P = [], bx = W / 2;
      for (const t0 of [20, 75, 130]) { const lado = (alvo(t0).x > bx ? 1 : -1) * (t0 === 75 ? -1 : 1); for (let k = 0; k < 64; k++) { const x = bx + lado * (90 + k * 34); if (x < 20 || x > W - 20) break; P.push(Espinho(x, t0, 30 + k * 3, 170 + (k % 3) * 40)); } }
      return { dur: 290, perigos: P }; },
  'Cavaleiro de gelo · Deslize glacial': (r, alvo) => { const lado = alvo(1).x > W / 2 ? -1 : 1, x0 = lado === 1 ? 70 : W - 70, x1 = lado === 1 ? W - 70 : 70;
      return { dur: 170, perigos: [Caixa(t => ({ x: clamp(x0 + lado * 24 * (t - 50), Math.min(x0, x1), Math.max(x0, x1)), y: H - 62 }), 50, 50 + Math.abs(x1 - x0) / 24, 100, 110)] }; },
  'Cavaleiro de gelo · Anel de gelo': (r, alvo) => { const P = [], c = { x: W / 2, y: H - 240 };
      for (const t0 of [30, 85, 140, 195]) { const a = alvo(t0); P.push(Anel(c.x, c.y, Math.atan2(a.y - c.y, a.x - c.x) + (r() * 2.8 - 1.4), r() < 0.5 ? 0.009 : -0.009, t0)); }
      return { dur: 370, perigos: P }; },
  'Cavaleiro de gelo · Prisão de gelo': (r, alvo) => { const P = []; let ant = alvo(0); for (let t0 = 15; t0 <= 135; t0 += 24) { const a = alvo(t0), vx = (a.x - ant.x) / 24, vy = (a.y - ant.y) / 24; ant = a; P.push(Prisao(a.x, a.y, t0)); if (Math.hypot(vx, vy) > 3) P.push(Prisao(a.x + vx * 40, a.y + vy * 40, t0)); } return { dur: 220, perigos: P }; },
  'Cavaleiro de gelo · Sopro glacial': (r, alvo) => { const b = { x: clamp(alvo(1).x, 300, W - 300), y: H - 360 }, lado = alvo(40).x > b.x ? 1 : -1;
      return { dur: 220, perigos: [Cone(b.x, b.y, PI / 2 - lado * 1.1, lado * 2.2, 40)] }; },
  'Cavaleiro de gelo · Escudo': (r, alvo) => { const a = alvo(1), lado = 1, b = { x: a.x - 150, y: H - 62 };
      return { dur: 80, perigos: [Ret(b.x + lado * 130, b.y, 210, 160, 34, 1)] }; },
  'Cavaleiros do fundo · Lanças (15 s)': (r, alvo) => { const P = []; for (let t0 = 50; t0 < 900; t0 += 110) { const a = alvo(t0);
      P.push(LancaCeu(a.x, t0), LancaCeu(clamp(a.x + (180 + r() * 140) * (r() < 0.5 ? 1 : -1), 40, W - 40), t0)); } return { dur: 900, perigos: P }; },
};

// ─── SIMULAÇÃO: onde o jogador pode estar a cada momento ───
function simular(nome, inicio, semente) {
  const r = rng(semente);
  const fronteira = new Uint8Array(GX * GY);
  const ci = clamp(Math.floor(inicio.x / CEL), 0, GX - 1), cj = clamp(Math.floor(inicio.y / CEL), 0, GY - 1);
  fronteira[cj * GX + ci] = 1;
  let centro = { ...inicio };
  const alvo = () => centro;
  const ataque = ATAQUES[nome](r, alvo);
  const projs = ataque.perigos.filter(p => p.proj);
  let menorLivre = 1, morreu = false, tMorte = -1, totalLivre = 0, amostras = 0;
  for (let t = 0; t < ataque.dur; t++) {
    for (const p of projs) p.passo(t, centro);
    if (t % PASSO) continue;
    // expande 1 célula (≈ 7 px/tick × 3 ticks)
    const nova = new Uint8Array(GX * GY);
    for (let j = 0; j < GY; j++) for (let i = 0; i < GX; i++) {
      if (!fronteira[j * GX + i]) continue;
      for (let dj = -1; dj <= 1; dj++) for (let di = -1; di <= 1; di++) { const a = i + di, b = j + dj; if (a >= 0 && a < GX && b >= 1 && b < GY) nova[b * GX + a] = 1; }
    }
    // remove o que está no perigo (checa os 3 ticks do passo)
    let cont = 0, sx = 0, sy = 0, livresTotal = 0;
    const ativos = ataque.perigos.filter(p => t + PASSO >= p.ini && t <= p.fim);
    for (let j = 0; j < GY; j++) for (let i = 0; i < GX; i++) {
      const x = i * CEL + CEL / 2, y = j * CEL + CEL / 2;
      let perigo = false;
      for (const p of ativos) { for (let k = 0; k < PASSO && !perigo; k++) if (p.bate(x, y, t + k)) perigo = true; if (perigo) break; }
      if (!perigo && j >= 1) livresTotal++;
      if (perigo) nova[j * GX + i] = 0;
      if (nova[j * GX + i]) { cont++; sx += x; sy += y; }
    }
    fronteira.set(nova);
    const livre = livresTotal / (GX * (GY - 1));
    if (ativos.length) { menorLivre = Math.min(menorLivre, livre); totalLivre += livre; amostras++; }
    if (cont === 0) { morreu = true; tMorte = t; break; }
    centro = { x: sx / cont, y: sy / cont };
  }
  return { morreu, tMorte, menorLivre, mediaLivre: amostras ? totalLivre / amostras : 1 };
}

const INICIOS = [{ x: 300, y: 1250 }, { x: 1200, y: 1250 }, { x: 2100, y: 1250 }, { x: 700, y: 800 }, { x: 1700, y: 800 }, { x: 1200, y: 400 }, { x: 400, y: 200 }, { x: 2000, y: 300 }];

function avaliarTudo(nomes) {
  const res = [];
  for (const nome of (nomes || Object.keys(ATAQUES))) {
    let vivos = 0, n = 0, menor = 1, media = 0, mortes = [];
    for (const ini of INICIOS) for (let s = 1; s <= 3; s++) {
      const r = simular(nome, ini, s * 7919 + n); n++;
      if (!r.morreu) vivos++; else mortes.push(r.tMorte);
      menor = Math.min(menor, r.menorLivre); media += r.mediaLivre;
    }
    media /= n;
    const sobrevive = vivos / n;
    const veredito = sobrevive < 0.5 ? 'IMPOSSÍVEL' : sobrevive < 0.85 ? 'MUITO DIFÍCIL' : media > 0.85 && menor > 0.6 ? 'FÁCIL DEMAIS' : 'OK';
    res.push({ nome, sobrevive: Math.round(sobrevive * 100), menorLivre: Math.round(menor * 100), mediaLivre: Math.round(media * 100), veredito });
  }
  return res;
}

// ─── DESENHO (para olhar quadro a quadro) ───
function desenharQuadro(ctx, nome, semente, t, inicio) {
  const r = rng(semente); let centro = { ...inicio };
  const ataque = ATAQUES[nome](r, () => centro);
  const projs = ataque.perigos.filter(p => p.proj);
  for (let k = 0; k <= t; k++) for (const p of projs) p.passo(k, centro);
  const e = ctx.canvas.width / W;
  ctx.fillStyle = '#0d0b18'; ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
  ctx.fillStyle = '#2a2240'; ctx.fillRect(0, H * e - 3, W * e, 3);
  for (const p of ataque.perigos) {
    if (t < p.ini || t > p.fim) continue;
    const f = p.forma(t), av = p.aviso(t);
    ctx.save(); ctx.globalAlpha = av ? 0.35 : 0.95; ctx.fillStyle = ctx.strokeStyle = p.cor; ctx.lineWidth = Math.max(1, 2 * e);
    if (f.tipo === 'ret') { av ? ctx.strokeRect((f.cx - f.w / 2) * e, (f.cy - f.h / 2) * e, f.w * e, f.h * e) : ctx.fillRect((f.cx - f.w / 2) * e, (f.cy - f.h / 2) * e, f.w * e, f.h * e); }
    else if (f.tipo === 'linha') { ctx.lineWidth = (av ? 3 : f.larg) * e; ctx.beginPath(); ctx.moveTo(f.ax * e, f.ay * e); ctx.lineTo(f.bx * e, f.by * e); ctx.stroke(); }
    else if (f.tipo === 'bola') { ctx.beginPath(); ctx.arc(f.x * e, f.y * e, Math.max(2, f.r * e), 0, TAU); ctx.fill(); }
    else if (f.tipo === 'anel') { ctx.lineWidth = 52 * e; ctx.beginPath(); ctx.arc(f.cx * e, f.cy * e, f.r * e, f.brecha + f.meia, f.brecha - f.meia + TAU); ctx.stroke(); }
    else if (f.tipo === 'circulo') { ctx.beginPath(); ctx.arc(f.x * e, f.y * e, f.r * e, 0, TAU); av ? ctx.stroke() : ctx.fill(); }
    else if (f.tipo === 'cone') { ctx.beginPath(); ctx.moveTo(f.cx * e, f.cy * e); ctx.arc(f.cx * e, f.cy * e, f.L * e, f.a - f.ab, f.a + f.ab); ctx.closePath(); av ? ctx.stroke() : ctx.fill(); }
    else if (f.tipo === 'dragao') { ctx.beginPath(); ctx.arc(f.x * e, f.y * e, 27 * e, 0, TAU); ctx.fill(); for (const c of f.corpo) { ctx.beginPath(); ctx.arc(c[0] * e, c[1] * e, 20 * e, 0, TAU); ctx.fill(); } }
    ctx.restore();
  }
  ctx.fillStyle = '#ffe080'; ctx.fillRect((centro.x - PX) * e, (centro.y - PY) * e, PX * 2 * e, PY * 2 * e);
}

if (typeof module !== 'undefined') module.exports = { ATAQUES, avaliarTudo, simular, desenharQuadro, W, H };

// Jogador PARADO no lugar: ele é atingido? (mede se o ataque obriga a reagir)
function parado(nome, inicio, semente) {
  const r = rng(semente); const centro = { ...inicio };
  const ataque = ATAQUES[nome](r, () => centro);
  const projs = ataque.perigos.filter(p => p.proj);
  for (let t = 0; t < ataque.dur; t++) {
    for (const p of projs) p.passo(t, centro);
    for (const p of ataque.perigos) if (t >= p.ini && t <= p.fim && p.bate(centro.x, centro.y, t)) return true;
  }
  return false;
}
if (typeof module !== 'undefined') module.exports.parado = parado;
