# Spec: Migrate the solution to .NET 11

Source: the user's description
Status: Draft

## Problem

The solution, its SDK pin, its Microsoft.Extensions packages and its container images are on .NET 10. .NET 11 is
released, and Dependabot deliberately leaves the next major alone (`.github/dependabot.yml`, `.squad/stack.md`,
*Toolchain*): the move happens only as one change of all of them together. Until it does, contributors build on
the old major, and the image runs on the .NET 10 runtime. On .NET 11 the build reports two new diagnostics, an
obsolete API in the tests (SYSLIB0066) and a now-redundant package reference (NU1510), which the zero-diagnostic
rule (`AGENTS.md`) does not allow to stay.

## Scenarios

- S1: Given a clone with the .NET 11 SDK of `global.json` installed, when a contributor runs *Restore*, *Build*,
  *Test* and the *Analyzer gate* (`.squad/stack.md`), then all pass on `net11.0`, every test included, with no
  analyzer diagnostic and no restore warning.
- S2: Given the release workflow builds the image, when an operator pulls and starts it with an unchanged
  configuration, then it runs on the .NET 11 ASP.NET runtime, listens on port 8080, runs as the same non-root
  user and uses the same volumes and environment variables as before; the image labels name the new base image.
- S3: Given a contributor reads `README.md`, `docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md`, `AGENTS.md` or
  `.squad/stack.md`, when they look for the .NET version, SDK or base image, then they find .NET 11, SDK
  `11.0.100` and the `11.0-alpine` images.

## Errors and edge cases

- E1: An invalid configuration still stops the start before the worker and the web host start
  ([Configuration](../../docs/areas/configuration.md#startup-check)); the startup-validation tests keep their
  assertions and exercise the validation the .NET 11 host runs, without the obsolete API.
- E2: A machine with only a .NET 10 SDK cannot build: `global.json` asks for an 11.0 SDK, and the error names it
  (the SDK's own message); `rollForward` stays `latestFeature`, so no later major is picked silently.
- E3: Where several majors are installed (a CI runner, a developer machine), every place that names a major
  (target frameworks, `global.json`, the Dockerfile's images) names 11; `dotnet_major.py` reports none other.
- E4: The runtime image is pinned by the digest of the current `aspnet:11.0-alpine`, not by the tag alone; an
  image whose digest does not resolve fails the build rather than falling back to an unpinned image.
- E5: CI, which installs only the SDK of `global.json`, still runs its tools (the SonarQube scanner, the
  formatter from the tool manifest) and CodeQL; a tool that needs an older runtime shows as a failed CI run, not
  as a skipped step.

## Non-functional requirements

- N1: Compatibility for operators: the container contract (image name and tags, port, user, volumes, settings,
  state file and `.nfo` output) is unchanged; no setting is added or changed.
- N2: Support window: .NET 11 is an STS release and .NET 10 an LTS release, both with end of support on
  2028-11-14 (the user's lookup of the .NET release index), so the move does not shorten the supported life of the
  image.
- N3: One major per repository (`.squad/stack.md`, *Toolchain*): target frameworks, SDK, Microsoft.Extensions
  packages and .NET images move together in this change.

## Acceptance criteria

- AC1: Every project targets `net11.0`, `global.json` pins SDK `11.0.100` with `rollForward` `latestFeature`, the
  Microsoft.Extensions packages are at `11.0.0`, and *Restore*, *Build*, *Test* (every test passing) and the
  *Analyzer gate* (`dotnet_major.py` included) finish without any diagnostic, SYSLIB0066 and NU1510 included
  (S1, E2, E3, N3).
- AC2: The startup-validation tests of `ServiceCollectionExtensionsTests` assert the same outcomes as today (a
  valid configuration passes, an empty or invalid one fails with `OptionsValidationException`, the message leaks
  no secret) through the validation the .NET 11 host runs, with no obsolete API (E1).
- AC3: The Dockerfile builds from `dotnet/sdk:11.0-alpine` and runs on `dotnet/aspnet:11.0-alpine` pinned by its
  current digest; the built image starts, listens on 8080 and runs as the base image's non-root user, and its
  base-image labels name the new tag and digest (S2, E4, N1).
- AC4: `README.md`, `docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md`, `.squad/stack.md`, `AGENTS.md` and
  `.github/copilot-instructions.md` name .NET 11, SDK `11.0.100`, `net11.0` and the `11.0-alpine` images; no
  document or configuration comment states .NET 10 as the current major (the major-jump comment of
  `.github/dependabot.yml` included); `AGENTS.md` and `.github/copilot-instructions.md` stay identical below the
  first heading (`config-check.py` passes) (S3, N2).
- AC5: The pull request's CI — build, tests, SonarQube analysis and CodeQL — passes on the runner with only the SDK
  of `global.json` set up (E5).

## Out of scope

- New .NET 11 language or library features beyond what the two diagnostics require.
- Changes to the squad's tools: `.squad/tools/dotnet_major.py` derives the major from `global.json` and names
  `10.0` only in a docstring example; it is a squad file a product PR does not change (`AGENTS.md`).
- Released decision records: 0031's "measured on .NET 10" is a historical fact of that decision and stays.
- Package updates other than the Microsoft.Extensions packages (Dependabot's).

## Open questions

- Is the move from an LTS to an STS release wanted? Answered: yes, the user asked for it; both end on 2028-11-14
  (N2). Whether that choice needs a decision record is the plan's call.
- Does `.squad/tools/dotnet_major.py` need a change? Answered: no (code: it reads the major from `global.json`;
  see *Out of scope*).
- Is the NU1510 removal safe for `PlexToJellyfinSync.Core`? Answered: the user's probe reports the reference as
  pruned on `net11.0` and the build green without it; whether its central `PackageVersion` entry stays (it also
  pins the transitive version, `CentralPackageTransitivePinningEnabled`) is the plan's call.
- Is this a breaking change for operators? Answered: no — the container contract is unchanged (N1); the release
  label is the plan's call (`docs/CONTRIBUTING.md`).
- Which digest does `aspnet:11.0-alpine` have? Answered: looked up when the Dockerfile is edited (AC3), not fixed
  here.

## Area documents

- [Configuration](../../docs/areas/configuration.md), *Container*: unchanged in contract; it names no .NET
  version today and gains none. The runtime base image is described in `docs/ARCHITECTURE.md` (deployment
  section), which names the `11.0-alpine` images.
- No other area document names the .NET major; none changes.
