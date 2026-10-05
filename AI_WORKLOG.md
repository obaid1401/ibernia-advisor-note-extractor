# AI Work Log

Keep this concise. We want to understand how you used AI-assisted engineering tools and how you validated their output.

Tool for all entries so far: **Claude Code** (VS Code extension). Entries are chronological.

## 1. Repository inspection

### Task
Understand the starter repository before any changes.

### Prompt or instruction
Inspect the entire starter repository without modifying files and explain the backend, frontend, API and models, service abstraction, tests, CI, Docker, configuration, what the starter already implements, and what the assessment still requires.

### Outcome
Claude read every tracked file and summarised the starter. Notable findings:
- `NoteExtractionService` throws `NotImplementedException`, so the endpoint currently returns an unhandled 500.
- `FinancialFacts` is `IReadOnlyDictionary<string, decimal>`, which cannot represent missing or approximate values, currency, or period.
- The frontend has no `tsconfig.json`, no type checking, and all dependencies are pinned to `"latest"`; `package-lock.json` is untracked.
- CORS allows any origin; there is no exception handling, input length limit, or `HttpClient` setup.
- The Dockerfile has no `.dockerignore` and runs as root; Compose only covers the API.
- The test project contains a single placeholder test and lacks `Microsoft.AspNetCore.Mvc.Testing`.

### Your decision
Accepted as the baseline for design. No files changed.

## 2. Initial design proposal

### Task
Design the simplest implementation that satisfies the README within the 3–4 hour timebox and the project rules.

### Prompt or instruction
Read `.claude/rules/assessment.md`, then propose the backend/frontend behaviour, request/response JSON, `FinancialFacts` structure, LLM integration, test seam, failure handling, logging, configuration, CI/Docker/deployment changes, and documentation plan. No file changes.

### Outcome
- Claude found the rules file at `.cluade/rules/assessment.md` (misspelt folder), which Claude Code would not auto-load.
- Proposed replacing the `FinancialFacts` dictionary with a list of `FinancialFact` objects (nullable amount, currency, period, `isApproximate`, verbatim `sourceText`) and a server-side grounding check.
- Proposed an `ILlmClient`-style seam so tests use a fake provider.
- Proposed ProblemDetails error mapping, a 10,000-character input limit, a safe-logging policy, CORS restriction, and two Railway services.
- Proposed Anthropic Claude via the official SDK as the provider.

### Your decision
- Renamed the rules folder to `.claude/` myself.
- Accepted the structured `FinancialFact` design, grounding check, test seam, error strategy, input limit, and logging policy.
- **Rejected the Anthropic provider** — chose Google Gemini instead (next entry).

## 3. Revised design for Google Gemini

### Task
Revise the design for the Gemini API and simplify anything unnecessary for the timebox.

### Prompt or instruction
Use the Gemini API with `gemini-3.8-flash`, key from `AI_API_KEY`, structured JSON output, and the free tier if available. Explain the integration, package choice, exact request, validation, test isolation, error handling, prompt injection, and non-invention of facts; review the previous design for unnecessary complexity.

### Outcome
Claude checked Google's current documentation rather than relying on memory and found:
- Google now recommends the new **Interactions API**, which **stores interactions server-side by default** (1 day free tier / 55 days paid); `generateContent` is described as legacy but fully supported and is stateless.
- `gemini-3.8-flash` has a free tier, but **free-tier content may be used to improve Google's products**.
- For Gemini 3 models Google strongly recommends **keeping temperature at the default 1.0**.
- The API key can be sent in the `x-goog-api-key` header (keeps it out of URLs and logs).
- An official `Google.GenAI` .NET SDK exists, but its support for `responseJsonSchema` on `generateContent` could not be confirmed.

Claude proposed `generateContent` with a raw typed `HttpClient`, and these simplifications to its own earlier design: no SDK, no automatic retries, no app-level rate limiter, per-request key check instead of startup validation, no Docker-build CI job, fewer frontend and test files. It also flagged that `docker-compose.yml` passes `AI_MODEL=${AI_MODEL:-}`, where an empty value would override the default model.

### Your decision
- Accepted Gemini `generateContent` with raw `HttpClient` (stateless, no extra dependency).
- Accepted **synthetic test data only**, because of the free-tier data-use terms.
- Accepted leaving temperature at the default rather than the common low-temperature setting for extraction, following Google's guidance.
- Still to confirm on the first live call: `thinkingLevel: "low"` and the schema keywords.

## 4. Phase-based workflow

### Task
Agree how implementation will proceed.

### Prompt or instruction
Work in explicit phases: implement only the assigned phase, run tests/builds, update documentation progressively, review the git diff, and never commit without explicit instruction. Never fabricate results.

### Outcome
Claude proposed eight phases (design doc → core extraction logic → Gemini client → API wiring → frontend → CI/Docker → deploy → wrap-up).

### Your decision
Adopted phase-based delivery with human review before each commit.

## 5. Persisting the design in the repository

### Task
Make repository documentation the persistent source of truth instead of the chat history.

### Prompt or instruction
Update `docs/DESIGN.md` with the complete approved design and label it as the canonical technical design; add a rule to `.claude/rules/assessment.md` making `docs/DESIGN.md` the source of truth; record the decision here. Do not implement anything or claim anything is implemented, tested, or deployed.

### Outcome
- The design had previously been discussed only in chat. It is now persisted in `docs/DESIGN.md`, marked as the canonical design and as an approved baseline that is **not yet implemented, tested, or deployed**.
- Added a "Design Source of Truth" section to `.claude/rules/assessment.md`: read `docs/DESIGN.md` before every implementation phase; do not silently change approved decisions; record any design change in `docs/DESIGN.md` in the same phase; `AI_WORKLOG.md` is the chronological history.
- No application code, tests, CI, Docker, or deployment configuration changed.

### Your decision
Future implementation phases use `docs/DESIGN.md` as the repository-level source of truth.

### Verification
Documentation review only: checked that the three files are consistent with each other. No builds or tests run, since no code changed.

## 6. Design corrections: `responseSchema` and verbatim `sourceText`

### Task
Correct two points in `docs/DESIGN.md` after my review.

### Prompt or instruction
1. Use the documented `generateContent` field `responseSchema` instead of `responseJsonSchema`, keeping `responseMimeType: "application/json"`, the schema structure, and server-side validation.
2. Make the `sourceText` rule consistent: a verbatim quote; the server may normalise only harmless whitespace/newline differences when grounding; substantive changes must not be accepted as verbatim; ungrounded facts are still removed with a warning.

### Outcome
- Claude pointed out that `responseSchema` takes Gemini's `Schema` object (an OpenAPI subset), not full JSON Schema. Renaming the field alone would have left an incompatible schema, so the schema in `DESIGN.md` was rewritten in that dialect with the same structure: upper-case types, `nullable: true` instead of type arrays, `format: "enum"` for string enums, and no `additionalProperties`. The docs page was partly truncated, so the exact keywords remain on the "verify on first live call" list.
- Removed the now-invalid reason "no reliance on SDK support for `responseJsonSchema`" from the raw-`HttpClient` rationale.
- Grounding now normalises only whitespace and line breaks. The earlier wording also ignored letter case and curly quotes; that is removed, so any other difference makes a fact ungrounded.
- Updated the API field table, schema description, validation rules, security section, and planned test cases to match.
- Added a known limitation: strict matching may drop a correct fact if the model changes typography (e.g. curly → straight apostrophe). It fails safe (warning, not invention) but should be monitored.

### Your decision
Requested both corrections. The schema dialect rewrite was Claude's follow-on change to keep the design valid and is pending my review. Entry 3 above still mentions `responseJsonSchema` because that was the design at the time.

### Verification
Documentation review only; no code, tests, CI, Docker, or deployment configuration changed.
