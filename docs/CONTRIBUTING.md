# Contributing

<!-- project:begin getting-started -->
## Getting started

### Machine setup

To begin you'll need Git and the .NET SDK.

The `PlexToJellyfinSync` repository uses Git as its source control system. If you haven't already
installed it, you can download it [here](https://git-scm.com/downloads) or, if you prefer a
GUI-based approach, try [GitHub Desktop](https://desktop.github.com/).

Once Git is installed, you'll also need the .NET SDK matching the version targeted by the
solution (currently `net10.0`). Instructions and downloads for your preferred OS can be found
[here](https://dotnet.microsoft.com/download).

A running Plex Media Server and Jellyfin instance are **not** required for day-to-day
development: the unit test suite exercises the sync pipeline entirely against fakes and stubs
(see [`UNIT_TESTS.md`](UNIT_TESTS.md)), and `PlexClient` only needs a reachable Plex server when
you are manually verifying an end-to-end change against real data.

Format checks rely on `reihitsu-format`, a .NET tool. Install it once with:

```shell
dotnet tool install -g Reihitsu.Cli
```

Keep the CLI on the same release line as the **Reihitsu.Analyzer** version pinned in
`Directory.Packages.props`; otherwise the formatter can revert code the analyzer considers correct.

> [!IMPORTANT]
> The above steps are a one-time setup for your machine and do not need to be repeated after the
> initial configuration.

### Cloning the repository

Now that your machine is set up, you can clone the `PlexToJellyfinSync` repository. Open a
terminal and run this command:

```shell
git clone https://github.com/networlddev/PlexToJellyfinSync.git
```

Cloning via SSH:

```shell
git clone git@github.com:networlddev/PlexToJellyfinSync.git
```

### Building the project

The solution file at the repository root (`PlexToJellyfinSync.slnx`) covers every project —
`PlexToJellyfinSync.Core`, `PlexToJellyfinSync.Data`, `PlexToJellyfinSync.Service`, the
`PlexToJellyfinSync` host, and `PlexToJellyfinSync.Tests`:

```shell
dotnet restore PlexToJellyfinSync.slnx
reihitsu-format ./
dotnet build PlexToJellyfinSync.slnx -c Release --no-restore
```

### Running the app locally

The host reads configuration from `src/PlexToJellyfinSync/appsettings.json` /
`appsettings.Development.json`, or from `PLEXSYNC__`-prefixed environment variables (see
[`README.md`](../README.md) for the full configuration table). At minimum you need a reachable Plex
`BaseUrl`/`Token` and at least one path mapping to see the sync pipeline do useful work. `Plex:BaseUrl`
is required: the host refuses to start without a valid absolute `http`/`https` URL, so set it (for
example `PLEXSYNC__Plex__BaseUrl`) before `dotnet run`. Without a token or path mapping the host still
starts and serves the dashboard, but every sync run has nothing to process.

```shell
dotnet run --project src/PlexToJellyfinSync/PlexToJellyfinSync.csproj
```

### Running tests

```shell
dotnet test PlexToJellyfinSync.slnx -c Release --no-build
```

For detailed rules on how unit tests should be structured and named, see
[`UNIT_TESTS.md`](UNIT_TESTS.md). **Unit tests are mandatory for newly written code** — see the
checklist there before opening a pull request.
<!-- project:end getting-started -->

## Submitting a pull request

Nothing is ever committed or pushed directly to `main` — every change goes through a separate branch and
a pull request.

Pull requests are merged with **Squash and merge**: the PR title becomes the single commit subject on
`main` and the description its body, so the commits on the branch are working history and need not be
curated. Keep the branch up to date by merging the current `main` into it (no force-push needed); do not
use the plain *Create a merge commit* or *Rebase and merge* buttons (see the decision record on
squash-merging in [`decisions/`](decisions/README.md)).

For PR naming use the following convention: `[area] Description` (no period at the end).

- For the area, use one of the areas listed below, capitalized.
- For the description, do not reference an issue number in there. A clear, short summary of what
  the change entails is enough; there is room to elaborate in the description.

<!-- project:begin areas -->
Areas: `Core`, `Data`, `Service`, `Host`, `Dashboard`, `Tests`, `Docker`, `CI`, `Docs` — the affected
project or feature, not a class name (a `Worker` change is `Host`).
<!-- project:end areas -->

When a PR is related to an issue, use the `Closes #issuenumber` syntax so the issue links to the
PR automatically and closes when the PR is merged.

Follow the PR template in [`.github/pull_request_template.md`](../.github/pull_request_template.md).

## Quality gates

Code-style rules are documented in [`CLAUDE.md`](../CLAUDE.md) (mirrored in `AGENTS.md` and
[`.github/copilot-instructions.md`](../.github/copilot-instructions.md)) and in
[`.squad/stack.md`](../.squad/stack.md), and are binding for all contributions. Before opening a pull
request, run the commands from `stack.md`: *Format*, *Build*, the *Analyzer gate* (no analyzer diagnostic
of any severity in a changed file) and the *Coverage gate* (at least 80 % line coverage on new or changed
production code and overall, see [`UNIT_TESTS.md`](UNIT_TESTS.md#code-coverage)). A pull request is
expected to arrive clean (see the decision record on quality gates in [`decisions/`](decisions/README.md)).

<!-- project:begin releases -->
## Versioning and releases

Releases are triggered manually, never by merging a PR (see
[`ARCHITECTURE.md`](ARCHITECTURE.md#deployment)): create a `v<major>.<minor>.<patch>` release
(with its tag) on `main` via the GitHub UI when you want a new release; that tag push builds and
publishes the Docker image. Merging a PR into `main` by itself never publishes an image or a
release.
<!-- project:end releases -->

<!-- project:begin stability -->
## Stability policy

An essential consideration in every pull request is its impact on the system. Avoid introducing
unnecessary breaking changes, performance or functional regressions, or negative impacts on
usability. In particular:

- Preserve the "existing `.nfo` files are only ever touched in their watch fields" guarantee (see
  [`ARCHITECTURE.md`](ARCHITECTURE.md#sync-pipeline)) unless a change explicitly intends to alter
  it.
- `PathMapper` must keep rejecting path-traversal sequences and keep requiring an explicit
  mapping match — never fall back to passing an unmapped path through unchanged.
- `NfoWriter` must keep independently verifying that a resolved write target stays under a
  configured `PathMappings:N:Local` root before creating or modifying any file, rather than
  relying solely on `PathMapper`'s own traversal guard.
- Keep the dashboard optional and unauthenticated-by-default behavior intact; do not silently
  add a hard authentication requirement.
<!-- project:end stability -->

## Reporting security issues

Do not report security vulnerabilities through public GitHub issues. See
[`SECURITY.md`](../SECURITY.md) for the private reporting process.

## License

By contributing to this project, you agree that your contributions will be licensed under the
same license that covers the project (see `LICENSE` or `LICENSE.md` in the repository root).
