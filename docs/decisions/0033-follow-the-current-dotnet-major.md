# 0033: The solution moves to .NET 11, an STS release, instead of staying on .NET 10 LTS

- **Status:** Proposed
- **Date:** 2026-11-11
- **Area:** —
- **Source:** the user's request to migrate to .NET 11 (squad work folder `specs/feature-dotnet-11-migration/`)
- **Supersedes:** —

## Context

The solution, its SDK pin, its Microsoft.Extensions packages and its container images were on .NET 10, a
long-term support (LTS) release. .NET 11 is a standard-term support (STS) release. The .NET release index lists
the same end of support for both, 2028-11-14, because STS releases are supported for 24 months. Dependabot keeps
the solution current within one major and leaves the next major to a change of its own (`.squad/stack.md`,
*Toolchain*), so staying on a major or moving on is a choice someone has to make.

## Options considered

1. **Stay on .NET 10 LTS until .NET 12 LTS** — no migration work now. But support would end on the same date as
   .NET 11's, the image would run on an older runtime meanwhile, and the later jump would skip one major.
2. **Move to .NET 11 STS now** — the current runtime and libraries for operators and contributors, and support
   that lasts just as long. One migration per year instead of one every two years.
3. **Multi-target `net10.0;net11.0`** — no gain for a container application that ships one runtime, and it
   breaks the one-major-per-repository rule that `dotnet_major.py` checks.

## Decision

The solution moves to .NET 11 in one change: target frameworks, the `global.json` SDK, the Microsoft.Extensions
packages and both container images. A move to an STS release is acceptable as long as its end of support is no
earlier than that of the LTS release it replaces.

## Consequences

- The move to .NET 12 is due before 2028-11-14 at the latest, the same deadline that staying on .NET 10 would have
  set.
- The container contract ([Configuration](../areas/configuration.md#container)) is unchanged: same port, user
  (UID 1654), volumes and settings.
- Revisit this if an STS release's end of support falls before that of the LTS release it would replace.
