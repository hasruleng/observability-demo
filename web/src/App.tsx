import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';

type Phase = 'idle' | 'starting' | 'pending' | 'completed' | 'failed' | 'error';

type OrderAccepted = {
  orderId: string;
  traceId: string | null;
};

type OrderStatus = {
  orderId: string;
  itemCount: number;
  status: 'pending' | 'completed' | 'failed';
  elapsedMilliseconds: number;
  error: string | null;
};

async function requestJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Request failed with HTTP ${response.status}`);
  }
  return response.json() as Promise<T>;
}

export default function App() {
  const [itemCount, setItemCount] = useState(8);
  const [phase, setPhase] = useState<Phase>('idle');
  const [orderId, setOrderId] = useState<string | null>(null);
  const [traceId, setTraceId] = useState<string | null>(null);
  const [order, setOrder] = useState<OrderStatus | null>(null);
  const [browserElapsedMilliseconds, setBrowserElapsedMilliseconds] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const journeyStartedAt = useRef<number | null>(null);

  useEffect(() => {
    if (!orderId) {
      return;
    }

    let active = true;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const status = await requestJson<OrderStatus>(`/api/orders/${orderId}`);
        if (!active) {
          return;
        }

        setOrder(status);
        if (status.status === 'pending') {
          timer = window.setTimeout(poll, 350);
        } else {
          if (journeyStartedAt.current !== null) {
            setBrowserElapsedMilliseconds(performance.now() - journeyStartedAt.current);
          }
          setPhase(status.status);
          setError(status.error);
        }
      } catch (requestError) {
        if (active) {
          setPhase('error');
          setError(requestError instanceof Error ? requestError.message : 'Order status unavailable.');
        }
      }
    };

    void poll();
    return () => {
      active = false;
      if (timer !== undefined) {
        window.clearTimeout(timer);
      }
    };
  }, [orderId]);

  async function startOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPhase('starting');
    setOrderId(null);
    setTraceId(null);
    setOrder(null);
    setBrowserElapsedMilliseconds(null);
    setError(null);
    journeyStartedAt.current = performance.now();

    try {
      const accepted = await requestJson<OrderAccepted>('/api/orders', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ itemCount }),
      });
      setTraceId(accepted.traceId);
      setOrderId(accepted.orderId);
      setPhase('pending');
    } catch (requestError) {
      setPhase('error');
      setError(requestError instanceof Error ? requestError.message : 'Order could not be started.');
    }
  }

  const statusLabel = {
    idle: 'Ready',
    starting: 'Submitting',
    pending: 'In progress',
    completed: 'Completed',
    failed: 'Failed',
    error: 'Unavailable',
  }[phase];

  return (
    <main className="page-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="Order Flow home">
          <span className="brand-mark" aria-hidden="true">O</span>
          <span>Order Flow</span>
        </a>
        <div className="environment"><span /> LOCAL / SYNTHETIC</div>
      </header>

      <section className="intro">
        <p className="eyebrow">A small system, seen end to end</p>
        <h1>Place an order<span>.</span></h1>
        <p className="intro-copy">Follow a synthetic order across the API, worker, and fulfillment service.</p>
      </section>

      <section className="workbench" aria-label="Order controls and result">
        <div className="control-pane">
          <div className="section-index"><span>01</span> START AN ORDER</div>
          <form onSubmit={startOrder}>
            <label htmlFor="item-count">Items in order</label>
            <div className="form-row">
              <input
                id="item-count"
                type="number"
                min={1}
                max={50}
                value={itemCount}
                onChange={(event) => setItemCount(Number(event.target.value))}
                required
              />
              <button type="submit" disabled={phase === 'starting' || phase === 'pending'}>
                {phase === 'starting' || phase === 'pending' ? 'Placing…' : 'Place order'}
                <span aria-hidden="true">↗</span>
              </button>
            </div>
            <p className="field-note">Allowed range: 1–50 synthetic items</p>
          </form>

          <ol className="service-path" aria-label="Request path">
            <li><span>01</span><strong>Browser</strong></li>
            <li><span>02</span><strong>Order API</strong></li>
            <li><span>03</span><strong>Worker</strong></li>
            <li><span>04</span><strong>Fulfillment</strong></li>
          </ol>
        </div>

        <div className="result-pane" aria-live="polite">
          <div className="result-heading">
            <div className="section-index"><span>02</span> LIVE RESULT</div>
            <span className={`status status-${phase}`}><i />{statusLabel}</span>
          </div>

          {order ? (
            <dl className="measurements">
              <div>
                <dt>Order duration</dt>
                <dd>{((browserElapsedMilliseconds ?? order.elapsedMilliseconds) / 1000).toFixed(2)}<small> s</small></dd>
              </div>
              <div>
                <dt>Items</dt>
                <dd>{order.itemCount}</dd>
              </div>
              <div>
                <dt>Outcome</dt>
                <dd className={`outcome outcome-${order.status}`}>{order.status}</dd>
              </div>
            </dl>
          ) : (
            <div className="empty-result">
              <div className="pulse-line" aria-hidden="true"><span /><span /><span /><span /></div>
              <p>{phase === 'starting' ? 'Submitting order' : 'No order in this session'}</p>
            </div>
          )}

          <div className="trace-row">
            <span>TRACE ID</span>
            <code>{traceId ?? '—'}</code>
          </div>
          {error && <p className="error-message" role="alert">{error}</p>}
          {orderId && <p className="order-id">Order ID <code>{orderId}</code></p>}
        </div>
      </section>

      <footer className="footnote">
        <span>SYNTHETIC ORDER FLOW</span>
        <span>No customer or production data</span>
        <span>.NET 10 · OpenTelemetry</span>
      </footer>
    </main>
  );
}
