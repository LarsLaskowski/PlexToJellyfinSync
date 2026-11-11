# 0034: Both base images are pinned by tag and index digest in literal FROM lines

- **Status:** Proposed
- **Date:** 2026-11-11
- **Area:** Configuration
- **Source:** Issue #290
- **Supersedes:** —

## Context

The `Dockerfile` named the build image `mcr.microsoft.com/dotnet/sdk:11.0-alpine` by tag only, in the build
argument `BASE_SDK`, while the runtime image was pinned by its multi-architecture index digest through
`BASE_RUNTIME_IMAGE`, `BASE_RUNTIME_TAG` and `BASE_RUNTIME_DIGEST` (since PR #119). A moved tag could change the
build environment of a release without a diff.

`.github/dependabot.yml` assumed that Dependabot's `docker` updates read those build arguments. They do not:
Dependabot's Docker file parser (`dependabot-core`, `docker/lib/dependabot/docker/file_parser.rb`, `FROM_LINE`)
only reads a `FROM` line that names the image literally, and its file updater rewrites only such `FROM` lines. A
`FROM ${…}` matches neither, so no Docker update was ever proposed for this repository. A digest pin that nothing
refreshes keeps every release on the base image of the day it was pinned, security fixes of the image included.

## Options considered

1. **Pin the SDK the way the runtime is pinned (build arguments)** — what the issue names; symmetric with the
   existing pin. But Dependabot sees neither image, so both digests would be refreshed by hand only, and a
   forgotten refresh ships an outdated base image without any signal.
2. **Literal `FROM <image>:<tag>@sha256:<index digest>` for both stages, without build arguments** — Dependabot
   parses both lines and can propose a new digest (and, within the major, a new tag) in its grouped `docker` pull
   request. Costs: the `org.opencontainers.image.base.digest` label can no longer be derived from the pin, because
   Dependabot rewrites only `FROM` lines; and the images can no longer be swapped with `--build-arg`.
3. **As 2, with the full version tag (`11.0.0-alpine3.22`)** — every refresh is a visible tag change, which
   Dependabot proposes even where it suppresses digest-only updates. But it ties the build to one Alpine release
   whose tags .NET stops publishing, and drops the short tag the issue asks to keep.
4. **As 2, with the base labels set by the release workflow from the `Dockerfile`** — keeps `base.digest`, but adds
   a parsing step to `.github/workflows/release.yml` and a second place that must follow the `FROM` line.

## Decision

Both stages name their image literally as `<image>:<major>.0-alpine@sha256:<multi-architecture index digest>`,
with no build arguments for the images. The runtime stage keeps the `org.opencontainers.image.base.name` label as a
literal and no longer sets `org.opencontainers.image.base.digest`. What the image carries is stated under
[*Container*](../areas/configuration.md#container).

## Consequences

- Dependabot's grouped `docker` pull request is expected to refresh both digests (verified after merge, issue
  #290); a move to the next .NET major still edits both
  `FROM` lines and the `base.name` label by hand ([0033](0033-follow-the-current-dotnet-major.md)).
- If Dependabot suppresses digest-only updates for a version-like tag (its experiment
  `docker_digest_only_update_suppression`), no refresh arrives; the after-merge check of issue #290 watches for the
  first one, and option 3 is the fallback.
- The base digest of a released image is read from the `Dockerfile` at the release tag, not from a label. The
  release has no provenance attestation, so the dropped `base.digest` label was the image's only machine-readable
  record of its base; a follow-up issue adds release provenance.
