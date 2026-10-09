// ═══ Dashboard view — KPIs, trend chart, station cards, operators ═══

import { $, $$, el, pill, fmtNumber, fmtPercent, fmtDateTime, timeAgo, toast } from '../ui.js';
import { api } from '../api.js';
import { LiveEventStream } from '../sse.js';
import { barChart, legend } from '../charts.js';

export async function render(container) {
  const state = { summary: null, trend: [], stations: [], employees: [] };
  let refreshTimer = null;
  let stream = null;

  // ── Layout ─────────────────────────────────────────
  const kpiGrid = el('div', { class: 'grid kpi-4' });
  const trendCard = el('div', { class: 'card' },
    el('div', { class: 'card-header' },
      el('span', {}, 'Biểu đồ xu hướng theo giờ'),
      el('div', { class: 'card-tools' }, legend([['OK', 'var(--chart-green)'], ['NG', 'var(--chart-red)']]))),
    el('div', { class: 'card-body chart-host' }));

  const bottomGrid = el('div', { class: 'grid', style: 'grid-template-columns: 1.4fr 1fr; margin-top:20px;' });
  const stationCard = el('div', { class: 'card' },
    el('div', { class: 'card-header' }, 'Trạng thái trạm'),
    el('div', { class: 'card-body', style: 'display:grid;gap:12px;' }));
  const empCard = el('div', { class: 'card' },
    el('div', { class: 'card-header' }, 'Top operator hôm nay'),
    el('div', { class: 'table-wrap' },
      el('table', { class: 'data' },
        el('thead', {}, el('tr', {},
          el('th', {}, 'Nhân viên'), el('th', { class: 'num' }, 'Sự kiện'),
          el('th', { class: 'num' }, 'OK'), el('th', { class: 'num' }, 'NG'),
          el('th', {}, 'Tuân thủ'))),
        el('tbody', { class: 'emp-body' })))));

  bottomGrid.append(stationCard, empCard);
  container.append(kpiGrid, trendCard, bottomGrid);

  // ── Renderers ──────────────────────────────────────
  function renderKpis() {
    const s = state.summary;
    if (!s) return;
    kpiGrid.replaceChildren(
      kpiCard('Sự kiện hôm nay', fmtNumber(s.totalEvents), `${fmtNumber(s.okEvents)} OK · ${fmtNumber(s.ngEvents)} NG`),
      kpiCard('Tuân thủ', fmtPercent(s.compliancePercent), 'OK / (OK + NG + Warning)',
        s.compliancePercent >= 95 ? 'up' : s.compliancePercent >= 80 ? null : 'down',
        s.compliancePercent >= 95 ? '≥ 95%' : 'dưới mục tiêu 95%'),
      kpiCard('Trạm hoạt động', fmtNumber(s.activeStations), `${s.onlineDevices}/${s.totalDevices} thiết bị kết nối`),
      kpiCard('Cảnh báo', fmtNumber(s.alarmCount), `Hàng đợi DB: ${fmtNumber(s.dbQueueLength)}`,
        s.alarmCount > 0 ? 'down' : null, s.alarmCount > 0 ? 'cần kiểm tra' : 'không có báo động'));
  }

  function kpiCard(label, value, sub, trendDir = null, trendText = null) {
    return el('div', { class: 'card kpi-card' },
      el('div', { class: 'kpi-label' }, label),
      el('div', { class: 'kpi-value' }, value),
      trendDir ? el('div', { class: `kpi-trend ${trendDir}` }, trendText ?? '') : null,
      el('div', { class: 'kpi-sub' }, sub));
  }

  function renderTrend() {
    const host = $('.chart-host', trendCard);
    host.replaceChildren();
    if (!state.trend.length) {
      host.append(el('div', { class: 'empty-state' }, 'Chưa có dữ liệu hôm nay'));
      return;
    }
    // Pad empty hours so the chart always spans the workday
    const data = [];
    for (let h = 0; h < 24; h++) {
      const p = state.trend.find(t => t.hour === h);
      data.push({
        label: `${String(h).padStart(2, '0')}h`,
        ok: p?.ok ?? 0,
        ng: p?.ng ?? 0,
        total: p?.total ?? 0,
      });
    }
    host.append(barChart(data, { height: 230 }));
  }

  function renderStations() {
    const host = $('.card-body', stationCard);
    host.replaceChildren();
    if (!state.stations.length) {
      host.append(el('div', { class: 'empty-state' },
        'Chưa có trạm nào — kiểm tra cấu hình thiết bị.'));
      return;
    }
    for (const st of state.stations) {
      const tested = st.ok + st.ng + st.warning;
      const okW = tested ? (st.ok / Math.max(st.total, 1)) * 100 : 0;
      const ngW = tested ? (st.ng / Math.max(st.total, 1)) * 100 : 0;
      const waW = tested ? (st.warning / Math.max(st.total, 1)) * 100 : 0;

      host.append(el('div', { class: 'card station-card' },
        el('div', { class: 'card-body' },
          el('div', { class: 'station-head' },
            el('div', {},
              el('div', { class: 'station-name' }, st.deviceName),
              el('div', { class: 'station-meta' },
                st.lastEventAt ? `Sự kiện cuối: ${timeAgo(st.lastEventAt)}` : 'Chưa có sự kiện')),
            st.isConnected
              ? pill(st.state ?? 'Idle')
              : pill('offline', 'OFFLINE')),
          el('div', { class: 'station-stats' },
            stationStat(st.ok, 'OK', 'var(--ok)'),
            stationStat(st.ng, 'NG', 'var(--ng)'),
            stationStat(st.warning, 'WARN', 'var(--warn)'),
            stationStat(st.total, 'TỔNG', 'var(--text-secondary)')),
          el('div', { class: 'progress' },
            el('span', { class: 'seg-ok', style: `width:${okW}%` }),
            el('span', { class: 'seg-ng', style: `width:${ngW}%` }),
            el('span', { class: 'seg-warn', style: `width:${waW}%` })),
          el('div', { class: 'flex items-center gap-2' },
            el('span', { class: 'muted', style: 'font-size:12px' },
              `Tuân thủ ${fmtPercent(st.compliancePercent)}`),
            st.lastEventType ? pill(st.lastEventType, `Cuối: ${st.lastEventType}`) : null))));
    }
  }

  function stationStat(value, label, color) {
    return el('div', { class: 'station-stat' },
      el('b', { style: `color:${color}` }, String(value)),
      el('span', {}, label));
  }

  function renderEmployees() {
    const tbody = $('.emp-body', empCard);
    tbody.replaceChildren();
    if (!state.employees.length) {
      tbody.append(el('tr', {}, el('td', { colspan: 5, class: 'empty-state' },
        'Chưa có dữ liệu operator hôm nay')));
      return;
    }
    for (const e of state.employees) {
      tbody.append(el('tr', {},
        el('td', {}, el('div', { class: 'flex items-center gap-2' },
          el('span', { class: 'pill info' }, e.employeeId)),
          el('div', { class: 'muted', style: 'font-size:11px' }, timeAgo(e.lastEvent))),
        el('td', { class: 'num' }, fmtNumber(e.total)),
        el('td', { class: 'num' }, fmtNumber(e.ok)),
        el('td', { class: 'num' }, fmtNumber(e.ng)),
        el('td', {}, pill(e.compliancePercent >= 95 ? 'Ok' : e.compliancePercent >= 80 ? 'Warning' : 'Ng',
          fmtPercent(e.compliancePercent)))));
    }
  }

  // ── Data loading ───────────────────────────────────
  async function loadAll() {
    try {
      const [summary, trend, stations, employees] = await Promise.all([
        api.dashboardSummary(),
        api.hourlyTrend(),
        api.stations(),
        api.employees(null, 8),
      ]);
      state.summary = summary;
      state.trend = trend;
      state.stations = stations;
      state.employees = employees;
      renderKpis(); renderTrend(); renderStations(); renderEmployees();
    } catch (ex) {
      console.error(ex);
      toast(`Lỗi tải dashboard: ${ex.message}`, 'error');
    }
  }

  await loadAll();
  refreshTimer = setInterval(loadAll, 10_000);

  // Instant updates via SSE — refresh aggregates on each device event
  stream = new LiveEventStream({
    onEvent: () => {
      // soft refresh (debounced by the browser's render cycle)
      loadAll();
    },
  });
  stream.connect();

  // cleanup on route change
  return () => {
    clearInterval(refreshTimer);
    stream?.close();
  };
}
