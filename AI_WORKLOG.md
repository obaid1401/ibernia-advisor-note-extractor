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
