# Codex history: Madlan

Exported 2026-10-07. This is the user-facing transcript available in the active session. Internal prompts, tool traces, and credentials are not included.

## Architecture, planning, and data

**User:** Read the documents in `C:\code\madlan`; `madlan_deals_sample.csv` and `madlan_rnd_ops_engineer_challenge.md` are the source of truth. Act as product architect and later engineer.

**User:** Create an architecture document and a small, reviewable implementation plan. Start with backend work.

**User:** Simplify the plan. Load data at runtime into in-memory SQLite. Do not add data entry or rate limiting.

**User:** Create one reusable project skill that reads the implementation plan. Each step must branch, change, test, commit, push, open a PR, and wait for approval. Step 1 creates the private repository.

**User:** Use conventional C# classes, no `sealed` classes, one class or DTO per file, review each change against the step and project, and log every commit and requested correction.

**User:** Use `israeli towns.csv` to produce reusable official locality data. Remove the manual city alias map. Handle typos later.

**User:** Add locality typo handling, preserve original and normalized data, and plan four separate LLM stages: generate query, verify query, summarize result, verify result against the original prompt. Use different models by stage.

**User:** Queries must handle production scale. Filtering and aggregation must execute in the database. The service builds provider-neutral filters and queries; repositories adapt them to a database. Use demo SQLite only; do not implement PostgreSQL.

## Steps 6 through 8

**User:** Move to step 6.

**Assistant:** Added `QueryService`, `IDealRepository`, `DealQuery`, and `SqliteDealRepository`. SQL was isolated in the repository. Step 6 was merged as PR #7.

**User:** Move to step 7. LLM use must be abstracted; use Grok first but keep it replaceable.

**User:** Why is `ApiEndpoints` a class? Why not use normal .NET Web API?

**Assistant:** Replaced minimal endpoint mapping with controller-based Web API.

**User:** Use one controller that calls an application service, which orchestrates LLM and repository requests and builds the query.

**Assistant:** Replaced multiple controllers with `MadlanController` and `MadlanApplicationService`.

**User:** The user sends natural text that the system turns into a query. The controller should have only one endpoint for now.

**Assistant:** Replaced public internal routes with `POST /api/ask`. The controller passes the original prompt to `MadlanApplicationService`; later LLM stages belong behind that service. Step 7 was merged as PR #8.

**User:** Approved, move to step 8.

**Assistant:** Added the initial provider-neutral LLM layer: `ILlmProvider`, `GrokLlmProvider`, stage-specific model configuration, structured query generation, cancellation, and C# filter validation. Opened PR #9.

## Step 8 review against the challenge

**User:** Review step 8 against `madlan_rnd_ops_engineer_challenge.md`.

**Assistant:** Found these blocking gaps:

- A generated `query` could omit filters.
- The model prompt lacked the supported-query contract, locality/date rules, and current Israeli date.
- `LlmOptions.Provider` was not actually used to select a provider.
- Credential, timeout, outage, and malformed-output errors were not clear Hebrew user failures.
- `/api/ask` endpoint tests had been removed instead of using a fake provider.
- Tests lacked coverage for clarification, unsupported questions, missing filters, relative dates, ambiguity, cancellation, and timeouts.

**User:** Update the plan with your findings.

**Assistant:** Updated step 8 acceptance criteria to require those safeguards and tests. PR #9 remains open and needs those fixes before approval.

## Current intended product flow

```text
Natural-language question
→ POST /api/ask
→ MadlanController
→ MadlanApplicationService
→ LLM generates structured filters
→ LLM verifies filters
→ QueryService builds a validated DealQuery
→ IDealRepository executes it
→ LLM summarizes and verifies the result
→ evidence-backed Hebrew response
```

## Current repository status at export

- PRs #1 through #8 were merged.
- PR #9, Step 8: add Grok query generation workflow, is open.
- The branch builds and its initial tests pass, but the step-8 review findings above must be implemented before approval.
