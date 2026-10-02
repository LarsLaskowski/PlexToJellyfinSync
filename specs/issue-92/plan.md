# Plan: Align all image references with the Docker Hub release

Source: Issue #92
Status: Draft
Tier: trivial — the change touches only Markdown documentation and a GitHub issue template; no production
code, workflow, Docker or configuration file changes, so behavior and control flow are unaffected.

## Problem / root cause

The release workflow publishes only to Docker Hub (`.github/workflows/release.yml:15`,
`IMAGE_NAME: networlddev/plextojellyfinsync`, login with `DOCKERHUB_*` secrets at lines 35-39). The issue
was filed when `README.md` still pulled `ghcr.io/larslaskowski/plextojellyfinsync:latest`.

Current state on `main`:

- `README.md:26-48` already uses `networlddev/plextojellyfinsync` and links the Docker Hub page — fixed.
- `docs/ARCHITECTURE.md:356` already describes the Docker Hub push — fixed.
- **Remaining:** `.github/ISSUE_TEMPLATE/bug_report.md:22` still gives the example image tag
  `ghcr.io/larslaskowski/plextojellyfinsync:1.4.0`. This is the only `ghcr.io` reference left in the
  repository (`grep -rn -i ghcr` outside `.git`).
- **Minor point from the issue (F-101 cross-reference), still inconsistent:** `README.md:92` lists the
  default of `Plex:BaseUrl` as `http://plex:32400`, but `src/PlexToJellyfinSync/appsettings.json` ships
  `"BaseUrl": ""` and `PlexOptions.BaseUrl` defaults to `string.Empty`
  (`src/PlexToJellyfinSync.Core/Options/PlexOptions.cs:22`); with an empty value
  `ServiceCollectionExtensions.cs:58-61` sets no `BaseAddress`. There is no default — the value must be
  configured. `http://plex:32400` is only the example used in the quick start.

Product Manager decision: Docker Hub only, no GHCR. So the fix is documentation alignment, not a workflow
change.

## Acceptance criteria

Tier `trivial` with no production code change: no unit tests are written (steps 3-5 skipped). Each
criterion is checked by the Reviewer against the diff and with the commands given.

- [ ] AC1: `.github/ISSUE_TEMPLATE/bug_report.md` gives the image tag example as
      `networlddev/plextojellyfinsync:<version>` (e.g. `networlddev/plextojellyfinsync:1.2.2` or `latest`),
      matching the README example version.
- [ ] AC2: `grep -rn -i "ghcr" --exclude-dir=.git .` finds no match in the repository (the new decision
      record may mention `ghcr.io` only as the rejected option — it is the one allowed hit; see note below).
- [ ] AC3: The `Plex:BaseUrl` row of the README configuration table no longer claims a default of
      `http://plex:32400`; it shows `–` as the default and says the value is required, e.g.
      `Plex base URL (required, e.g. http://plex:32400)`.
- [ ] AC4: No file under `src/`, `tests/`, `.github/workflows/`, `Dockerfile`, `.squad/`, `.claude/`,
      `.github/skills/`, `CLAUDE.md`, `AGENTS.md` or `.github/copilot-instructions.md` is changed.
- [ ] AC5: Build and tests stay green (unchanged code; run once as a sanity check).

Note on AC2: `docs/decisions/0015-container-images-on-docker-hub-only.md` names the GHCR coordinates in its
*Context* and *Options considered*. That is intended; AC2 means no *usage* reference outside that record. Hits under
`specs/issue-92/` (this plan, `log.md`) are working records and are ignored as well; the folder is removed
before the PR.

## Approach

1. In `.github/ISSUE_TEMPLATE/bug_report.md:22` replace
   `` [e.g. `ghcr.io/larslaskowski/plextojellyfinsync:1.4.0` or `latest`] `` with
   `` [e.g. `networlddev/plextojellyfinsync:1.2.2` or `latest`] ``.
2. In `README.md:92` change the `Plex:BaseUrl` row to default `–` and description
   `Plex base URL (required, e.g. `http://plex:32400`)`. Do not change the default in code or
   `appsettings.json` — whether to validate required options is issue #31 (F-101), not this one.
3. Verify with `grep -rn -i ghcr --exclude-dir=.git .`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| repo | `.github/ISSUE_TEMPLATE/bug_report.md` | Image tag example uses Docker Hub coordinates |
| repo | `README.md` | `Plex:BaseUrl` row: no default, marked required |
| docs | `docs/decisions/0015-container-images-on-docker-hub-only.md` | New record (Lead) |

## Signatures (for the Dev's skeleton)

None — no code change, no skeleton (step 4 skipped).

## Documentation updates

- `README.md` configuration table, `Plex:BaseUrl` row (AC3) — Dev.
- `.github/ISSUE_TEMPLATE/bug_report.md` (AC1) — Dev.
- `docs/ARCHITECTURE.md`: no change needed (line 356 already correct). At approval the Lead may link
  decision 0015 from the release bullet there.

## Architecture check

No guarantee from `docs/ARCHITECTURE.md` is touched. The release flow described at
`docs/ARCHITECTURE.md:350-358` stays as is and is now the single documented registry.

## Security considerations

None: no workflow, permission, secret or dependency change. Not adding GHCR avoids granting the release
job `packages: write`.

## Decision records

- `docs/decisions/0015-container-images-on-docker-hub-only.md` (Proposed) — Docker Hub only vs. also
  publishing to GHCR (Product Manager decision).

## Out of scope / follow-ups

- Validating required options such as `Plex:BaseUrl` / `Plex:Token` at startup is already tracked in
  open issue #31 (F-101); this change only corrects the documented default. No new follow-up issue.
- The issue's file reference `.github/workflows/release.yml:91-116` and "computed version" describe an older
  workflow; the current one takes the version from the tag (decision is unaffected).