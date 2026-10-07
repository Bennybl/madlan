# Madlan Deal Explorer - Implementation Plan

Status: revised on 2026-10-05. Steps 1-12 are merged; step 13 is under review. Steps 14-15 below are planned, not implemented.

Follow the [architecture](madlan_architecture.md). The [challenge](madlan_rnd_ops_engineer_challenge.md) and [CSV](madlan_deals_sample.csv) are the sources of truth.

Implement each numbered step as a small reviewable change with its relevant checks. Steps 1-11 build the backend, 12-13 the UI, and 14-15 deployment and handoff. Existing steps 1-5 retain their numbers. One application project and one test project are sufficient.

The application loads the supplied CSV into in-memory SQLite once at startup. There is no data-upload feature, admin access, persistent storage, rate limiting or usage-quota infrastructure.

## 1. Create the backend skeleton

**Change:** Create the .NET 10 ASP.NET Core application and xUnit project. Add a basic health endpoint, configuration and a Docker build path because the inspected local SDK is .NET 8. Start the README and factual AI work log.

**Verify:** Build, run the application and call the health endpoint without an LLM key.

**Done when:** A fresh checkout has a documented way to build and start the backend.

## 2. Load and normalize the CSV into in-memory SQLite

**Change:** Use CsvHelper and Microsoft.Data.Sqlite to populate the `Reports` table at startup in one transaction. Preserve raw fields and row numbers; normalize numbers, exact official locality names, rooms, booleans and dates. Typo handling is added in step 5. Keep month-only precision, exact decimals and unknown values. Compute a dataset hash. Use the architecture's named shared-memory connection string, a lifetime keeper connection and separate request connections. Fail startup clearly for a missing/unreadable file; flag individual field problems.

**Verify:** Read all 530 rows back from SQLite. Test the observed formats, quoted fields, missing values and date precision. Verify exact decimals and raw strings survive storage, a second connection sees the loaded rows, and closing a request connection does not erase the database. Give each test database a unique name for isolation.

**Done when:** The app loads the CSV into SQLite at runtime with no disk database or separate import command; shutdown releases the database and restart rebuilds it.

## 3. Handle duplicate and conflicting reports

**Change:** Group normalized reports by deal ID and populate the `Deals` table in the same startup transaction as `Reports`. Count identical reports once and mark differing reports as conflicting. Retain every original report and its quality flags; do not select an arbitrary conflict winner. After startup, services only read the database.

**Verify:** Confirm 520 deal groups, six duplicate pairs and the four documented conflicting IDs. Reversing row order must not change the outcome.

**Done when:** SQLite contains 530 reports and 520 deal groups, and later query code can distinguish usable deals from conflicts without losing evidence.

## 4. Implement filters and calculated results

**Change:** Add typed query columns and indexes to the demo SQLite schema. Define the provider-neutral query/result DTOs and validation rules used by the later query service. Implement date containment, missing-value eligibility and the architecture's warning/rounding rules. Database execution moves to the repository in step 6.

**Verify:** Hand-check odd/even medians, empty results, missing area, zero price, low-price warning and partial-month overlap. Regression: Holon/apartment/exactly four rooms/2025 returns D100027 only, with NIS 3,826,000 and NIS 38,260/m².

**Done when:** Query semantics are reproducible from their DTOs and ready for a repository implementation, independent of the LLM.

## 5. Resolve locality typos and preserve normalization evidence

**Change:** Extend `NormalizeCity(string value)` with exact matching followed by conservative typo matching against the bundled official locality catalog. Document similarity and candidate-gap thresholds with examples. Preserve unresolved/ambiguous values and flag them. Reuse the resolver for query filters, with clarification for ambiguity. Keep original `RawJson`, normalized fields and locality resolution metadata (code, original/resolved value, method and rule version). No model calls at startup or manual alias dictionary.

**Verify:** Test exact Hebrew/English matches, a clear typo, tied candidates, unknown names, whitespace and unchanged original values. Recheck 530 reports, duplicate/conflict outcomes and the Holon regression; explain any intended normalization changes.

**Done when:** Clear typos resolve deterministically and every correction can be inspected; uncertain names are never silently changed.

## 6. Introduce a provider-agnostic query service and repository

**Change:** Add `QueryService` and `IDealRepository`. The service validates filters and creates a provider-neutral `DealQuery`; it has no SQL or provider types. Move SQLite query execution into `SqliteDealRepository`, including database-side filtering, conflict exclusion, medians, warnings and bounded evidence pagination. Do not implement PostgreSQL, add a PostgreSQL package, or add production-database configuration. Keep the contract, query/result DTOs and service semantics free of SQLite types so another repository implementation can be added later.

**Verify:** Service tests use a fake repository and prove it sends the expected validated query. SQLite repository tests cover the Holon regression, empty results, partial-month exclusion, conflict exclusion, metric warnings and the evidence-page limit. Confirm request code does not materialize all matching rows.

**Done when:** The service is independent of the SQLite implementation, and a future repository can be added without changing filter semantics.

## 7. Expose the query and evidence APIs

**Change:** Add one `POST /api/ask` endpoint that accepts and preserves the original Hebrew prompt, then delegates to the application service. Add consistent Hebrew errors, request IDs and simple structured logs. Health succeeds only after data loading. Keep all data read-only. The LLM workflow itself is implemented in the following steps; do not expose repository or manual-query endpoints publicly.

**Verify:** API tests cover a valid prompt, an invalid prompt and request IDs. Check that the original prompt is unchanged and errors do not expose stack traces.

**Done when:** The public HTTP entry point preserves a user question and passes it to the application service; later steps add generation, verification and deterministic query execution.

## 8. Generate structured queries with an LLM

**Change:** Add a small provider interface and query-generation stage. Configure separate model IDs for query generation, query verification, result summary and result verification. Implement a `GrokLlmProvider` as the initial adapter, selected by configuration, while keeping Grok authentication, HTTP calls and response parsing behind the provider interface so another provider can be added without workflow changes. Make the configured provider selection real in dependency injection; do not register Grok unconditionally. Generate typed filters, clarification or unsupported results; validate with the same C# rules as manual filters. A `query` outcome must contain a valid filter object; reject missing filters, unknown outcome values and malformed JSON. Retain the unchanged original prompt.

Supply the model with the supported filter schema, exact-room semantics, date behavior, locality-resolution rules and current Israel date. Keep the prompt, CSV text and model output untrusted. Add stage timeouts, cancellation and structured output validation. Translate missing credentials, provider outages, timeouts and invalid model output into clear Hebrew API errors with request IDs; do not expose stack traces. Keep the single `/api/ask` endpoint covered through a fake provider.

**Verify:** Fake-provider tests cover supported questions, exact rooms, locality typos, ambiguous places, relative dates, unsupported requests, missing filters, malformed output, provider timeout, cancellation and missing credentials. HTTP tests cover successful structured generation and clear Hebrew failures with request IDs. Confirm the configured provider and generation model are used. Run real Hebrew examples when credentials are available.

**Done when:** Proposed filters are validated and testable independently; generation alone does not authorize automatic execution.

## 9. Verify generated queries against the original prompt

**Change:** Add query verification using its separately configured model. Supply the original prompt, proposed filters and supported semantics. Accept only structured approval; rejection or ambiguity returns reasons/clarification. Expose the interpretation endpoint after this check. No automatic retries or repair loop.

**Verify:** Check omitted constraints, wrong room operators, incorrect city/date ranges, ambiguous neighborhood, verifier refusal/outage and invalid responses. Confirm no generated query executes on failed verification and the verification model is used.

**Done when:** Automatic query execution requires C# validation and model verification; manual queries remain available.

## 10. Summarize calculated results with an LLM

**Change:** Add a summary stage using its separately configured model and the original prompt, approved filters, dataset hash, deterministic metrics, contributor IDs, exclusions and warnings. Require evidence references. Keep calculated values unchanged; summary output remains a candidate until step 11 verification.

**Verify:** Cover empty results, small samples, missing area, conflicts, low-price warnings and provider failure. Verify the summarization model receives the exact calculation evidence and no candidate prose is presented as verified.

**Done when:** Evidence-based Hebrew summaries can be generated without changing calculations.

## 11. Verify results against the original prompt

**Change:** Add the result-verification model and `/api/ask` orchestration. Check the candidate summary against the original prompt, approved filters and calculated evidence. Validate referenced IDs and numeric claims in C# where structured claims allow it. Publish prose only after approval; on failure show deterministic results with summary unavailable. Bind verification to the exact prompt, filters and dataset hash. Use configurable 15-second stage, 65-second server and 70-second browser deadlines; propagate cancellation.

**Verify:** Inject incorrect numbers, invented IDs, omitted limitations and a correct summary answering the wrong question. Test rejection, malformed output, timeout, distinct model routing for all four stages and manual fallback. All backend tests must pass before UI work.

**Done when:** The four-stage question workflow returns verified prose with reproducible evidence, or a clear partial/failure response without unverified prose.

## 12. Build the RTL manual explorer and evidence view

**Change:** Serve plain HTML/CSS/JavaScript with Hebrew labels and RTL direction. Add manual filters, three metrics, contributor counts, warnings, evidence table and expandable original/normalized report details including locality correction metadata. Show coverage and calculation definitions. Add loading, empty and error states; use safe text rendering and accessible labels.

**Verify:** In the browser, run the known Holon query, inspect a conflict and a missing-area record, and check keyboard use and a narrow viewport. Confirm pagination, if used, does not change metric totals.

**Done when:** A CSM can filter, understand a number and inspect its evidence without developer tools.

## 13. Connect natural-language questions to the UI

**Change:** Add question input and example questions. Connect to the four-stage question workflow and display verified filters, deterministic results and verified summaries with evidence. Show progress and clarification/failure states. Editing filters invalidates the prior summary and verification and runs an explicitly manual query. Clarification and unsupported responses explain what to change. Use the configurable 70-second browser deadline and ignore older responses after a new request. Clearly label old results after a failed search.

**Verify:** Exercise a complete four-model Hebrew question, query rejection, result rejection, clarification, invalid output and timeout at each stage. Complete a manual search after model failure. Check that an older response cannot overwrite newer results.

**Done when:** The complete question-to-evidence journey works and has an obvious manual fallback.

## 14. Deploy the application

**Change:** Package the app and CSV in one Docker image and deploy to Render. Configure the API key as a server secret, the four independent model IDs, workflow deadlines and the health endpoint. Recreate in-memory SQLite on startup; no persistent disk or database service is needed. Add a small CI build/test workflow and document startup/deployment commands. Confirm any actual hosting charge before provisioning.

**Verify:** Open the public URL and run a real question plus manual query. Restart and verify the bundled dataset produces the same results. Confirm secrets are absent from browser assets and logs. Demonstrate provider failure using a local/test configuration without disrupting the public demo.

**Done when:** The application works at a public URL and can be rebuilt from the repository.

## 15. Complete the handoff

**Change:** Write the one-page Hebrew CSM guide: what the app does, its limitations, how to inspect a disputed number and a reply the CSM can send. Finish the English README with calculation assumptions, URL, setup and actual effort. Finish the honest AI log with a real caught mistake and correction. Link the guide in the UI.

**Verify:** Rehearse a successful question, the one-deal sample warning, a disputed report and model failure followed by manual recovery. Run relevant tests on the final revision and check all delivery links.

**Done when:** The public URL, repository, guide and AI log satisfy the challenge and the CSM can use the app without the engineer present.
