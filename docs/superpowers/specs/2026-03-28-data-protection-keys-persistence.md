# Data Protection Keys Persistence Design

**Date:** 2026-03-28
**Status:** Approved

## Overview

Persist ASP.NET Core Data Protection keys to the Docker named volume so that user sessions survive container restarts. Local dev is unaffected.

## Problem

By default, Data Protection keys are stored in `/home/app/.aspnet/DataProtection-Keys` inside the container. These are lost on container restart, invalidating all existing session cookies and requiring users to re-enter their name.

## Solution

Configure Data Protection via a `DataProtection:KeysPath` config key. When set, keys are persisted to that directory. In Docker, this points to `/data/keys` on the named volume. In local dev, the key is absent and the default behaviour is preserved.

## Files Changed

| File | Change |
|------|--------|
| `src/Examageddon.Web/Program.cs` | Add conditional `AddDataProtection().PersistKeysToFileSystem()` |
| `docker-compose.yml` | Add `DataProtection__KeysPath: /data/keys` env var |

## Program.cs Change

Add after the `AddDbContext` call:

```csharp
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
```

- No `DataProtection:KeysPath` in any `appsettings*.json` — local dev uses the default key location.
- `DirectoryInfo` is created even if the directory doesn't exist yet; ASP.NET Core creates it on first startup.
- The `app` user already owns `/data` (set via `chown` in the Dockerfile), so creating `/data/keys` at runtime succeeds.

## docker-compose.yml Change

Add to the `environment` block of the `examageddon` service:

```yaml
DataProtection__KeysPath: /data/keys
```

## Non-Goals

- No key encryption at rest (acceptable for a self-hosted single-tenant app)
- No key rotation configuration (default 90-day lifetime is fine)
- No EF Core key storage (unnecessary given the volume is already present)
