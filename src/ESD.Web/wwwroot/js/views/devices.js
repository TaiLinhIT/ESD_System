// ═══ Devices view — live device/station status cards ═══

import { $, el, pill, fmtTime, timeAgo, escapeHtml, toast } from '../ui.js';
import { api } from '../api.js';
import { LiveEventStream } from '../sse.js';

export async function render(container) {
  let devices = [];
  let timer = null;
  let stream = null;

  const grid = el('div', { class: 'grid cards' });

  container.append(
    el('div', { class: 'card' },
      el('div', { class: 'card-header' },
        el('span', {}, 'Danh sách thiết bị'),
        el('div', { class: 'card-tools' },
          el('span', { class: 'muted', style: 'font-weight:400;font-size:12px' },
            'Cập nhật theo thời gian thực'))),
      el('div', { class: 'card-body' }, grid)));

  function renderDevices() {
    grid.replaceChildren();
    if (!devices.length) {
      grid.append(el('div', { class: 'empty-state', style: 'grid-column:1/-1' },
        'Không có thiết bị nào — kiểm tra cấu hình ESD.API.'));
      return;
    }
    for (const d of devices) {
      const conn = d.isConnected ? 'ok' : 'offline';
      grid.append(el('div', { class: 'card station-card' },
        el('div', { class: 'card-body' },
          el('div', { class: 'station-head' },
            el('div', {},
              el('div', { class: 'station-name' }, escapeHtml(d.name)),
              el('div', { class: 'station-meta' },
                `Địa chỉ 0x${d.address.toString(16).toUpperCase().padStart(2, '0')} · ${escapeHtml(d.protocol)} · ${escapeHtml(d.portName || '—')}`)),
            pill(d.isConnected ? 'Ok' : 'NotConnected',
              d.isConnected ? 'CONNECTED' : 'DISCONNECTED')),
          el('div', { class: 'station-stats' },
            stat('Trạng thái ESD', d.state, stateClass(d.state)),
            stat('Kết nối', d.isConnected ? 'Online' : 'Offline', d.isConnected ? 'var(--ok)' : 'var(--offline)')),
          el('div', { class: 'flex items-center gap-2', style: 'border-top:1px solid var(--divider);padding-top:10px' },
            el('span', { class: 'muted', style: 'font-size:12px' }, 'Sự kiện cuối:'),
            d.lastEventType
              ? el('span', {},
                  pill(d.lastEventType),
                  el('span', { class: 'muted', style: 'font-size:12px' },
                    d.lastEmployeeId ? `${d.lastEmployeeId} · ` : '',
                    d.lastEventAt ? timeAgo(d.lastEventAt) : ''))
              : el('span', { class: 'muted', style: 'font-size:12px' }, 'chưa có')))));
    }
  }

  function stat(label, value, color) {
    return el('div', { class: 'station-stat' },
      el('b', { style: `color:${color};font-size:15px` }, String(value ?? '—')),
      el('span', {}, label));
  }

  function stateClass(s) {
    return {
      StrapOk: 'var(--ok)', StrapNg: 'var(--ng)',
      Idle: 'var(--accent)', WorkerPresent: 'var(--accent)',
      Error: 'var(--ng)', Disconnected: 'var(--offline)',
    }[s] ?? 'var(--text-secondary)';
  }

  async function load() {
    try {
      devices = await api.devices();
      renderDevices();
    } catch (ex) {
      console.error(ex);
      toast(`Lỗi tải thiết bị: ${ex.message}`, 'error');
    }
  }

  await load();
  timer = setInterval(load, 15_000);

  // Refresh instantly when any device event arrives
  stream = new LiveEventStream({ onEvent: () => load() });
  stream.connect();

  return () => {
    clearInterval(timer);
    stream?.close();
  };
}
