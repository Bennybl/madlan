# Madlan Deal Explorer - Architecture

Status: agreed design, simplified on 2026-10-04. Implementation has not started.

## 1. Product

Build one public Hebrew RTL page where a CSM can ask about the supplied property transactions, see calculated statistics and inspect the records behind them. Manual filters remain available when the LLM fails.

The [challenge](madlan_rnd_ops_engineer_challenge.md) and [CSV](madlan_deals_sample.csv) are the sources of truth. This document replaces earlier architecture recommendations, including those in `madlan_architecture_proposal.md` and `product_guiede.md`.

Scope: explore the supplied dataset and explain its numbers. Load it into in-memory SQLite at startup. No data upload, admin area, authentication, persistent database, separate import pipeline, rate limiting, usage quotas or historical-version system. No valuations, forecasts or external data.

## 2. Simple application structure

Use one C# ASP.NET Core application serving plain HTML/CSS/JavaScript from `wwwroot`, plus one xUnit test project. Use .NET 10, CsvHelper and Microsoft.Data.Sqlite, with direct SQL and no ORM. Build with Docker when the local SDK is unavailable; the inspected machine has .NET 8 only.

Three small components are enough:

| Component | Responsibility |
|---|---|
| Dataset loader | Read the CSV once at startup and populate in-memory SQLite with raw and normalized reports |
| Query service | Read reports from SQLite, apply filters, calculate metrics and return evidence and warnings |
| Question interpreter | Translate Hebrew questions into validated filters through an LLM |

Flow: browser -> ASP.NET Core -> query service -> in-memory SQLite. Natural-language questions first pass through the question interpreter. Filtering, eligibility and decimal calculations run in C# over reports read from SQLite; this small dataset does not need a dynamic SQL query builder.

Keep ordinary classes in one application project. Only the interpreter needs an interface (`IQuestionInterpreter`) so tests can replace it and another provider can be added later.

## 3. Runtime data loading and correctness

Bundle the CSV with the application, outside `wwwroot`. At startup, create an in-memory SQLite database, read and normalize the CSV, then insert all reports in one transaction before accepting requests. Keep the database read-only at the application level after loading. Changing the file requires restarting/redeploying the app; there is no reload service.

Use two tables: `Reports` (row ID, deal ID, original field JSON, normalized field JSON and quality flags) and `Deals` (deal ID, conflict status and canonical report ID when usable). Retain every source row. Store decimal values exactly in the normalized JSON and parse them as C# decimal for calculations. Keep dataset hash and coverage as application metadata. Read from SQLite for each query; do not maintain a second long-lived copy of the dataset.

Use `Data Source=Madlan;Mode=Memory;Cache=Shared;Pooling=False`. Keep one keeper connection open for the application's lifetime and open separate short-lived connections for requests; do not share a connection object across concurrent requests. Dispose the keeper at shutdown. This preserves the database between requests without creating a disk file. See [Microsoft's in-memory SQLite guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases).

If the file is missing or structurally unreadable, stop startup with a clear log message. Individual missing or questionable field values become warnings. Compute a file hash for identifying the dataset in results and logs; this is just an identifier, not a version-management system.

The supplied file has 530 rows, 520 deal IDs, six exact duplicate pairs and four conflicting IDs: D100017, D100124, D100032 and D100303. It also has 26 month-only date rows, missing values, a zero price and an NIS 18,000 price.

Apply these explicit rules:

- Normalize whitespace, known city aliases, numeric formats, room suffixes and supported boolean values. Preserve missing values as unknown.
- Group by deal ID. Count identical normalized reports once, retaining all source rows. Compare business fields including reported source. Exclude conflicting groups from metrics and keep them inspectable; do not choose a winner.
- Parse ISO, day/month/year, dot-separated dates and English month/year. Preserve month precision as an interval. Include a month-only date in a date-filtered query only if the whole month is inside the range; explain partial-overlap exclusions.
- Support city, neighborhood, property type, room minimum/maximum and date range. Filters combine with AND; omitted filters impose no restriction. Four rooms means exactly four when minimum and maximum are four. Require clarification for an ambiguous neighborhood.
- Count non-conflicting deals that definitely satisfy the filters. Missing filter values cannot be treated as matches.
- Calculate median price from positive prices, and median price per m² from individual positive-price/positive-area ratios. Missing area excludes only the latter metric. Use C# decimal and round only for display, to the nearest shekel, midpoint away from zero.
- Return null for an empty metric. Show each metric's contributing count and records, plus relevant exclusions and reasons.
- Preserve the supplied price per m² and flag differences greater than max(NIS 1, 1% of the computed ratio).
- Flag positive prices below NIS 100,000 but keep them eligible. Exclude zero/negative prices from price metrics. Warn when a metric has fewer than five contributors. These warning thresholds are documented heuristics.

Results describe this sample, not the housing market as a whole. A source label is a value in the CSV, not independent verification.

## 4. LLM and HTTP interface

Use OpenAI structured output, initially with configurable `gpt-4.1-mini`. Keep its API details inside the interpreter implementation. Switching OpenAI models is configuration; another provider requires a small implementation of the same interface.

Make one model call per question. Provide supported filters, dataset vocabulary and the current Israel date for relative periods. Return one of: valid filters, clarification, or unsupported request. Validate the output in C# before use. Unsupported parts of a question must not be silently dropped.

The model interprets language; it does not calculate numbers or generate factual answer prose. The UI shows the interpreted filters and lets the user correct them. Clarification uses these same editable fields, without chat history.

| Endpoint | Purpose |
|---|---|
| `GET /api/dataset` | File identifier, coverage, quality summary and filter options |
| `POST /api/interpret` | Hebrew question -> validated interpretation |
| `POST /api/query` | Filters -> metrics, evidence and warnings |
| `GET /api/deals/{id}` | Original reports and quality details |
| `GET /healthz` | Application is running with its dataset loaded |

Use the same query service for manual and interpreted searches. Return all matching evidence for this small dataset; the browser can paginate the table.

Set a 15-second model timeout and a 20-second browser deadline. No automatic retries. Invalid model output, refusal, timeout or outage produces a clear Hebrew message and leaves manual filtering available. Handle missing API credentials the same way. There is no application rate limiter, quota store or concurrency limiter.

Keep credentials on the server. Validate request fields and render untrusted text safely. Errors have a request ID, stable code and plain Hebrew message. Log that ID, dataset hash, filters, duration and error category; never log secrets. No separate monitoring service is needed.

## 5. UI, deployment and delivery

One RTL page contains a question input, examples, editable filters, three metric cards, a supporting-record table and expandable record details. Show sample sizes, warnings, data coverage and calculation definitions. Show empty/error states explicitly; a failed new question must not make old results look current. Ignore stale responses from earlier requests.

Deploy one Docker container to Render with the CSV included and the API key configured as a secret. No persistent disk is needed. Verify the actual hosting configuration and any charge before provisioning. Restarting recreates and reloads the in-memory SQLite database from the bundled CSV.

Follow the [implementation plan](madlan_implementation_plan.md), backend first. Test normalization, duplicates/conflicts, dates, calculations and model failures with small hand-checked fixtures and sample regressions. Check the main RTL browser workflow and the public URL.

Deliver the URL, repository link, one-page Hebrew CSM guide with a disputed-number reply, a short English README and an honest AI work log containing a real caught mistake. Start the log during implementation.
