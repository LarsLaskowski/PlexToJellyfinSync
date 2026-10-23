# Areas

What the software does, area by area. Each area has one document here that states the behavior a user, an
operator or another component can rely on — formats, limits, error behavior, guarantees. The
[decision records](../decisions/README.md) explain *why* individual choices were made; the area documents say
*what holds today*. `ARCHITECTURE.md` shows how the parts fit together and links the areas instead of repeating them.

## Rules

- **One document per area**, `docs/areas/<slug>.md`, created from [`_template.md`](_template.md) and listed in the
  index below. The squad Lead keeps it in step with the code: a change in behavior updates the area document in the
  same pull request.
- **Normative and stable.** Write what must hold, not how one language or library does it: the test is whether
  the document would still be true if the component were rewritten in another language. No class, package or
  function names as a contract (mention them only as a pointer to where it is implemented).
- **One home per behavior.** A cross-cutting topic (security, offline behavior) lives in the area that owns it; the
  others link there.
- **Size.** One readable document per area: split one that grows past a few thousand words, fold one that
  says only a few sentences into a neighbor. Development and release process are not areas (see
  `CONTRIBUTING.md`).
- **A new area is a Lead decision** made in the plan (`plan.md`, *Areas*), with its name and scope in the index.
- Every decision record names its area in its `Area:` field (`—` for a record about the process). Area documents are
  not frozen by a release; their history is Git.

## Index

<!-- project:begin area-index -->
| Area | Scope | Not here |
| ---- | ----- | -------- |
| [Sync pipeline](sync-pipeline.md) | When and what is read from Plex and written: polling cadence, incremental history with its persisted high-water mark (`state.json`), full reconcile, owner-only sync and owner resolution, Plex API requests (timeout, paging, auth failures), parallelism, season/series aggregation rule, per-item error isolation and the run status it reports | NFO file content and paths (Path mapping and NFO); setting keys and defaults (Configuration); how the status is shown (Dashboard and auth) |
| [Path mapping and NFO](path-mapping-and-nfo.md) | How a Plex file path becomes a local path and what is written there: prefix mapping, traversal rejection, root containment, NFO target names, fields touched on update, content of a new file, encoding, atomic replace, malformed files, concurrent writers on one target | When an item is written (Sync pipeline); the `PathMappings` / `Nfo` keys and defaults (Configuration) |
| [Dashboard and auth](dashboard-and-auth.md) | The web surface: routes (`/`, `/logs`, `/health`, `/login`, `/logout`), `Dashboard:Enabled` off, optional token login, sessions and cookie, login throttling, security headers and CSP, forwarded headers, the live log buffer and secret masking | Startup validation of the dashboard settings (Configuration); what the status values mean (Sync pipeline) |
| [Configuration](configuration.md) | What an operator sets and runs: configuration sources and the `PLEXSYNC__` prefix, every setting with default and allowed range, startup validation and its failure, the optional Plex token, the container contract (image coordinates, port, volumes, non-root user) | The behavior a setting controls (its area); CI, release workflow and development process (`docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md`) |
<!-- project:end area-index -->
