# Plan: Pin the Dockerfile's SDK build image by digest

Source: Issue #290
Tier: security — the change pins what the release build may pull (the build environment of a release), a control on the trust of the build, and replaces the mechanism that keeps both base-image pins current.
Security areas touched: Docker/build configuration (`Dockerfile`); CI configuration (`.github/dependabot.yml`, a comment only)

## Problem / root cause

`Dockerfile:1` (`ARG BASE_SDK="mcr.microsoft.com/dotnet/sdk:11.0-alpine"`) and `Dockerfile:6` (`FROM ${BASE_SDK} AS
build`) resolve the SDK image by its mutable tag. The deeper defect: both images reach `FROM` only through build
arguments (`Dockerfile:6`, `Dockerfile:19`), which Dependabot's `docker` ecosystem cannot read, so neither pin has
ever been refreshed by Dependabot; `.github/dependabot.yml:46-47` claims the opposite.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The SDK image is named by tag only; the runtime image by index digest | `Dockerfile:1`, `:4`, `:6`, `:19` | confirmed |
| 2 | (issue) Dependabot's docker updates raise both images today | Dependabot's parser (`dependabot-core` main, read 2026-11-11, `docker/lib/dependabot/docker/file_parser.rb` `FROM_LINE` = `^FROM\s+(--platform=\S+\s+)?(REGISTRY/)?IMAGE(:TAG)?(@sha256:DIGEST)?`, `IMAGE` starts with `[a-z\d]`) cannot match `FROM ${…}`; its updater (`docker/lib/dependabot/shared/shared_file_updater.rb:137`) rewrites only `FROM` lines naming the image. The repository's PR list holds no Dependabot `docker` PR ever (only `nuget` and `actions`), while the runtime pin dates from July (PR #119) — Dependabot raises neither | refuted |
| 3 | A literal `FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine@sha256:<64 hex> AS build` parses as image `dotnet/sdk`, registry `mcr.microsoft.com`, tag and digest; the dependency names are `dotnet/sdk` and `dotnet/aspnet`; the ignore pattern `dotnet/*` matches them, `mcr.microsoft.com/dotnet/*` does not | same files; `shared_file_parser.rb` `build_dependency` uses the `image` capture as the name ; run as AC3's check | confirmed |
| 4 | Dependabot refreshes the digest of a `tag@digest` when the tag is unchanged | to verify: after merge, the first grouped docker PR after MCR's next `11.0-alpine` refresh bumps both digests; owner: maintainer (PM), linked verification issue (step 10). Read so far: `update_checker.rb` `digest_requirement_up_to_date?` refreshes it, but `digest_only_update_suppressed?` (experiment `docker_digest_only_update_suppression`) skips it for a version-like tag such as `11.0-alpine` when enabled | unverified |
| 5 | The SDK tag `sdk:11.0-alpine` is a multi-architecture index covering `linux/amd64` and `linux/arm64` (the release builds both) | `docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:11.0-alpine` on 2026-11-11: list `sha256:52556c0fb9d0f609ae9aeab9efeeba96f4acc2f7038f7696f2235ccbdbb8e290`, platforms amd64, arm/v7, arm64 (digest as of today; re-resolved in step 6) | confirmed |
| 6 | The runtime pin is the current index digest of `aspnet:11.0-alpine` | same command: `sha256:d39b72ad…41bf5` = `Dockerfile:4` (as of today) | confirmed |
| 7 | No workflow or document passes `--build-arg BASE_*` | grep over the repository: only `Dockerfile` and `.github/dependabot.yml:47` name them; `release.yml:88-95` passes none | confirmed |
| 8 | The script dotnet_major.py reads a literal `FROM mcr.microsoft.com/dotnet/…:11.0…` | `.squad/tools/dotnet_major.py:28` (`IMAGE`), `:72-74`; run in AC5 | confirmed |
| 9 | The runtime digest pin is released (v1.6.0 exists, PR #119 predates it) | `git ls-remote --tags origin`; no record covers the pin, so 0034 is new | confirmed |

## Acceptance criteria

- AC1: The build stage is `FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine@sha256:<digest> AS build`, where
  `<digest>` is the index digest the tag resolves to in step 6 and that index lists `linux/amd64` and `linux/arm64`.
- AC2: The runtime stage is `FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine@sha256:<digest>`, with the same
  condition on `<digest>`.
- AC3: The `Dockerfile` declares no build argument naming an image, tag or digest, and Dependabot's `FROM_LINE`
  matches both `FROM` lines with tag `11.0-alpine` and their digests.
- AC4: The image builds; its `org.opencontainers.image.base.name` label is
  `mcr.microsoft.com/dotnet/aspnet:11.0-alpine`, it has no `org.opencontainers.image.base.digest` label, the
  `description` and `licenses` labels are unchanged, and it runs as UID 1654.
- AC5: `dotnet_major.py` passes and reports both images; with either tag changed to `12.0-alpine` in a scratch copy,
  it fails naming that `FROM` line.

## Tests per criterion

None: every criterion is verified under *Verification without tests*.

## Verification without tests

No production or test code changes: steps 4, 5 and the *Coverage gate* are not applicable.

- AC1, AC2: check: Dev in step 6 runs `docker buildx imagetools inspect` for both tags and writes the index digests
  it prints (not a platform digest) into the `FROM` lines; the output's *Manifests* list both platforms. The
  Reviewer re-runs it; a digest moved by MCR in between is no finding.
- AC3: check: Dev, step 7 — a Ruby script in the scratchpad with `REGISTRY`, `IMAGE`, `TAG`, `NAME` and `FROM_LINE`
  copied verbatim from `dependabot-core` (commit SHA named in the hand-back) prints the captures of every
  `Dockerfile` line it matches: both `FROM` lines with `image`, `tag`, `digest`; against `git show
  origin/main:Dockerfile` it prints no match. `grep -n '^ARG' Dockerfile` prints nothing.
- AC3: after merge: maintainer (Product Manager), verification issue of step 10 — right after the merge, run
  Dependabot's docker "Check for updates" (Insights > Dependency graph > Dependabot) and confirm the job log lists
  `dotnet/sdk` and `dotnet/aspnet` (AC3's parse on the live service). Then watch for MCR's refresh with `docker buildx
  imagetools inspect mcr.microsoft.com/dotnet/aspnet:11.0-alpine`: a `Digest` that differs from the `Dockerfile` pin
  starts a two-week clock, within which a grouped `docker` PR bumps both digests; if none comes, Fact 4's
  suppression applies and option 3 of 0034 is the follow-up.
- AC4: check: Dev — *Image build check* (`docker build -f Dockerfile -t plexsync-290 .`), then `docker inspect
  --format '{{json .Config.Labels}}' plexsync-290` and `docker run --rm --entrypoint id plexsync-290`.
- AC5: check: Dev — *Analyzer gate* with `--lint-only` (hadolint, `dotnet_major.py`) clean on the branch, with the
  same image count as on origin/main; `python3 -c "import sys; sys.path.insert(0,'.squad/tools'); import dotnet_major
  as d; print(d.image_majors('Dockerfile', open('Dockerfile').read()))"` lists both `FROM` lines (SDK and aspnet,
  major 11, with digest); `dotnet_major.py` on a scratch worktree copy with one tag changed to `12.0-alpine` fails
  on that line.

## Approach

`Dockerfile`: delete the four leading `ARG` lines and the three `ARG` lines after the runtime `FROM`; line 6 becomes
the literal SDK `FROM` of AC1, line 19 the literal runtime `FROM` of AC2; above the first `FROM` stands, verbatim,
`# Both base images are pinned by tag and multi-architecture index digest in the FROM line itself, the only place
Dependabot's docker updates read (decision 0034).` `LABEL org.opencontainers.image.base.name` becomes the
literal `"mcr.microsoft.com/dotnet/aspnet:11.0-alpine"`, with a comment directly above it saying that the label
names the image and tag of the runtime `FROM` line and is edited with it; the `base.digest` label line is deleted. Nothing else in
the file changes (no `--platform`: the release builds the SDK stage per target platform as today).

`.github/dependabot.yml:45-47`: the comment keeps its first sentence and replaces the second with: Dependabot reads
only `FROM` lines that name the image, so both images stand there literally with tag and digest (decision 0034).
It states no refresh and no ignore pattern beyond what Facts 2 and 3 show.

## Options considered

Weighed in [0034](../../docs/decisions/0034-base-images-pinned-in-literal-from-lines.md): the issue's build-argument
pin (loses: Dependabot sees neither image, Fact 2), the full version tag (loses: ties the build to one Alpine
release and drops the short tag), and base labels written by the release workflow (loses: a second place to follow
the `FROM` line, in the release workflow).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `Dockerfile` | literal pinned `FROM` lines, build arguments and `base.digest` label removed |
| — | `.github/dependabot.yml` | comment corrected |

## Signatures (for the Dev's skeleton)

None: the change adds no API member.

## Test files

None. Existing test code that calls a changed signature: none. Files no unit test can cover: `Dockerfile`,
`.github/dependabot.yml` — build and CI configuration, verified as above.

## Areas

Configuration: *Container* gains the base image and its labels (below).

## Documentation updates

All by the Dev, once the build is green:

- `docs/areas/configuration.md`, *Container*: a bullet before the closing "Implemented in" line — the image is
  built on `mcr.microsoft.com/dotnet/aspnet:<major>.0-alpine`, pinned by digest; its `org.opencontainers.image.base.name`
  label names that image and tag; it carries no `base.digest` label, the digest of a release is the one in the
  `Dockerfile` at its tag.
- `docs/ARCHITECTURE.md:293-295` (*Deployment*): both stages pinned by tag and multi-architecture index digest in
  literal `FROM` lines, linking decision 0034; `:309-312` (*Dependabot*), verbatim: "its docker updates read the two
  literal FROM lines and propose a new digest within the .NET major (decision 0034)."
- Sweep result: no other enumeration of the build arguments (Fact 7). `dotnet_major.py`'s docstring example names
  `ARG BASE_SDK`; it is a squad file and stays (it still describes a supported form). No changelog file exists; the
  PR template asks for nothing else.

## Architecture check

No guarantee of `.squad/project.md` changes: port, user, volumes and settings of the container are untouched. The
release image's contents change only in the dropped `base.digest` label.

## Security considerations

- [x] No limits: no parsed input.
- [x] None: no user-visible failure message changes.
- A stale pin is the risk this design trades for reproducibility; AC3's after-merge check makes a missing refresh
  visible instead of silent (fails closed into a follow-up, never into an unpinned build).

## Decision records

- `docs/decisions/0034-base-images-pinned-in-literal-from-lines.md` (Proposed)

## Out of scope / follow-ups

Follow-up issues the orchestrator creates in step 10:

- **Record the base image digest in release provenance** — the release has no provenance attestation, and with the
  `base.digest` label gone the published image carries no machine-readable base digest; add a provenance
  attestation (or an equivalent) to `.github/workflows/release.yml`.
- **Build the image in CI on pull requests that change the `Dockerfile`** — `ci.yml` builds no image, so a
  Dependabot digest bump is first built at release; build it on such pull requests.

## Challenge notes

Devil's Advocate, plan mode, verdict `OBJECTIONS 0 major, 3 minor` (verbatim):

1. **minor:** The published image loses its only machine-readable record of its base image. The release has no provenance attestation (`imagetools inspect … --format '{{json .Provenance.SLSA.materials}}'` prints `null`), so `base.digest` is the only base-digest record operators and image scanners can read today. Why it matters: dropping it is visible on the published artifact, and 0034 does not say so. The Lead adds one sentence to 0034's *Consequences* naming this loss and the follow-up (release provenance, already under *Out of scope*). The orchestrator opens that follow-up as a linked issue and names the label removal in the PR description.
2. **minor:** The `org.opencontainers.image.base.name` label becomes a hand-written literal. Neither Dependabot (it only rewrites `FROM` lines) nor `dotnet_major.py` (it only reads `FROM` and `ARG` lines, `dotnet_major.py:73-80`) checks it. Why it matters: on the next major move it can silently disagree with the `FROM` line. The Dev puts the comment the plan already plans directly above that `LABEL` (or extends it there), stating that the label must name the image and tag of the runtime `FROM` line and is edited with it.
3. **minor:** AC5's "reports both images" cannot be observed in the tool's output. On success `dotnet_major.py` prints only a count (`dotnet_major.py:142-143`, today: `.NET major: 11 everywhere (7 target framework(s) and image(s); …)`). Why it matters: the Dev and Reviewer need a check they can actually see. The Dev verifies AC5 by running `python3 -c "import sys; sys.path.insert(0,'.squad/tools'); import dotnet_major as d; print(d.image_majors('Dockerfile', open('Dockerfile').read()))"` and confirming it lists both `FROM` lines (SDK and aspnet, major 11, with digest), next to the gate's PASS. The count must stay the same as on origin/main. The failing check on the scratch copy stays as planned.

Answered (Lead, revise): 1 accepted — 0034 *Consequences* names the loss, the follow-up is under *Out of scope /
follow-ups*; 2 accepted — the `LABEL` comment is in *Approach*; 3 accepted — the `image_majors` check is in AC5's
verification.

## Security notes

Security, plan mode, verdict `APPROVED`, four non-blocking findings (verbatim):

1. **Fact 3 overstates the ignore patterns.** Dependabot names the dependency from the `image` capture alone (`shared_file_parser.rb` `build_dependency`), so the names are `dotnet/sdk` and `dotnet/aspnet`. Only `dotnet/*` matches them. `mcr.microsoft.com/dotnet/*` matches neither. Effect: the major-version ignore still works through `dotnet/*`. Fact 3 and the dependabot.yml comment shouldn't imply that both patterns are effective. Fix now, in plan Fact 3: replace "the existing ignore patterns `mcr.microsoft.com/dotnet/*` / `dotnet/*` still match the name" with "the dependency names are `dotnet/sdk` and `dotnet/aspnet`; the ignore pattern `dotnet/*` matches them, `mcr.microsoft.com/dotnet/*` does not".
2. **The new comments and docs state Fact 4 as fact before it is verified.** This is the same kind of error the issue found in today's dependabot.yml comment. Affected text: the Dockerfile comment ("where Dependabot's docker updates refresh them"), the ARCHITECTURE `:309-312` wording ("its `docker` updates refresh those digests") and 0034's Option 2 / Consequences ("refreshes both digests"). Fix now:
   - Dockerfile comment, verbatim: `# Both base images are pinned by tag and multi-architecture index digest in the FROM line itself, the only place Dependabot's docker updates read (decision 0034).`
   - ARCHITECTURE *Dependabot*, verbatim: `its docker updates read the two literal FROM lines and propose a new digest within the .NET major (decision 0034).`
   - 0034 Consequences, first bullet: begin with "Dependabot's grouped `docker` pull request is expected to refresh both digests (verified after merge, issue #290);".
3. **The after-merge check only fires on an event the PM can't see, and it doesn't test parsing right away.** Fix now, in the verification issue text of step 10: right after merge, run Dependabot's docker "Check for updates" (Insights > Dependency graph > Dependabot) and confirm the job log lists `dotnet/sdk` and `dotnet/aspnet`. This verifies AC3's parse on the live service immediately. Detect MCR's refresh with `docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:11.0-alpine`; its `Digest` differing from the `Dockerfile` pin starts the two-week clock.
4. **A Dependabot digest PR is never built before merge.** `ci.yml` has no image build, and `release.yml` is the only workflow that builds the Dockerfile. A digest bump is therefore first built at release, and a reviewer has nothing to check its digest against. This was already true before this change, but the change makes such PRs routine. Recommendation: open a linked issue now ("build the image in CI on PRs that change `Dockerfile`"). Not in this change.

Answered (Lead, revise): 1 applied to Fact 3 (the `dependabot.yml` comment names no ignore pattern); 2 applied —
the Dockerfile comment and the ARCHITECTURE wording verbatim in *Approach* and *Documentation updates*, 0034 Option 2
and *Consequences* reworded; 3 applied to AC3's after-merge verification; 4 listed under *Out of scope /
follow-ups* for the orchestrator.
