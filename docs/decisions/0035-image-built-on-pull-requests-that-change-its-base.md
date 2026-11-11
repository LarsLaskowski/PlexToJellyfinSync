# 0035: Pull requests that change the image's base build and start it in a path-filtered workflow

- **Status:** Proposed
- **Date:** 2026-11-11
- **Area:** —
- **Source:** Issue #295
- **Supersedes:** —

## Context

`.github/workflows/release.yml` was the only workflow that built the `Dockerfile`, after a release was published.
Dependabot's grouped `docker` pull request refreshes the digests of both base images
([0034](0034-base-images-pinned-in-literal-from-lines.md)), and its `dotnet-sdk` pull request raises `global.json`,
which the `Dockerfile` copies into the build stage before `dotnet publish`: an SDK version above the one in the
pinned SDK image stops that publish. Neither kind of pull request was ever built as an image before the merge, so a
digest that does not resolve, an image that does not start or a `global.json` the image's SDK cannot satisfy failed
first at release. The `main` ruleset requires the status check `build` (the job of `ci.yml`), so a new job must not
take that name.

## Options considered

1. **A separate workflow with a `paths` filter, both platforms natively** (`ubuntu-latest` and the arm64 runner
   `ubuntu-24.04-arm`, free for public repositories), build without push, then check the user and start the
   container — runs only where the base can change, with its own read-only permissions; a native arm64 build
   proves the arm64 variant of the index digest in minutes. Cost: a path-filtered check cannot be a required
   status check, because it stays pending on every pull request it skips.
2. **A job in `ci.yml` that runs on every pull request and skips its steps when no base input changed** — could be
   required, but needs change detection in shell (or a third-party filter action, a further dependency to trust)
   and inherits `ci.yml`'s workflow-level `checks: write` unless overridden.
3. **Build the image on every pull request** — no filter, also catches project-file changes, but adds a
   several-minute image build to every pull request; project-file changes are already covered locally by the
   squad's *Image build check* (`.squad/stack.md`).
4. **Build both platforms in one job under QEMU, as the release does** — matches the release build exactly, but
   .NET restore and publish under emulation take many times as long as a native build.
5. **`linux/amd64` only** — cheapest, but leaves the arm64 half of every published release unproven.
6. **`docker/build-push-action` with Buildx, as the release does** — adds two actions to pin and trust and gains
   nothing without a push or a cache; a plain `docker build` builds the same `Dockerfile`.

## Decision

Option 1: the workflow *Image build* (`.github/workflows/image.yml`) runs on pull requests to `main` that change the
`Dockerfile`, `.dockerignore`, `global.json` or the workflow itself, builds the image natively for `linux/amd64`
and `linux/arm64` with `docker build`, pushes nothing and uses no secret, and fails unless the image has the
expected platform, runs as UID 1654 and answers `/health`. How it fits into CI is described under
[*Deployment*](../ARCHITECTURE.md#deployment).

## Consequences

- A broken base digest, an unstartable runtime image or an SDK mismatch with `global.json` fails the pull request
  instead of the release; the release still builds arm64 under QEMU, which this check does not reproduce.
- The check is not a required status check; making it one needs option 2 first.
- Changes to project files are not built as an image here; they rely on the local *Image build check* and the
  release build.
