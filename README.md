# Madlan Deal Explorer

A Hebrew RTL application for exploring the supplied Madlan property-deal sample. The project is being built step by step from `madlan_implementation_plan.md`.

## Run locally

The project targets .NET 10. Install the .NET 10 SDK, then run:

```powershell
dotnet test MadlanExplorer.sln
dotnet run --project src/MadlanExplorer
```

Open `http://localhost:5000/healthz` or the port printed by ASP.NET Core.

## Run with Docker

```powershell
docker build --target test -t madlan-explorer-test .
docker build -t madlan-explorer .
docker run --rm -p 8080:8080 madlan-explorer
```

Open `http://localhost:8080/healthz`.

## Current scope

The application loads the bundled deals CSV into shared in-memory SQLite at startup. It preserves raw report fields and row numbers, normalizes the fields needed for later querying, and keeps the database alive for the process lifetime. It also loads the bundled UTF-8 Israeli-localities JSON catalog for exact city validation and future filter options. Query APIs and the Hebrew interface are added in later steps.

`israeli towns.csv` is a one-time source file and is not used at runtime. The generated `src/MadlanExplorer/Data/israeli-localities.json` is the committed application asset, so the source CSV can be deleted after this change is merged.
