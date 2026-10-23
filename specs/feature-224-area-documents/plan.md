# Plan: Introduce area documents

Source: Issue #224 | [spec.md](spec.md)
Status: Draft
Tier: trivial — documentation only, with a decision record (0021) and no change to behavior or control flow (`.squad/routing.md`, *Tiers*: "a documentation change that needs a decision record").
Security areas touched: Dashboard authentication, Tokens and secrets — documentation only: `docs/areas/dashboard-and-auth.md` becomes the normative statement of the token login, sessions, cookie flags, throttle, security headers, forwarded-header trust and log masking, and it publishes the forwarded-header behavior behind spec E12; no security control is created, changed or removed, so the tier stays below `security`, but Security reviews the plan for the accuracy of those statements and the disclosure wording (step 3).

## Problem / root cause

Feature: see [spec.md](spec.md) (*Problem*); no area exists, so behavior has no normative home and no record
can name an area.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The area index is empty; `docs/areas/` holds only `README.md` and `_template.md` | `ls docs/areas`; the `area-index` block has header and separator only | confirmed |
| 2 | `decision-check.py` checks areas only once the index has a row; a released record without `Area:` needs exactly one assignment table row; a table row for a record with its own `Area:` or a missing record fails; an unreleased record without `Area:` fails | `.squad/tools/decision-check.py`, `check_areas`, `collect_assignments`, `assigned_area` | confirmed |
| 3 | 0001–0014 are released in `v1.5.0`; 0015–0020 in no tag; remote tags end at `v1.5.0` | `git log --diff-filter=A` per record and `git tag --contains` (0014 added in `487c34b` = `v1.5.0`); `git ls-remote --tags origin` | confirmed |
| 4 | 0020 already has `- **Area:** —`; no other record has an `Area:` line | `grep -c '^- \*\*Area:\*\*'` per record | confirmed |
| 5 | This session's clone is shallow, so `decision-check.py` skips the freeze checks with a warning | `git rev-parse --is-shallow-repository` prints `true`; the tool's output | confirmed |
| 6 | The release state of 0015–0020 is unchanged when the PR opens | to verify: `git ls-remote --tags origin` lists no tag after `v1.5.0`, else move the affected `Area:` edits into the 0021 table (spec E1); orchestrator, step 10 before the PR | unverified |
| 7 | `docs/CONTRIBUTING.md` links `ARCHITECTURE.md#sync-pipeline` and `#deployment`; both headings exist | `docs/CONTRIBUTING.md` (stability and releases blocks); `docs/ARCHITECTURE.md` headings | confirmed |
| 8 | The ARCHITECTURE text to be linked lies in the `architecture` project block; the *Development process* part after it is template-managed | `docs/ARCHITECTURE.md` block markers | confirmed |
| 9 | Options defaults, ranges and messages; `Nfo` and `PathMappings` not validated | `src/PlexToJellyfinSync.Core/Options/*.cs`; `ServiceCollectionExtensions.cs` lines 30–35 | confirmed |
| 10 | Security headers and CSP values; `/health` mapped regardless of `Dashboard:Enabled` | `src/PlexToJellyfinSync/Program.cs` lines 60–100 | confirmed |
| 11 | Forwarded headers: `X-Forwarded-For` and `X-Forwarded-Proto` with known networks and proxies cleared; the throttle keys on `Connection.RemoteIpAddress` | `src/PlexToJellyfinSync/Security/DashboardStartup.cs` lines 28–40; `LoginEndpoints.cs` line 61 | confirmed |
| 12 | Throttle 5 free attempts, 5 min cap, 15 min retention, 1024 clients; 8 h session; Plex client timeout 30 s; `state.json.corrupt` only for a parse error | `LoginThrottle.cs` lines 17–30; `LoginEndpoints.cs` line 21; `ServiceCollectionExtensions.cs` line 72; `StateStore.cs` lines 85–115 | confirmed |
| 13 | The first history run without a mark sets it to "now" and returns; aggregates after a history run only with `Sync:WriteSeriesSeasonAggregates` | `SyncOrchestrator.cs` lines 241–264 | confirmed |
| 14 | No Jellyfin API key exists in the application, contrary to `SECURITY.md` | no option or code path in `src/` (grep for `apikey` / `api key`) | confirmed |

## Acceptance criteria

The criteria are those of [spec.md](spec.md), AC1–AC12.

## Tests per criterion

None: the change has no production or test code; every criterion is verified under *Verification without
tests*.

## Verification without tests

Steps 4 (*Skeleton*), 5 (*Tests first*) and the *Coverage gate* are not applicable (*Changes without production
or test code*); step 6 is the Lead's documentation edits (mode `docs`, `tasks.md`), step 7 runs every other gate.

| AC | Verification | Who, when |
| -- | ------------ | --------- |
| AC1 | Read the `area-index` block: four rows in the given order, linked files exist with the five template sections | Lead after G2; Reviewer, step 8 |
| AC2 | On a full clone (`git fetch --unshallow --tags origin`, then `git rev-parse --is-shallow-repository` prints `false`; tag names of `git tag --list 'v*'` equal `git ls-remote --tags origin` without `refs/tags/` and `^{}` entries): `python3 .squad/tools/decision-check.py` prints `Checked 21 decision records: PASS` and no `WARNING`; `python3 .squad/tools/config-check.py` passes | Orchestrator, step 7 (output to the Reviewer) |
| AC3 | `git diff $(git merge-base origin/main HEAD) HEAD -- docs/decisions/00{01..14}-*.md` is empty; read the 0021 table: rows 0001–0014 once each with the spec's areas | Orchestrator, step 7; Reviewer, step 8 |
| AC4 | `git diff` of 0015–0019 shows only the `Area:` line, the area-document link (0015–0018) and the 0016 bullet cut; 0020 unchanged; 0021 and its index row `Proposed` (set `Accepted` at step 9) | Reviewer, step 8; orchestrator, step 9 |
| AC5 | Each value of each area document compared with the code location named in *Verified facts* or the file it comes from; class names only as "implemented in" pointers; no Jellyfin API key; E12 wording present in `dashboard-and-auth.md` | Lead while writing (G1); Security (plan review sets the wording bar), Reviewer, step 8 |
| AC6 | Read each *Related decisions*: exactly the spec table's records for its area, 0012 marked superseded by 0013, no `—` record | Reviewer, step 8 |
| AC7 | Read the `architecture` block: four area links; no value, format, limit or error rule left that an area document states; diagram, project references, component roles, CI/CodeQL/Dependabot/release text kept; headings *Sync pipeline* and *Deployment* unchanged | Reviewer, step 8 |
| AC8 | Read `.squad/project.md` *Guarantees* and *Integration surface* against the spec's list | Reviewer, step 8 |
| AC9 | Read the `stability` block of `docs/CONTRIBUTING.md` (four bullets linked) and the `README.md` *Development* paragraph | Reviewer, step 8 |
| AC10 | Link check of every new and changed Markdown file: each relative target exists and each `#anchor` matches a heading slug of its target (a read-only script in the session's scratch directory, not committed) | Orchestrator, step 7 (output to the Reviewer) |
| AC11 | `git diff` hunks of the four marked files lie between `<!-- project:begin … -->` and `<!-- project:end … -->`; `python3 .squad/tools/scope-check.py` clean; `git diff --name-only` outside `specs/` within the spec's list | Orchestrator, step 7; Reviewer, step 8 |
| AC12 | `file` reports CRLF for each new or changed file; a new file's last byte is not `\n`, a changed file's final line matches `git show HEAD:<file>`; no control character (`scope-check.py`) | Orchestrator, step 7 |

## Approach

1. **Area documents (G1).** One file per area from `docs/areas/_template.md`, content per the spec's *Area
   documents* outline, taken from the code (values) and from `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md`,
   `.squad/project.md` and the records (rules). Layout: the template's five `##` sections in its order; under
   *Behavior* one `###` sub-heading per topic, short normative sentences or bullets; values once, in the area
   that owns them, the other areas link. The Configuration document carries one settings table (key, environment
   variable, default, allowed values) in the README's order. `dashboard-and-auth.md` states the forwarded-header
   behavior factually ("`X-Forwarded-For` and `X-Forwarded-Proto` are accepted from any sender; the login
   throttle counts by the resulting client address") without a claim of a per-client limit and without
   describing how to exploit it — the remedy is the private follow-up (*Out of scope / follow-ups*).
2. **Records and index (G2).** In 0015–0019 insert `- **Area:** <area>` after `- **Date:**`; in 0015–0018 add one
   sentence to *Decision* that links the area document for the resulting rules; in 0016 replace the three value
   bullets (`Plex:BaseUrl`…, `Sync:PollIntervalSeconds`…, `Dashboard:LogBufferSize`…) with "The rules — required
   settings, ranges and messages — are in the [Configuration area](../areas/configuration.md)." Then add the four
   rows to the `area-index` block. Record 0021 and its index row already exist (written with this plan); the area
   index goes in last, because `decision-check.py` enforces areas from that moment.
3. **Cross-references (G3).** `docs/ARCHITECTURE.md` (project block only): add an *Areas* paragraph after the
   introduction linking the four documents; in the four sections replace each value, limit, format and error rule
   an area document now states with a link, keeping the component descriptions. `docs/CONTRIBUTING.md` stability
   block: re-link the four bullets. `README.md` *Development*: one clause naming `docs/areas/README.md`.
   `.squad/project.md`: *Guarantees* intro and links, *Integration surface* items as spec AC8.

## Options considered

- Areas: the Product Manager decided the four areas; alternatives are recorded in 0021 (*Options considered*).
- Released records: superseding 0001–0014 instead of one assignment table — rejected in 0021.
- 0016's value bullets: keep them (rejected: duplicates the Configuration document, against the record rules)
  or cut them to a link (taken, spec *Challenge*, MAJOR 2).
- Owner of the documentation edits: the Dev (step 6 default) or the Lead in mode `docs` — the Lead, because
  every file is documentation or a record the Lead owns, and the content is a reading of decisions the Lead made.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| docs | `docs/areas/sync-pipeline.md`, `path-mapping-and-nfo.md`, `dashboard-and-auth.md`, `configuration.md` | new |
| docs | `docs/areas/README.md` (`area-index` block) | four rows |
| docs | `docs/decisions/0015`–`0019` | `Area:` line; link (0015–0018); 0016 bullet cut |
| docs | `docs/decisions/0021-area-documents-and-area-assignment.md`, `docs/decisions/README.md` | new record, index row (done in step 2) |
| docs | `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md` (project blocks) | links instead of restated behavior |
| root | `README.md` | one clause |
| squad (product file) | `.squad/project.md` | *Guarantees*, *Integration surface* |

## Signatures (for the Dev's skeleton)

none

## Test files

None (no production or test code).

Existing test code that calls a changed signature: none.

Files no unit test can cover: none (the change has no code).

## Areas

New areas (Lead decision, Product Manager names): Sync pipeline (`sync-pipeline.md`), Path mapping and NFO
(`path-mapping-and-nfo.md`), Dashboard and auth (`dashboard-and-auth.md`), Configuration (`configuration.md`),
with the scopes and "not here" of the spec's area table, which go verbatim into the index. Each document is
created as the spec's *Area documents* outline says; no behavior changes.

## Documentation updates

Sweep of enumerations and "only these" statements this change extends: the `area-index` block (empty → four
rows); `.squad/project.md` *Guarantees* ("Each one is described in `docs/ARCHITECTURE.md`") and the three
*Integration surface* items naming `docs/ARCHITECTURE.md` / `README.md`; `docs/CONTRIBUTING.md` stability bullets
(`ARCHITECTURE.md#sync-pipeline`, class names); `README.md` *Development* (lists the reference documents);
`docs/decisions/README.md` index (0021). Not changed: `.github/pull_request_template.md` checklist line (outside
its project block, template-managed), `AGENTS.md` (squad instruction file), released records. All edits owned by
the Lead (`tasks.md`).

## Architecture check

No guarantee of `.squad/project.md` changes; each is restated in its area document with the same content and
linked from `.squad/project.md`. `docs/ARCHITECTURE.md` keeps the structure and gains links (its own
introduction already says it links the areas). No flow changes, so no record is linked from a new
`ARCHITECTURE.md` passage beyond 0021 being indexed.

## Security considerations

- [ ] Byte or length limits on parsed input: no limits (no code).
- [ ] Exceptions reaching a user-visible message: none (no code).
- [ ] The area documents name no token value, host or path of a real installation; the forwarded-header
  statement in `dashboard-and-auth.md` describes behavior without exploitation steps (Security, step 3).

## Decision records

- `docs/decisions/0021-area-documents-and-area-assignment.md` (Proposed) — the four areas and the assignment
  table for 0001–0014; indexed as `Proposed`.
- 0015–0019 edited in place (unreleased): `Area:` line, area link, 0016 bullet cut.

## Out of scope / follow-ups

Created by the orchestrator after the PR, not part of this change:

1. Issue "[Docs] SECURITY.md mentions a Jellyfin API key the application does not have" — body: "The
   *Deployment Security Considerations* and *Scope* sections of `SECURITY.md` refer to a Jellyfin API key, but
   the application has no such setting (see `src/PlexToJellyfinSync.Core/Options/`); only the Plex token and the
   dashboard token exist. Remove or correct the references."
2. "[Security] Login throttle can be bypassed through a spoofed X-Forwarded-For header" — body as in spec.md
   (*Out of scope*, E12). Lead decision: report it as a private GitHub security advisory, as `SECURITY.md` asks
   for vulnerabilities, not as a public issue, unless the Product Manager decides otherwise; Security confirms the
   finding in step 3.