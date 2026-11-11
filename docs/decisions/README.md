# Decision records

Why the code is the way it is. Each file records one decision — its context, the options considered, what
was chosen and the consequences — so that months later the reasoning is still available without the
pull request, the issue thread or the session that produced it.

A record explains the *why*. What holds today — formats, limits, error behavior, guarantees — is written down
once in the [area document](../areas/README.md) of the record's area, and the record links to it.
`docs/ARCHITECTURE.md` describes how the parts fit together. When a decision changes the architecture,
`ARCHITECTURE.md` is updated as well and links the record.

## Rules

- One decision per file: `NNNN-short-title.md` (four digits, next free number), created from
  [`_template.md`](_template.md). In the index row and in a `Superseded by NNNN` status the number may be
  written as a link, `[NNNN](NNNN-short-title.md)`.
- **The record states the decision in one to three sentences and does not restate behavior.** Context,
  *Options considered* and *Consequences* carry the reasoning; the rules of the resulting behavior (values,
  formats, limits, error texts) belong in the area document and are linked from the record.
- The `Area:` field names the record's area as listed in [`docs/areas/README.md`](../areas/README.md), or `—`
  for a record about the process or about no area. Once areas are defined, every record needs the field. For a
  released record it is part of the content: it changes only through a new record. A released record without
  the field gets its area from an `## Area assignment` table (`| Record | Area |`) in a new record; the
  released record itself stays unedited.
- Written by the squad Lead (see [`.claude/agents/squad-lead.md`](../../.claude/agents/squad-lead.md));
  anyone may add one for a change made outside the squad.
- The squad's own process decisions are no records: they stand in [`process.md`](process.md), whose introduction
  says how they change.
- Committed together with the change it explains.
- **Released or not decides how a record changes.** A record is *released* once the commit that added it is
  contained in a release tag (`v*`; check with `git tag --contains <commit>`, the commit is
  `git log --diff-filter=A --format=%H -- <file>`). Until the first release nothing is released.
- A record that is **not yet released** is edited in place, in the pull request that changes the decision
  (status stays `Proposed` or `Accepted`). Its history is Git and the pull request; there is no
  `Superseded by` chain for it. A record that no longer applies is deleted (numbers are never reused, gaps are
  fine) together with its index row and every link to it.
- A **released** record is **append-only**: it is never rewritten. A changed decision gets a new record that
  names the old one under *Supersedes*, and the old record's status becomes `Superseded by NNNN` (the only
  edit allowed). `Superseded by` is therefore only ever set on a released record.
- A new record that **amends** a released record without superseding it names the older one under
  *Supersedes* and says there, in one sentence, what it amends. The older record is **not edited** — not even
  with a pointer; a reader of the older record finds the amendment through the index and the new record.
- **One record per decision that has lasting weight — not one per pull request, and not one per topic.** Not for
  routine changes: a record is needed when a choice between real alternatives was made that shapes the
  architecture or behavior, a trade-off or limitation was accepted, or a documented guarantee was touched.
  Before adding a record, look for an unreleased record of the same decision and extend it instead (a changed
  option, a new consequence); another decision on a topic that already has a record gets a record of its own,
  which links the other. A review finding that was deliberately not fixed, a follow-up split, or a dependency
  change gets its own record only if it touches a guarantee or the architecture; otherwise it belongs in the pull
  request description or the follow-up issue, or as a sentence in the record of the decision it concerns.
- **Size.** A record that an extension would take past `RECORD_MAX_WORDS` words or `RECORD_MAX_OPTIONS` options
  (`.squad/tools/squad_settings.py`) holds more than one decision and is split instead: one record per decision,
  each with its own context, options and consequences. An unreleased record keeps its number for one of them, the
  others take the next free numbers with their index rows, and every link to it points at the record of its
  decision; a released record is never split.
- `.squad/tools/decision-check.py` (also run by `config-check.py`) checks the index, the links between records
  and that no released record was changed, and warns about an unreleased record past the size limits and an area
  document past `AREA_MAX_WORDS`, naming the split.

## Index

<!-- project:begin index -->
| #    | Title | Status | Date |
| ---- | ----- | ------ | ---- |
| [0001](0001-polling-instead-of-webhooks.md) | Poll Plex instead of using webhooks | Accepted | 2026-10-01 |
| [0002](0002-single-user-plex-owner.md) | Sync a single user: the Plex server owner | Accepted | 2026-10-01 |
| [0003](0003-nfo-only-watch-fields-touched.md) | Existing NFO files are only changed in their watch fields | Accepted | 2026-10-01 |
| [0004](0004-unmapped-paths-are-skipped.md) | Path mapping is mandatory; unmapped paths are skipped | Accepted | 2026-10-01 |
| [0005](0005-incremental-history-plus-full-reconcile.md) | Incremental history sync plus periodic full reconcile | Accepted | 2026-10-01 |
| [0006](0006-item-level-error-isolation.md) | Isolate failures per item and keep the worker alive | Accepted | 2026-10-01 |
| [0007](0007-dashboard-auth-model.md) | Dashboard is open by default, optionally protected by a token | Accepted | 2026-10-01 |
| [0008](0008-named-httpclient-for-singleton-plexclient.md) | Named HttpClient for the singleton PlexClient | Accepted | 2026-10-01 |
| [0009](0009-quality-gates-before-the-pull-request.md) | Quality gates before the pull request | Superseded by [0029](0029-process-decisions-live-in-process-md.md) | 2026-10-01 |
| [0010](0010-squash-merge-pull-requests.md) | Squash-merge pull requests | Superseded by [0029](0029-process-decisions-live-in-process-md.md) | 2026-10-01 |
| [0011](0011-local-analyzer-gate-covers-all-roslyn-diagnostics.md) | Local analyzer gate covers all Roslyn diagnostics | Accepted | 2026-10-02 |
| [0012](0012-worker-reconcile-guard-kept-as-defense-in-depth.md) | Worker keeps its reconcile guard as defense-in-depth | Superseded by [0013](0013-worker-guards-both-orchestrator-calls.md) | 2026-10-02 |
| [0013](0013-worker-guards-both-orchestrator-calls.md) | Worker guards both orchestrator calls alike | Accepted | 2026-10-02 |
| [0014](0014-squad-working-records-off-main.md) | Squad working records stay off main, and product PRs never change the squad | Superseded by [0019](0019-squad-maintained-in-the-template-repository.md) | 2026-10-02 |
| [0015](0015-container-images-on-docker-hub-only.md) | Container images are published to Docker Hub only | Accepted | 2026-10-02 |
| [0016](0016-options-validated-at-startup.md) | Options are validated at startup with DataAnnotations | Accepted | 2026-10-02 |
| [0017](0017-plex-token-stays-optional.md) | The Plex token stays optional | Accepted | 2026-10-02 |
| [0018](0018-sync-orchestrator-split-into-collaborators.md) | SyncOrchestrator is split into an item writer, a series aggregate writer and a library reconciler | Accepted | 2026-10-03 |
| [0019](0019-squad-maintained-in-the-template-repository.md) | Product PRs never change the squad; squad lessons go to the template repository | Superseded by [0029](0029-process-decisions-live-in-process-md.md) | 2026-10-03 |
| [0020](0020-squad-member-who-changed-files-runs-the-formatter.md) | In the squad, the member that changed the files runs the formatter | Superseded by [0029](0029-process-decisions-live-in-process-md.md) | 2026-10-17 |
| [0021](0021-area-documents-and-area-assignment.md) | Four area documents; released records get their area from this record | Accepted | 2026-10-23 |
| [0022](0022-forwarded-headers-from-configured-proxies-only.md) | Forwarded headers are trusted only from configured proxies; the login throttle is bounded | Accepted | 2026-10-23 |
| [0023](0023-history-run-filters-by-library-and-kind.md) | The history run filters by library on the history entry and by kind on the item | Accepted | 2026-10-24 |
| [0024](0024-null-elements-in-plex-lists.md) | Plex list elements are declared nullable; a null element is handled like an empty one | Accepted | 2026-10-24 |
| [0025](0025-plex-keys-allow-listed-before-request-paths.md) | Plex keys are checked against an allow-list before they reach a request path | Accepted | 2026-10-24 |
| [0026](0026-releases-published-as-pre-release-and-promoted.md) | Releases are published as a pre-release and promoted by the release workflow | Accepted | 2026-10-24 |
| [0027](0027-state-file-schema-version.md) | The state file records its schema version; an unknown version is set aside, not guessed at | Accepted | 2026-10-25 |
| [0029](0029-process-decisions-live-in-process-md.md) | The squad's process decisions live in process.md | Accepted | 2026-11-06 |
| [0030](0030-settings-listed-once-in-the-configuration-area.md) | The settings are listed once, in the Configuration area | Accepted | 2026-11-07 |
| [0031](0031-nfo-with-a-dtd-is-refused.md) | An existing NFO file with a document type declaration is refused, not parsed | Accepted | 2026-11-07 |
| [0032](0032-existing-nfo-size-bound.md) | An existing NFO file is read only up to a fixed size bound | Accepted | 2026-11-07 |
| [0033](0033-follow-the-current-dotnet-major.md) | The solution moves to .NET 11, an STS release, instead of staying on .NET 10 LTS | Accepted | 2026-11-11 |
| [0034](0034-base-images-pinned-in-literal-from-lines.md) | Both base images are pinned by tag and index digest in literal FROM lines | Accepted | 2026-11-11 |
| [0035](0035-image-built-on-pull-requests-that-change-its-base.md) | Pull requests that change the image's base build and start it in a path-filtered workflow | Accepted | 2026-11-11 |
<!-- project:end index -->
