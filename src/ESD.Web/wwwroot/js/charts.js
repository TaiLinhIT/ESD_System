// ═══ Lightweight SVG charts — no external dependencies ═══

import { el } from './ui.js';

/**
 * Grouped vertical bar chart.
 * data: [{ label, ok, ng, total }]
 */
export function barChart(data, { height = 220, max = null } = {}) {
  const ns = 'http://www.w3.org/2000/svg';
  const W = 760, H = height, PAD_L = 34, PAD_B = 26, PAD_T = 12;
  const chartW = W - PAD_L - 12, chartH = H - PAD_T - PAD_B;

  const peak = max ?? Math.max(1, ...data.map(d => d.total));
  const nicePeak = niceCeil(peak);

  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
  svg.setAttribute('class', 'chart');
  svg.setAttribute('role', 'img');

  const g = document.createElementNS(ns, 'g');
  svg.append(g);

  // horizontal gridlines + y labels (0, 25%, 50%, 75%, 100%)
  for (let i = 0; i <= 4; i++) {
    const v = (nicePeak / 4) * i;
    const y = PAD_T + chartH - (chartH * i) / 4;
    g.append(line(ns, PAD_L, y, W - 12, y, 'grid-line'));
    const txt = text(ns, PAD_L - 6, y + 3, String(Math.round(v)), 'axis-text');
    txt.setAttribute('text-anchor', 'end');
    g.append(txt);
  }

  // baseline
  g.append(line(ns, PAD_L, PAD_T + chartH, W - 12, PAD_T + chartH, 'axis-line'));

  if (!data.length) {
    const t = text(ns, W / 2, H / 2, 'No data', 'axis-text');
    t.setAttribute('text-anchor', 'middle');
    g.append(t);
    return svg;
  }

  const slot = chartW / data.length;
  const barW = Math.min(26, (slot - 14) / 2 - 2);

  data.forEach((d, i) => {
    const cx = PAD_L + slot * i + slot / 2;

    const okH = (d.ok / nicePeak) * chartH;
    const ngH = (d.ng / nicePeak) * chartH;

    const okBar = rect(ns, cx - barW - 2, PAD_T + chartH - okH, barW, okH, 'bar-ok');
    const ngBar = rect(ns, cx + 2, PAD_T + chartH - ngH, barW, ngH, 'bar-ng');
    g.append(okBar, ngBar);

    // tooltip
    const tip = document.createElementNS(ns, 'title');
    tip.textContent = `${d.label}: OK ${d.ok}, NG ${d.ng}`;
    okBar.append(tip);
    ngBar.append(tip.cloneNode(true));

    // x label (every slot, rotate if crowded)
    const lbl = text(ns, cx, H - 8, d.label, 'axis-text');
    lbl.setAttribute('text-anchor', 'middle');
    g.append(lbl);
  });

  return svg;
}

/**
 * Donut chart for status distribution.
 * segments: [{ value, color, label }]
 */
export function donut(segments, { size = 150, thickness = 22 } = {}) {
  const ns = 'http://www.w3.org/2000/svg';
  const total = segments.reduce((s, x) => s + x.value, 0) || 1;
  const r = (size - thickness) / 2;
  const c = 2 * Math.PI * r;

  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', `0 0 ${size} ${size}`);
  svg.setAttribute('width', size);
  svg.setAttribute('height', size);
  svg.setAttribute('class', 'chart');

  const inner = el('div', {
    style: `position:relative;width:${size}px;height:${size}px;`
  });

  const wrap = el('div', {
    style: `position:absolute;inset:0;display:grid;place-items:center;text-align:center;`
  });

  let offset = c * 0.25; // start at top
  for (const seg of segments) {
    const frac = seg.value / total;
    if (frac <= 0) continue;
    const dash = `${frac * c} ${c}`;
    const circle = document.createElementNS(ns, 'circle');
    circle.setAttribute('cx', size / 2);
    circle.setAttribute('cy', size / 2);
    circle.setAttribute('r', r);
    circle.setAttribute('fill', 'none');
    circle.setAttribute('stroke', seg.color);
    circle.setAttribute('stroke-width', thickness);
    circle.setAttribute('stroke-dasharray', dash);
    circle.setAttribute('stroke-dashoffset', String(offset));
    circle.setAttribute('transform', `rotate(-90 ${size / 2} ${size / 2})`);
    const tip = document.createElementNS(ns, 'title');
    tip.textContent = `${seg.label}: ${seg.value}`;
    circle.append(tip);
    svg.append(circle);
    offset -= frac * c;
  }

  inner.append(svg, wrap);
  return inner;
}

export function legend(items) {
  return el('div', { class: 'legend' },
    items.map(([label, color]) =>
      el('span', { class: 'key' },
        el('span', { class: 'swatch', style: `background:${color}` }), label)));
}

// ─── svg element factories ───────────────────────────
function line(ns, x1, y1, x2, y2, cls) {
  const l = document.createElementNS(ns, 'line');
  l.setAttribute('x1', x1); l.setAttribute('y1', y1);
  l.setAttribute('x2', x2); l.setAttribute('y2', y2);
  if (cls) l.setAttribute('class', cls);
  return l;
}

function rect(ns, x, y, w, h, cls) {
  const r = document.createElementNS(ns, 'rect');
  r.setAttribute('x', x.toFixed(2));
  r.setAttribute('y', Math.max(0, y).toFixed(2));
  r.setAttribute('width', w.toFixed(2));
  r.setAttribute('height', Math.max(0, h).toFixed(2));
  r.setAttribute('rx', 2);
  if (cls) r.setAttribute('class', cls);
  return r;
}

function text(ns, x, y, str, cls) {
  const t = document.createElementNS(ns, 'text');
  t.setAttribute('x', x);
  t.setAttribute('y', y);
  if (cls) t.setAttribute('class', cls);
  t.textContent = str;
  return t;
}

function niceCeil(v) {
  if (v <= 5) return 5;
  const pow = Math.pow(10, Math.floor(Math.log10(v)));
  const n = v / pow;
  const nice = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
  return nice * pow;
}
