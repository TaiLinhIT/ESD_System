// ═══ History view — searchable, paged event log ═══

import { $, el, pill, fmtDateTime, escapeHtml, debounce, toast } from '../ui.js';
import { api } from '../api.js';

const PAGE_SIZE = 25;

export async function render(container) {
  let state = { page: 1, total: 0, totalPages: 0, items: [] };
  let filters = { devices: [], employees: [], eventTypes: [], statuses: [] };
  let timer = null;

  // ── Filter bar ─────────────────────────────────
  const fDevice   = el('select', { class: 'input' }, el('option', { value: '' }, 'Tất cả thiết bị'));
  const fEmployee = el('select', { class: 'input' }, el('option', { value: '' }, 'Tất cả nhân viên'));
  const fType     = el('select', { class: 'input' }, el('option', { value: '' }, 'Tất cả loại'));
  const fStatus   = el('select', { class: 'input' }, el('option', { value: '' }, 'Tất cả trạng thái'));
  const fFrom     = el('input', { class: 'input', type: 'date' });
  const fTo       = el('input', { class: 'input', type: 'date' });

  const filterRow = el('div', { class: 'filters' },
    field('Thiết bị', fDevice),
    field('Nhân viên', fEmployee),
    field('Loại sự kiện', fType),
    field('Trạng thái', fStatus),
    field('Từ', fFrom),
    field('Đến', fTo),
    el('button', { class: 'btn', onclick: applyFilters }, 'Tìm kiếm'),
    el('button', {
      class: 'btn ghost',
      onclick: () => {
        [fDevice, fEmployee, fType, fStatus].forEach(s => s.value = '');
        fFrom.value = ''; fTo.value = '';
        applyFilters();
      },
    }, 'Đặt lại'),
    el('button', { class: 'btn ghost', onclick: exportCsv }, 'Xuất CSV'));

  const table = el('table', { class: 'data' },
    el('thead', {}, el('tr', {},
      el('th', {}, '#'),
      el('th', {}, 'Thời gian'),
      el('th', {}, 'Thiết bị'),
      el('th', {}, 'Sự kiện'),
      el('th', {}, 'Nhân viên'),
      el('th', {}, 'Trạng thái'),
      el('th', {}, 'Dữ liệu gốc'),
      el('th', {}, 'Ghi chú')),
    el('tbody', { class: 'rows' },
      el('tr', {}, el('td', { colspan: 8, class: 'empty-state' }, 'Đang tải…'))));

  const pager = el('div', { class: 'pagination' });

  container.append(
    el('div', { class: 'card' },
      el('div', { class: 'card-header' }, 'Bộ lọc'),
      el('div', { class: 'card-body' }, filterRow)),
    el('div', { class: 'card section-gap' },
      el('div', { class: 'card-header' },
        el('span', {}, 'Lịch sử sự kiện'),
        el('span', { class: 'muted', style: 'font-weight:400;font-size:12px', id: 'row-count' })),
      el('div', { class: 'table-wrap' }, table),
      pager));

  function field(label, input) {
    return el('div', { class: 'field' }, el('label', {}, label), input);
  }

  // ── Populate filter dropdowns ──────────────────
  try {
    const f = await api.eventFilters();
    filters = f;
    for (const d of f.devices ?? [])     fDevice.append(el('option', { value: d }, d));
    for (const e of f.employees ?? [])   fEmployee.append(el('option', { value: e }, e));
    for (const t of f.eventTypes ?? [])  fType.append(el('option', { value: t }, t));
    for (const s of f.statuses ?? [])    fStatus.append(el('option', { value: s }, s));
  } catch (ex) {
    toast(`Không tải được bộ lọc: ${ex.message}`, 'error');
  }

  // ── Load paged data ────────────────────────────
  async function load() {
    const params = {
      page: state.page,
      pageSize: PAGE_SIZE,
      device: fDevice.value || null,
      employee: fEmployee.value || null,
      eventType: fType.value || null,
      status: fStatus.value || null,
      from: fFrom.value ? `${fFrom.value}T00:00:00` : null,
      to: fTo.value ? `${fTo.value}T23:59:59` : null,
    };

    try {
      const res = await api.events(params);
      state.items = res.items;
      state.total = res.totalCount;
      state.totalPages = res.totalPages;
      renderRows();
      renderPager();
      const rc = $('#row-count');
      if (rc) rc.textContent = `${res.totalCount.toLocaleString()} bản ghi`;
    } catch (ex) {
      console.error(ex);
      toast(`Lỗi tải lịch sử: ${ex.message}`, 'error');
      $('.rows', table).replaceChildren(
        el('tr', {}, el('td', { colspan: 8, class: 'empty-state' },
          `Lỗi: ${escapeHtml(ex.message)}`)));
    }
  }

  function applyFilters() {
    state.page = 1;
    load();
  }

  function renderRows() {
    const tbody = $('.rows', table);
    tbody.replaceChildren();
    if (!state.items.length) {
      tbody.append(el('tr', {}, el('td', { colspan: 8, class: 'empty-state' },
        'Không có bản ghi nào khớp bộ lọc')));
      return;
    }
    const startId = state.total - (state.page - 1) * PAGE_SIZE;
    state.items.forEach((e, i) => {
      tbody.append(el('tr', {},
        el('td', { class: 'num muted' }, String(startId - i)),
        el('td', { class: 'mono' }, fmtDateTime(e.timestamp)),
        el('td', {}, escapeHtml(e.deviceName)),
        el('td', {}, escapeHtml(e.eventType)),
        el('td', {}, e.employeeId ? el('span', { class: 'pill info' }, escapeHtml(e.employeeId)) : '—'),
        el('td', {}, pill(e.status)),
        el('td', { class: 'mono muted' }, escapeHtml(e.rawData ?? '')),
        el('td', { class: 'muted' }, escapeHtml(e.message ?? ''))));
    });
  }

  function renderPager() {
    pager.replaceChildren();
    const info = el('span', { class: 'page-info' },
      `Trang ${state.page} / ${Math.max(state.totalPages, 1)}`);
    const first = el('button', {
      disabled: state.page <= 1 ? '' : null,
      onclick: () => goTo(1),
    }, '«');
    const prev = el('button', {
      disabled: state.page <= 1 ? '' : null,
      onclick: () => goTo(state.page - 1),
    }, '‹');
    const next = el('button', {
      disabled: state.page >= state.totalPages ? '' : null,
      onclick: () => goTo(state.page + 1),
    }, '›');
    const last = el('button', {
      disabled: state.page >= state.totalPages ? '' : null,
      onclick: () => goTo(state.totalPages),
    }, '»');

    // windowed page numbers
    const nums = pageWindow(state.page, state.totalPages, 5);
    pager.append(info, first, prev,
      ...nums.map(n => n === '…'
        ? el('span', { class: 'page-info' }, '…')
        : el('button', {
            class: n === state.page ? 'active' : '',
            onclick: () => goTo(n),
          }, String(n))),
      next, last);
  }

  function goTo(page) {
    state.page = page;
    load();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  async function exportCsv() {
    try {
      const res = await api.events({
        page: 1, pageSize: 500,
        device: fDevice.value || null,
        employee: fEmployee.value || null,
        eventType: fType.value || null,
        status: fStatus.value || null,
        from: fFrom.value ? `${fFrom.value}T00:00:00` : null,
        to: fTo.value ? `${fTo.value}T23:59:59` : null,
      });
      const header = 'Timestamp,Device,Event,Employee,Status,RawData,Message\n';
      const rows = res.items.map(e =>
        [e.timestamp, e.deviceName, e.eventType, e.employeeId ?? '',
         e.status, e.rawData ?? '', e.message ?? '']
          .map(csvCell).join(',')).join('\n');
      const blob = new Blob(['﻿' + header + rows], { type: 'text/csv;charset=utf-8' });
      const a = el('a', {
        href: URL.createObjectURL(blob),
        download: `esd-events-${new Date().toISOString().slice(0, 10)}.csv`,
      });
      document.body.append(a);
      a.click();
      a.remove();
      toast(`Đã xuất ${res.items.length} bản ghi`, 'success');
    } catch (ex) {
      toast(`Xuất CSV thất bại: ${ex.message}`, 'error');
    }
  }

  // auto-refresh while the view is open
  timer = setInterval(load, 30_000);

  await load();

  return () => clearInterval(timer);
}

function pageWindow(current, total, size) {
  if (total <= size + 2) return range(1, total);
  const half = Math.floor(size / 2);
  let start = Math.max(1, Math.min(current - half, total - size - 1));
  let end = Math.min(total, start + size - 1);
  start = Math.max(1, end - size + 1);
  const pages = range(start, end);
  if (start > 1) pages.unshift('…');
  if (end < total) pages.push('…');
  return pages;
}

function range(a, b) {
  return Array.from({ length: Math.max(0, b - a + 1) }, (_, i) => a + i);
}

function csvCell(v) {
  const s = String(v ?? '');
  return /[",\n]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
}
