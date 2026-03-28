# GitHub Actions Release Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a GitHub Actions workflow that builds the Docker image and pushes it to ghcr.io when a release is published.

**Architecture:** A single workflow file triggered on `release: published`. Uses the official Docker GitHub Actions (`login-action`, `metadata-action`, `build-push-action`) with the automatic `GITHUB_TOKEN` — no manual secrets. Image name and tags are derived from `github.repository` and the release tag automatically.

**Tech Stack:** GitHub Actions, `docker/login-action@v3`, `docker/metadata-action@v5`, `docker/build-push-action@v6`, ghcr.io

---

## File Map

| File | Action |
|------|--------|
| `.github/workflows/release.yml` | Create |

---

### Task 1: Create the release workflow

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Step 1: Create the `.github/workflows/` directory and workflow file**

Create `.github/workflows/release.yml` with the following content:

```yaml
name: Release

on:
  release:
    types: [published]

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ghcr.io/${{ github.repository }}

      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
```

Key notes:
- `permissions: packages: write` at the job level allows pushing to ghcr.io using the automatic `GITHUB_TOKEN`. No secret setup required in the repo settings.
- `docker/metadata-action` with `images: ghcr.io/${{ github.repository }}` produces two tags on a release: the version tag (e.g. `ghcr.io/owner/examageddon:v1.2.3`) and `latest`.
- `github.repository` is always lowercase-safe for ghcr.io image names.
- `context: .` uses the repo root as the Docker build context, where the `Dockerfile` lives.

- [ ] **Step 2: Validate the YAML is well-formed**

```bash
yq e '.' .github/workflows/release.yml > /dev/null && echo "YAML valid"
```

Expected output: `YAML valid`

If `yq` is not installed: `brew install yq`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "feat: add GitHub Actions release workflow to push to ghcr.io"
```
