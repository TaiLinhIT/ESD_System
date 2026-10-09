// ═══ API client — typed wrapper around the ESD.API REST endpoints ═══

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

export class ApiClient {
  /** @param {string} baseUrl — leave empty to use same-origin /api proxy */
  constructor(baseUrl = '') {
    this.base = baseUrl.replace(/\/$/, '');
  }

  async request(path, { params, signal } = {}) {
    const url = new URL(this.base + path, window.location.origin);
    if (params) {
      for (const [k, v] of Object.entries(params)) {
        if (v != null && v !== '' && !Number.isNaN(v))
          url.searchParams.set(k, v);
      }
    }

    const res = await fetch(url, { signal });
    if (!res.ok) {
      let message = `${res.status} ${res.statusText}`;
      try {
        const body = await res.json();
        if (body?.error) message = body.error;
      } catch { /* non-JSON error body */ }
      throw new ApiError(res.status, message);
    }
    // 204 No Content
    if (res.status === 204) return null;
    return res.json();
  }

  // ── Health & status ─────────────────────────────────────
  status()          { return this.request('/api/status'); }
  health()          { return this.request('/api/health'); }

  // ── Devices ─────────────────────────────────────────────
  devices()         { return this.request('/api/devices'); }
  device(name)      { return this.request(`/api/devices/${encodeURIComponent(name)}`); }

  // ── Events ──────────────────────────────────────────────
  events(params)    { return this.request('/api/events', { params }); }
  eventFilters()    { return this.request('/api/events/filters'); }

  // ── Dashboard ───────────────────────────────────────────
  dashboardSummary(day) { return this.request('/api/dashboard/summary', { params: { day } }); }
  hourlyTrend(day)      { return this.request('/api/dashboard/hourly-trend', { params: { day } }); }
  stations(day)         { return this.request('/api/dashboard/stations', { params: { day } }); }
  employees(day, count) { return this.request('/api/dashboard/employees', { params: { day, count } }); }
}

/** Default client — talks to the same origin (proxied to ESD.API). */
export const api = new ApiClient();
