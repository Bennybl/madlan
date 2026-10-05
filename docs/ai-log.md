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
