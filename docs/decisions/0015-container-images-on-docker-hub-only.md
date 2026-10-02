# 0015: Container images are published to Docker Hub only

- **Status:** Proposed
- **Date:** 2026-10-02
- **Source:** Issue #92 (Product Manager decision)
- **Supersedes:** —

## Context

The repository review (finding F-804, issue #92) found that the documentation pointed users at a GitHub
Container Registry image (`ghcr.io/larslaskowski/plextojellyfinsync`), while the only release workflow
(`.github/workflows/release.yml`) logs in to Docker Hub and pushes `networlddev/plextojellyfinsync` with
the tags `<version>` and `latest`. A user following the documented coordinates would pull from a registry
the project never populates. By the time the issue was planned, `README.md` and `docs/ARCHITECTURE.md`
already named the Docker Hub image; the bug report template still showed the GHCR coordinates.

## Options considered

1. **Also publish to GHCR** — add a `ghcr.io` image to the metadata step and `permissions: packages: write`
   to the release job. Users could pick either registry; a second registry has to be kept in sync, the
   release job needs write access to packages, and the documentation has to name two coordinates.
2. **Publish to Docker Hub only and align every reference** — no workflow or permission change; one
   coordinate (`networlddev/plextojellyfinsync`) everywhere. Users who expect GHCR find nothing there.

## Decision

Option 2, decided by the Product Manager. Release images are published only to Docker Hub as
`networlddev/plextojellyfinsync` (`<version>` and `latest`), the name held in `IMAGE_NAME` in
`.github/workflows/release.yml`. Every user-facing reference — `README.md`, `docs/ARCHITECTURE.md` and the
GitHub issue templates — uses these coordinates; no file in the repository refers to `ghcr.io`.

## Consequences

- The release workflow keeps a single registry login (`DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN`) and needs
  no `packages: write` permission.
- A new reference to the image (docs, templates, examples) must use the Docker Hub coordinates; a search
  for `ghcr.io` in the repository should come up empty.
- Revisiting this (adding GHCR as a mirror or switching registries) needs a new record that supersedes
  this one, a release workflow change, and an update of every reference listed above.