import { StrictMode, useState, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { extractNotes, MAX_NOTES_LENGTH, validateNotes, type ExtractedNote, type FinancialFact } from './api.ts';
import { config } from './config.ts';
import './styles.css';

// All model output is rendered as plain React text (escaped); nothing is stored in the browser.

type Status =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; result: ExtractedNote }
  | { kind: 'error'; message: string };

const NOT_STATED = 'Not stated';
const NONE_MENTIONED = 'None mentioned';

function App() {
  const [notes, setNotes] = useState('');
  const [touched, setTouched] = useState(false);
  const [status, setStatus] = useState<Status>({ kind: 'idle' });

  const validationError = validateNotes(notes);
  const isLoading = status.kind === 'loading';
  const isOverLimit = notes.trim().length > MAX_NOTES_LENGTH;
  const canSubmit = config.ok && validationError === null && !isLoading;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (!config.ok || validationError !== null || isLoading) {
      return;
    }

    setStatus({ kind: 'loading' });
    const outcome = await extractNotes(config.apiBaseUrl, notes);
    setStatus(outcome.ok ? { kind: 'success', result: outcome.data } : { kind: 'error', message: outcome.message });
  }

  return (
    <main className="page">
      <section className="card">
        <p className="eyebrow">IBERNIA · ADVISER TOOLS</p>
        <h1>Advisor Note Extractor</h1>
        <p className="muted">
          Paste meeting notes to extract goals, financial facts, future events, and risks or questions for your
          review.
        </p>

        {!config.ok && (
          <div className="alert alert-error" role="alert">
            <strong>Configuration error.</strong> {config.error}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <label htmlFor="notes">Meeting notes</label>
          <textarea
            id="notes"
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
            onBlur={() => setTouched(true)}
            placeholder="Paste adviser/client meeting notes here..."
            rows={12}
            aria-describedby="notes-help"
            aria-invalid={touched && validationError !== null}
          />
          <div id="notes-help" className="field-help">
            <span className={touched && validationError ? 'field-error' : undefined}>
              {touched && validationError ? validationError : 'Use synthetic or approved notes only.'}
            </span>
            <span className={isOverLimit ? 'counter counter-over' : 'counter'}>
              {notes.length.toLocaleString('en-GB')} / {MAX_NOTES_LENGTH.toLocaleString('en-GB')}
            </span>
          </div>

          <button type="submit" disabled={!canSubmit} aria-busy={isLoading}>
            {isLoading ? 'Extracting…' : 'Extract information'}
          </button>
        </form>

        {status.kind === 'error' && (
          <div className="alert alert-error" role="alert">
            {status.message}
          </div>
        )}

        {status.kind === 'success' && <Results result={status.result} />}
      </section>
    </main>
  );
}

function Results({ result }: { result: ExtractedNote }) {
  return (
    <section className="results" aria-label="Extracted information">
      <p className="disclaimer">AI-extracted — verify against the original notes</p>

      {result.warnings.length > 0 && (
        <div className="alert alert-warning" role="status">
          <strong>Warnings</strong>
          <ul>
            {result.warnings.map((warning, index) => (
              <li key={index}>{warning}</li>
            ))}
          </ul>
        </div>
      )}

      <ListSection title="Goals" items={result.goals} />

      <h2>Financial facts</h2>
      {result.financialFacts.length === 0 ? (
        <p className="empty">{NONE_MENTIONED}</p>
      ) : (
        <div className="facts">
          {result.financialFacts.map((fact, index) => (
            <FactCard key={index} fact={fact} />
          ))}
        </div>
      )}

      <ListSection title="Future events" items={result.futureEvents} />
      <ListSection title="Risks and questions" items={result.risksOrQuestions} />
    </section>
  );
}

function ListSection({ title, items }: { title: string; items: string[] }) {
  return (
    <>
      <h2>{title}</h2>
      {items.length === 0 ? (
        <p className="empty">{NONE_MENTIONED}</p>
      ) : (
        <ul className="list">
          {items.map((item, index) => (
            <li key={index}>{item}</li>
          ))}
        </ul>
      )}
    </>
  );
}

function FactCard({ fact }: { fact: FinancialFact }) {
  return (
    <article className="fact">
      <header className="fact-header">
        <span className="fact-label">{fact.label}</span>
        <span className="tag">{fact.category}</span>
        {fact.isApproximate && <span className="tag tag-approx">Approximate</span>}
      </header>
      <dl className="fact-details">
        <div>
          <dt>Amount</dt>
          <dd>{formatAmount(fact.amount, fact.currency)}</dd>
        </div>
        <div>
          <dt>Currency</dt>
          <dd>{fact.currency ?? NOT_STATED}</dd>
        </div>
        <div>
          <dt>Period</dt>
          <dd>{formatPeriod(fact.period)}</dd>
        </div>
      </dl>
      <blockquote className="quote">
        <span className="quote-label">From the notes:</span> “{fact.sourceText}”
      </blockquote>
    </article>
  );
}

function formatAmount(amount: number | null, currency: string | null): string {
  if (amount === null) {
    return NOT_STATED;
  }

  // Whole amounts without decimals (£420,000); anything else with two (£2,500.50).
  const digits = Number.isInteger(amount) ? 0 : 2;
  const fraction = { minimumFractionDigits: digits, maximumFractionDigits: digits };

  if (currency !== null) {
    try {
      return new Intl.NumberFormat('en-GB', { style: 'currency', currency, ...fraction }).format(amount);
    } catch {
      // Unknown currency code: show the number and the code as given.
    }
  }

  const number = new Intl.NumberFormat('en-GB', fraction).format(amount);
  return currency === null ? number : `${number} ${currency}`;
}

function formatPeriod(period: FinancialFact['period']): string {
  switch (period) {
    case 'annual':
      return 'Per year';
    case 'monthly':
      return 'Per month';
    default:
      return NOT_STATED;
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
