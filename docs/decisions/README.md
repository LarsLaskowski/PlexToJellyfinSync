# Decision records

Why the code is the way it is. Each file records one decision — its context, the options considered, what
was chosen and the consequences — so that months later the reasoning is still available without the
pull request, the issue thread or the session that produced it.

`docs/ARCHITECTURE.md` describes *how* the system works today; these records explain *why* individual
choices were made. When a decision changes the architecture, `ARCHITECTURE.md` is updated as well and
links the record.

## Rules

- One decision per file: `NNNN-short-title.md` (four digits, next free number), created from
  [`_template.md`](_template.md).
- Written by the squad Lead (see [`.squad/agents/lead/charter.md`](../../.squad/agents/lead/charter.md));
  anyone may add one for a change made outside the squad.
- Committed together with the change it explains.
- Records are **append-only**: an accepted record is never rewritten. A changed decision gets a new record
  that names the old one under *Supersedes*, and the old record's status becomes
  `Superseded by NNNN` (the only edit allowed).
- Not for routine changes: a record is needed when a choice between real alternatives was made, a
  trade-off or limitation was accepted, a review finding was deliberately not fixed, a documented
  guarantee was touched, a dependency was added or removed, or work was split into a follow-up issue.

## Index

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
| [0009](0009-quality-gates-before-the-pull-request.md) | Quality gates before the pull request | Accepted | 2026-10-01 |
