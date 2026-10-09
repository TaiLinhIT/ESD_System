// ═══ Settings view — app configuration & API info ═══

import { $, $$, el, toast, escapeHtml } from '../ui.js';
import { api } from '../api.js';
import { fmtDateTime, fmtTime } from '../ui.js';

const THEME_KEY = 'esd-web-theme';
const REFRESH_KEY = 'esd-web-refresh';

export async function render(container) {
  // ── Theme toggle ─────────────────────────────
  const themeSwitch = el('input', { type: 'checkbox', id: 'theme-toggle' });
  themeSwitch.checked = localStorage.getItem(THEME_KEY) === 'dark';
  themeSwitch.addEventListener('change', () => {
    const dark = themeSwitch.checked;
    document.documentElement.dataset.theme = dark ? 'dark' : '';
    localStorage.setItem(THEME_KEY, dark ? 'dark' : 'light');
    toast(dark ? 'Đã bật chế độ tối' : 'Đã bật chế độ sáng', 'success', 2000);
  });

  // apply persisted theme (also on first load)
  if (themeSwitch.checked) document.documentElement.dataset.theme = 'dark';

  // ── API info (live) ──────────────────────────
  const apiInfo = el('div', { class: 'card-body' },
    el('div', { class: 'empty-state' }, 'Đang kiểm tra kết nối API…'));

  async function refreshApiInfo() {
    try {
      const [status, devices] = await Promise.all([api.status(), api.devices()]);
      const rows = [
        ['Trạng thái', status.status],
        ['Thiết bị', `${status.totalDevices} (kết nối: ${status.connectedDevices})`],
        ['Hàng đợi DB', String(status.dbQueueLength)],
        ['Giờ server (UTC)', fmtTime(status.serverTimeUtc, true)],
        ['Số trạm đang theo dõi', String(devices.length)],
      ];
      apiInfo.replaceChildren(el('div', { style: 'display:flex;flex-direction:column' },
        rows.map(([k, v]) =>
          el('div', { class: 'settings-row' },
            el('div', { class: 'title' }, k),
            el('div', { class: 'mono muted' }, v)))));
    } catch (ex) {
      apiInfo.replaceChildren(
        el('div', { class: 'empty-state' },
          `⚠ API không khả dụng: ${escapeHtml(ex.message)}`,
          el('div', { class: 'mt-2' },
            'Khởi động project ESD.API hoặc sửa Api:BaseUrl trong appsettings.json')));
    }
  }

  await refreshApiInfo();
  const timer = setInterval(refreshApiInfo, 10_000);

  // ── Keyboard shortcuts info ──────────────────
  container.append(
    el('div', { class: 'grid', style: 'grid-template-columns: 1fr 1fr;' },
      el('div', { class: 'card' },
        el('div', { class: 'card-header' }, 'Giao diện'),
        el('div', { class: 'card-body' },
          el('div', { class: 'settings-row' },
            el('div', {},
              el('div', { class: 'title' }, 'Chế độ tối'),
              el('div', { class: 'desc' }, 'Lưu vào localStorage, áp dụng ngay lập tức')),
            el('label', { class: 'switch' }, themeSwitch, el('span', { class: 'track' })))),
      el('div', { class: 'card' },
        el('div', { class: 'card-header' }, 'Kết nối API'),
        apiInfo)),

    el('div', { class: 'card section-gap' },
      el('div', { class: 'card-header' }, 'Thông tin hệ thống'),
      el('div', { class: 'card-body' },
        el('div', { class: 'settings-row' },
          el('div', {},
            el('div', { class: 'title' }, 'Kiến trúc'),
            el('div', { class: 'desc' },
              'ESD.Device → ESD.Service → ESD.API → ESD.Web')),
          el('span', { class: 'pill info' }, '7 projects')),
        el('div', { class: 'settings-row' },
          el('div', {},
            el('div', { class: 'title' }, 'Real-time'),
            el('div', { class: 'desc' },
              'Server-Sent Events (SSE) qua /api/events/live — tự động nối lại khi mất kết nối')),
          el('span', { class: 'pill ok' }, 'SSE')),
        el('div', { class: 'settings-row' },
          el('div', {},
            el('div', { class: 'title' }, 'Phân trang'),
            el('div', { class: 'desc' }, '25 bản ghi/trang trên trang History')),
          el('span', { class: 'pill info' }, '25/trang')))));

  return () => clearInterval(timer);
}
