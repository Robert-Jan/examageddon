# Examageddon

Self-hosted exam practice platform.

## Features

- **Multiple question types** — multiple choice (single or multi-answer), true/false statements, drag-and-drop matching
- **Configurable sessions** — fixed or random question order, immediate or end-of-session feedback, subset question count
- **Scoring presets** — percentage-based or Azure Certification scaled (0–1000) with per-exam passing thresholds
- **Bulk import** — load questions from JSON with a staged preview before committing
- **History** — per-person pass/fail history with scores and timestamps
- **Multi-user** — name-based, no accounts required

## Quickstart

Requires [Podman](https://podman.io) and [podman-compose](https://github.com/containers/podman-compose).

```bash
git clone <repo-url>
cd examageddon
podman compose up --build
```

Open [http://localhost:8080](http://localhost:8080).

The database and session keys are stored in the `examageddon-db` Podman volume and persist across container restarts. To wipe all data: `podman compose down --volumes`.

## Configuration

All configuration is via environment variables. The defaults in `docker-compose.yml` work out of the box.

| Variable | Default | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Data Source=/data/examageddon.db` | SQLite connection string |
| `DataProtection__KeysPath` | `/data/keys` | Directory for ASP.NET session key persistence |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment name |

To change the host port, edit the `ports` mapping in `docker-compose.yml`:

```yaml
ports:
  - "9090:8080"   # expose on host port 9090 instead
```

## Upgrading

```bash
git pull
podman compose up --build
```

The database schema is created with `EnsureCreated` and is a no-op when the schema already exists, so upgrades that don't change the schema are safe to run directly. For schema-breaking changes, see the release notes.

## Releasing

Tag a commit and publish a GitHub Release. The Actions workflow automatically builds the image and pushes it to:

```
ghcr.io/<owner>/examageddon:<tag>
ghcr.io/<owner>/examageddon:latest
```

No manual secrets or tokens are required — the workflow uses the automatic `GITHUB_TOKEN`.
