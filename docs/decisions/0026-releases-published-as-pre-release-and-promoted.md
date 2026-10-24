# 0026: Releases are published as a pre-release and promoted by the release workflow

- **Status:** Accepted
- **Date:** 2026-10-24
- **Area:** —
- **Source:** Product Manager decision
- **Supersedes:** —

## Context

The release workflow started on a pushed `v*.*.*` tag and published the Docker image without any check of the
tagged commit. The maintainer creates releases and their tags on GitHub's release page, without a console, and a
release that is already the latest release when its image build fails misleads everyone who looks at it. The
Seluna project solves this with a pre-release that its release workflow promotes after its checks.

## Options considered

1. **Keep the tag-push trigger** — no change, but the release is public before the image exists and nothing
   checks the tagged commit.
2. **Pre-release on the release page, the workflow checks the tag, pushes the image and promotes the release** —
   no console, no second manual step; until the image is pushed the release is only a pre-release. Chosen.
3. **Promote with a personal access token so that a `released` trigger can notify the website** — the token
   would hold write access to repository contents. Lost: the notification is a job of the same workflow instead.
4. **Set `latest` on the image only in the promotion job** — a failed build could never move `latest`, but the
   promotion job which holds `contents: write`, would also need the Docker Hub credentials. Lost: the tags `X.Y.Z`
   and `latest` are two names for one manifest, pushed together by one build.

## Decision

The maintainer publishes each release as a pre-release with a new `vMAJOR.MINOR.PATCH` tag on `main`. The release
workflow (`.github/workflows/release.yml`, trigger `release: published`) checks the tag format and that the tag is
on `main`, builds and pushes the image, and a separate job that holds `contents: write` and runs no project code
turns the pre-release into the latest full release. A third job notifies the website after that.

## Consequences

If the workflow fails before the promotion, the release stays a pre-release; the maintainer deletes the release and
its tag, fixes `main` and creates the release again. If only the `notify` job fails, the release is already promoted
and the maintainer re-runs that job. An image that was already pushed under that version is overwritten by the
next run. The tests and the analyzer gate are not repeated in the release workflow: they have already passed on
`main`. A release published without *Set as a pre-release* is built too but is public before the image exists; the
workflow warns about it. The separate workflow `notify-website.yml` is replaced by the `notify` job, which uses the
same secret.