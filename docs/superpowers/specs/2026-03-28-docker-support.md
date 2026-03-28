# Docker Support Design

**Date:** 2026-03-28
**Status:** Approved

## Overview

Add Docker support to Examageddon using a multi-stage Alpine-based Dockerfile and a docker-compose.yml for easy single-command startup. The SQLite database is persisted in a named Docker volume. Local dev workflow is unchanged.

## Files Changed

| File | Change |
|------|--------|
| `Dockerfile` | New — multi-stage Alpine build |
| `docker-compose.yml` | New — named volume, environment config |
| `src/Examageddon.Web/Program.cs` | Small update — read connection string from config with dev fallback |

## Dockerfile

Two-stage build, both using Alpine base images:

**Build stage** (`mcr.microsoft.com/dotnet/sdk:10.0-alpine`):
- Copies the full `src/` directory
- Restores NuGet packages
- Publishes `Examageddon.Web` in Release mode (`dotnet publish`) with output to `/publish`

**Runtime stage** (`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`):
- Copies `/publish` from build stage
- `WORKDIR /app`
- Exposes port `8080`
- Sets `ASPNETCORE_URLS=http://+:8080`
- Runs as the built-in non-root `app` user
- Entrypoint: `dotnet Examageddon.Web.dll`

## docker-compose.yml

Single service `examageddon`:
- Built from `Dockerfile` at repo root
- Port mapping: `8080:8080`
- Named volume `examageddon-db` mounted at `/data`
- Environment:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ConnectionStrings__DefaultConnection=Data Source=/data/examageddon.db`

Volume `examageddon-db` is declared at the top level so Docker manages its lifecycle independently of the container.

## Program.cs Change

Replace the hardcoded path calculation with a config-first approach:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "database", "examageddon.db"))}";

builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite(connectionString));
```

- **Docker**: `ConnectionStrings__DefaultConnection` env var from compose resolves to `/data/examageddon.db` on the named volume.
- **Dev (`dotnet run`)**: No `ConnectionStrings:DefaultConnection` in config, falls back to the existing relative path. No dev workflow change.

## Data Flow

```
docker compose up
  → builds image (sdk:10.0-alpine → aspnet:10.0-alpine)
  → creates named volume examageddon-db (if not exists)
  → starts container with /data mounted
  → app reads ConnectionStrings__DefaultConnection env var
  → EnsureCreatedAsync() creates DB at /data/examageddon.db on first run
  → data persists across container restarts via the volume
```

## Non-Goals

- No HTTPS in the container (terminate TLS at a reverse proxy)
- No `.env` file (single service, compose file is self-documenting enough)
- No changes to dev workflow or `appsettings.json`
