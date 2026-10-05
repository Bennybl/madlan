# Madlan Deal Explorer - Architecture

Status: revised on 2026-10-05. Steps 1-4 are merged; step 5 is under review. Later steps are planned work.

## 1. Product

Build one public Hebrew RTL page where a CSM can ask about the supplied property transactions, see calculated statistics and inspect the records behind them. Manual filters remain available when the LLM fails.

The [challenge](madlan_rnd_ops_engineer_challenge.md) and [CSV](madlan_deals_sample.csv) are the sources of truth. This document replaces earlier architecture recommendations, including those in `madlan_architecture_proposal.md` and `product_guiede.md`.

Scope: explore the supplied dataset and explain its numbers. Load it into in-memory SQLite at startup. No data upload, admin area, authentication, persistent database, separate import pipeline, rate limiting, usage quotas or historical-version system. No valuations, forecasts or external data.

## 2. Simple application structure

Use one C# ASP.NET Core application serving plain HTML/CSS/JavaScript from `wwwroot`, plus one xUnit test project. Use .NET 10, CsvHelper and Microsoft.Data.Sqlite, with direct SQL and no ORM. Build with Docker when the local SDK is unavailable; the inspected machine has .NET 8 only.

Keep three application components:

| Component | Responsibility |
|---|---|
| Dataset loader | Read the CSV once at startup and populate in-memory SQLite with raw, normalized and queryable report/deal data |
| Query service | Validate filters, construct provider-neutral query semantics, and call the deal repository |
| Deal repository | Translate a provider-neutral query into parameterized database commands and return results/evidence pages |
| Question workflow | Generate and verify structured filters, then summarize and verify calculated results using independently configured models |

Flow: browser -> ASP.NET Core -> query service -> deal repository -> database. Natural-language questions pass through the verified question workflow described below. The query service validates filters and builds a provider-neutral `DealQuery`; it never contains provider SQL. `IDealRepository` translates that query into fixed parameterized commands for its provider. Database filtering, conflict exclusion, aggregation, median calculation and pagination prevent request code from loading a full result set into application memory.

Keep ordinary classes in one application project. `QueryService` depends only on `IDealRepository`; it does not know which database implementation is registered. `SqliteDealRepository` supports the demo. Do not implement a PostgreSQL repository, add a PostgreSQL package, or configure a PostgreSQL connection in this project. The repository contract, typed query/result objects, and provider-neutral service semantics make a future PostgreSQL adapter straightforward to add. Isolate LLM provider calls behind one small interface so tests can supply stage responses. Keep workflow orchestration in an ordinary class; use a stage parameter and configuration instead of separate provider implementations for each model.

## 3. Runtime data loading and correctness

Bundle the CSV with the application, outside `wwwroot`. At startup, create an in-memory SQLite database, read and normalize the CSV, then insert all reports in one transaction before accepting requests. Keep the database read-only at the application level after loading. Changing the file requires restarting/redeploying the app; there is no reload service.

Use two tables: `Reports` (row ID, deal ID, original field JSON, normalized field JSON and quality flags) and `Deals` (deal ID, conflict status, canonical report ID and indexed query columns when usable). Retain every source row. Preserve JSON for evidence, but store filter and metric fields in typed columns: city, neighborhood, property type, rooms, date interval, price, size and supplied price per m². Add indexes matching the supported filters and evidence pagination. Keep dataset hash and coverage as application metadata.

The demo uses shared in-memory SQLite because the supplied CSV has 530 rows. Treat it as a repository implementation, not a production capacity target. A future production adapter can use PostgreSQL with typed `numeric`, `date` and text columns, the same query semantics, migrations, pooled connections and database-side median/aggregate queries. This repository does not implement that adapter. Do not use SQLite JSON scans, C# full-result materialization or unbounded contributor ID lists for large data. Return aggregate values plus a paginated evidence page; retrieve more evidence by cursor. Keep provider-specific SQL inside the relevant future repository implementation.

Use `Data Source=Madlan;Mode=Memory;Cache=Shared;Pooling=False`. Keep one keeper connection open for the application's lifetime and open separate short-lived connections for requests; do not share a connection object across concurrent requests. Dispose the keeper at shutdown. This preserves the database between requests without creating a disk file. See [Microsoft's in-memory SQLite guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases).

If the file is missing or structurally unreadable, stop startup with a clear log message. Individual missing or questionable field values become warnings. Compute a file hash for identifying the dataset in results and logs; this is just an identifier, not a version-management system.

The supplied file has 530 rows, 520 deal IDs, six exact duplicate pairs and four conflicting IDs: D100017, D100124, D100032 and D100303. It also has 26 month-only date rows, missing values, a zero price and an NIS 18,000 price.

Apply these explicit rules:

- Normalize whitespace, numeric formats, room suffixes and supported boolean values. Preserve missing values as unknown.
- Extend `NormalizeCity(string value)` using the bundled official locality catalog: prefer exact Hebrew/English matches, then conservative typo matching. Define and test a minimum similarity and a minimum gap from the next candidate before enabling automatic correction. Ambiguous or weak matches retain the original city and receive a quality flag; never choose a tied candidate. Use the same resolver for question filters, requesting clarification when needed. No LLM calls during CSV loading.
- Preserve both original and normalized data. Keep untouched `RawJson`, normalized fields, and locality resolution metadata (official code, original value, resolved value, exact/typo/unresolved status and rule version). Show corrections in evidence details. Rebuild normalization at startup; no additional persistent store is needed.
- Group by deal ID. Count identical normalized reports once, retaining all source rows. Compare business fields including reported source. Exclude conflicting groups from metrics and keep them inspectable; do not choose a winner.
- Parse ISO, day/month/year, dot-separated dates and English month/year. Preserve month precision as an interval. Include a month-only date in a date-filtered query only if the whole month is inside the range; explain partial-overlap exclusions.
- Support city, neighborhood, property type, room minimum/maximum and date range. Filters combine with AND; omitted filters impose no restriction. Four rooms means exactly four when minimum and maximum are four. Require clarification for an ambiguous neighborhood.
- Count non-conflicting deals that definitely satisfy the filters. Missing filter values cannot be treated as matches.
- Calculate median price from positive prices, and median price per m² from individual positive-price/positive-area ratios in database SQL. Missing area excludes only the latter metric. Use database-native exact numeric handling where available and round only for display, to the nearest shekel, midpoint away from zero.
- Return null for an empty metric. Show each metric's contributing count and a paginated evidence page, plus relevant exclusions and reasons.
- Preserve the supplied price per m² and flag differences greater than max(NIS 1, 1% of the computed ratio).
- Flag positive prices below NIS 100,000 but keep them eligible. Exclude zero/negative prices from price metrics. Warn when a metric has fewer than five contributors. These warning thresholds are documented heuristics.

Results describe this sample, not the housing market as a whole. A source label is a value in the CSV, not independent verification.

## 4. LLM and HTTP interface

Use structured output and four sequential LLM stages. A query means the supported typed filter object, not model-generated SQL. C# validates filters and remains responsible for all filtering, eligibility and calculations.

| Stage | Input and output | Model configuration |
|---|---|---|
| Generate query | Original Hebrew prompt, supported filters, locality vocabulary and current Israel date -> proposed filters, clarification or unsupported request | `Llm.Models.QueryGeneration` |
| Verify query | Original prompt and proposed filters -> approved/rejected/clarification with reasons; check omissions, rooms, places and dates | `Llm.Models.QueryVerification` |
| Summarize result | Original prompt, approved filters, calculated metrics, contributor IDs, exclusions and warnings -> Hebrew summary with evidence references | `Llm.Models.ResultSummary` |
| Verify result | Original prompt, approved filters, calculated evidence and candidate summary -> approved/rejected with reasons; check relevance, numeric claims, references and limitations | `Llm.Models.ResultVerification` |

Configure a different model for each stage independently; choose actual model IDs during implementation, not in this document. Record stage and model ID in request diagnostics. Use the same provider adapter with the stage's configured model. Verification is an additional check, not a guarantee of correctness: shared C# validation and deterministic calculations remain mandatory.

Only execute a generated query after query verification passes. Only display an LLM summary after result verification passes. Reject malformed verifier output and never treat a failed or unavailable verifier as approval. No automatic repair loops or retries. A failed query check asks for clarification or offers manual filters. A failed summary/check retains the deterministic metrics and evidence and reports that the summary is unavailable. Unsupported prompt requirements must not be silently dropped. Treat prompts and dataset text as untrusted data in every stage.

Keep the original prompt unchanged for both verifiers. Bind the workflow to the exact filters, dataset hash and result used for summarization. If the user edits filters, show that the request changed and invalidate the previous verification and summary; do not claim that manual edits were verified against the original question.

| Endpoint | Purpose |
|---|---|
| `GET /api/dataset` | File identifier, coverage, quality summary and filter options |
| `POST /api/interpret` | Hebrew question -> generated and verified filters or clarification |
| `POST /api/ask` | Original question -> verified filters, deterministic results and verified summary |
| `POST /api/query` | Filters -> metrics, evidence and warnings |
| `GET /api/deals/{id}` | Original reports and quality details |
| `GET /healthz` | Application is running with its dataset loaded |

Use the same query service and repository contract for manual and interpreted searches. Return a bounded evidence page and cursor; the browser requests later pages explicitly.

Use configurable deadlines: initially 15 seconds per model call, 65 seconds for the complete server workflow, and 70 seconds for the browser. Propagate cancellation and stop remaining stages after a failure. No automatic retries. Invalid model output, refusal, timeout or outage produces a clear Hebrew message and leaves manual filtering available. Handle missing API credentials the same way. There is no application rate limiter, quota store or concurrency limiter.

Keep credentials on the server. Validate request fields and render untrusted text safely. Errors have a request ID, stable code and plain Hebrew message. Log that ID, dataset hash, filters, duration and error category; never log secrets. No separate monitoring service is needed.

## 5. UI, deployment and delivery

One RTL page contains a question input, examples, editable filters, three metric cards, a supporting-record table and expandable record details. Show sample sizes, warnings, data coverage and calculation definitions. Show the verified Hebrew summary above its evidence, and show original/normalized locality values and correction status in report details. Display progress through interpretation, verification and summary stages. Show empty/error states explicitly; a failed new question must not make old results look current. Ignore stale responses from earlier requests.

Deploy one Docker container to Render with the CSV included and the API key configured as a secret. No persistent disk is needed. Verify the actual hosting configuration and any charge before provisioning. Restarting recreates and reloads the in-memory SQLite database from the bundled CSV.

Follow the [implementation plan](madlan_implementation_plan.md), backend first. Test normalization, duplicates/conflicts, dates, calculations and model failures with small hand-checked fixtures and sample regressions. Check the main RTL browser workflow and the public URL.

Deliver the URL, repository link, one-page Hebrew CSM guide with a disputed-number reply, a short English README and an honest AI work log containing a real caught mistake. Start the log during implementation.
