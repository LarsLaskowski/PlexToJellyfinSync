# Contributing

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
dotnet tool install -g Reihitsu.Cli --prerelease
```

`--prerelease` is required: the repository pins a prerelease **Reihitsu.Analyzer**, and the CLI has
to match it, otherwise the formatter reverts code the analyzer considers correct.

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
`BaseUrl`/`Token` and at least one path mapping to see the sync pipeline do useful work; without
those the host still starts and serves the dashboard, but every sync run has nothing to process.

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

### Submitting a pull request

If you'd like to contribute by fixing a bug, implementing a feature, or even correcting typos in
the documentation, you'll need to submit a pull request.

Before submitting a pull request, be sure to [rebase](https://www.atlassian.com/git/tutorials/merging-vs-rebasing)
your branch onto the current `main`. Do not use `git merge` or the *merge* button provided by
GitHub.

For PR naming use the following convention: `[area] Description` (no period at the end).

- For the area, use the affected project or feature (for example `Core`, `Data`, `Service`,
  `Host`, `Dashboard`, `Tests`, `Docker`, `CI`, `Docs`).
- For the description, do not reference an issue number in there. A clear, short summary of what
  the change entails is enough; there is room to elaborate in the description.

When a PR is related to an issue, use the `Closes #issuenumber` syntax so the issue links to the
PR automatically and closes when the PR is merged.

Follow the PR template in [`.github/pull_request_template.md`](../.github/pull_request_template.md).

## Code style

Detailed C# code-style rules (naming, `#region` layout, formatting, XML docs, null handling) are
documented in [`CLAUDE.md`](../CLAUDE.md) and [`.github/copilot-instructions.md`](../.github/copilot-instructions.md)
and are binding for all contributions. Run `reihitsu-format ./` before opening a pull request; a
clean build must show **zero Reihitsu (`RH####`) warnings and errors** — CI enforces this by
running the formatter and failing the build on any resulting diff.

## Versioning and releases

Releases are fully automated (see [`ARCHITECTURE.md`](ARCHITECTURE.md#deployment)): every PR
merged into `main` that touches image-relevant files triggers a new Docker image build, a
`v<major>.<minor>.<patch>` tag, and a GitHub release with auto-generated notes. The version bump
is derived from the size of the merged PR (patch by default, minor once it exceeds 5 files or 100
changed lines) — the major version only changes on an explicit, manual tag. Docs-only, test-only,
and CI/config-only PRs do not trigger a release. You do not need to bump any version number
yourself in a pull request.

## Stability policy

An essential consideration in every pull request is its impact on the system. Avoid introducing
unnecessary breaking changes, performance or functional regressions, or negative impacts on
usability. In particular:

- Preserve the "existing `.nfo` files are only ever touched in their watch fields" guarantee (see
  [`ARCHITECTURE.md`](ARCHITECTURE.md#sync-pipeline)) unless a change explicitly intends to alter
  it.
- `PathMapper` must keep rejecting path-traversal sequences and keep requiring an explicit
  mapping match — never fall back to passing an unmapped path through unchanged.
- Keep the dashboard optional and unauthenticated-by-default behavior intact; do not silently
  add a hard authentication requirement.

## Reporting security issues

Do not report security vulnerabilities through public GitHub issues. See
[`SECURITY.md`](../SECURITY.md) for the private reporting process.

## License

By contributing to this project, you agree that your contributions will be licensed under the
same [MIT License](../LICENSE.md) that covers the project.
