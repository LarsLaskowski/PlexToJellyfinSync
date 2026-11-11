# Plan: Migrate the solution to .NET 11

Source: [spec.md](spec.md)
Tier: standard — this updates the target framework, the SDK, dependencies and the base images, and adapts tests, but it creates, changes or removes no security control.
Security areas touched: Docker configuration (`Dockerfile`: SDK and runtime base images, runtime digest), CI configuration (`.github/dependabot.yml`, comments only), build configuration (`global.json`, every `*.csproj`, `Directory.Packages.props`), dependencies (Microsoft.Extensions packages 10.0.14 to 11.0.0, `Microsoft.Extensions.Logging.Abstractions` removed); none of the areas of `.squad/project.md`; Tokens and secrets: only through the runtime's startup-validation message, held by ServiceCollectionExtensionsValidationMessageDoesNotLeakSecrets (AC2)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | All five projects target `net10.0`; `global.json` pins `10.0.404` with `latestFeature`; four Microsoft.Extensions packages are at `10.0.14` | `src/*/*.csproj:4`, `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj:4`, `global.json:3`, `Directory.Packages.props:11-14` | confirmed |
| 2 | SDK 11.0.100 and runtimes 11.0.0 are installed in this session | `dotnet --list-sdks`; `/usr/lib/dotnet/shared/Microsoft.NETCore.App/11.0.0` | confirmed |
| 3 | On .NET 11, `Microsoft.Extensions.Logging.Abstractions` (also Options, DependencyInjection.Abstractions, Hosting.Abstractions) is part of `Microsoft.NETCore.App`, so NuGet prunes the package and reports NU1510 for the reference in `PlexToJellyfinSync.Core.csproj:12` | the `Microsoft.NETCore.App/11.0.0/` directory listing contains `Microsoft.Extensions.Logging.Abstractions.dll`; the user's probe reports NU1510 | confirmed |
| 4 | After the reference is removed, the `PackageVersion` of Logging.Abstractions pins nothing: no project references the package, and a pruned package never appears in the graph that transitive pinning acts on | fact 3; `grep PackageReference` lists the only reference at `Core.csproj:12` | confirmed |
| 5 | `IStartupValidator` is obsolete on .NET 11 (SYSLIB0066, "Implement IAsyncStartupValidator instead"); the tests call it at `ServiceCollectionExtensionsTests.cs:186, 197, 258, 288`; production code never calls it | `strings` of `Microsoft.Extensions.Options.dll` 11.0.0; grep | confirmed |
| 6 | The .NET 11 host runs `IAsyncStartupValidator.ValidateAsync`, not `IStartupValidator.Validate`. `ValidateOnStart` registers both against one `StartupValidator` instance | scratch probe (`Host.CreateApplicationBuilder`, a failing `ValidateOnStart` option on Hosting 11.0.0): the `StartAsync` stack trace goes through `StartupValidator.ValidateAsync`, and the two resolved services are the same object | confirmed |
| 7 | `IAsyncStartupValidator` also exists on .NET 10 with the 10.0.14 packages | `strings` of `microsoft.extensions.options/10.0.14/lib/net10.0/Microsoft.Extensions.Options.dll` and `microsoft.extensions.hosting/10.0.14/lib/net10.0/Microsoft.Extensions.Hosting.dll`: no `AsyncStartupValidator` (the earlier reading came from the net10.0 build of the 11.0.0 Hosting package); the type ships in `Microsoft.NETCore.App/11.0.0/Microsoft.Extensions.Options.dll`. The adapted tests compile only after the framework and package move | refuted |
| 8 | CI's SonarQube scanner runs on the .NET 11 runtime: `dotnet-sonarscanner` targets `netcoreapp3.1` with `rollForward` `LatestMajor` in its runtimeconfig, and CI installs it with `--tool-path` (`ci.yml:57`), so the manifest's `rollForward: false` does not apply | `dotnet-sonarscanner/12.0.0/tools/netcoreapp3.1/any/SonarScanner.MSBuild.runtimeconfig.json`; `ci.yml:57,65` | confirmed |
| 9 | CI does not run `reihitsu-format` (net10.0); locally it runs on 11 through the manifest's `rollForward: true` | `ci.yml` (no `reihitsu`); `.config/dotnet-tools.json` | confirmed |
| 10 | CodeQL builds nothing (`build-mode: none`); CI sets up only the SDK of `global.json` | `codeql.yml:24-28`; `ci.yml:34-36` | confirmed |
| 11 | `dotnet_major.py` reads the major from `global.json`, target frameworks and image tags, never the digest; "10.0" appears in it only in a docstring | `.squad/tools/dotnet_major.py:26-29,66-88` | confirmed |
| 12 | Release builds `linux/amd64,linux/arm64` from `FROM ${BASE_RUNTIME_IMAGE}@${BASE_RUNTIME_DIGEST}`; no workflow builds the image on a PR | `release.yml:92`; `Dockerfile:19`; `ci.yml` | confirmed |
| 13 | The `aspnet:11.0-alpine` index lists amd64 and arm64, its configuration carries `ASPNET_VERSION` 11.0.x and `APP_UID=1654`, as 10.0-alpine does | Security's measurement (step 3): index `sha256:d39b72ad1534fa0faf52b488bffb0762df7ba269d120f0338713d4dda2a41bf5` lists amd64, arm/v7 and arm64 with `APP_UID=1654`, `ASPNET_VERSION=11.0.0`; the Dev looks it up again when editing (AC3) | confirmed |
| 14 | A Docker daemon can be started in the session (`dockerd` is installed but not running) | to verify: Dev starts `dockerd` in step 6; if it cannot, the Dev reports and the Lead decides (mode `decide`) | unverified |
| 15 | All 640 tests pass on `net11.0` | the user's scratch probe on `net11.0` (restore, build, 640 passed); *Test* re-runs it in step 7 | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: *Restore*, *Build*, *Test* and the *Analyzer gate* (it runs `dotnet_major.py`) on the branch, with no SYSLIB0066 or NU1510 in the build output and no diagnostic in a changed file; the Code Officer runs the analyzer gate (step 7) | — |
| AC2 | `ServiceCollectionExtensionsMinimalConfigurationIsValid`, `ServiceCollectionExtensionsEmptyConfigurationFailsStartupValidation`, `ServiceCollectionExtensionsInvalidSyncOptionsFailValidation`, `ServiceCollectionExtensionsInvalidDashboardOptionsFailValidation`, `ServiceCollectionExtensionsInvalidTrustedProxyFailsValidation`, `ServiceCollectionExtensionsBlankStateDirectoryFailsValidation`, `ServiceCollectionExtensionsValidationMessageDoesNotLeakSecrets` (adapted, each `[TestCategory("AC2")]`) | `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs` |
| AC3 | check: by the Dev in step 6, see *Approach*, AC3 (index inspection, image build, start, `id`, labels) | — |
| AC4 | check: `grep` sweep (*Approach*, AC4) and `python3 .squad/tools/config-check.py`, by the Dev in step 6 and again in step 7 | — |
| AC5 | CI: the pull request's `ci.yml` (build, tests, SonarQube) and `codeql.yml` runs are green | — |

The AC2 tests are existing tests moved to the API the host runs. Their outcomes do not change. `IAsyncStartupValidator`
exists only from .NET 11 on (fact 7), so the Dev moves the framework and packages first, as group G1's skeleton
(step 4, `tasks.md`). Step 5's red signal is then SYSLIB0066 at the four old call sites in the *Analyzer gate*,
not a failing test; the Tester's adapted tests clear it and pass.

## Approach

- **AC1:**
  - Change `<TargetFramework>` to `net11.0` in the five project files.
  - `global.json`: `"version": "11.0.100"`, with `rollForward` and `allowPrerelease` unchanged.
  - `Directory.Packages.props`: Hosting, Http and Options.DataAnnotations go to `11.0.0`. The
    `Microsoft.Extensions.Logging.Abstractions` `PackageVersion` is deleted (fact 4).
  - `PlexToJellyfinSync.Core.csproj`: delete the `PackageReference` and its now-empty `ItemGroup`.
  - No suppression of NU1510 or SYSLIB0066.
- **AC2 (Tester, after the AC1 edits, which are G1's skeleton):**
  - In the seven tests, resolve `IAsyncStartupValidator` and `await` `ValidateAsync(_testContext.CancellationToken)`.
  - Failures are asserted with `await Assert.ThrowsAsync<OptionsValidationException>(…)`, keeping today's messages.
  - The tests become `async Task`, with their names unchanged (MSTest precedent: `DashboardTests`).
  - The private helper `AssertInvalid<TOptions>` becomes `AssertInvalidAsync<TOptions>` and returns `Task`. Its second
    assertion, the `IOptions<TOptions>.Value` resolution, is unchanged.
  - The class gets a `TestContext` constructor and a `_testContext` field (`docs/UNIT_TESTS.md`, MSTEST0049).
  - Use `await using` for the provider where an analyzer asks for it.
- **AC3 (Dev):**
  - Dockerfile line 1: `BASE_SDK="mcr.microsoft.com/dotnet/sdk:11.0-alpine"`. Line 3: `BASE_RUNTIME_TAG="11.0-alpine"`.
  - Line 4: `BASE_RUNTIME_DIGEST` becomes the index digest that `docker buildx imagetools inspect
    mcr.microsoft.com/dotnet/aspnet:11.0-alpine` prints as `Digest:` (not a per-platform manifest digest).
  - Check, recording each output in the hand-back:
    1. The same command lists `linux/amd64` and `linux/arm64`.
    2. `docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet@<digest> --format '{{json .Image}}'`
       shows `ASPNET_VERSION=11.0.*` and `APP_UID=1654` for both platforms.
    3. Start `dockerd`, then run the *Image build check* (`docker build -f Dockerfile -t plexsync:net11 .`).
    4. `docker run --rm plexsync:net11 id` prints `uid=1654`.
    5. `docker run -d -p 8080:8080 -e PLEXSYNC__Plex__BaseUrl=http://plex.invalid:32400 plexsync:net11`, then
       `curl -fsS http://localhost:8080/health` answers JSON with `"status":"ok"`.
    6. `docker image inspect` shows the `org.opencontainers.image.base.name` and `.base.digest` labels naming the
       new tag and digest.
  - Without a daemon (fact 14), checks 1 and 2 still run. The Dev runs the restore-order reproduction from
    `.squad/stack.md`, *Image build check*, and reports. Checks 3–6 then go to the Lead (mode `decide`), never
    silently skipped.
- **AC4 (Dev):**
  - The edits in *Documentation updates*.
  - The sweep `grep -rnE '\.NET 10|net10\.0|10\.0\.404|10\.0-alpine|10\.0\.14' --exclude-dir={specs,.git,.claude,bin,obj} .`
    may only hit `docs/decisions/` (0031, 0033) and `.squad/tools/`.
  - `config-check.py` passes (AGENTS.md and the Copilot file identical).
- New user-visible output: none.

## Options considered

1. **Stay on .NET 10 LTS until .NET 12** — lost. The user asked for the move, and support ends on the same date
   (decision 0033).
2. **Multi-target `net10.0;net11.0`** — lost. It breaks the one-major rule that `dotnet_major.py` checks, and the
   image ships one runtime anyway.
3. **AC2: keep `IStartupValidator` and suppress SYSLIB0066** — lost. The tests would hold a path the host no longer
   runs (fact 6), and a suppression needs a reason that does not exist here.
4. **AC2: build a real host in the tests and assert `StartAsync` fails** — lost. It adds hosting setup to a
   registration test, and `IAsyncStartupValidator` is the exact service the host resolves (fact 6).
5. **Keep the Logging.Abstractions `PackageVersion`** — lost. It pins nothing (fact 4), and Dependabot would keep
   raising a dead entry.
6. **Pin the runtime image by tag only, or by its amd64 manifest digest** — lost: spec E4 (arm64 users would get
   amd64 layers).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| all | `src/*/*.csproj`, `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` | `net11.0` |
| PlexToJellyfinSync.Core | `PlexToJellyfinSync.Core.csproj` | Logging.Abstractions reference removed |
| solution | `global.json`, `Directory.Packages.props` | SDK 11.0.100, packages 11.0.0, one entry removed |
| image | `Dockerfile` | 11.0-alpine images, new digest |
| CI | `.github/dependabot.yml` | two comments |
| PlexToJellyfinSync.Tests | `ServiceCollectionExtensionsTests` | async startup validation |

## Signatures (for the Dev's skeleton)

None: no production API changes. The test-only changes (the constructor of `ServiceCollectionExtensionsTests`
taking the test context, the asynchronous helper) are the Tester's and are described in *Approach*.

## Test files

- `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs`, adapted by the Tester. The other tests in
  this file and the rest of the suite have no input that the move handles differently, so they stay unchanged.
- Existing test code that calls a changed signature: none (only the file's own private helper changes).
- Files no unit test can cover: `Dockerfile`, `global.json`, the project files, `Directory.Packages.props`,
  `.github/dependabot.yml`. They are configuration, not code, and are verified as AC1, AC3, AC4 and AC5 say. No
  coverage exclusion is needed.

## Areas

None: no behavior changes. The container contract in `docs/areas/configuration.md` (*Container*) names no .NET
version and stays as written. AC3 holds the UID it implies.

## Documentation updates

All owned by the Dev, written once the build is green:

- `README.md:8`: "C# / .NET 10" becomes "C# / .NET 11".
- `docs/CONTRIBUTING.md:15`: `10.0.404` becomes `11.0.100`, and `net10.0` becomes `net11.0`.
- `docs/ARCHITECTURE.md:293,295`: `dotnet/sdk:10.0-alpine` and `dotnet/aspnet:10.0-alpine` become `11.0-alpine`.
- `AGENTS.md:19` and `.github/copilot-instructions.md:18` (project block `overview`): "A .NET 10 worker" becomes "A
  .NET 11 worker", identically in both.
- `.squad/stack.md:9-10`: `10.0.404` becomes `11.0.100`, and `net10.0` becomes `net11.0`. The Dev reports the
  exact text and the orchestrator writes it.
- `.github/dependabot.yml:23`: "(Microsoft.Extensions.* 11.0.0)" becomes "(Microsoft.Extensions.* 12.0.0)".
  Line 31: "(10 to 11)" becomes "(11 to 12)".
- `docs/decisions/0033` and its index row: written by the Lead (status set to `Accepted` at merge per the
  records' practice).
- Changelog: none. Release notes come from the PR label (`docs/CONTRIBUTING.md:114`), and the label is
  `enhancement`: not `breaking-change`, because the container contract is unchanged (spec N1).

## Architecture check

No guarantee in `.squad/project.md` is weakened. "An invalid configuration stops the start" still holds: the
host's `StartAsync` runs the same `ValidateOnStart` validators (fact 6), and AC2 keeps their assertions. The
deployment description in `docs/ARCHITECTURE.md` changes only its tags. Decision 0033 is linked from no
architecture section, because the structure does not change.

## Security considerations

- [x] No limits: no parsed input changes.
- [x] Exceptions that reach a user-visible message: none new. The startup failure message is unchanged, and AC2's
  `ServiceCollectionExtensionsValidationMessageDoesNotLeakSecrets` holds it.
- The base image moves to a new digest that the Dev looks up. Security's plan review may ask for the digest's
  provenance (`mcr.microsoft.com` only).

## Decision records

- `docs/decisions/0033-follow-the-current-dotnet-major.md` (Proposed): moving to .NET 11 STS instead of staying on
  .NET 10 LTS.

## Challenge

- 1 (major) Fact 7 is false: `IAsyncStartupValidator` does not exist with the 10.0.14 packages — accepted. I
  re-read the 10.0.14 Options and Hosting assemblies and found no `AsyncStartupValidator`; my earlier reading was
  of the net10.0 build inside the 11.0.0 Hosting package. Fact 7 is now refuted. The framework and package move
  (AC1) is G1's skeleton, done by the Dev before the Tester adapts the tests. The red signal is SYSLIB0066 at the
  old call sites. The sentences claiming "passes on net10.0" and "no red phase" are removed (`tasks.md`
  reordered to match).

## Security review

- (a) Tokens and secrets reached through the runtime's startup-validation message — accepted. The line is
  appended to *Security areas touched* as worded.
- (b) `BASE_SDK` is pinned by tag only (pre-existing) — decided: a linked follow-up issue, not a fix here. The
  build stage's SDK is not in the shipped image, and pinning it changes how Dependabot updates the Dockerfile,
  which is a change of its own. See *Out of scope / follow-ups*.
- Security's measurements are taken over: fact 6 matches, fact 13 is confirmed, and the 11.0.0 packages have no
  known vulnerabilities.

## Out of scope / follow-ups

As in spec *Out of scope*. `.claude/hooks/session-start.sh` mentions "SDK 11 builds a net10.0 project" only as
an example and is a squad file.

Follow-up issue (created by Security/orchestrator), title "[build] Pin the Dockerfile's SDK build image by digest".
Body: "`BASE_SDK` in the `Dockerfile` names `mcr.microsoft.com/dotnet/sdk:11.0-alpine` by tag only, while the
runtime image is pinned by its multi-architecture index digest. A changed tag can change the build environment of
a release without a diff. Pin the SDK image by its index digest as well, with the tag kept for readability, and
check that Dependabot's docker updates still raise both. Found during the .NET 11 migration; the pin predates it." 
