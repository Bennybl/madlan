Product Definition and Architecture Handoff — Madlan Deal Explorer

Build a focused, trustworthy application that allows a Customer Success Manager to ask questions in Hebrew about the supplied residential property transactions, receive calculated answers, and inspect the records and rules behind those answers.

The primary product value is helping a CSM answer a customer confidently and investigate a disputed number.

1. Challenge requirements

The application must:

- Be deployed at a publicly accessible URL.
- Support Hebrew and right-to-left layout.
- Use an LLM for meaningful work.
- Make claims only supported by the supplied data and show their basis.
- Explain missing or questionable information in plain language.
- Handle slow, incorrect, or unavailable model responses gracefully.
- Include meaningful tests and useful debugging information.
- Include a one-page CSM guide explaining capabilities, limitations, and how to respond when a customer disputes a number.
- Include an honest AI-assisted development log with at least one actual example of catching a bad AI answer.

Delivery includes the URL, repository link, CSM guide, and AI log. The implementation should fit approximately five focused hours of work.

2. Primary user journey

A CSM asks:

“What is the median transaction price for four-room apartments in Holon in 2025?”

The application:

- Translates the question into supported, structured filters.
- Shows how it interpreted the question.
- Filters the dataset and calculates results deterministically.
- Displays transaction count, median transaction price, and median price per square meter.
- Shows the supporting transactions, reported sources, warnings, and exclusions.
- Allows the CSM to adjust filters or inspect a disputed record.

Each question is independent. Multi-turn conversation is unnecessary for the MVP.

3. MVP interface

Use one main screen containing:

- A Hebrew question input and example questions.
- Editable filters for city, property type, room count, and date range; neighborhood when specified.
- A visible interpretation of the question.
- Summary metrics, each with its own contributing-record count.
- A transaction table with identifiers, location, date, price, area, reported source, and quality warnings.
- An explanation of calculation methods and excluded records.
- Dataset version, import time, and transaction-date coverage.
- Clear loading, empty-result, and error states.

Manual filters must remain usable when the LLM is unavailable.

4. LLM responsibilities

Use the LLM to translate natural-language questions into a constrained query object.

The backend must validate its output against an explicit schema and supported fields, operators, and intents before execution.

Deterministic code owns normalization, filtering, deduplication, calculations, and factual result presentation. The LLM must not calculate metrics, invent missing values, or execute unrestricted SQL.

One model call per question should normally be sufficient. A second call for prose summarization is unnecessary.

Ambiguous requests should trigger a focused clarification. Unsupported requests, such as investment recommendations or property valuations, should receive an explanation of the available capabilities.

5. Data policy

The supplied CSV contains 530 rows and 520 unique deal identifiers, including exact duplicates, conflicting reports, inconsistent formats, missing fields, and inconsistent price-per-square-meter values.

Implement these rules:

- Preserve original rows and values for traceability.
- Normalize whitespace, numeric formats, supported boolean representations, and explicit city aliases.
- Preserve missing values as unknown rather than zero or false.
- Count identical duplicate reports once.
- Exclude conflicting reports for the same deal identifier from aggregate metrics in the MVP, while keeping them inspectable.
- Calculate price per square meter from valid price and area. Preserve and flag disagreement with the supplied value.
- Exclude nonpositive prices from price metrics.
- Flag suspicious positive values without automatically correcting them or assuming they are invalid.
- Apply eligibility rules per metric: missing area prevents price-per-square-meter calculation but does not necessarily prevent inclusion in median transaction price.
- Preserve date precision. A month-only date must not become an invented exact day. Include it in a date-range query only when the entire month falls within that range; otherwise explain its exclusion.
- Do not assume the sample represents the entire housing market.

Define median price per square meter as the median of individual eligible transactions’ price-per-square-meter values.

6. Data lifecycle and architecture direction

Treat the MVP as a static dataset snapshot. Live ingestion is not required.

Use a separate import command within the same project to:

- Read the CSV.
- Normalize and validate records.
- Identify duplicates, conflicts, and quality issues.
- Create a versioned dataset snapshot.
- Activate the new snapshot only after a successful import.

Repeated imports must not multiply records. Initially, a new full dataset replaces the active snapshot; incremental reconciliation can follow later.

Persist dataset metadata, raw reports, normalized deals, and quality flags, with links from normalized records to their source rows.

Prefer a single deployable application and SQLite for this dataset and read-only workflow. Ensure the deployment supports the chosen database lifecycle. Keep the import, query, calculation, and LLM integration components logically separated.

Keep model credentials on the server. Use parameterized queries, request-size limits, rate limiting, and a bounded model timeout.

Embeddings, vector databases, agents, queues, microservices, Kubernetes, and real-time processing have no demonstrated MVP requirement.

7. Failure behavior and observability

- No matches: explain that no transactions meet the selected criteria; do not silently broaden filters.
- Small result sets: show the sample size and warn against broad market conclusions.
- Invalid model output: reject it and offer manual filtering.
- Model timeout or outage: end the loading state and show a clear recovery path.
- Conflicting or incomplete data: explain the issue and its effect on the result.
- Failed new search: clearly distinguish any previous result from the unsuccessful request.

Associate each query with a request identifier. Log dataset version, validated filters, execution timing, and error category so a disputed result can be reproduced.

8. Acceptance criteria

The prototype is successful when:

- A CSM can complete the main workflow without developer assistance.
- Every displayed metric can be reproduced from its supporting records and documented calculation rules.
- Duplicate reports do not inflate transaction counts.
- Exclusions and differing metric sample sizes are understandable.
- Representative Hebrew questions produce correct filters, appropriate clarification, or a supported limitation response.
- An intentionally simulated model failure produces a clear message and leaves manual search functional.
- Reimporting the dataset does not duplicate records.

Prioritize tests for normalization, duplicate/conflict handling, date precision, metric calculations, query validation, and model failure behavior.

9. Scope boundaries and future extensions

Exclude property valuation, forecasts, investment advice, maps, live external data, user accounts, public CSV uploads, and multi-turn chat from the MVP.

Potential extensions include group comparisons, result export, authenticated dataset uploads, and incremental ingestion.

The architect should design the smallest implementation that meets this workflow and its trust requirements, document consequential assumptions, and make the calculation and evidence path easy to demonstrate and defend.