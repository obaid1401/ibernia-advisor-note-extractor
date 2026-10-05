// Client for POST /api/notes/extract. Never logs notes or responses.

export type FinancialFact = {
  category: string;
  label: string;
  amount: number | null;
  currency: string | null;
  period: 'annual' | 'monthly' | null;
  isApproximate: boolean;
  sourceText: string;
};

export type ExtractedNote = {
  goals: string[];
  financialFacts: FinancialFact[];
  futureEvents: string[];
  risksOrQuestions: string[];
  warnings: string[];
};

// RFC 7807 ProblemDetails as returned by the API; `detail` is a user-safe message.
export type ProblemDetails = {
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
};

export type ExtractResult = { ok: true; data: ExtractedNote } | { ok: false; message: string };

export const MAX_NOTES_LENGTH = 10_000;
export const REQUEST_TIMEOUT_MS = 45_000; // longer than the API's own AI_TIMEOUT_SECONDS

// Mirrors the API's validation (trimmed, non-empty, at most 10,000 characters). The API checks again.
export function validateNotes(notes: string): string | null {
  const trimmed = notes.trim();
  if (trimmed.length === 0) {
    return 'Please enter meeting notes.';
  }

  if (trimmed.length > MAX_NOTES_LENGTH) {
    return 'Notes must be 10,000 characters or fewer.';
  }

  return null;
}

export async function extractNotes(
  apiBaseUrl: string,
  notes: string,
  timeoutMs: number = REQUEST_TIMEOUT_MS,
): Promise<ExtractResult> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/api/notes/extract`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ notes }),
      signal: AbortSignal.timeout(timeoutMs),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'TimeoutError') {
      return { ok: false, message: 'The request took too long. Please try again.' };
    }

    return {
      ok: false,
      message: 'Could not reach the extraction service. Check that it is running and try again.',
    };
  }

  const body: unknown = await response.json().catch(() => null);

  if (!response.ok) {
    const detail = isProblemDetails(body) ? body.detail : undefined;
    return {
      ok: false,
      message: detail ?? `The extraction service returned an error (HTTP ${response.status}). Please try again.`,
    };
  }

  if (!isExtractedNote(body)) {
    return { ok: false, message: 'The extraction service returned an unexpected response. Please try again.' };
  }

  return { ok: true, data: body };
}

function isProblemDetails(value: unknown): value is ProblemDetails & { detail: string } {
  return typeof value === 'object' && value !== null && typeof (value as ProblemDetails).detail === 'string';
}

// Light shape check: the API already validates the content, but the UI must not crash on a surprise.
function isExtractedNote(value: unknown): value is ExtractedNote {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const note = value as Record<string, unknown>;
  const isStringList = (list: unknown) => Array.isArray(list) && list.every((item) => typeof item === 'string');

  return (
    isStringList(note.goals) &&
    isStringList(note.futureEvents) &&
    isStringList(note.risksOrQuestions) &&
    isStringList(note.warnings) &&
    Array.isArray(note.financialFacts) &&
    note.financialFacts.every(isFinancialFact)
  );
}

function isFinancialFact(value: unknown): value is FinancialFact {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const fact = value as Record<string, unknown>;
  return (
    typeof fact.category === 'string' &&
    typeof fact.label === 'string' &&
    (fact.amount === null || typeof fact.amount === 'number') &&
    (fact.currency === null || typeof fact.currency === 'string') &&
    (fact.period === null || fact.period === 'annual' || fact.period === 'monthly') &&
    typeof fact.isApproximate === 'boolean' &&
    typeof fact.sourceText === 'string'
  );
}
