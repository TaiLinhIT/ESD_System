// ═══ Live Events view — real-time event stream via SSE ═══

import { $, $$, el, pill, fmtTime, fmtDateTime, escapeHtml, toast } from '../ui.js';
import { LiveEventStream } from '../sse.js';
import { api } from '../api.js';

const MAX_ROWS = 200;

export async function render(container) {
  let stream = null;
  let paused = false;
  let eventCount = 0;
  const pending = [];

  // ── Layout ─────────────────────────────────────────
  const statusChip = el('span', { class: 'feed-status' },
    el('span', { class: 'dot' }), 'đang kết nối…');

  const filterSel = el('select', { class: 'input', style: 'min-width:170px' },
    el('option', { value: '' }, 'Tất cả sự kiện'));

  const table = el('table', { class: 'data' },
    el('thead', {}, el('tr', {},
      el('th', {}, 'Thời gian'),
      el('th', {}, 'Thiết bị'),
      el('th', {}, 'Sự kiện'),
      el('th', {}, 'Nhân viên'),
      el('th', {}, 'Trạng thái'),
      el('th', {}, 'Dữ liệu gốc'),
      el('th', {}, 'Ghi chú'))),
    el('tbody', { class: 'feed-body' },
      el('tr', {}, el('td', { colspan: 7, class: 'empty-state' },
        'Đang chờ sự kiện từ thiết bị…'))));

  const pauseBtn = el('button', { class: 'btn ghost', onclick: togglePause },
    'Tạm dừng');

  container.append(
    el('div', { class: 'card' },
      el('div', { class: 'card-header' },
        el('span', { class: 'flex items-center gap-2' }, statusChip),
        el('div', { class: 'card-tools' },
          filterSel, pauseBtn,
          el('button', {
            class: 'btn ghost',
            onclick: () => {
              tbody().replaceChildren(
                el('tr', {}, el('td', { colspan: 7, class: 'empty-state' },
                  'Đang chờ sự kiện từ thiết bị…')));
              eventCount = 0;
              updateBadge();
            },
          }, 'Xóa'))),
      el('div', { class: 'table-wrap', style: 'max-height: calc(100vh - 280px); overflow-y:auto;' }, table)));

  function tbody() { return $('.feed-body', table); }

  function togglePause() {
    paused = !paused;
    pauseBtn.textContent = paused ? 'Tiếp tục' : 'Tạm dừng';
    if (!paused) flushPending();
  }

  function flushPending() {
    while (pending.length) addRow(pending.shift(), false);
  }

  function updateBadge() {
    const badge = $('#live-badge');
    if (badge) {
      badge.hidden = eventCount === 0;
      badge.textContent = eventCount > 99 ? '99+' : String(eventCount);
    }
  }

  function addRow(evt, isNew = true) {
    // remove placeholder row if present
    const placeholder = $('tr td[colspan]', tbody());
    if (placeholder) placeholder.closest('tr')?.remove();

    const row = el('tr', { class: isNew ? 'row-new' : '' },
      el('td', { class: 'mono' }, fmtTime(evt.timestamp, true)),
      el('td', {}, escapeHtml(evt.device)),
      el('td', {}, escapeHtml(evt.eventType)),
      el('td', {}, evt.employee ? el('span', { class: 'pill info' }, escapeHtml(evt.employee)) : '—'),
      el('td', {}, pill(evt.status)),
      el('td', { class: 'mono muted' }, escapeHtml(evt.rawData ?? '')),
      el('td', { class: 'muted' }, escapeHtml(evt.message ?? '')));

    tbody().prepend(row);

    // bound the DOM
    while (tbody().children.length > MAX_ROWS)
      tbody().lastElementChild?.remove();

    eventCount++;
    updateBadge();
  }

  function setStatus(status) {
    statusChip.className = `feed-status ${status === 'live' ? 'live' : ''}`;
    statusChip.querySelector('.dot')?.remove();
    statusChip.prepend(el('span', { class: 'dot' }));
    statusChip.lastChild.textContent =
      status === 'live' ? 'LIVE — đang lắng nghe' :
      status === 'connecting' ? 'đang kết nối…' : 'mất kết nối — thử lại';
  }

  // ── Event type filter (populated from API) ───────
  try {
    const filters = await api.eventFilters();
    for (const t of filters.eventTypes ?? [])
      filterSel.append(el('option', { value: t }, t));
  } catch { /* filter stays empty — non-fatal */ }

  filterSel.addEventListener('change', () => {
    const v = filterSel.value;
    $$('tr', tbody()).forEach(tr => {
      if (!v || !tr.children[2]) return;
      tr.style.display = tr.children[2].textContent === v ? '' : 'none';
    });
  });

  // ── Connect SSE ───────────────────────────────────
  stream = new LiveEventStream({
    onStatus: setStatus,
    onEvent: (evt) => {
      if (paused) { pending.push(evt); return; }
      addRow(evt);
    },
  });
  stream.connect();

  return () => stream?.close();
}
