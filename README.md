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

Step 1 creates the application skeleton only. CSV loading, SQLite, query APIs and the Hebrew interface are added in later steps.
