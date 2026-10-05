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

## 7. Phase 2: core domain, prompt, and response parser

### Task
Implement the domain models, configuration model, extraction exception, prompt construction, Gemini `responseSchema`, and a pure response parser with grounding, plus unit tests. No Gemini calls, no wiring, no later phases.

### Prompt or instruction
Phase 2 instruction: create `FinancialFact`, update `ExtractedNote` (fact list plus server-generated `warnings`), `AiOptions`, `ExtractionException`, `ExtractionPrompt`, and `ExtractionResponseParser`; reject structural/schema violations with `InvalidModelResponse`; drop ungrounded facts with a warning; allow only whitespace/newline normalisation when grounding; ignore model-supplied `warnings`; add at least 17 listed test scenarios without a mocking framework; update `DESIGN.md` and this log with real results only.

### Outcome
Implementation choices made by Claude:
- The parser reads the JSON with `JsonDocument` and checks each rule explicitly instead of deserialising into a type, so every rule is visible and each error names only the field path, never note or model text.
- `AiOptions` is a class, not a record, so a generated `ToString()` cannot print the API key (covered by a test). Blank configuration values are treated as unset, which also covers the `AI_MODEL=` value passed by the current `docker-compose.yml`.
- **Problem found while implementing:** a single regex pass to remove `<notes>` tags can be bypassed — removing the inner tag in `<no<notes>tes>` leaves a new `<notes>`. Tag removal now repeats until nothing changes, with tests for this and similar inputs.
- Category, period, and currency are matched exactly (case-sensitive) and never corrected; strings are only trimmed of surrounding whitespace.
- Warnings are aggregated per reason with a count and contain no note text.
- Unit tests check that the schema's enums and `maxItems` stay in sync with the model constants and parser limits, so the schema sent to Gemini and the server-side validation cannot silently drift apart.
- Added `AiOptionsTests` (not in the requested list) because `AiOptions` contains parsing logic.

### Your decision
Pending my review of Phase 2.

### Verification
- First attempt: `dotnet build apps/api/Ibernia.Assessment.Api.csproj` failed with MSB3021/MSB3027 because a running `Ibernia.Assessment.Api` process (PID 11492) had the output `.exe` locked. There were no compiler errors or warnings. Claude did not stop the process; as an interim check the build and tests were run with `--artifacts-path` in a temporary folder (build succeeded; 101 passed).
- The lock was temporary: after I stopped the process, the exact commands were re-run:
  - `dotnet build apps/api/Ibernia.Assessment.Api.csproj`: **Build succeeded, 0 warnings, 0 errors**;
  - `dotnet test tests/Ibernia.Assessment.Api.Tests/Ibernia.Assessment.Api.Tests.csproj`: **101 passed, 0 failed, 0 skipped**;
  - `git diff --check`: clean (exit 0; only line-ending notices).
- Mutation check: in a temporary copy, the grounding check was disabled; 10 tests failed (all grounding-related), confirming those tests detect the behaviour they claim to cover.
- See entry 9 for the review fixes made before the Phase 2 commit, and the final test count.

## 8. Rules file rewrite (before Phase 2; recorded afterwards)

### Task
Replace `.claude/rules/assessment.md` with a new rule set I supplied.

### Prompt or instruction
Replace the rules file with the exact content provided; modify no other file.

### Outcome
- Claude replaced the file verbatim with nine sections: source of truth, assigned phase only, simplicity, security and data handling, AI-assisted development, testing and verification, documentation, Git, and phase completion.
- The "Design Source of Truth" section described in entry 5 no longer exists under that name; its rules are now in section 1.
- Some earlier rules (3–4 hour timebox, CORS restriction, user-safe errors, structured JSON output, input/output validation, authorization boundary) are no longer in the rules file; they remain in `docs/DESIGN.md`, which the new rules make canonical.

### Your decision
The rule set was my own wording; Claude made no changes to it.

### Verification
Claude printed the complete file. `git diff -- .claude/rules/assessment.md` showed nothing because `.claude/` was not yet tracked by git at that point. This entry was added after Phase 2 because the rewrite had not been logged at the time.

## 9. Phase 2 review fixes: amount grounding and broader tests

### Task
Before committing Phase 2: strengthen amount grounding, broaden the tests beyond schema/numeric validation, and correct this log.

### Prompt or instruction
Make the server verify that a fact's numeric amount is actually supported by its `sourceText` (no second LLM call, no full number parser, fail closed, keep range/approximate behaviour, drop with a warning). Add focused tests for goals, future events, risks/questions, missing information, approximate/range values, multiple same-category facts, currency/period, grounding, injection, and a mixed realistic note.

### Outcome
- **Bug found during my review:** the original grounding check only required `sourceText` to contain *any* digit when `amount` was non-null. A quote "His pension is £420,000." with `amount: 1000000` was therefore accepted.
- **Fix:** `ExtractionResponseParser.IsAmountSupported` extracts figures from `sourceText` with one regular expression (digits with optional comma thousands separators and decimals, optional `k`/`m`/`bn`/`thousand`/`million`/`billion` suffix) and keeps the fact only if the amount exactly equals one of them. Anything unrecognised fails closed (fact dropped, existing "amount is not supported" warning). Regression tests added.
- New `ExtractionContractTests` (goals, events, risks/questions, missing values, currency/period, multiple same-category facts, mixed note) and a shared `ModelOutput` test helper, which replaced the private `Fact` helper in `ExtractionResponseParserTests`.
- Claude noted that the parser cannot detect financial advice; the "question is not turned into advice" test therefore asserts that the parser returns the question exactly as recorded and adds nothing. Advice avoidance itself is a prompt rule.
- A stray extra semicolon (`Warnings);;`) had appeared in `ExtractedNote.cs` after the earlier verification and broke the build (CS1022). Claude reported it instead of silently changing it; with my approval it was removed.
- Known remaining limits recorded in `DESIGN.md`: currency is not checked against the quote; amounts in words are dropped; an amount equal to an unrelated figure in the same quote (e.g. an age) would match.

### Your decision
Requested the fix and the test additions; approved removing the stray semicolon. Phase 2 commit pending my review.

### Verification
- `dotnet build apps/api/Ibernia.Assessment.Api.csproj`: Build succeeded, 0 warnings, 0 errors.
- `dotnet test tests/Ibernia.Assessment.Api.Tests/Ibernia.Assessment.Api.Tests.csproj`: **142 passed, 0 failed, 0 skipped**.
- Mutation check: in a temporary copy the old "any digit" rule was restored; the end-to-end regression test `Amount_that_differs_from_its_source_text_is_dropped_with_a_warning` failed as expected (141 passed, 1 failed).
- `git diff --check`: clean (exit 0; only line-ending notices). New untracked files checked separately: no trailing whitespace.

## 10. Phase 3: Gemini integration and API wiring

### Task
Make `POST /api/notes/extract` work end to end with Gemini: `ILlmClient`, `GeminiLlmClient` (raw typed `HttpClient`, `generateContent`), `NoteExtractionService`, `NotesController`, `Program.cs` (DI, CORS, ProblemDetails, safe logging), deterministic tests, then one real synthetic smoke test.

### Prompt or instruction
Follow `docs/DESIGN.md` exactly; key only in the `x-goog-api-key` header; `responseSchema` + `thinkingLevel: low` + `AI_TIMEOUT_SECONDS`; map failures to 400/502/503/504/500 ProblemDetails; never expose or log note text, prompts, model output, provider messages, or the key; fake `ILlmClient` and stub `HttpMessageHandler` in tests; no frontend, CI, Docker, or deployment changes.

### Outcome
Implementation choices made by Claude:
- `ExtractNoteRequest.Notes` changed to `string?`. With a non-nullable `string`, `[ApiController]` applies an implicit `[Required]`, so empty or whitespace notes would have been rejected by ASP.NET's own validation with a different message. The JSON contract is unchanged.
- `AiOptions`, the `HttpClient` timeout, and the CORS origins are read lazily from the final configuration, so `WebApplicationFactory` overrides apply in tests.
- Gemini codes are logged only if they match a short upper-case pattern; error `message` fields and bodies are never read into logs or exceptions.
- **Problem found while writing tests:** ASP.NET's exception handler middleware logs the full exception (message and stack trace), which conflicts with the "log exception type only" policy. Added `UnhandledExceptionLogger` (type only) and filtered out the middleware's own log entry. A mutation check (filter removed in a temporary copy) made `Unexpected_exception_returns_generic_500_without_details` fail, confirming the test catches it.
- **Bug found by the local smoke run (real Kestrel):** a 70 KB body returned **500 instead of 413**. Kestrel enforces `[RequestSizeLimit]` by throwing `BadHttpRequestException(413)`, and the exception handler turned it into 500. The in-memory test server does not enforce Kestrel's body limit, so the automated tests had not caught it. Fixed with `ExceptionHandlerOptions.StatusCodeSelector`, logged at Warning, and added a regression test. Re-run: 413.
- Created `apps/api/.env` (git-ignored, confirmed with `git check-ignore`) with variable names and an empty `AI_API_KEY` at my request; updated `.env.example` to list all four backend variables.

### Your decision
Asked Claude to create the `.env` file with empty values; I will add the key later. Phase 3 commit pending my review.

### Verification
- `dotnet build apps/api/Ibernia.Assessment.Api.csproj`: Build succeeded, 0 warnings, 0 errors.
- `dotnet test tests/Ibernia.Assessment.Api.Tests/Ibernia.Assessment.Api.Tests.csproj`: **196 passed, 0 failed, 0 skipped**.
- `git diff --check`: clean (exit 0; only line-ending notices).
- Local run against real Kestrel with no API key (no Gemini call made): `/health` 200; 70 KB body 413 (after the fix); README example 503 "temporarily unavailable", with an Error log "AI_API_KEY is not configured".
- **Real Gemini smoke test: not yet run** — no API key is configured. Whether Gemini accepts `responseSchema` and `thinkingLevel: "low"` is still unverified.

## 11. Phase 3 real Gemini smoke test (first attempt)

### Task
Run one real Gemini request through `POST /api/notes/extract` with the synthetic README example and verify structured output, `responseSchema`, `thinkingLevel: "low"`, and parsing. Change code only if a genuine integration problem appears.

### Prompt or instruction
Verify how `AI_API_KEY` reaches the app; never expose the key; one real request; if it fails on an API/schema issue, diagnose, make the minimum fix, and re-run.

### Outcome
- **Secret-handling issue caught before the run:** the key had been placed in `apps/api/.env.example`, which is tracked by git. Claude checked (counting lines, never printing values) that it was not staged and not in any commit, and asked before touching the file. I moved the key into the git-ignored `apps/api/.env` myself; Claude confirmed `.env.example` again matches the committed placeholder for `AI_API_KEY`.
- How the key reaches the app: ASP.NET Core does not load `.env`. A scratch smoke script (outside the repository) reads `apps/api/.env` into the environment of the single API process it starts. The key was never printed; the API log contained 0 occurrences of it.
- **Smoke result: failed.** `POST /api/notes/extract` returned 503; the log showed Gemini `HTTP 400 INVALID_ARGUMENT`. `/health` 200 and the 70 KB body 413 behaved correctly.
- Diagnosis with direct `curl` requests (only the error status/message printed, key redacted):
  - a minimal request without schema returned **200**, so the key, endpoint, and model are valid;
  - responseMimeType + the full extraction `responseSchema` returned **400 "Request contains an invalid argument."** (no field named);
  - single-keyword schemas that Gemini **accepted (200)**: upper-case `OBJECT`/`STRING` with `required`; `format: "enum"` + `enum`; `ARRAY` + `maxItems`;
  - **untested:** `nullable`, `minimum`, their combination in the full schema, the `responseJsonSchema` alternative, and `thinkingLevel: "low"` — these hit 503 "high demand" and then 429 `RESOURCE_EXHAUSTED`. The 429 persisted after a pause, which suggests the free-tier daily request quota was used up. About 28 requests were sent in total, mostly retries during the 503 period; retrying on 503 consumed quota.
- Observation: at default thinking, the 200 responses used 267–403 thinking tokens for a one-line prompt, which supports `thinkingLevel: "low"` once it can be verified.
- **No code change was made**, because the failing keyword is not yet identified and any fix must be confirmed against the real API.

### Your decision
Pending: resume the diagnosis when the Gemini quota resets.

### Verification
Automated tests unchanged: 196 passed (no code changed since). Real end-to-end extraction: **not working yet** (schema rejected).

## 12. Phase 3 configuration audit: no hidden defaults

### Task
Audit the implementation for hardcoded runtime configuration before the Phase 3 commit, after I found that changing `AI_MODEL` in `apps/api/.env` did not change the API's behaviour.

### Prompt or instruction
Classify every runtime value as configuration or a safety/policy constant; `AI_API_KEY`, `AI_MODEL`, and `AI_TIMEOUT_SECONDS` must come from configuration with no silent default for model or timeout; fail clearly when missing or invalid; establish exactly how `.env` reaches the app without assuming it is auto-loaded; no new dependency just for `.env`; tests must not need real secrets; no real Gemini request in this step.

### Outcome
- **Root cause confirmed:** ASP.NET Core does not read `apps/api/.env`. When the API is started with plain `dotnet run`, `AI_MODEL` is unset and the Phase 2 `AiOptions` silently fell back to `gemini-3.8-flash` (and to a 30 s timeout), so editing `.env` had no effect. Only Claude's scratch smoke script had ever loaded `.env`.
- **This reverses a Phase 2 choice Claude had made:** "blank values are treated as unset, so the default applies". That hid configuration mistakes. Now all three `AI_*` values are required. Missing or invalid values throw an error naming only the variables, and `Program.cs` resolves `AiOptions` right after `Build()`, so the API fails at startup. The missing-key check moved from per request (503) to startup, and `GeminiLlmClient` keeps a defensive empty-key check.
- Kept in code, with reasons recorded in `DESIGN.md`: the Gemini base URL and `v1beta` path (fixed public endpoint, and the request and response code is written for that API version); `MaxTimeoutSeconds = 300` (a limit that rejects, never substitutes); the request design (`thinkingLevel`, `maxOutputTokens`, schema); input and parser limits; and `http://localhost:5173` as a Development-only CORS origin.
- Design change for the frontend phase: `VITE_API_BASE_URL` is now required, with no `http://localhost:5000` fallback.
- `.env` approach: no dependency added. `DESIGN.md` documents a PowerShell snippet that loads `apps/api/.env` into the current session before `dotnet run`. Railway uses service variables; tests use dummy `UseSetting` values.
- Fixed `apps/api/.env.example`: the earlier manual key removal had merged two comment lines and left a stray `=` line. Its comments also described defaults that no longer exist. It now lists all variables as required, with empty values.
- Flagged for the Docker phase (not changed): `docker-compose.yml` passes an empty `AI_MODEL` and no `AI_TIMEOUT_SECONDS`, so the container would now fail at startup with a clear message until Compose is updated.

### Your decision
Requested the audit and the "no hidden defaults" rule. Phase 3 commit pending my review.

### Verification
- `dotnet build apps/api/Ibernia.Assessment.Api.csproj`: Build succeeded, 0 warnings, 0 errors.
- `dotnet test tests/Ibernia.Assessment.Api.Tests/Ibernia.Assessment.Api.Tests.csproj`: **209 passed, 0 failed, 0 skipped**. New: `ConfigurationWiringTests` (real `Program.cs` wiring with a stubbed network: configured model and key in the outgoing request; `AI_TIMEOUT_SECONDS=1` gives 504 after about 1 s; the app refuses to start for each missing or invalid `AI_*` value) and a rewritten `AiOptionsTests`.
- Mutation check: hardcoding `gemini-3.8-flash` back into the Gemini URL in a temporary copy made 2 tests fail, including `Configured_model_and_key_are_used_for_the_gemini_request`.
- Real server, no Gemini request: started without the `AI_*` variables, the process exited with `Invalid AI configuration: AI_API_KEY is not set. AI_MODEL is not set. AI_TIMEOUT_SECONDS must be …`. Started after loading `apps/api/.env` with the documented snippet, it used the model from `.env`, `/health` returned 200, and the key appeared 0 times in the API output.
- `git diff --check`: clean (exit 0; only line-ending notices). Scan of all tracked and untracked repository files (git-ignored `.env` excluded): the API key appears in 0 files.
- No real Gemini request was made in this step.

## 13. Phase 3 schema diagnosis and `maxItems` fix

### Task
Find which part of the `responseSchema` made Gemini return 400, then make the minimal fix.

### Prompt or instruction
Diagnose with a small number of direct, sequential Gemini requests (no retries, at least 25 s apart, stop on 503/429, key never printed), then remove only `maxItems` from `ResponseSchemaJson` and make the test contract explicit.

### Outcome
- First, the smoke test through the API with `AI_MODEL=gemini-3.1-flash-lite` (loaded from `apps/api/.env` with the documented PowerShell snippet): HTTP 503 from our API; Gemini had returned 400 `INVALID_ARGUMENT`.
- Diagnostic requests on `gemini-3.1-flash-lite` (bodies derived from `ExtractionPrompt.cs`):
  - minimal schema: 200; the fact object alone (enum, description, required): 200;
  - full structure without `nullable`/`minimum` but **with `maxItems`**: 400; plus `nullable`: 400; plus `minimum`: 503 (run stopped);
  - the same full structure **without `maxItems`**: **200**; `financialFacts` alone as an array of objects: 200;
  - the complete schema as `responseJsonSchema` (with `maxItems`): 400.
- Conclusion: **`maxItems` caused the 400.** One final direct request (the app's full request minus `maxItems`, with `nullable`, `minimum: 0`, and `thinkingLevel: "low"`) returned 503 "high demand" and was not retried.
- **Fix:** removed `maxItems` from the four arrays in `ResponseSchemaJson`; nothing else in the schema changed. The parser already enforces the limits (20 per list for goals, futureEvents, and risksOrQuestions; 30 financialFacts).
- The test `Response_schema_limits_match_the_parser_limits` read `maxItems` from the schema. It was replaced by `Response_schema_does_not_send_max_items`.
- `DESIGN.md` updated: schema block, a dialect note recording the 400 and the parser-only limits, the stale "schema limits in sync with the parser" wording, and the open items.

### Your decision
Chose to remove `maxItems` after the diagnostics isolated it. Ran the diagnostics in steps and asked for no retries on 503.

### Verification
- `dotnet test`: **209 passed, 0 failed, 0 skipped**. `dotnet build`: succeeded, 0 warnings, 0 errors. `git diff --check`: clean.
- **Not yet verified against the real API:** `nullable`, `minimum: 0`, and `thinkingLevel: "low"`. The end-to-end smoke test with the fixed schema is pending, because Gemini was returning HTTP 503 high demand.

## 14. Phase 3 real end-to-end smoke test: success

### Task
Confirm the fixed request/schema against the real Gemini API through the application, after the Phase 3 code (commit `1d778ca`) was pushed.

### Prompt or instruction
I ran the smoke test myself: `POST /api/notes/extract` with synthetic notes, using the configured `AI_MODEL=gemini-3.1-flash-lite` and the current application request and schema. I then asked Claude to update only `DESIGN.md` and this log to record the result, without inventing output values.

### Outcome
- The request returned a **successful structured extraction response** through the full path: controller → `NoteExtractionService` → `GeminiLlmClient` → Gemini `generateContent` → `ExtractionResponseParser`.
- This confirms on `gemini-3.1-flash-lite` that the current request is accepted: `responseMimeType: application/json`, the `responseSchema` without `maxItems` (with `nullable`, `minimum: 0`, `format: "enum"`), and `thinkingLevel: "low"`. Gemini's output also passed our parser.
- Not re-verified on `gemini-3.8-flash` since the schema fix.
- `DESIGN.md` updated: Phase 3 marked done; the "first live call" item is marked resolved; the stale "pending" and "unverified" wording is replaced; the `maxItems` diagnosis is kept as history.

### Your decision
Phase 3 is complete.

### Verification
Real end-to-end request run by me, with synthetic data: successful structured response. No code, tests, or configuration changed in this documentation update.

## 15. Phase 4: frontend

### Task
Implement the Advisor Note Extractor UI in `apps/web` according to `DESIGN.md`, with pinned dependencies and type checking; no backend, CI, Docker, or deployment changes.

### Prompt or instruction
Pin the existing versions from the lockfile; move tooling to `devDependencies`; add React type packages, a strict `tsconfig.json`, `vite.config.ts`, and `.env.example`; commit-ready lockfile; typed API client with a 45 s timeout and safe ProblemDetails, network, and timeout handling; a UI with counter, validation, loading/success/error states, all result sections, "Not stated" / "None mentioned", Approximate badge, source quote, and the disclaimer; plain React text only; no browser storage. My decisions: `VITE_API_BASE_URL` fails clearly **at runtime**, not at build time, with no fallback; keep TypeScript 7.0.2 unless it actually breaks.

### Outcome
- Pinned react/react-dom 19.3.0 (dependencies) and vite 8.3.2, @vitejs/plugin-react 6.1.2, typescript 7.0.2, plus new @types/react and @types/react-dom 19.3.0 (devDependencies). Regenerated `package-lock.json` (0 vulnerabilities). TypeScript 7.0.2 worked without changes; a deliberate type error was caught, confirming `tsc` really checks the files.
- New `src/config.ts` (runtime check of `VITE_API_BASE_URL`, no fallback) and `src/api.ts` (types, `validateNotes`, `extractNotes`). The client has no React or Vite dependencies, so it could be run directly under Node against the real API. `main.tsx` was rewritten on the starter's structure, `styles.css` was extended, and the page title changed to "Advisor Note Extractor".
- **Issue found in browser verification:** amounts rendered as `£420,000.00`. Changed to no decimals for whole amounts and two otherwise (`£420,000`, `£2,500.50`).
- **Problem in Claude's own verification harness:** the first browser run started the API and preview server inside bash subshells, so `kill` did not stop them. The API from the success step (with the real key) kept running, and the "invalid key" and "API down" steps silently hit it. Those two steps were invalid, and they made **two extra real Gemini calls**. Claude found the leftover processes by port, stopped them, rewrote the harness to start processes directly with a port-free guard, and re-ran only those two steps.
- **`npm ci` failed with EPERM** on the final re-run: a Vite dev server (`npm run dev`, port 5174) that Claude had not started was locking `node_modules`. Claude asked before stopping it; with my approval it was stopped and the checks re-ran cleanly.

### Your decision
Chose runtime failure for a missing `VITE_API_BASE_URL` and keeping TypeScript 7.0.2; approved stopping the dev server. Phase 4 commit pending my review.

### Verification
- `npm ci`: success, 0 vulnerabilities. `npm run typecheck`: pass. `npm run build`: pass (built without `VITE_API_BASE_URL`, confirming a missing value does not fail the build and no `localhost` is embedded).
- Real API client (`src/api.ts` under Node, local API on :5000 loaded from `apps/api/.env`, `gemini-3.1-flash-lite`):
  - successful extraction: `ok: true`, 1 goal, 2 financial facts, 2 future events, 1 risk/question, 0 warnings (7.8 s);
  - empty and 10,001-character notes: rejected by `validateNotes` and, when sent anyway, by the API with ProblemDetails, whose `detail` came through as the message;
  - API stopped: "Could not reach the extraction service…".
- CORS: preflight from `http://localhost:5173` allowed (`Access-Control-Allow-Origin: http://localhost:5173`, methods GET,POST); another origin got no CORS headers.
- Headless Edge driving the built app on :5173:
  - built without `VITE_API_BASE_URL`: configuration error shown, button disabled;
  - initial: counter `0 / 10,000`, button disabled; whitespace-only: "Please enter meeting notes."; 10,001 characters: counter `10,001 / 10,000` in red and "Notes must be 10,000 characters or fewer."; valid notes enable the button;
  - submit: button showed "Extracting…" (disabled) while loading, then rendered the disclaimer, goals, both facts (with "Not stated" for a null period and "Per year" for annual), future events, and risks/questions;
  - API with an invalid key (Gemini 400, API 503 ProblemDetails): alert "The AI service is temporarily unavailable. Please try again.";
  - API stopped: alert "Could not reach the extraction service…";
  - `localStorage`/`sessionStorage` empty throughout; no `dangerouslySetInnerHTML`, storage, or `console` use in `src`.
- Real Gemini usage this phase: 4 successful requests (1 intended for the client check, 1 intended and 2 unintended in the browser run) and 1 request rejected for the invalid key. The API key appeared 0 times in the API logs.
- Not verified live: the 45 s browser timeout message, which was not triggered deliberately.

## 16. Phase 5: CI and Docker

### Task
Make the minimal CI and Docker changes found in the read-only Phase 5 inspection. No application code changes, no Docker-build CI job, no deployment.

### Prompt or instruction
Add `apps/api/.dockerignore` (env files, bin/obj, editor/OS files; keep `.env.example`); add `USER $APP_UID` to the Dockerfile; give Compose the required configuration from `apps/api/.env` via `env_file` with no hidden defaults; in CI, add `permissions: contents: read`, use `npm ci` with npm caching, and add NuGet caching; update `DESIGN.md` and this log; verify Docker only if Docker Desktop is running, otherwise report it as unverified.

### Outcome
- **Issue found during the inspection:** with no `.dockerignore`, a local `docker build` would have copied `apps/api/.env` (the real API key) and Windows `bin/`/`obj/` into the SDK build stage. The new `.dockerignore` excludes them.
- **Compose detail:** the old `environment:` entries were removed rather than kept next to `env_file`. Compose gives `environment:` precedence, so `AI_MODEL=${AI_MODEL:-}` would have overridden the value from `apps/api/.env` with an empty string.
- **NuGet caching:** used `actions/cache` keyed on the `*.csproj` hash. `setup-dotnet`'s built-in cache requires `packages.lock.json`, and adding lock files would have meant changing project files.
- **Exit code:** the container exits with code 139 (not 1) when configuration is missing, after printing the clear `Invalid AI configuration` message. Recorded as a known limitation; fixing it needs a `Program.cs` change, which is out of scope here.
- **Process interruptions:** my local API (`dotnet run`, :5000) and Vite dev server (:5173) were locking build files, so `dotnet test` and `npm ci` first failed (`npm ci` had already removed part of `node_modules`). Claude asked; with my approval it stopped both and re-ran the checks. Docker Desktop was not running; I started it, and Claude waited for the engine before running the Docker checks.
- **Transient issue:** the first build-stage image build failed because Docker Desktop could not resolve `mcr.microsoft.com` right after starting (DNS). The build-stage checks from that attempt were discarded as invalid and re-run successfully.

### Your decision
Requested the Phase 5 scope; approved stopping the running API and dev server; started Docker Desktop myself. Phase 5 commit pending my review.

### Verification
- `dotnet test`: **209 passed, 0 failed, 0 skipped**. `npm ci`: success, 0 vulnerabilities. `npm run typecheck`: pass. `npm run build`: pass.
- GitHub Actions workflow validated against the official schema with `@action-validator/cli` 0.6.0 (run via `npx`; nothing added to the repository): exit 0. The real GitHub Actions run happens after the push.
- Docker (Docker Desktop 29.8.0, no Gemini calls):
  - the API image builds;
  - build stage: no `/src/.env`, `.env.example` present, no Windows `bin/`; the key is in 0 build-stage files and 0 history entries;
  - final image: 0 `.env*` files, key not in the image `Env`;
  - non-root: `Config.User` = `1654`, `id` = `uid=1654(app)`;
  - no configuration: exit code 139 with `Invalid AI configuration: AI_API_KEY is not set. AI_MODEL is not set. AI_TIMEOUT_SECONDS must be …`;
  - `docker compose config`: valid; `docker compose up` with `apps/api/.env`: `GET /health` → 200 `{"status":"ok"}`, `AI_MODEL` from the file, user `uid=1654(app)`, key 0 times in the container logs; `docker compose down` afterwards.
