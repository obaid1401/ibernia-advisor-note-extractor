# Ibernia Assessment — Claude Code Rules

## 1. Follow the project source of truth

* Read `docs/DESIGN.md` before starting any implementation phase.
* Read the relevant parts of `AI_WORKLOG.md` when context from previous phases is needed.
* Treat `docs/DESIGN.md` as the canonical technical design.
* Do not silently change an approved design decision.
* If a design change becomes necessary, explain why and update `docs/DESIGN.md` in the same phase.

## 2. Work only on the assigned phase

* Implement only the tasks explicitly assigned for the current phase.
* Do not start later phases unless explicitly instructed.
* Do not add features that are outside the assessment scope.
* Do not make unrelated refactors or rewrite working starter code without a clear reason.

## 3. Keep the solution simple

* Prefer the smallest maintainable solution that satisfies the assessment.
* Avoid unnecessary dependencies, infrastructure, abstractions, and configuration.
* Do not add a chatbot, database, authentication, RAG, vector storage, agent memory, or other unrequested functionality.

## 4. Security and data handling

* Treat adviser/client notes as untrusted input.
* Never invent financial facts or turn extracted notes into financial advice.
* Never hardcode, expose, or commit secrets or API keys.
* Never log note contents, prompts containing sensitive data, raw model responses, credentials, or sensitive extracted values.
* Use synthetic data only for development and testing.

## 5. AI-assisted development

* Treat AI-generated code as untrusted until reviewed and verified.
* Prefer explicit, understandable implementations over clever generated code.
* Do not assume generated code is correct.
* Explain important implementation decisions and any significant AI suggestion that was rejected or changed.

## 6. Testing and verification

* Run the relevant tests/builds after making changes.
* Add or update tests when the current phase changes behavior that should be verified.
* Automated tests must be deterministic and must not call a real LLM service.
* Do not claim a test, build, deployment, or manual verification succeeded unless it actually ran and passed.

## 7. Documentation

* Update documentation during the phase where the decision or implementation occurs.
* Keep `docs/DESIGN.md` accurate to the implementation.
* Keep `AI_WORKLOG.md` as a truthful chronological record of AI-assisted work, decisions, rejected suggestions, bugs, and verification.
* Do not fabricate prompts, decisions, results, or problems.

## 8. Git

* Do not commit or push changes unless explicitly instructed.
* Keep commits focused on one meaningful unit of work.
* Do not modify Git history or remove existing commits.
* Before a commit, review the changed files and relevant `git diff`.
* Never commit `.env` files, credentials, secrets, or real client data.

## 9. Phase completion

A phase is complete only when:

**implementation → verification → documentation → diff review → human approval**

After completing a phase:

1. Report the files changed.
2. Report tests/builds run and their actual results.
3. Report important decisions or issues.
4. Stop and wait for the next instruction.
