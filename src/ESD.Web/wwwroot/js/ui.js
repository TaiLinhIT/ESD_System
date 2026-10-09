// ═══ DOM helpers, icons, formatting, badges, toasts ═══
// Shared UI utilities — every view imports from here.

export const $  = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

/** Create an element: el('div', {class:'x', onclick: fn}, children) */
export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null) continue;
    if (k === 'class') node.className = v;
    else if (k === 'dataset') Object.assign(node.dataset, v);
    else if (k.startsWith('on') && typeof v === 'function')
      node.addEventListener(k.slice(2), v);
    else if (k === 'html') node.innerHTML = v;
    else node.setAttribute(k, v);
  }
  for (const c of children.flat()) {
    if (c == null) continue;
    node.append(c.nodeType ? c : document.createTextNode(String(c)));
  }
  return node;
}

// ─── Inline SVG icons (stroke style, 24×24 viewBox) ───────────
const PATHS = {
  dashboard: '<rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/>',
  activity:  '<polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>',
  history:   '<circle cx="12" cy="12" r="9"/><polyline points="12 7 12 12 15.5 14"/>',
  device:    '<rect x="5" y="5" width="14" height="14" rx="2"/><line x1="9" y1="2" x2="9" y2="5"/><line x1="15" y1="2" x2="15" y2="5"/><line x1="9" y1="19" x2="9" y2="22"/><line x1="15" y1="19" x2="15" y2="22"/>',
  settings:  '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
  refresh:   '<polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/>',
  download:  '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>',
  search:    '<circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
  pause:     '<rect x="6" y="4" width="4" height="16" rx="1"/><rect x="14" y="4" width="4" height="16" rx="1"/>',
  play:      '<polygon points="6 3 20 12 6 21 6 3"/>',
  trash:     '<polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>',
  user:      '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  zap:       '<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>',
  check:     '<polyline points="20 6 9 17 4 12"/>',
  x:         '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  alert:     '<path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
};

export function icon(name, size = 18) {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('width', size);
  svg.setAttribute('height', size);
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.innerHTML = PATHS[name] ?? '';
  return svg;
}

/** Inject icons marked with data-icon="name" (used in index.html). */
export function hydrateIcons(root = document) {
  $$('[data-icon]', root).forEach(node => {
    node.append(icon(node.dataset.icon, 18));
    node.removeAttribute('data-icon');
  });
}

// ─── Formatting ──────────────────────────────────────────────
export const fmtTime = (d, withMs = false) => {
  const dt = new Date(d);
  return dt.toLocaleTimeString('vi-VN', {
    hour12: false,
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    ...(withMs ? { fractionalSecondDigits: 3 } : {}),
  });
};

export const fmtDateTime = (d) => {
  const dt = new Date(d);
  return `${dt.toLocaleDateString('vi-VN')} ${fmtTime(dt)}`;
};

export const fmtNumber = (n) =>
  (n ?? 0).toLocaleString('vi-VN', { maximumFractionDigits: 1 });

export const fmtPercent = (n) => `${fmtNumber(n)} %`;

// ─── Status helpers ──────────────────────────────────────────
const STATUS_MAP = {
  Ok: 'ok', Ng: 'ng', Warning: 'warn',
  NotConnected: 'offline', Error: 'ng',
  // EsdState
  Idle: 'info', WorkerPresent: 'info', StrapOk: 'ok',
  StrapNg: 'ng', Disconnected: 'offline',
  // Event types worth highlighting
  Alarm: 'ng', AlarmReset: 'warn',
};

export function statusClass(value) {
  if (!value) return 'offline';
  return STATUS_MAP[value] ?? 'info';
}

export function pill(value, label = value) {
  return el('span', { class: `pill ${statusClass(value)}` }, label ?? value);
}

// ─── Toast notifications ─────────────────────────────────────
export function toast(message, type = 'info', timeoutMs = 3500) {
  const stack = $('#toast-stack');
  if (!stack) return;
  const node = el('div', { class: `toast ${type}` }, message);
  stack.append(node);
  setTimeout(() => {
    node.style.opacity = '0';
    node.style.transform = 'translateX(30px)';
    node.style.transition = 'all .3s';
    setTimeout(() => node.remove(), 320);
  }, timeoutMs);
}

// ─── Misc ────────────────────────────────────────────────────
export function escapeHtml(s) {
  return String(s ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

export function debounce(fn, ms) {
  let t;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), ms);
  };
}

export function timeAgo(d) {
  const diff = (Date.now() - new Date(d).getTime()) / 1000;
  if (diff < 5) return 'vừa xong';
  if (diff < 60) return `${Math.floor(diff)} giây trước`;
  if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
  return fmtDateTime(d);
}
