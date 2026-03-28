# Docker Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a multi-stage Alpine Dockerfile and docker-compose.yml so the app can be built and run with `docker compose up`, with the SQLite database persisted in a named Docker volume.

**Architecture:** A two-stage Dockerfile (sdk:10.0-alpine → aspnet:10.0-alpine) publishes the web project and produces a minimal runtime image. `docker-compose.yml` wires up the named volume and injects the connection string as an environment variable. `Program.cs` reads the connection string from config (standard .NET pattern) and falls back to the existing relative path for local dev.

**Tech Stack:** .NET 10, ASP.NET Core, SQLite, Docker (multi-stage Alpine), Docker Compose v2

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `.dockerignore` | Create | Exclude build artefacts from Docker build context |
| `Dockerfile` | Create | Multi-stage Alpine build and runtime image |
| `docker-compose.yml` | Create | Named volume, port mapping, environment config |
| `src/Examageddon.Web/Program.cs` | Modify (line 11-13) | Read connection string from config with dev fallback |

---

### Task 1: Add .dockerignore

**Files:**
- Create: `.dockerignore`

- [ ] **Step 1: Create .dockerignore at repo root**

```
bin/
obj/
.vs/
.git/
.idea/
database/
docs/
*.md
```

- [ ] **Step 2: Commit**

```bash
git add .dockerignore
git commit -m "chore: add .dockerignore"
```

---

### Task 2: Update Program.cs to read connection string from config

**Files:**
- Modify: `src/Examageddon.Web/Program.cs:11-13`

- [ ] **Step 1: Replace the hardcoded DB path calculation**

Current code at lines 11-13:
```csharp
var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "database", "examageddon.db"));
builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite($"Data Source={dbPath}"));
```

Replace with:
```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "database", "examageddon.db"))}";
builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite(connectionString));
```

- [ ] **Step 2: Run the existing test suite to confirm nothing broke**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all tests pass. The tests use an in-memory SQLite via `TestDbContextFactory` and are unaffected by this change.

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Program.cs
git commit -m "feat: read DB connection string from config with dev fallback"
```

---

### Task 3: Create Dockerfile

**Files:**
- Create: `Dockerfile`

- [ ] **Step 1: Create Dockerfile at repo root**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY src/ src/
RUN dotnet publish src/Examageddon.Web/Examageddon.Web.csproj \
    -c Release \
    -o /publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /publish .

RUN mkdir /data && chown app:app /data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Examageddon.Web.dll"]
```

Key notes:
- `--no-self-contained` keeps the image small; the runtime stage provides the .NET runtime.
- `mkdir /data && chown app:app /data` ensures the `app` user can write to the volume mount point. Docker copies this directory's ownership into the named volume on first initialization.
- Runs as the built-in non-root `app` user from the ASP.NET Alpine image.

- [ ] **Step 2: Build the image to verify it compiles cleanly**

```bash
docker build -t examageddon:test .
```

Expected: build completes with no errors. Final image size should be ~120-150 MB.

- [ ] **Step 3: Smoke-test the image runs (without compose)**

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Data Source=/tmp/test.db" \
  examageddon:test
```

Expected: app starts, logs show `Now listening on: http://[::]:8080`. Stop with Ctrl+C.

- [ ] **Step 4: Remove the test image**

```bash
docker rmi examageddon:test
```

- [ ] **Step 5: Commit**

```bash
git add Dockerfile
git commit -m "feat: add multi-stage Alpine Dockerfile"
```

---

### Task 4: Create docker-compose.yml

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Create docker-compose.yml at repo root**

```yaml
services:
  examageddon:
    build: .
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Data Source=/data/examageddon.db"
    volumes:
      - examageddon-db:/data

volumes:
  examageddon-db:
```

- [ ] **Step 2: Start the stack**

```bash
docker compose up --build
```

Expected: image builds, container starts, logs show `Now listening on: http://[::]:8080`. Open `http://localhost:8080` in a browser — the app should load.

- [ ] **Step 3: Verify database persists across restarts**

Stop the stack (Ctrl+C), then start again:

```bash
docker compose up
```

Expected: app starts without re-creating the DB schema (no "Creating table…" logs), and any data entered before the restart is still present.

- [ ] **Step 4: Stop and clean up**

```bash
docker compose down
```

The named volume `examageddon-db` is intentionally NOT removed by `docker compose down` (no `--volumes` flag). This preserves data across deploys. To wipe it: `docker compose down --volumes`.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "feat: add docker-compose with named volume for SQLite"
```
