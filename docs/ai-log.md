# AI Work Log

## 2026-10-05 — Step 1

Created the backend skeleton, Docker build path, health endpoint and its integration test. The project targets .NET 10 while the inspected local environment has .NET 8, so Docker is the repeatable validation path.

The first version of the test omitted `using Xunit;`. The .NET 10 Docker test run failed to resolve `IClassFixture` and `Fact`. Adding the missing import fixed the compilation failure; the same Docker test suite then passed.

## Commit: docs: add challenge source and architecture plan

Committed the supplied CSV, challenge, architecture and implementation plan to establish the repository base branch.

## Commit: chore: consolidate implementation step skill

The user requested one reusable skill instead of one skill per step. Replaced the ten step-specific skills with `madlan-implementation-step`, which reads the implementation plan for the requested step.

## Commit: fix: apply project conventions and workflow reviews

The user requested conventional C# classes in `Program.cs`, no `sealed` classes, a review after each meaningful change against the requested step and the project, and AI-log entries for every commit and material user-requested corrections. This update applies those changes.

## Commit: feat: load bundled CSV into in-memory SQLite

Implemented step 2: startup reads the bundled CSV, preserves raw report fields and source row numbers, normalizes the planned query fields, and inserts all 530 source rows into shared in-memory SQLite in one transaction. The process retains one keeper connection and opens separate request connections.

The first step-2 test compilation omitted the hosting namespace in the test environment. The test run caught the missing `IHostEnvironment` type; adding `using Microsoft.Extensions.Hosting;` corrected it. The same run reported a high-severity transitive SQLite advisory from version 10.0.0, so the SQLite and ASP.NET test packages were raised to 10.0.12.

The next test run exposed a source-directory content-root path under `WebApplicationFactory`; it could not find the CSV copied beside the compiled application. The loader now first uses the configured content-root path and then falls back to `AppContext.BaseDirectory`, which is the Docker runtime location. The Docker test target then passed all four tests.

## Commit: feat: add Israeli locality catalog

The user provided `israeli towns.csv` and requested a reusable class containing the locality data for future use. The source uses Windows-1255 Hebrew encoding, so it was converted once into the committed UTF-8 `israeli-localities.json` runtime asset. `IsraeliLocalityCatalog` loads 1,316 typed localities at startup, supports lookup by code and Hebrew/English name, and is used by city normalization.

The first conversion used Hebrew CSV header literals through PowerShell and failed at the encoding boundary. The converter was changed to decode Windows-1255 and use the file's stable column positions. The first catalog test then revealed case-sensitive JSON deserialization against camelCase JSON properties; enabling case-insensitive property matching corrected the issue. The Docker test target passed all six tests afterward.

## Commit: refactor: use only official locality names

The user requested removal of the manually maintained city-alias map. City normalization now uses only exact Hebrew or English matches from the official locality catalog. Noncanonical names, abbreviations and typographical variants remain unchanged until a later data-quality policy addresses them. The test that previously expected an alias conversion for `בית-שמש` now verifies that the source value is preserved.

## Commit: feat: group duplicate and conflicting deals

Step 3 adds the `Deals` table in the same SQLite startup transaction that imports `Reports`. A deal is `usable` when all of its normalized reports are identical; duplicate source rows remain in `Reports` and the deal points to the earliest retained report. A deal is `conflicting` when its normalized reports differ, has no canonical report, and remains fully inspectable through its reports. The normalized data now includes all business fields used for equality, including the reported source.

Tests confirm the sample's 520 deal groups, six duplicate pairs, four documented conflicts, retained canonical-report rules, and identical outcomes after reversing CSV row order.

## Commit: feat: filter deals and calculate results

Step 4 adds a read-only query service that loads only usable canonical reports from SQLite, validates filters, applies filtering and calculates exact-decimal medians in C#. The first query tests exposed a fixed-name shared-memory SQLite collision when separate web-application test fixtures ran concurrently. Test execution is now sequential so each fixture releases its keeper connection before the next fixture starts.

## Review correction: separate step 4 DTO files

The user requested one class or DTO per file. Moved DealFilters and DealQueryResult into their own matching files and removed an unused import. Query behavior is unchanged. The other step 4 test classes already have their own files.

Validation: Docker test target passed all 12 tests.

## Commit: docs: plan locality correction and verified LLM workflow

The user requested architecture and plan coverage for locality typo handling, preservation of original and normalized values, and four separately configured LLM stages: query generation, query verification, result summarization and result verification against the original prompt. The documents now define conservative official-catalog matching, auditable locality metadata, verification gates, manual fallback, independent model configuration and new reviewable implementation steps. No runtime behavior changes in this commit.

## Review correction: extract query warnings and name the contributor threshold

The user requested that warning construction move out of `Query` and that the metric contributor threshold be configurable in code. `GetWarnings` now owns warning construction and `MinimumMetricContributorCount` replaces the literal `5`. Behavior is unchanged.

## Review correction: production-scale database query path

The user clarified that the application must be designed as a production service even though this demo uses in-memory SQLite. The architecture and implementation plan now require fixed, parameterized database SQL over typed indexed columns, database-side aggregates and bounded evidence pages. They explicitly prohibit a query service, repository or query builder. The existing C# materializing query implementation is being replaced before PR approval.

## Review correction: execute queries in SQLite

Removed `DealQueryService`. `DatasetStore` now loads typed, indexed deal columns and executes fixed parameterized SQLite statements for filters, medians, counts, warnings and a bounded evidence page. The same request shape can use PostgreSQL-specific statement text in a production deployment without adding a repository or query builder.

The step 5 build exposed a compile error in the earlier SQL median mapping: the conditional expressions combined `null` and `decimal` without an explicit nullable target type. Declaring both medians as `decimal?` fixes the build without changing query behavior.

## Planning correction: provider-agnostic query repository

The user clarified that query semantics belong in a provider-agnostic service and database translation belongs in a repository implementation. The architecture now defines `QueryService`, `IDealRepository`, `SqliteDealRepository` for the demo, and a future adapter path for production. The plan adds a dedicated step 6 to introduce and test this boundary, then shifts later API, LLM, UI and deployment steps by one.

The user then clarified that PostgreSQL must not be implemented in this project. The architecture and step 6 now limit implementation to SQLite while preserving a provider-neutral repository contract for a future adapter.

## Commit: feat: isolate query execution behind repository

Implemented step 6. `QueryService` validates application filters and produces a provider-neutral `DealQuery`; `IDealRepository` defines execution; and `SqliteDealRepository` owns the parameterized SQLite statements, database-side aggregates and bounded evidence read. `DatasetStore` now has only dataset lifecycle and loading responsibilities. Dependency injection selects the SQLite implementation for this demo, and a fake repository verifies the service boundary without SQLite.

The integration tests now resolve `QueryService` through dependency injection and retain coverage for filtering, null metrics and invalid filters. A new test verifies that a broad query returns at most the default 100 evidence IDs while reporting that more matching evidence exists. Docker's .NET 10 test target passed all 16 tests.

## Commit: feat: expose query and evidence APIs

Implemented step 7 with read-only dataset, manual-query and deal-detail endpoints. The API returns dataset hash, applied filters, warnings and bounded evidence. It exposes all reports for a deal, including conflicting deals, while query metrics continue to exclude conflicts. Errors have stable codes, Hebrew messages and request IDs; successful responses include the same request-ID header.

The user requested that the future LLM integration remain replaceable while starting with Grok. The architecture and implementation plan now require workflow code to depend on `ILlmProvider`; a later `GrokLlmProvider` will isolate Grok HTTP, authentication and response parsing behind that interface.

The first API test compilation showed that the existing fake repository no longer implemented the two new evidence methods on `IDealRepository`. Adding inert fake implementations corrected the test double; no production behavior changed.

## Commit: refactor: use Web API controllers

The user requested conventional ASP.NET Core Web API structure. Replaced the minimal-API endpoint mapping class with separate dataset, query and deal controllers. The routes, response contracts, service/repository boundary, request IDs and error behavior remain unchanged.

## Commit: refactor: use one controller and application service

The user clarified that the API must use one controller that delegates to an application service. Replaced the three resource controllers with `MadlanController` and introduced `MadlanApplicationService`. It coordinates dataset, manual query and deal-detail requests; later LLM stages will be added to this service, while controllers remain HTTP-only.

## Commit: refactor: expose one natural-language endpoint

The user clarified that the public controller must expose only the natural-language product entry point. Replaced the dataset, manual-query and deal-detail routes with `POST /api/ask`. It preserves the original prompt and delegates it to `MadlanApplicationService`; the next approved step adds the provider-neutral Grok workflow behind this application-service method.

The first compile after removing the internal response contracts found the now-unused `DatasetService` still depended on one of them. Removing that obsolete service and its registration fixed the build; the repository remains available for the application service when the LLM workflow is added.

## Commit: feat: add Grok query generation workflow

Added a provider-neutral LLM contract, stage/model configuration and a Grok HTTP adapter. Query generation is orchestrated by `MadlanApplicationService`, preserves the original prompt, accepts only structured query/clarification/unsupported outcomes, and validates generated filters in C# before returning them.

Fake-provider tests verify model selection, prompt preservation, structured query filters and malformed output rejection. The Docker .NET 10 test suite passed all 18 tests.

## Review correction: strengthen step 8 acceptance criteria

Reviewing the Grok query-generation work against the challenge found missing structured-output guards, incomplete prompt context, inactive provider selection, insufficient graceful-failure behavior and missing HTTP fake-provider tests. The step 8 plan now makes these explicit implementation and verification requirements before approval.

## Commit: feat: verify generated queries against the original prompt

Implemented step 9. Added `QueryVerificationService`, using the separately configured `Llm.Models.QueryVerification` model, which checks the original prompt against the filters `QueryGenerationService` proposed and returns an approved/rejected/clarification outcome with a reason. `MadlanApplicationService` now only returns generated filters to the caller after verification approves them; a rejected or ambiguous verification is surfaced as a clarification response with no filters, and a malformed or unavailable verifier throws, so it is never treated as approval. No automatic retry or repair loop was added, matching the architecture.

Fake-provider tests cover approval, rejection, clarification, malformed verifier output, an unsupported outcome value and a missing verification-model configuration. Orchestration tests on `MadlanApplicationService` confirm the verification model is actually called with the generated filters, that a rejected or ambiguous verification strips filters from the response, and that generation outcomes which never produce filters (clarification, unsupported) skip the verification call entirely. The Docker .NET 10 test suite passed all 30 tests.

A broader pre-existing gap surfaced during this review: the step 8 query-generation prompt still does not supply the filter schema, room semantics, date behavior or current Israel date that the architecture calls for, and `/api/ask` still has no HTTP-level test coverage with a fake provider. Both are out of step 9's scope and are left for a dedicated follow-up rather than folded into this change.

## Commit: feat: summarize calculated results with an LLM

Implemented step 10. Added `ResultSummaryService`, using the separately configured `Llm.Models.ResultSummary` model, which turns the original prompt, the approved filters, the dataset hash and `QueryService`'s deterministic `DealQueryResult` (metrics, contributor IDs, exclusions and warnings) into a candidate Hebrew summary. The calculated values themselves are only read, never changed. The service rejects, rather than trusts, anything the model cannot support from the evidence it was given: an empty summary, a referenced deal ID that is not among the actual contributor IDs, a summary with no references when the result has transactions, or any reference at all when the result is empty. This is deliberately only the summary stage in isolation — it is not yet wired into `MadlanApplicationService` or `/api/ask`, matching the plan's split between step 10 (produce a candidate summary) and step 11 (verify it and orchestrate the full four-stage workflow). The service is registered in dependency injection so step 11 only has to add it as a constructor parameter.

Fake-provider tests cover a grounded summary, the exact evidence (dataset hash and metrics) reaching the model, an empty result with no references, a hallucinated deal ID, a summary missing required references, an empty summary, malformed output and a missing summary-model configuration. The Docker .NET 10 test suite passed all 39 tests.

## Commit: feat: verify results against the original prompt

Implemented step 11. Added `ResultVerificationService`, using the separately configured `Llm.Models.ResultVerification` model, which checks a candidate summary against the original prompt, approved filters and the exact calculated evidence, and rejects a summary that cites a deal ID outside that evidence without even calling the model. `MadlanApplicationService` now runs the full four-stage workflow: generate, verify the query, execute it through `QueryService`, summarize, and verify the summary. It always returns the deterministic `DealQueryResult` and dataset hash once the query is approved; the Hebrew summary is only attached after result verification approves it. A failure in the summary or result-verification stage — malformed output, an unavailable model, a verifier rejection — falls back to the deterministic results with a plain "summary unavailable" message instead of a 500, which is the one place in this workflow that intentionally degrades instead of failing hard, matching the architecture's "failed summary/check" rule. A real caller-requested cancellation is still distinguished from that internal fallback and propagates rather than being swallowed. Added a configurable `ServerTimeoutSeconds` (default 65s) wrapping the whole workflow; the per-call 15s stage timeout already existed from step 8, and the 70s browser deadline is deferred to the UI work in step 13.

Query execution needed the dataset hash outside `DatasetStore`'s existing surface, so `MadlanApplicationService` now depends on a new minimal `IDatasetMetadataProvider` interface (implemented by `DatasetStore`) instead of the concrete class, so the orchestration stays testable with a fake the way every other stage already is. `ResultSummaryService`'s evidence-serialization and known-deal-ID logic was extracted into a shared `CalculatedEvidence` helper so `ResultVerificationService` does not duplicate it; behavior is unchanged.

The first version of the graceful-fallback catch block could not tell an internal provider timeout from the caller actually cancelling the request, because .NET surfaces both as `OperationCanceledException` with no reliable type distinction. Checking whether the original caller token (not the internal 65s workflow deadline) was the one that requested cancellation fixed it, and a dedicated test asserts real cancellation still propagates instead of being reported as "summary unavailable."

Orchestration tests cover the full four-stage approval path with model-routing order, a failed summary stage, a rejected result verification, query-verification rejection/ambiguity (no query execution), generation clarification/unsupported skipping later stages, an unavailable query verifier, an internal summary-stage cancellation treated as unavailable, and a real caller cancellation propagating. `ResultVerificationServiceTests` cover approval, rejection, the no-model-call hallucinated-ID short-circuit, malformed output, an unsupported outcome and a missing model configuration. The Docker .NET 10 test suite passed all 49 tests.

## Review correction: move user-facing Hebrew strings out of code

The user requested that no Hebrew string live in the C# source; every user-facing Hebrew message must come from configuration. Added `MessagesOptions` (`Messages` section in `appsettings.json`) with `InternalError`, `InvalidPrompt` and `SummaryUnavailable`, and replaced the three hardcoded literals that carried them: `Program.cs`'s generic 500 handler, `MadlanController`'s invalid-prompt response, and `MadlanApplicationService`'s summary-unavailable fallback. Each now reads its message through `IOptions<MessagesOptions>`.

Left two things out of scope, since they are not user-facing copy: `DatasetStore`'s CSV-normalization literals (`חדרים` room-suffix stripping, `כן`/`לא` boolean matching) sit next to their hardcoded English equivalents (`"true"`, `"yes"`, `"1"`) in the same parsing logic and are dataset-format constants, not messages; and the Hebrew sample questions in test files are test input data, not application copy. The Docker .NET 10 test suite passed all 49 tests after the change.

## Commit: feat: build the RTL manual explorer and evidence view

Implemented step 12. Step 7 had narrowed the public API down to `POST /api/ask` alone, but the product brief and architecture both require manual filters to keep working when the LLM is unavailable — a guarantee with no meaning unless there is a non-LLM request path for the UI to call. Reinstated three thin, deterministic, LLM-free read endpoints on `MadlanController`: `POST /api/query` (filters straight to `QueryService`, same `DealQueryResult` shape `/api/ask` returns once a query is approved), `GET /api/dataset` (coverage facts and dataset hash, for the coverage panel and filter autocomplete), and `GET /api/deals/{dealId}` (full report detail for one deal, including conflicting reports and locality-correction metadata). None of them touch `ILlmProvider`. Documented the reversal and rationale directly in `madlan_architecture.md`'s endpoint table rather than leaving it implicit.

Added a plain HTML/CSS/JavaScript page under `wwwroot` (served via `UseDefaultFiles`/`UseStaticFiles`, added to `Program.cs`): RTL Hebrew layout, dataset coverage, manual filters with autocomplete from `/api/dataset`, the three metric cards with their own contributor counts, warnings translated into plain-language Hebrew sentences, an evidence table with expandable per-deal report detail (raw and normalized fields side by side, including locality correction method), a direct deal-ID lookup panel for inspecting a specific disputed deal independent of any filter, and static calculation-definition text. All dynamic content is built with `textContent`/`createElement`, never `innerHTML`, since dataset text is untrusted per the architecture. Loading, empty-result and error states are explicit and distinct, matching the "no matches: explain, don't silently broaden" rule.

Caught while testing in a real browser rather than just reading the code: the evidence table only ever lists usable deals (conflicting deals are excluded from query results by design), so there was no way to inspect a conflicting deal like D100017 at all — exactly the "customer disputes a specific deal" scenario the challenge's customer-moment session is built around. Added the direct deal-ID lookup panel specifically to close that gap; it was missing from the first version of the page.

Verified live in Docker end to end: the documented Holon regression (4-room apartment, 2025) returns transaction count 1, ₪3,826,000 median price, ₪38,260 median price/m², deal D100027, with both small-sample warnings shown; D100017's conflicting-report detail shows both differing reports (prices 1,919,000 vs 1,851,548, different sources) side by side; an unmatched filter shows the explicit empty-result message instead of silently broadening; invalid room bounds and an unknown deal ID both return their configured Hebrew `ApiErrorResponse` messages; keyboard tab order reaches every control with a visible focus ring.

Extended `MadlanApplicationService` with `Query`, `GetDatasetSummary` and `GetDeal`, backed by a new `IDealRepository` dependency alongside the existing `QueryService`. Added two messages to `MessagesOptions` (`InvalidFilters`, `DealNotFound`). Added `Query`/`Facts`/`Deal` configuration points to `FakeDealRepository` and new `MadlanApplicationService` tests for all three new methods. The Docker .NET 10 test suite passed all 54 tests.

## Commit: feat: connect natural-language questions to the UI

Implemented step 13, frontend-only — `/api/ask` already returned everything the page needs (filters, deterministic result, summary, dataset hash) since step 11. Added a question panel with example questions above the manual-filter panel, calling `POST /api/ask` with a client-side 70-second timeout (`AbortController`), matching the architecture's browser-deadline requirement.

On a verified `query` response, the page now shows an interpretation line built from the returned filters, copies those filters into the manual filter form so the CSM can see and edit them, shows the verified summary above the evidence when one was approved, and falls back to a plain "summary unavailable" note (the server's own message) when it was not — without ever inventing or relabeling an unverified summary as verified. On `clarification`/`unsupported`, the page explains what to change and leaves the manual filter form untouched. Editing the manual filters and clicking "חפש" always calls `POST /api/query` directly, bypassing generation/verification entirely, so a manually-run query can never be mislabeled as LLM-verified.

Added a monotonically increasing request token shared by both the question flow and the manual-filter flow: each request captures the token at the moment it starts, and checks it again before touching the DOM after the response arrives, so a slow older request can never overwrite a result from a newer one. A request that fails after a prior request already displayed results keeps those results visible and adds an explicit "these are from the previous search; the last search failed" banner, rather than silently clearing the page or leaving stale results looking current.

Verified live in Docker: with no Grok credentials configured, asking a question correctly surfaces the generic Hebrew 500 message (no stack trace) from the existing exception-handler path, and a manual filter search run immediately afterward succeeds normally (39 Holon transactions, correct median and warning), confirming the manual fallback actually works after a real model failure rather than just in theory. Backend tests are unaffected by this step (no backend changes); the Docker .NET 10 test suite still passed all 54 tests.

## Caught a bad AI answer: real Grok credentials, example question wrongly rejected

The user supplied a real xAI API key and `grok-4.6` to test the live site end to end for the first time. Two real problems surfaced immediately that no amount of fake-provider testing could have caught:

1. A real Grok call took roughly 26 seconds. The 15-second per-stage timeout from the architecture's original estimate failed every single request with a TLS-handshake-level `TaskCanceledException`, even though the key and model were both valid. Raised `Llm.TimeoutSeconds` to 30 and `Llm.ServerTimeoutSeconds` to 120 (and the browser-side `ASK_TIMEOUT_MS` to 130000) in `appsettings.json`/`site.js`, and documented the change and its reasoning directly in `madlan_architecture.md` rather than silently changing a number the architecture had specified.

2. With the timeout fixed, asking the flagship example question — "מה המחיר החציוני של דירת 4 חדרים בחולון ב-2025?" (the exact Holon regression question used throughout this project's own tests) — returned `outcome: "unsupported"` with the English message *"Statistical market data such as median prices (including for 2025) is not supported."* That is wrong: median price over the historical sample is precisely what this system calculates and is supposed to answer. This is the concrete failure that two earlier ai-log entries (steps 9 and 10) had already flagged as a standing risk and deferred: `QueryGenerationService`'s prompt never actually told the model what filters, semantics, or current date it had to work with, so a capable model had no way to know a sample-median query was in scope. It also exposed a second problem beyond the misclassification itself — the message it returned was in English, not Hebrew, which would be wrong on its own even for a genuinely unsupported request.

Rewrote the generation prompt to state the supported filter schema, exact-room semantics, the current Israel date (for resolving relative dates), and — critically — an explicit rule that a historical statistic over the sample (including one naming a specific year) is always `"query"`, never `"unsupported"`, with `"unsupported"` reserved for genuine forecasts, valuations and investment advice, and required the returned `message` to be Hebrew. Verified against the real API afterward: the Holon question now correctly returns `outcome: "query"` with the exact documented filters and, after the full four-stage pipeline, the exact documented result (1 transaction, ₪3,826,000, ₪38,260/m², deal D100027) plus a correct Hebrew verified summary noting the small-sample warning. A genuinely unsupported question ("כמה תשתלם הדירה שלי בעוד שנה?" — a future-value prediction) still correctly returns `outcome: "unsupported"` with a Hebrew explanation. Confirmed the full round trip rendering correctly in the browser: interpretation line, verified summary panel, metric cards and warnings all matched the API response.

The Docker .NET 10 test suite still passed all 54 tests (no fake-provider test asserted on prompt wording, so none needed updating, though this is itself a gap worth noting: the existing tests could not have caught this class of mistake, because it lives in what we ask the model to do, not in how we validate what it returns).
