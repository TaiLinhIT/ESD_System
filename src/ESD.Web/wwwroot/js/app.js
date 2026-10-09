// ═══ App bootstrap — hash router, navigation, clock ═══

import { $, $$, el, hydrateIcons, toast } from './ui.js';
import { api } from './api.js';
import { fmtTime } from './ui.js';

const ROUTES = {
  dashboard: {
    title: 'Dashboard',
    sub:   'Tổng quan hệ thống',
    load:  () => import('./views/dashboard.js'),
  },
  events: {
    title: 'Live Events',
    sub:   'Sự kiện thiết bị theo thời gian thực',
    load:  () => import('./views/events.js'),
  },
  history: {
    title: 'History',
    sub:   'Lịch sử sử dụng vòng tay ESD',
    load:  () => import('./views/history.js'),
  },
  devices: {
    title: 'Devices',
    sub:   'Trạng thái thiết bị & trạm',
    load:  () => import('./views/devices.js'),
  },
  settings: {
    title: 'Settings',
    sub:   'Cài đặt ứng dụng',
    load:  () => import('./views/settings.js'),
  },
};

const viewEl = $('#view');
let currentCleanup = null;
let currentRoute = null;

function parseHash() {
  const h = window.location.hash.replace(/^#\/?/, '') || 'dashboard';
  const [route] = h.split('/');
  return ROUTES[route] ? route : 'dashboard';
}

async function render(route) {
  // cleanup previous view (SSE streams, intervals, observers)
  if (typeof currentCleanup === 'function') {
    try { currentCleanup(); } catch { /* ignore */ }
    currentCleanup = null;
  }

  currentRoute = route;
  const config = ROUTES[route];

  $('#page-title').textContent = config.title;
  $('#page-sub').textContent   = config.sub;

  $$('#nav .nav-item').forEach(a =>
    a.classList.toggle('active', a.dataset.route === route));

  // loading skeleton
  viewEl.replaceChildren(
    el('div', { class: 'grid kpi-4' },
      Array.from({ length: 4 }, () =>
        el('div', { class: 'card kpi-card' },
          el('div', { class: 'skeleton', style: 'height:14px;width:60%' }),
          el('div', { class: 'skeleton mt-3', style: 'height:30px;width:45%' }),
          el('div', { class: 'skeleton mt-3', style: 'height:12px;width:75%' })))));

  try {
    const module = await config.load();
    // guard: user navigated away while the module was loading
    if (currentRoute !== route) return;
    viewEl.replaceChildren();
    currentCleanup = (await module.render(viewEl)) ?? null;
  } catch (ex) {
    console.error(ex);
    viewEl.replaceChildren(
      el('div', { class: 'card' },
        el('div', { class: 'card-body empty-state' },
          `⚠ Không tải được trang: ${ex.message}`)));
  }
}

// ── Connection status pill ─────────────────────────────
async function refreshConnection() {
  const pill = $('#conn-pill');
  try {
    await api.health();
    pill.className = 'conn-pill online';
    pill.querySelector('.conn-text').textContent = 'API online';
  } catch {
    pill.className = 'conn-pill offline';
    pill.querySelector('.conn-text').textContent = 'API offline';
  }
}

// ── Clock ──────────────────────────────────────────────
function startClock() {
  const clock = $('#clock');
  const tick = () => { clock.textContent = fmtTime(new Date()); };
  tick();
  setInterval(tick, 1000);
}

// ── Boot ───────────────────────────────────────────────
function boot() {
  hydrateIcons();
  startClock();

  window.addEventListener('hashchange', () => render(parseHash()));

  $('#refresh-btn').addEventListener('click', () => {
    render(currentRoute ?? parseHash());
    toast('Đã làm mới', 'success', 1500);
  });

  // poll API connectivity in the background
  refreshConnection();
  setInterval(refreshConnection, 15_000);

  render(parseHash());
}

boot();
