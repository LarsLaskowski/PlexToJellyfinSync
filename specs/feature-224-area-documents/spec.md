# Spec: Introduce area documents

Source: Issue #224
Status: Draft

## Problem

The repository has the machinery for area documents but no area: the index in `docs/areas/README.md` is an
empty table (`<!-- project:begin area-index -->` with header and separator only). Contributors, operators and
the squad Lead who want to know *what holds today* — formats, limits, error behavior, guarantees — have to piece
it together from `docs/ARCHITECTURE.md` (where it is mixed with component structure and class names), the
`README.md`, `SECURITY.md`, `.squad/project.md` and twenty decision records. Plans cannot name an area under
*Areas*, and new decision records have no area to put into their `Area:` field.

Introducing the first area is not free: as soon as `docs/areas/README.md` lists one,
`.squad/tools/decision-check.py` (`check_areas`) requires every decision record to resolve to a listed area or
`—`. Records 0001–0014 are released (contained in `v1.5.0`) and append-only, so they cannot gain an `Area:` line.
The tool on `main` (since PR #229) lets a released record without the field take its area from an
`## Area assignment` table (`| Record | Area |`) in a later record; this feature uses that path.

Claims of the issue checked against the repository:

- "`docs/areas/README.md` is empty, so no area document exists" — confirmed: the area index has no row and
  `docs/areas/` holds only `README.md` and `_template.md`.
- "Once it lists an area, `decision-check.py` requires an `Area:` line in every decision record" — confirmed
  (`check_areas` returns early only while `load_areas` finds no row), now with the assignment-table exception
  for released records.
- "0001 is released in v1.5.0 and may not be edited" — confirmed: its adding commit `d626590` (#184) is
  contained in `v1.5.0` and in no earlier tag; `check_released_record` compares the file with the tag.

## Scenarios

- S1: Given a contributor who wants to know what an `.nfo` update may change, when they open
  `docs/areas/README.md`, then the *Index* table lists the areas `Sync pipeline`, `Path mapping and NFO`,
  `Dashboard and auth` and `Configuration`, each name linked to its document with a scope and a "not here"
  column, and the `Path mapping and NFO` document states the rules (only `watched` / `playcount` /
  `lastplayed` change, an unchanged value skips the write, encoding kept, …) as normative statements, without
  a class or function name as the contract.
- S2: Given the maintainer on a full clone with the release tags fetched, when they run
  `python3 .squad/tools/decision-check.py`, then it prints `Checked 21 decision records: PASS` with no
  warning: every record resolves to one of the four areas or `—`.
- S3: Given a reader of a released record (0001–0014), when they look for its area, then the record file is
  unchanged since `v1.5.0` and its area is named in the `## Area assignment` table of record 0021, which the
  decision index lists.
- S4: Given a reader of an unreleased record (0015–0019), when they open it, then it carries a line
  `- **Area:** <area>` directly after its `Date` line, as `docs/decisions/_template.md` places it; 0020 keeps
  the `- **Area:** —` it already has.
- S5: Given a reader of `docs/ARCHITECTURE.md`, when they reach the sync pipeline, the web host and dashboard,
  the configuration or the deployment section, then the section links the area document that owns the
  behavior instead of restating its values, formats and error behavior; the component structure (diagram,
  project references, collaborators, CI/release pipeline) stays in `ARCHITECTURE.md`.
- S6: Given the squad Lead planning a later change, when they fill the plan's *Areas* section or a new
  record's `Area:` field, then they take the name verbatim from the index.
- S7: Given a reviewer checking a change against `.squad/project.md` *Guarantees*, when they look up where a
  guarantee is described, then the guarantee links its area document (besides its decision record), and the
  *Integration surface* names the area document wherever it today names the behavior part of
  `docs/ARCHITECTURE.md`.
- S8: Given a contributor reading the stability policy in `docs/CONTRIBUTING.md`, when they follow the link
  of the `.nfo` guarantee, then it opens the `Path mapping and NFO` area document.

Record-to-area assignment (release state from `git log --diff-filter=A` and `git tag --contains`; the remote
tags end at `v1.5.0`, checked with `git ls-remote --tags origin`):

| Record | Released | Area | Where the area is written |
| ------ | -------- | ---- | ------------------------- |
| 0001 Poll Plex instead of webhooks | v1.5.0 | Sync pipeline | table in 0021 |
| 0002 Single user: the Plex owner | v1.5.0 | Sync pipeline | table in 0021 |
| 0003 NFO only watch fields touched | v1.5.0 | Path mapping and NFO | table in 0021 |
| 0004 Unmapped paths are skipped | v1.5.0 | Path mapping and NFO | table in 0021 |
| 0005 Incremental history plus full reconcile | v1.5.0 | Sync pipeline | table in 0021 |
| 0006 Item-level error isolation | v1.5.0 | Sync pipeline | table in 0021 |
| 0007 Dashboard auth model | v1.5.0 | Dashboard and auth | table in 0021 |
| 0008 Named HttpClient for the PlexClient | v1.5.0 | Sync pipeline | table in 0021 |
| 0009 Quality gates before the pull request | v1.5.0 | — | table in 0021 |
| 0010 Squash-merge pull requests | v1.5.0 | — | table in 0021 |
| 0011 Local analyzer gate | v1.5.0 | — | table in 0021 |
| 0012 Worker reconcile guard (superseded by 0013) | v1.5.0 | Sync pipeline | table in 0021 |
| 0013 Worker guards both orchestrator calls | v1.5.0 | Sync pipeline | table in 0021 |
| 0014 Squad working records off main (superseded by 0019) | v1.5.0 | — | table in 0021 |
| 0015 Container images on Docker Hub only | no | Configuration | `Area:` line, edited in place |
| 0016 Options validated at startup | no | Configuration | `Area:` line, edited in place |
| 0017 Plex token stays optional | no | Configuration | `Area:` line, edited in place |
| 0018 SyncOrchestrator split into collaborators | no | Sync pipeline | `Area:` line, edited in place |
| 0019 Squad maintained in the template repository | no | — | `Area:` line, edited in place |
| 0020 Member that changed the files runs the formatter | no | — | `Area:` line already present, unchanged |
| 0021 (new) Area documents and area assignment | no | — | own `Area:` line |

Areas, slugs and scopes (Lead proposal; confirmed as a Lead decision in `plan.md`, *Areas*):

| Area | Document | Scope | Not here |
| ---- | -------- | ----- | -------- |
| Sync pipeline | `sync-pipeline.md` | When and what is read from Plex and written: polling cadence, incremental history with its persisted high-water mark (`state.json`), full reconcile, owner-only sync and owner resolution, Plex API requests (timeout, paging, auth failures), parallelism, season/series aggregation rule, per-item error isolation and the run status it reports | NFO file content and paths (Path mapping and NFO); setting keys and defaults (Configuration); how the status is shown (Dashboard and auth) |
| Path mapping and NFO | `path-mapping-and-nfo.md` | How a Plex file path becomes a local path and what is written there: prefix mapping, traversal rejection, root containment, NFO target names, fields touched on update, content of a new file, encoding, atomic replace, malformed files, concurrent writers on one target | When an item is written (Sync pipeline); the `PathMappings` / `Nfo` keys and defaults (Configuration) |
| Dashboard and auth | `dashboard-and-auth.md` | The web surface: routes (`/`, `/logs`, `/health`, `/login`, `/logout`), `Dashboard:Enabled` off, optional token login, sessions and cookie, login throttling, security headers and CSP, forwarded headers, the live log buffer and secret masking | Startup validation of the dashboard settings (Configuration); what the status values mean (Sync pipeline) |
| Configuration | `configuration.md` | What an operator sets and runs: configuration sources and the `PLEXSYNC__` prefix, every setting with default and allowed range, startup validation and its failure, the optional Plex token, the container contract (image coordinates, port, volumes, non-root user) | The behavior a setting controls (its area); CI, release workflow and development process (`docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md`) |

## Errors and edge cases

- E1: A release tag is pushed from `main` while this change is open. Records 0015–0020 then become released,
  and an in-place `Area:` edit makes `decision-check.py` fail with `released in <tag> and changed since - only
  its status may change`. The user sees a failing check; the fix is to drop the in-place edit and name the
  record in 0021's table instead. The release state is re-checked against `git ls-remote --tags origin`
  right before the PR opens.
- E2: The check runs in a shallow clone (as this session's checkout is) or without tags. `decision-check.py`
  then prints `WARNING: shallow clone - released records are not checked` and passes without proving that
  0001–0014 are untouched. A PASS counts only from a full clone with tags.
- E3: The assignment table names a record that has its own `Area:` line (for example 0020 or 0015–0019) or a
  number that does not exist. The check fails with `Area assignment names <NNNN>, which has its own '- **Area:**'
  line` or `… which does not exist`.
- E4: An area is written differently in a record or the table than in the index — other case
  (`Path mapping and Nfo`), a trailing word, a hyphen-minus `-` or en dash instead of the em dash `—` (U+2014)
  for "no area". The check fails with `area '<value>' is not listed in docs/areas/README.md`.
- E5: An index row whose first cell is not a link, or links a file that does not exist. The check fails with
  `area '<name>' must link its document, [name](slug.md)` or `… links <file>, which does not exist`.
- E6: A broken link outside the decision records: `decision-check.py` checks only links from records and the
  decision index to record files. A link from an area document, `docs/ARCHITECTURE.md`, `.squad/project.md` or
  `docs/CONTRIBUTING.md` to a missing file or heading anchor is not caught by any tool; the existing anchors
  `ARCHITECTURE.md#sync-pipeline` and `ARCHITECTURE.md#deployment` (linked from `docs/CONTRIBUTING.md`) must
  keep resolving or be re-pointed.
- E7: An area document states a value that differs from the code (a range, a default, a lifetime, a header).
  A reader would rely on a wrong contract; every concrete value is taken from the code, not from older prose.
- E8: An existing document disagrees with the code. Found while specifying: `SECURITY.md` asks to keep "the
  Plex token and the Jellyfin API key" in environment variables and lists leakage of "the Jellyfin API key" in
  scope, but no option, code path or setting of the application holds a Jellyfin API key
  (`src/PlexToJellyfinSync.Core/Options/` has none). The area documents do not repeat the claim.
- E9: Number 0021 is taken on `main` by another change before this one merges. The check fails with
  `number 0021 is used twice`; the new record takes the next free number with its index row and every link.
- E10: A template refresh (`adopt-template`) rebuilds the marked files outside their `<!-- project:… -->`
  blocks. An edit outside a block in `docs/ARCHITECTURE.md`, `docs/areas/README.md`,
  `docs/decisions/README.md` or `docs/CONTRIBUTING.md` would be lost on the next refresh.
- E11: An area document grows beyond a few thousand words or shrinks to a few sentences (`docs/areas/README.md`,
  *Size*); `Configuration` is the smallest candidate and still carries the full settings list and the
  container contract.

## Non-functional requirements

- No production code, test code, build, CI or Docker file changes; no new configuration, no new log output.
- No squad or instruction file changes (`.squad/` except `project.md`, `.claude/`, `AGENTS.md`, `CLAUDE.md`,
  `.github/copilot-instructions.md`), no template-managed file (`docs/areas/_template.md`,
  `docs/decisions/_template.md`, the PR template outside its project block).
- Every new or changed file is UTF-8, CRLF line endings, no final newline (`.editorconfig`), and English.
- Area documents are normative and stable: they state what must hold, not how .NET does it; a class or
  function name appears only as a pointer to where a rule is implemented, never as the rule.
- Each area document follows `docs/areas/_template.md` (*Scope*, *Behavior*, *Limits and errors*, *Related
  decisions*, *Not here*) and stays within the size rule of `docs/areas/README.md`.
- No secret, token value, host name or user path from a real installation appears in the documents; examples
  use the placeholders the `README.md` already uses.

## Acceptance criteria

- [ ] AC1: The area index in `docs/areas/README.md` (inside its project block) has exactly four rows, in this
  order: `[Sync pipeline](sync-pipeline.md)`, `[Path mapping and NFO](path-mapping-and-nfo.md)`,
  `[Dashboard and auth](dashboard-and-auth.md)`, `[Configuration](configuration.md)`, each with a non-empty
  scope and "not here" cell; each linked file exists in `docs/areas/` and has the five sections of
  `docs/areas/_template.md` filled. (S1, S6, E5, E11)
- [ ] AC2: On a full clone with tags (`git rev-parse --is-shallow-repository` prints `false`, `git tag --list
  'v*'` matches `git ls-remote --tags origin`), `python3 .squad/tools/decision-check.py` prints `Checked 21
  decision records: PASS` and no `WARNING` line, and `python3 .squad/tools/config-check.py` passes. (S2, E2, E4,
  E9)
- [ ] AC3: `git diff v1.5.0 HEAD -- docs/decisions/0001-*.md … docs/decisions/0014-*.md` is empty, and record
  0021 has an `## Area assignment` table with exactly fourteen rows, one per record 0001–0014, each with the area
  of the assignment table in *Scenarios*, and no row for any other record. (S3, E1, E3)
- [ ] AC4: Records 0015–0019 each have exactly one `- **Area:**` line, placed after `- **Date:**`, with the area
  of the assignment table; their other lines are unchanged. Record 0020 is unchanged. Record 0021 exists from
  `docs/decisions/_template.md` with `- **Area:** —` (U+2014), status `Proposed`, and its row in the index of
  `docs/decisions/README.md` with status `Proposed`. (S4, E3, E4)
- [ ] AC5: Every concrete value in an area document (default, range, limit, lifetime, header, file name, route,
  error behavior) matches the current code and `README.md`; every class, type or function name in an area
  document is a pointer to the implementation, not the subject of a rule; no area document repeats the
  Jellyfin API key claim of `SECURITY.md`. (S1, E7, E8)
- [ ] AC6: Each area document's *Related decisions* lists exactly the records of its area from the assignment
  table (0012 and 0014 marked as superseded with their successor), each linked by its relative path from `docs/areas/` (for example
  `../decisions/0003-nfo-only-watch-fields-touched.md`); the
  process records (`—`) are listed in no area document. (S1, S3)
- [ ] AC7: The `architecture` project block of `docs/ARCHITECTURE.md` links all four area documents, and in the
  sections *Sync pipeline*, *Web host & dashboard*, *Configuration & dependency injection* and *Deployment* every
  value, format, limit and error behavior that an area document now states is replaced by a link to it, while
  the diagram, project references, component responsibilities and the CI/CodeQL/Dependabot/release description
  remain; the headings *Sync pipeline* and *Deployment* keep their anchors. (S5, E6)
- [ ] AC8: In `.squad/project.md`, the *Guarantees* introduction says the guarantees are described in the area
  documents, each guarantee links its area document besides its record, and each *Integration surface*
  reference to the behavior part of `docs/ARCHITECTURE.md` names the area document of that behavior (the
  component list and diagram references stay with `ARCHITECTURE.md`). (S7)
- [ ] AC9: The `stability` project block of `docs/CONTRIBUTING.md` links the `.nfo` guarantee to
  `areas/path-mapping-and-nfo.md` instead of `ARCHITECTURE.md#sync-pipeline`, and the *Development* paragraph of
  `README.md` names `docs/areas/README.md` as the reference for what the software guarantees. (S8)
- [ ] AC10: Every relative Markdown link in the new and changed files resolves to an existing file, and every
  `#anchor` to an existing heading of its target. (E6)
- [ ] AC11: Every edit to `docs/ARCHITECTURE.md`, `docs/areas/README.md`, `docs/decisions/README.md` and
  `docs/CONTRIBUTING.md` lies inside a `<!-- project:… -->` block; `python3 .squad/tools/scope-check.py` reports
  no file; the diff outside `specs/` touches only `docs/areas/`, `docs/decisions/`, `docs/ARCHITECTURE.md`,
  `docs/CONTRIBUTING.md`, `README.md` and `.squad/project.md`. (E10)
- [ ] AC12: Every new or changed file is UTF-8 with CRLF line endings and no final newline. (non-functional)

## Out of scope

- Production code, tests, CI and Docker changes; there is no runtime behavior change.
- Rewriting or superseding a released record (0001–0014); their text, including stale class names or wording,
  stays as released.
- Squad, instruction and template-managed files (`AGENTS.md`, `CLAUDE.md`, `.claude/`, `.squad/` other than
  `project.md`, the `_template.md` files, the PR template checklist line that names `ARCHITECTURE.md`): changes
  there go through a `squad` issue or the template repository.
- E8: correcting the "Jellyfin API key" wording of `SECURITY.md` — proposed as a follow-up issue,
  "[Docs] SECURITY.md mentions a Jellyfin API key the application does not have".
- Moving the configuration table out of `README.md`: it stays the user's setup reference, required by
  `.squad/project.md` (*Integration surface*); the `Configuration` area document is the normative statement and
  must agree with it.
- A link checker tool or a CI job for `decision-check.py`.

## Open questions

- Which records are released? Answered: from Git. The adding commits of 0001–0014 (`d626590` #184 to `487c34b`
  #191) are contained in `v1.5.0`; 0015–0020 (#192 to #214) in no tag. The remote tags end at `v1.5.0`
  (`git ls-remote --tags origin`).
- Does 0021 have to name 0001–0014 under *Supersedes*? Answered: no. `docs/decisions/README.md` (*Rules*,
  `Area:` field) gives a released record its area through an `## Area assignment` table in a new record, with
  the released record unedited; assigning an area does not change or amend the decision, and
  `decision-check.py` reads the table without a `Supersedes` link. 0021 keeps `- **Supersedes:** —`.
- Where does 0015 (Docker Hub images) belong? Answered: `Configuration`, whose scope includes the container
  contract an operator relies on (image coordinates, port, volumes, user). The release workflow itself is
  process (`docs/areas/README.md`, *Size*: release process is not an area) and stays in `ARCHITECTURE.md`.
- Where does `/health` belong? Answered: `Dashboard and auth` owns the HTTP surface, `/health` included (it is
  mapped even with `Dashboard:Enabled` false, `src/PlexToJellyfinSync/Program.cs`); the meaning of the status
  values it reports is stated in `Sync pipeline` and linked.
- Where do the high-water mark file and the Plex HTTP client belong? Answered: `Sync pipeline` — the file
  exists only to drive the incremental history sync, and the Plex requests are the pipeline's input.
- Exact area names and the no-area value? Answered: the Product Manager's names verbatim (`Sync pipeline`,
  `Path mapping and NFO`, `Dashboard and auth`, `Configuration`), compared by `decision-check.py` as exact
  strings against the link text of the index; no area is the em dash `—` (U+2014, `NO_AREA` in the tool).
- What does record 0021 decide? Answered: the four areas with their scopes and the use of the assignment table
  for the released records; its own area is `—` (a documentation-structure decision about no product area).

## Area documents

- `docs/areas/sync-pipeline.md` (new) — polling only, no webhooks; a reconcile at startup, then a poll every
  `Sync:PollIntervalSeconds` and a full reconcile every `Sync:FullReconcileIntervalHours`; runs never overlap;
  the first run seeds the high-water mark to "now" and processes nothing; the mark advances past a failing
  entry; the mark file (`state.json` in `State:Directory`) is replaced atomically, a missing file means "no
  mark", an unreadable one is moved to `state.json.corrupt` and logged; only the owner's watch state, owner id
  from `Plex:OwnerAccountId`, else resolved from Plex, fallback `1`, re-resolved after a failed cycle;
  `401`/`403` and timeouts count as a failed cycle; Plex requests time out after 30 s and library and episode
  listings are paged; `Plex:Libraries` filters libraries; library and episode parallelism bounds; episodes
  sharing one file are written one after another; season and series aggregates (watched only when every
  episode is watched, last played only then); a failure is isolated per item and per show, counted in the
  status, and never stops the worker; the status values (`plexConnected`, `isRunning`, last poll and
  reconcile, errors).
- `docs/areas/path-mapping-and-nfo.md` (new) — longest matching prefix wins; a mapping is mandatory, an
  unmapped path or an item without a file path is skipped with the warning `No path mapping for <path>,
  skipping`; `..` traversal forms are rejected; the resolved target must lie under a configured local root,
  checked independently of the mapping; NFO target names per kind and `Nfo:MovieFilenameStrategy`; an existing
  file changes only in `watched` / `playcount` / `lastplayed`, an unchanged value writes nothing, a missing last
  played removes `lastplayed`, no re-indenting, the encoding identified by the byte-order mark is kept (no BOM:
  UTF-8); a new file (only with `Sync:CreateMissingNfo`) is UTF-8 without BOM, indented, with the listed
  metadata; a malformed existing file is skipped and left untouched; every write goes through a temporary
  sibling file and an atomic replace that keeps the file mode; writers to the same target are serialized.
- `docs/areas/dashboard-and-auth.md` (new) — routes; `Dashboard:Enabled` false maps `/health` only; without
  `Dashboard:Token` the dashboard is open and a startup warning is logged; with a token: the public paths and
  static-asset types, the session cookie `pjf_auth` (`HttpOnly`, `SameSite=Strict`, `Secure` only over HTTPS),
  8-hour absolute server-side session, constant-time comparison, antiforgery on login and logout, `429` with
  `Retry-After` after 5 free failures with doubling backoff from 1 s to 5 min, 15-minute reset, 1024 tracked
  clients; the security headers and the CSP with its two Blazor-required relaxations; `X-Forwarded-Proto` is
  honoured; the live log is a ring buffer of `Dashboard:LogBufferSize` entries with the configured tokens masked
  and everything else shown verbatim.
- `docs/areas/configuration.md` (new) — sources (`appsettings.json`, environment variables with `PLEXSYNC__` and
  `__` for nesting); every setting with default and allowed values (taken from the options code, agreeing with
  the `README.md` table); startup validation stops the application before the worker and the web host start,
  with a message naming the setting and its environment variable, no network I/O; `Plex:Token` optional;
  `PathMappings` and `Nfo` are not validated at startup; the container: `networlddev/plextojellyfinsync` on
  Docker Hub (`<version>`, `latest`), port 8080, a writable media volume and `/config`, non-root user, a mounted
  `appsettings.json` must be readable by it.
- `docs/areas/README.md` — the four index rows of AC1.
- `docs/ARCHITECTURE.md` — keeps the structure and links the four documents (AC7).