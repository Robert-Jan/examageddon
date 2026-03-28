# Data Protection Keys Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist ASP.NET Core Data Protection keys to the Docker named volume so user sessions survive container restarts.

**Architecture:** `Program.cs` reads a `DataProtection:KeysPath` config key; if present it calls `AddDataProtection().PersistKeysToFileSystem()`. `docker-compose.yml` sets `DataProtection__KeysPath=/data/keys`. Local dev is unaffected (no config key → default key location).

**Tech Stack:** ASP.NET Core Data Protection (built-in, no new packages)

---

## File Map

| File | Action |
|------|--------|
| `src/Examageddon.Web/Program.cs` | Add `AddDataProtection` configuration after `AddDbContext` |
| `docker-compose.yml` | Add `DataProtection__KeysPath` env var |

---

### Task 1: Wire Data Protection key persistence

**Files:**
- Modify: `src/Examageddon.Web/Program.cs`
- Modify: `docker-compose.yml`

- [ ] **Step 1: Add Data Protection configuration to Program.cs**

Open `src/Examageddon.Web/Program.cs`. After the `AddDbContext` block (currently around line 12-14), add:

```csharp
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
```

The full relevant section of `Program.cs` should now look like:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "database", "examageddon.db"))}";
builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite(connectionString));

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
```

- [ ] **Step 2: Run existing tests to confirm nothing is broken**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all 65 tests pass. Data Protection is not exercised by the test suite, so this is purely a regression check.

- [ ] **Step 3: Add the env var to docker-compose.yml**

Open `docker-compose.yml`. The `environment` block currently has two entries. Add a third:

```yaml
services:
  examageddon:
    build: .
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Data Source=/data/examageddon.db"
      DataProtection__KeysPath: /data/keys
    volumes:
      - examageddon-db:/data

volumes:
  examageddon-db:
```

- [ ] **Step 4: Build and smoke-test with Podman to verify the warning is gone**

```bash
podman build -t examageddon:test .
podman run --rm -d --name examageddon-smoke -p 8081:8080 \
  -e ConnectionStrings__DefaultConnection="Data Source=/tmp/test.db" \
  -e DataProtection__KeysPath=/tmp/keys \
  localhost/examageddon:test
sleep 3
podman logs examageddon-smoke
```

Expected: logs show `Now listening on: http://[::]:8080`. The line:
```
warn: ...DataProtection.Repositories.FileSystemXmlRepository...Storing keys in a directory...that may not be persisted outside of the container
```
should be **absent**. Instead you should see no DataProtection warnings at all.

Then clean up:

```bash
podman stop examageddon-smoke
podman rmi localhost/examageddon:test
```

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Web/Program.cs docker-compose.yml
git commit -m "feat: persist data protection keys to Docker volume"
```
