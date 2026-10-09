// ═══ SSE client — live event stream from GET /api/events/live ═══
// Auto-reconnects with a small backoff. Emits:
//   onEvent(payload)   — one decoded device event
//   onStatus(state)    — 'live' | 'connecting' | 'offline'

export class LiveEventStream {
  constructor({ onEvent, onStatus } = {}) {
    this.onEvent  = onEvent  ?? (() => {});
    this.onStatus = onStatus ?? (() => {});
    this._es = null;
    this._closed = false;
    this._retryMs = 1_000;
  }

  connect() {
    if (this._closed) return;
    this.onStatus('connecting');

    let es;
    try {
      es = new EventSource('/api/events/live');
    } catch {
      this._scheduleReconnect();
      return;
    }
    this._es = es;

    es.addEventListener('esd', (msg) => {
      try {
        this.onEvent(JSON.parse(msg.data));
        this._retryMs = 1_000; // reset backoff on success
        this.onStatus('live');
      } catch { /* ignore malformed frame */ }
    });

    es.onopen = () => { this._retryMs = 1_000; this.onStatus('live'); };

    es.onerror = () => {
      this.onStatus('offline');
      es.close();
      this._scheduleReconnect();
    };
  }

  _scheduleReconnect() {
    if (this._closed) return;
    setTimeout(() => this.connect(), this._retryMs);
    this._retryMs = Math.min(this._retryMs * 1.6, 10_000);
  }

  close() {
    this._closed = true;
    this._es?.close();
    this._es = null;
  }
}
