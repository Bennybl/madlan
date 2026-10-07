# Madlan Deal Explorer

A Hebrew RTL application for exploring the supplied Madlan property-deal sample. The project is being built step by step from `madlan_implementation_plan.md`.

## Run locally

The backend targets .NET 10; the UI is a React app built with Vite under `src/MadlanExplorer/client`. Install the .NET 10 SDK and Node 20+, then run:

```powershell
cd src/MadlanExplorer/client
npm install
npm run build
cd ../../..
dotnet test MadlanExplorer.sln
dotnet run --project src/MadlanExplorer
```

`npm run build` writes the compiled UI into `src/MadlanExplorer/wwwroot`, which `dotnet run` serves as static files. `src/MadlanExplorer/wwwroot` is generated and gitignored — re-run the build after pulling or changing anything under `client/`. For frontend iteration with hot reload, run `npm run dev` inside `client/` instead; it proxies `/api` and `/healthz` to the backend (start `dotnet run` first).

Open `http://localhost:5000/healthz` or the port printed by ASP.NET Core.

## Run with Docker

The Docker build is self-contained — it builds the React client (Node) and the .NET backend as separate stages, so no Node or .NET SDK is required on the host:

```powershell
docker build --target test -t madlan-explorer-test .
docker build -t madlan-explorer .
docker run --rm -p 8080:8080 madlan-explorer
```

Open `http://localhost:8080/healthz`. To test the natural-language question flow with a real Grok key, copy `.env.example` to `.env`, fill in `Llm__ApiKey` and the four `Llm__Models__*` entries, and run with `--env-file .env`.

## Current scope

The application loads the bundled deals CSV into shared in-memory SQLite at startup. It preserves raw report fields and row numbers, normalizes the fields needed for later querying, and keeps the database alive for the process lifetime. It groups reports into usable and conflicting deal records without discarding any source evidence. A provider-agnostic query service validates filters and sends a query contract to the SQLite repository, which performs filtering, aggregation and bounded evidence retrieval in the database. It also loads the bundled UTF-8 Israeli-localities JSON catalog for exact city validation and future filter options. Query APIs and the Hebrew interface are added in later steps.

`israeli towns.csv` is a one-time source file and is not used at runtime. The generated `src/MadlanExplorer/Data/israeli-localities.json` is the committed application asset, so the source CSV can be deleted after this change is merged.
