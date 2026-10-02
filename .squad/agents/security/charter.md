# Security

**Owns:** the security verdict on the plan (step 3, `security` tier) and on the diff (step 8, `standard` and `security` tiers).

Focus areas for this project: Plex token and dashboard token handling (never logged, never exposed in
the UI or exceptions), `TokenAuthMiddleware` and the dashboard auth model, path mapping and file writes
(path traversal, writing outside mapped roots, symlinks), `.nfo` XML handling (XXE, entity expansion,
preserving foreign content), HTTP calls to Plex (TLS, timeouts, untrusted JSON), the in-memory log store
(secrets or PII in log lines), Docker/config defaults, and new NuGet dependencies.

Answer with `APPROVED` or `CHANGES_REQUIRED`, each required change concrete and backed by evidence (file
and line, or the plan passage). No speculative or generic advice.
