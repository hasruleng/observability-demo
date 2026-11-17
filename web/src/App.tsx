import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';

type Phase = 'idle' | 'starting' | 'pending' | 'completed' | 'failed' | 'error';

type JourneyAccepted = {
  journeyId: string;
  traceId: string | null;
};

type JourneyStatus = {
  journeyId: string;
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
  const [journeyId, setJourneyId] = useState<string | null>(null);
  const [traceId, setTraceId] = useState<string | null>(null);
  const [journey, setJourney] = useState<JourneyStatus | null>(null);
  const [browserElapsedMilliseconds, setBrowserElapsedMilliseconds] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const journeyStartedAt = useRef<number | null>(null);

  useEffect(() => {
    if (!journeyId) {
      return;
    }

    let active = true;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const status = await requestJson<JourneyStatus>(`/api/journeys/${journeyId}`);
        if (!active) {
          return;
        }

        setJourney(status);
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
          setError(requestError instanceof Error ? requestError.message : 'Journey status unavailable.');
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
  }, [journeyId]);

  async function startJourney(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPhase('starting');
    setJourneyId(null);
    setTraceId(null);
    setJourney(null);
    setBrowserElapsedMilliseconds(null);
    setError(null);
    journeyStartedAt.current = performance.now();

    try {
      const accepted = await requestJson<JourneyAccepted>('/api/journeys', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ itemCount }),
      });
      setTraceId(accepted.traceId);
      setJourneyId(accepted.journeyId);
      setPhase('pending');
    } catch (requestError) {
      setPhase('error');
      setError(requestError instanceof Error ? requestError.message : 'Journey could not be started.');
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
        <a className="brand" href="/" aria-label="Journey Lab home">
          <span className="brand-mark" aria-hidden="true">J</span>
          <span>Journey Lab</span>
        </a>
        <div className="environment"><span /> LOCAL / SYNTHETIC</div>
      </header>

      <section className="intro">
        <p className="eyebrow">A small system, seen end to end</p>
        <h1>Create contract<span>.</span></h1>
        <p className="intro-copy">Follow one request across the API, worker, and dependency.</p>
      </section>

      <section className="workbench" aria-label="Journey controls and result">
        <div className="control-pane">
          <div className="section-index"><span>01</span> START A JOURNEY</div>
          <form onSubmit={startJourney}>
            <label htmlFor="item-count">Synthetic line items</label>
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
                {phase === 'starting' || phase === 'pending' ? 'Running…' : 'Start journey'}
                <span aria-hidden="true">↗</span>
              </button>
            </div>
            <p className="field-note">Allowed range: 1–50 synthetic items</p>
          </form>

          <ol className="service-path" aria-label="Request path">
            <li><span>01</span><strong>Browser</strong></li>
            <li><span>02</span><strong>Journey API</strong></li>
            <li><span>03</span><strong>Worker</strong></li>
            <li><span>04</span><strong>Team B</strong></li>
          </ol>
        </div>

        <div className="result-pane" aria-live="polite">
          <div className="result-heading">
            <div className="section-index"><span>02</span> LIVE RESULT</div>
            <span className={`status status-${phase}`}><i />{statusLabel}</span>
          </div>

          {journey ? (
            <dl className="measurements">
              <div>
                <dt>Journey duration</dt>
                <dd>{((browserElapsedMilliseconds ?? journey.elapsedMilliseconds) / 1000).toFixed(2)}<small> s</small></dd>
              </div>
              <div>
                <dt>Line items</dt>
                <dd>{journey.itemCount}</dd>
              </div>
              <div>
                <dt>Outcome</dt>
                <dd className={`outcome outcome-${journey.status}`}>{journey.status}</dd>
              </div>
            </dl>
          ) : (
            <div className="empty-result">
              <div className="pulse-line" aria-hidden="true"><span /><span /><span /><span /></div>
              <p>{phase === 'starting' ? 'Submitting journey' : 'No journey in this session'}</p>
            </div>
          )}

          <div className="trace-row">
            <span>TRACE ID</span>
            <code>{traceId ?? '—'}</code>
          </div>
          {error && <p className="error-message" role="alert">{error}</p>}
          {journeyId && <p className="journey-id">Journey <code>{journeyId}</code></p>}
        </div>
      </section>

      <footer className="footnote">
        <span>LOCAL DEMO</span>
        <span>No customer or production data</span>
        <span>.NET 10 · OpenTelemetry</span>
      </footer>
    </main>
  );
}
