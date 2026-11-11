# Plan: Build and start the image in CI on pull requests that change its base

Source: Issue #295
Tier: standard — a new CI check (a behavior change of CI) that grants no permission, uses no secret and pushes nothing, so it creates or changes no security control.
Security areas touched: CI configuration (a new workflow `.github/workflows/image.yml` that runs pull request code, forks and Dependabot included)

## Problem / root cause

No pull request check builds the image: `.github/workflows/ci.yml` (job `build`, lines 13-111) restores, builds and
tests the solution only, `codeql.yml` and `fork-scope.yml` build nothing, and the one `docker` build is the
`Build and push` step of `release.yml` (lines 85-93), triggered by `release: published`. A Dependabot `docker` pull
request (digest bump, decision 0034) or `dotnet-sdk` pull request (`global.json`) is therefore first built as an
image at release.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `ci.yml` has no image build; `release.yml` is the only workflow that builds the `Dockerfile`, after a release is published | read all four files under `.github/workflows/` | confirmed |
| 2 | Both base images are pinned by tag and index digest in literal `FROM` lines | `Dockerfile` lines 3 and 16; decision 0034 | confirmed |
| 3 | `global.json` breaks the image build when it names an SDK above the image's: the `Dockerfile` copies it with `COPY . .` (line 13) before `dotnet publish` (line 14) | `global.json` (`11.0.100`, `latestFeature`); in a scratch directory with `"version": "11.0.999"` and SDK 11.0.100 installed, `dotnet --version` fails ("The command could not be loaded") | confirmed |
| 4 | The `main` ruleset requires the status checks `build`, `SonarCloud Code Analysis`, `CodeQL`, `Scope of a fork pull request`; the new job must not be named `build` | `gh api repos/larslaskowski/plextojellyfinsync/rulesets/17464030` | confirmed |
| 5 | The repository is public, so the `ubuntu-24.04-arm` runner is available without cost | to verify: the arm64 job starts on the Early draft PR, orchestrator, step 7 (public: `gh api repos/larslaskowski/plextojellyfinsync` → `"visibility": "public"`) | unverified |
| 6 | `actionlint` 1.7.12 (the *Analyzer gate*'s) accepts `runs-on: ubuntu-24.04-arm`, also through a matrix value | scratch workflow linted clean (exit 0) | confirmed |
| 7 | Docker, `curl` and `jq` exist on both runner images; MCR pulls need no login | to verify: the Early draft PR's run, orchestrator, step 7 | unverified |
| 8 | The app starts with only `Plex:BaseUrl` set; an unreachable Plex never stops the start; `/health` is always mapped, needs no login and answers `"status": "ok"` | `docs/areas/configuration.md` (*Startup check*); `Program.cs` lines 83-97; `TokenAuthMiddleware.cs` line 73 | confirmed |
| 9 | The image runs as UID 1654 (`USER $APP_UID` of the .NET base image) | to verify: the UID the Early draft PR's run prints, orchestrator, step 7 (`Dockerfile` line 36; decision 0033, *Consequences*: "user (UID 1654)") | unverified |
| 10 | The sandbox has the Docker client but no daemon, so no image can be built locally | `docker info` → no `/var/run/docker.sock` | confirmed |
| 11 | `.github/workflows/*.yml` are checked out CRLF (`* text=auto eol=crlf`); `ci.yml`/`release.yml` are CRLF in the index, `fork-scope.yml` LF | `git ls-files --eol` | confirmed |

Issue claims: the absence of an image build in `ci.yml` and the release-only build are confirmed (rows 1, 2).
Related defect found: `.squad/stack.md` *Image build check* omits `global.json` from the inputs the image build is
sensitive to (row 3), and its sandbox reproduction cannot catch it (it uses the sandbox's SDK). Unrelated gap:
`docs/ARCHITECTURE.md` *Deployment* does not list the *Fork scope* workflow (follow-up below).

## Acceptance criteria

- AC1: A pull request to `main` that changes `Dockerfile`, `.dockerignore`, `global.json` or
  `.github/workflows/image.yml` runs the workflow *Image build* with one job per platform, `linux/amd64` on
  `ubuntu-latest` and `linux/arm64` on `ubuntu-24.04-arm`, each building the image natively with `docker build`; a
  pull request that changes none of these files does not run it.
- AC2: A build that fails (for example a base digest that does not resolve) fails that platform's job; neither job
  is cancelled by the other's failure.
- AC3: After a successful build the job fails unless the image's `Os/Architecture` equals the job's platform and
  `id -u` in the image prints `1654`; both values appear in the step's log.
- AC4: After a successful build the job starts the container with `PLEXSYNC__Plex__BaseUrl=http://127.0.0.1:9` and
  `PLEXSYNC__State__Directory=/tmp`, and fails unless `GET http://127.0.0.1:8080/health` answers with a JSON
  `status` of `"ok"` within 60 seconds; on failure it prints `docker logs` of the container.
- AC5: The workflow's token has `contents: read` only, it references no secret, logs in to no registry, pushes no
  image, is triggered by `pull_request` only (never `pull_request_target`), checks out with
  `persist-credentials: false`, uses no action but `actions/checkout` pinned by the SHA the other workflows use,
  passes every `${{ }}` value to `run:` scripts through `env:`, and no job is named `build`.
- AC6: `docs/ARCHITECTURE.md` (*Deployment*) and `.squad/stack.md` (*Image build check*) state the new check as
  *Documentation updates* says.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | CI: run of this pull request (Early draft PR) | — |
| AC2 | CI: dry run A | — |
| AC3 | CI: dry run B and the green run's log | — |
| AC4 | CI: dry run B and the green run | — |
| AC5 | check: read-only review of `image.yml` | — |
| AC6 | check: Reviewer reads the diff | — |

## Verification without tests

No production or test code changes: steps 4, 5 and the *Coverage gate* are not applicable. Step 7 runs the
*Analyzer gate* (actionlint, with shellcheck on the `run:` scripts) on the new workflow. This plan asks for an
**Early draft PR**: the CI criteria can only be shown by GitHub Actions. The dry-run patches are written by the Tester in
step 6 after the Dev (*Dry-run artifacts*), applied and reverted by the orchestrator on the draft PR.

- AC1: CI: on the Early draft PR both jobs *Image (linux/amd64)* and *Image (linux/arm64)* run (the workflow file is
  in its own `paths`) and pass, orchestrator; the negative half: check: the `paths` list read by the Reviewer.
- AC2: CI: dry run A — the runtime `FROM` digest replaced by `sha256:` and 64 zeros; both jobs fail in the build
  step, orchestrator.
- AC3: CI: dry run B — `USER $APP_UID` replaced by `USER 1000` and `ASPNETCORE_URLS` set to `http://+:8081` in one
  patch; both jobs fail the user step and the health step (both run after a successful build, *Approach*), and the health
  step prints the container log, orchestrator. The platform half: check: the green run's log shows
  `linux/amd64` and `linux/arm64`.
- AC4: CI: dry run B (above) and the green run of AC1, orchestrator.
- AC5: check: Reviewer reads `image.yml`; CodeQL's `actions` analysis on the draft PR shows no alert.
- AC6: check: Reviewer.

## Approach

A new file `.github/workflows/image.yml` (LF, like `fork-scope.yml`), owned by the Dev:

- `name: Image build`; `on: pull_request` with `branches: [ main ]` and `paths:` exactly `Dockerfile`,
  `.dockerignore`, `global.json`, `.github/workflows/image.yml`; a comment says why these four and that the check
  must stay non-required (it is pending on every pull request it skips; decision 0035).
- Workflow-level `permissions: contents: read`; no `secrets.*`, no `env` from secrets.
- One job `image`, `name: Image (${{ matrix.platform }})`, `runs-on: ${{ matrix.runner }}`, `timeout-minutes: 30`,
  `strategy.fail-fast: false`, `matrix.include` of `{platform: linux/amd64, runner: ubuntu-latest}` and
  `{platform: linux/arm64, runner: ubuntu-24.04-arm}`.
- Steps, each `shell: bash` with `set -euo pipefail`, values through `env:`:
  1. `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1` with `persist-credentials: false`.
  2. *Build image* (`id: build`): `docker build --tag plextojellyfinsync:ci .` — no `--push`, no Buildx action.
  3. *Check platform and user* (`if: ${{ !cancelled() && steps.build.outcome == 'success' }}`): read
     `docker image inspect --format '{{.Os}}/{{.Architecture}}'` and `docker run --rm --entrypoint id <image> -u`,
     print both, `::error::` and exit 1 on a mismatch with `PLATFORM` or `1654`.
  4. *Start and check /health* (same `if:`): `docker run -d --name app -p 127.0.0.1:8080:8080` with the two
     variables of AC4; poll `curl -fsS --max-time 2 http://127.0.0.1:8080/health | jq -e '.status == "ok"'` every
     2 seconds for at most 60 seconds; on success print the answer; otherwise `docker logs app`, `::error::` with
     the reason, exit 1.

Steps 3 and 4 both run after a successful build so one run reports every defect. Fail-closed by design: a missing
runner label leaves the job queued (never green); a missing `jq` or `curl` fails the step under `set -e`.

New user-visible output: the job names and `::error::` lines; layout free for the Tester, approved in step 9.

## Options considered

Weighed in [0035](../../docs/decisions/0035-image-built-on-pull-requests-that-change-its-base.md): a job in `ci.yml`
with its own change detection (lost: shell or a third-party filter action, `checks: write` inherited; gains only
the ability to be required, which the issue does not ask), every pull request (lost: a multi-minute build on every
pull request; project files are covered by the local *Image build check*), QEMU for both platforms (lost: emulated
.NET publish is far slower), amd64 only (lost: the arm64 half of each release unproven), `docker/build-push-action`
(lost: two more actions to trust, no gain without push). The UID check pins `1654` rather than only "not 0": a
different UID in a digest bump breaks the volume permissions of every existing deployment (decision 0033).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `.github/workflows/image.yml` | new |
| — | `docs/ARCHITECTURE.md`, `.squad/stack.md` | documentation |

## Signatures (for the Dev's skeleton)

None: the change adds no API.

## Test files

None. Existing test call sites affected: none. Files no unit test can cover: none (no production code).

## Areas

None: the container contract (`docs/areas/configuration.md`, *Container*) is unchanged; the area excludes CI.

## Documentation updates

Sweep: `docs/ARCHITECTURE.md` *Deployment* enumerates the workflows; `.squad/stack.md` *Image build check* says
"no pull request check builds the image"; `docs/CONTRIBUTING.md` (*Checks on a fork*, *Versioning and releases*),
`README.md`, `docs/areas/*` and the PR template's checklist need no change (no changelog exists).

- `docs/ARCHITECTURE.md`, *Deployment* (Dev): a bullet **Image build** (`.github/workflows/image.yml`) after
  **CI** — triggers, both native platforms, no push and no secrets, the user and `/health` checks, not a required
  check, link to decision 0035; in the **Dependabot** bullet, that its `docker` and `dotnet-sdk` pull requests
  run it.
- `.squad/stack.md`, *Image build check* (Dev reports the exact text, the orchestrator writes it in): replace "no
  pull request check builds the image" by a sentence naming the *Image build* workflow, its four trigger paths
  and that a change to the `Dockerfile` or `global.json` is proven there, not by the sandbox reproduction
  (which uses the sandbox's SDK).

## Architecture check

No guarantee of `.squad/project.md` changes; the container contract is only checked. Images stay on Docker Hub only
(0015): nothing is pushed. The release flow (0026) is untouched.

## Security considerations

- [x] No limits on parsed input.
- [x] No exception reaches a user: CI output only; `docker logs` of a container started without token or secret.
- Fork pull requests run the `Dockerfile` of the fork with a read-only token and no secrets, as `ci.yml` already
  runs fork code; a first-time contributor's run waits for approval.

## Decision records

- `docs/decisions/0035-image-built-on-pull-requests-that-change-its-base.md` (Proposed)

## Out of scope / follow-ups

- Proposed issue: "[Docs] List the Fork scope workflow under Deployment in ARCHITECTURE.md" — body: the
  *Deployment* section lists CI, CodeQL, Dependabot and Release but not `.github/workflows/fork-scope.yml`; add a
  bullet linking `docs/CONTRIBUTING.md` (*Contributions from outside*).
- Making *Image build* a required check (needs option 2 of 0035), and building the image on project-file changes.
