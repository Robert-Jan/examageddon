# GitHub Actions Release Workflow Design

**Date:** 2026-03-28
**Status:** Approved

## Overview

Add a GitHub Actions workflow that builds the Docker image and pushes it to GitHub Container Registry (ghcr.io) whenever a release is published on GitHub. No manual secrets required — uses the automatic `GITHUB_TOKEN`.

## File

| File | Action |
|------|--------|
| `.github/workflows/release.yml` | Create |

## Trigger

```yaml
on:
  release:
    types: [published]
```

Fires when a GitHub release is published (not drafts). The release tag (e.g. `v1.2.3`) is available as `github.ref_name`.

## Permissions

Set at job level:
```yaml
permissions:
  contents: read
  packages: write
```

`packages: write` allows pushing to `ghcr.io/<owner>/<repo>` using the automatic `GITHUB_TOKEN`. No manually created secrets needed.

## Steps

1. **Checkout** — `actions/checkout@v4`
2. **Log in to ghcr.io** — `docker/login-action@v3` with `registry: ghcr.io`, username `${{ github.actor }}`, password `${{ secrets.GITHUB_TOKEN }}`
3. **Extract metadata** — `docker/metadata-action@v5` produces tags:
   - `ghcr.io/<owner>/<repo>:<release-tag>` (e.g. `ghcr.io/rjvdw/examageddon:v1.2.3`)
   - `ghcr.io/<owner>/<repo>:latest`
4. **Build and push** — `docker/build-push-action@v6` with `push: true`, `tags` and `labels` from the metadata step

## Image Name

Derived automatically from `github.repository` (lowercase). No hardcoded owner or repo name in the workflow.

## Non-Goals

- No multi-architecture builds (linux/amd64 only, GitHub-hosted runner default)
- No caching layer (GitHub Actions cache for Docker layers is optional complexity)
- No separate test step in the release workflow (tests run separately)
