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
