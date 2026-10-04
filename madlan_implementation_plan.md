# Madlan Deal Explorer - Implementation Plan

Status: simplified on 2026-10-04. No application implementation started.

Follow the [architecture](madlan_architecture.md). The [challenge](madlan_rnd_ops_engineer_challenge.md) and [CSV](madlan_deals_sample.csv) are the sources of truth.

Implement each numbered step as a small reviewable change with its relevant checks. Steps 1-6 build the backend, 7-8 the UI, and 9-10 deployment and handoff. One application project and one test project are sufficient.

The application loads the supplied CSV into in-memory SQLite once at startup. There is no data-upload feature, admin access, persistent storage, rate limiting or usage-quota infrastructure.

## 1. Create the backend skeleton

**Change:** Create the .NET 10 ASP.NET Core application and xUnit project. Add a basic health endpoint, configuration and a Docker build path because the inspected local SDK is .NET 8. Start the README and factual AI work log.

**Verify:** Build, run the application and call the health endpoint without an LLM key.

**Done when:** A fresh checkout has a documented way to build and start the backend.

## 2. Load and normalize the CSV into in-memory SQLite

**Change:** Use CsvHelper and Microsoft.Data.Sqlite to populate the `Reports` table at startup in one transaction. Preserve raw fields and row numbers; normalize numbers, city aliases, rooms, booleans and dates. Keep month-only precision, exact decimals and unknown values. Compute a dataset hash. Use the architecture's named shared-memory connection string, a lifetime keeper connection and separate request connections. Fail startup clearly for a missing/unreadable file; flag individual field problems.

**Verify:** Read all 530 rows back from SQLite. Test the observed formats, quoted fields, missing values and date precision. Verify exact decimals and raw strings survive storage, a second connection sees the loaded rows, and closing a request connection does not erase the database. Give each test database a unique name for isolation.

**Done when:** The app loads the CSV into SQLite at runtime with no disk database or separate import command; shutdown releases the database and restart rebuilds it.

## 3. Handle duplicate and conflicting reports

**Change:** Group normalized reports by deal ID and populate the `Deals` table in the same startup transaction as `Reports`. Count identical reports once and mark differing reports as conflicting. Retain every original report and its quality flags; do not select an arbitrary conflict winner. After startup, services only read the database.

**Verify:** Confirm 520 deal groups, six duplicate pairs and the four documented conflicting IDs. Reversing row order must not change the outcome.

**Done when:** SQLite contains 530 reports and 520 deal groups, and the query service can distinguish usable deals from conflicts without losing evidence.

## 4. Implement filters and calculated results

**Change:** Read reports/deals from SQLite and pass typed values to pure C# filtering and calculation functions. Add shared filter validation. Return transaction count, median price, median per-deal price/m², metric sample sizes, contributor IDs and exclusion reasons. Implement date containment, missing-value eligibility and the architecture's warning/rounding rules. Use parameterized SQL for lookups; no ORM or generated SQL.

**Verify:** Hand-check odd/even medians, empty results, missing area, zero price, low-price warning and partial-month overlap. Regression: Holon/apartment/exactly four rooms/2025 returns D100027 only, with NIS 3,826,000 and NIS 38,260/m².

**Done when:** Every metric is reproducible from its returned records, independent of HTTP and the LLM.

## 5. Expose the query and evidence APIs

**Change:** Add dataset, query and deal-detail endpoints. Include applied filters, dataset hash, warnings and supporting reports. Add consistent Hebrew errors, request IDs and simple structured logs. Health succeeds only after data loading. Keep all data read-only.

**Verify:** API tests cover a valid query, invalid filters, no matches, missing deal and conflict inspection. Check that contributor IDs resolve to the expected reports and errors do not expose stack traces.

**Done when:** Manual filtering and number investigation work through HTTP without model access.

## 6. Add OpenAI question interpretation

**Change:** Add `IQuestionInterpreter`, an OpenAI implementation and the interpretation endpoint. Use configurable structured output for filters/clarification/unsupported results. Validate against the same filter rules as manual queries. Add a 15-second timeout and clear handling for missing keys, refusal, invalid output and provider failure. Keep provider code isolated for later replacement.

**Verify:** Test failures with a fake provider and verify manual queries still work. Run a few real Hebrew questions when credentials are available: exact rooms, aliases, dates, ambiguity and unsupported valuation. Check semantic correctness as well as JSON validity.

**Done when:** Hebrew questions produce useful validated filters, and model failures terminate cleanly. All backend tests pass before UI work begins.

## 7. Build the RTL manual explorer and evidence view

**Change:** Serve plain HTML/CSS/JavaScript with Hebrew labels and RTL direction. Add manual filters, three metrics, contributor counts, warnings, evidence table and expandable raw report details. Show coverage and calculation definitions. Add loading, empty and error states; use safe text rendering and accessible labels.

**Verify:** In the browser, run the known Holon query, inspect a conflict and a missing-area record, and check keyboard use and a narrow viewport. Confirm pagination, if used, does not change metric totals.

**Done when:** A CSM can filter, understand a number and inspect its evidence without developer tools.

## 8. Connect natural-language questions to the UI

**Change:** Add question input and example questions. Interpret, display the resulting editable filters, then query. Clarification and unsupported responses explain what to change. Add a 20-second browser deadline and ignore older responses after a new request. Clearly label old results after a failed search.

**Verify:** Exercise a successful Hebrew question, clarification, invalid output and timeout/outage. Complete a manual search after model failure. Check that an older response cannot overwrite newer results.

**Done when:** The complete question-to-evidence journey works and has an obvious manual fallback.

## 9. Deploy the application

**Change:** Package the app and CSV in one Docker image and deploy to Render. Configure the API key as a server secret and the health endpoint. Recreate in-memory SQLite on startup; no persistent disk or database service is needed. Add a small CI build/test workflow and document startup/deployment commands. Confirm any actual hosting charge before provisioning.

**Verify:** Open the public URL and run a real question plus manual query. Restart and verify the bundled dataset produces the same results. Confirm secrets are absent from browser assets and logs. Demonstrate provider failure using a local/test configuration without disrupting the public demo.

**Done when:** The application works at a public URL and can be rebuilt from the repository.

## 10. Complete the handoff

**Change:** Write the one-page Hebrew CSM guide: what the app does, its limitations, how to inspect a disputed number and a reply the CSM can send. Finish the English README with calculation assumptions, URL, setup and actual effort. Finish the honest AI log with a real caught mistake and correction. Link the guide in the UI.

**Verify:** Rehearse a successful question, the one-deal sample warning, a disputed report and model failure followed by manual recovery. Run relevant tests on the final revision and check all delivery links.

**Done when:** The public URL, repository, guide and AI log satisfy the challenge and the CSM can use the app without the engineer present.
