# How to create Unit Tests for PlexToJellyfinSync

## Overview

This document describes how unit tests are written in this repository. It is binding for both
human contributors and AI agents: **unit tests are mandatory for newly written code**, new tests
must follow the conventions below, and existing tests are the reference implementation — when in
doubt, look at a neighboring test file in `PlexToJellyfinSync.Tests` before inventing a new
pattern.

## Unit tests vs. other test types

1. **Unit tests**

   A unit test exercises an individual component or method in isolation — `WatchAggregator`,
   `PathMapper`, `NfoWriter`, `StateStore`, `SyncStatusService`, the login/throttle security
   classes. This is the large majority of `PlexToJellyfinSync.Tests`.

2. **Integration-style tests**

   A few tests exercise a real collaborator instead of a fake to verify wiring:
   `ServiceCollectionExtensionsTests` builds a real `ServiceProvider` from
   `AddPlexToJellyfinSync` and resolves services/options/typed `HttpClient` from it; `PlexClient`
   is tested against a real `HttpClient` wired to `StubHttpMessageHandler` so the actual JSON
   deserialization and URL construction is exercised end to end; `SyncOrchestratorTests` drives
   the real `SyncOrchestrator` against fakes for its dependencies (`FakePlexClient`,
   `FakeStateStore`, `RecordingNfoWriter`, `StubPathMapper`) to verify the orchestration logic
   itself, not just each collaborator in isolation.

3. **Load tests**

   PlexToJellyfinSync does not currently have load tests — the sync workload (one Plex server,
   one user, polling once a minute) does not warrant one.

## Why unit test?

- **Fast feedback.** The suite runs in seconds without a running Plex server, Jellyfin instance,
  or filesystem fixtures beyond a temp directory.
- **Protection against regression.** The NFO writer's core promise — existing files are only ever
  touched in their watch fields — and the path mapper's traversal defenses are exactly the kind of
  behavior that silently breaks under refactoring without tests pinning it down.
- **Executable documentation.** A well-named test tells the reader what a component does for a
  given scenario without needing to read its implementation.
- **Less coupled code.** Every production class in this codebase is used through an interface
  from `PlexToJellyfinSync.Core.Abstractions`; that boundary is what makes hand-written fakes
  possible, and it exists largely because the code was written to be testable this way.

## Test stack

| Concern | Tooling |
| --- | --- |
| Test framework | MSTest 4.x (`[TestClass]`, `[TestMethod]`, `[DataRow]`) |
| Assertions | MSTest `Assert` / `CollectionAssert` APIs only — no FluentAssertions |
| Mocking | None. The project has no mocking framework; exercise real objects or hand-written fakes/stubs (see [Test doubles](#test-doubles) below) instead of introducing NSubstitute or Moq |
| DI container | Real `Microsoft.Extensions.DependencyInjection` `ServiceCollection`/`ServiceProvider`, built via the production `AddPlexToJellyfinSync` extension |
| HTTP | Real `HttpClient` wired to a fake `HttpMessageHandler` (`StubHttpMessageHandler`), never a mocked `HttpClient` |
| Time | `TimeProvider` injected everywhere it matters (e.g. `LoginThrottle`), faked in tests with `TestTimeProvider` instead of `Thread.Sleep`/`DateTimeOffset.UtcNow` |
| Code coverage | `coverlet.collector`, collected via `dotnet test --collect:"XPlat Code Coverage"` |

Do not introduce xUnit, NUnit, FluentAssertions, or a mocking library — the project standardizes
on MSTest and MSTest's own `Assert`/`CollectionAssert` APIs, exercising real collaborators or
hand-written test doubles wherever possible.

## Where tests live

- All tests live in the single `PlexToJellyfinSync.Tests` project (`tests/PlexToJellyfinSync.Tests`),
  referenced from `PlexToJellyfinSync.slnx`, with project references to all four production
  projects (`Core`, `Data`, `Service`, and the `PlexToJellyfinSync` host — the last one via
  `FrameworkReference Include="Microsoft.AspNetCore.App"` so host-project types are testable too).
- The project has no subfolder structure — every test class and test double lives directly under
  `tests/PlexToJellyfinSync.Tests/`, one file per type, named `{TypeUnderTest}Tests.cs` (for
  example `NfoWriterTests.cs`, `PathMapperTests.cs`, `SyncOrchestratorTests.cs`). Keep following
  this flat layout instead of introducing per-feature subfolders unless the project structure
  itself changes.
- `MSTestSettings.cs` carries `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` —
  tests run in parallel per method by default. Do not rely on shared mutable static state between
  tests, and do not assume test execution order.
- `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` has
  `<Using Include="Microsoft.VisualStudio.TestTools.UnitTesting" />` as a global using, so `using
  Microsoft.VisualStudio.TestTools.UnitTesting;` is never needed in a test file.

## Test doubles

There is no mocking framework in this project. Instead, `PlexToJellyfinSync.Tests` has a small set
of hand-written fakes/stubs, each implementing a `Core.Abstractions` interface directly:

- `FakePlexClient : IPlexClient` — serves preconfigured `Libraries`/`History`/`Items`/`Episodes`
  collections and records the calls it received (call counts, last arguments) as public
  properties, so a test can both drive scenario data in and assert on what the code under test
  requested.
- `FakeStateStore : IStateStore` — an in-memory high-water mark holder.
- `StubPathMapper : IPathMapper` — a configurable Plex-path → local-path function.
- `RecordingNfoWriter : INfoWriter` (paired with the `NfoWriteRecord` DTO) — records every write
  call instead of touching the filesystem, for tests that only care *whether and what* was
  written, not the resulting XML.
- `StubHttpMessageHandler : HttpMessageHandler` — returns a preconfigured response for the real
  `HttpClient` used by `PlexClientTests`, so JSON parsing and URL construction run for real while
  the network call itself is intercepted.
- `StubDashboardLoginService : IDashboardLoginService` — preconfigured `LoginResult` for
  `LoginEndpointsTests`.
- `TestTimeProvider : TimeProvider` — a controllable clock for `LoginThrottleTests`, so lockout
  windows and backoff timing can be asserted deterministically without real delays.

When a new component needs a test double for one of its collaborators, follow this pattern: a
plain `internal sealed class` implementing the interface, with public settable properties for
input data and public gettable properties/lists for anything the test needs to assert was
called. Do not reach for a mocking library instead.

## Naming your tests

Test method names are a single PascalCase identifier with no underscores, built from three parts,
concatenated directly — this is a binding project rule enforced by the `RH4103` analyzer, which
rejects underscores in member names:

- The name of the **type or method** being tested.
- The **scenario** under which it's being tested.
- The **expected behavior** when the scenario is invoked.

**Examples from this codebase:**

```csharp
public void WatchAggregatorAllWatchedReturnsWatched()
public void WatchAggregatorPartiallyWatchedReturnsNotWatched()
public void WatchAggregatorNoChildrenReturnsDefault()
public void ServiceCollectionExtensionsBindsConfigurationSections()
public void ServiceCollectionExtensionsRegistersSharedStateAsSingleton()
```

Test class names follow `{TypeUnderTest}Tests` (for example `WatchAggregatorTests`,
`ServiceCollectionExtensionsTests`, `PlexClientTests`), `[TestClass]`, `public sealed class`.

## Arranging your tests

Follow Arrange, Act, Assert without labeling the sections with comments — a blank line before the
act and before the assert block is enough to separate them, consistent with the blank-line rules
in [`CLAUDE.md`](../CLAUDE.md) / [`.github/copilot-instructions.md`](../.github/copilot-instructions.md).

```csharp
[TestMethod]
public void WatchAggregatorAllWatchedReturnsWatched()
{
    var aggregator = new WatchAggregator();
    var children = new List<WatchInfo>
                   {
                       new() { Watched = true, PlayCount = 1, LastPlayed = DateTimeOffset.UnixEpoch.AddDays(1) },
                       new() { Watched = true, PlayCount = 2, LastPlayed = DateTimeOffset.UnixEpoch.AddDays(2) }
                   };

    var result = aggregator.Aggregate(children);

    Assert.IsTrue(result.Watched, "Aggregate should be watched!");
    Assert.AreEqual(DateTimeOffset.UnixEpoch.AddDays(2), result.LastPlayed, "Last played should be the maximum!");
}
```

## Always include an assertion message

Every `Assert.*` call must include a message explaining what the assertion guarantees — not what
it checks mechanically, but why it matters, exactly as every existing test in this project already
does:

```csharp
Assert.IsNotNull(carStatus, "The registration should resolve the state store!");
```

Prefer the specific MSTest `Assert`/`CollectionAssert` member over `Assert.IsTrue` /
`Assert.IsFalse` wrapping a boolean expression — e.g. `Assert.HasCount(1, collection, message)`
instead of `Assert.IsTrue(collection.Count == 1, message)`, or `Assert.AreSame(a, b, message)`
instead of `Assert.IsTrue(ReferenceEquals(a, b), message)` — SonarQube flags the latter.

## Write minimally passing tests

Use the simplest input that exercises the behavior under test — a two-element `WatchInfo` list
for an aggregation rule, a single JSON fixture for a deserialization edge case, a minimal
`MediaItem` for an NFO field — rather than routing through the full sync pipeline when a narrower
test proves the same thing. Minimal tests stay resilient to unrelated changes elsewhere and keep
the focus on behavior rather than implementation details.

## Avoid logic in tests

Do not add `if`, `for`, `while`, or `switch` statements inside a test body. When multiple inputs
must be checked against the same behavior, use MSTest's `[DataRow]` on a single parameterized
`[TestMethod]` instead of writing conditional logic.

## Prefer helper methods over constructor setup

MSTest constructs a fresh test class instance per test, so shared state is already isolated. Even
so, factor repeated setup into a private (or private static) helper method rather than a
constructor, following the `#region Methods` ordering from [`CLAUDE.md`](../CLAUDE.md) — see
`ServiceCollectionExtensionsTests.BuildProvider(values)` for the pattern used throughout this
project: a private static helper that builds a real `ServiceProvider` from an in-memory
configuration dictionary, called from every test in the class instead of duplicating the setup.

**Why?** All setup relevant to a test stays visible from the call site, and there is no risk of
over-setting-up state that later tests then depend on — which matters even more here because
tests run in parallel per method (`MSTestSettings.cs`).

## Avoid multiple acts

Include a single logical action per test. When a scenario needs multiple related outcomes
checked, that's still one act followed by multiple assertions — not multiple acts. Add a separate
`[TestMethod]`, or a `[DataRow]`-parameterized test, for each distinct scenario instead of
branching within one test.

## Testing against the filesystem and the clock

- `NfoWriterTests` and `StateStoreTests` write to a real temporary directory (create one per test
  or per test class and clean it up) rather than mocking the filesystem — `NfoWriter` and
  `StateStore` are thin enough wrappers around `System.IO`/`System.Xml.Linq` that a real
  round-trip is both simple and the most faithful test of what Jellyfin will actually read.
- Never call `Thread.Sleep` or rely on wall-clock time to test timing-sensitive logic (lockout
  windows, backoff delays, session expiry). Inject `TimeProvider` into the component under test
  and drive it with `TestTimeProvider`, following `LoginThrottleTests`.

## Testing the DI registration

When a new service is added to `ServiceCollectionExtensions.AddPlexToJellyfinSync`, add coverage
in `ServiceCollectionExtensionsTests` following the existing groups: that it resolves
(`ServiceCollectionExtensionsRegistersSyncPipeline`-style), that its lifetime is correct if it
holds shared state (`ServiceCollectionExtensionsRegistersSharedStateAsSingleton`-style), and that
any new configuration section is bound (`ServiceCollectionExtensionsBindsConfigurationSections`-
style) using `new ConfigurationBuilder().AddInMemoryCollection(values).Build()` — never a real
`appsettings.json` file — as the configuration source.

## XML documentation on tests

Per [`CLAUDE.md`](../CLAUDE.md) / [`.github/copilot-instructions.md`](../.github/copilot-instructions.md),
XML documentation is required on all members, including test classes and test methods. Document
what the test verifies, not what MSTest attribute it carries:

```csharp
/// <summary>
/// When all children are watched the aggregate is watched
/// </summary>
[TestMethod]
public void WatchAggregatorAllWatchedReturnsWatched()
{
    // ...
}
```

## Code coverage

Code coverage is collected with `coverlet.collector`, already referenced by
`PlexToJellyfinSync.Tests` — no separate installation is needed to collect coverage during
`dotnet test`.

Run the full suite with coverage collection:

```shell
dotnet test PlexToJellyfinSync.slnx -c Release --no-build --logger trx --collect:"XPlat Code Coverage"
```

This produces a `coverage.opencover.xml` file, which CI feeds into SonarQube Cloud analysis (see
`.github/workflows/ci.yml`).

## Checklist for new tests

- [ ] New production code has accompanying unit tests — this is mandatory, not optional.
- [ ] Test class named `{TypeUnderTest}Tests`, placed directly in
      `tests/PlexToJellyfinSync.Tests/` (no subfolders).
- [ ] Test method named `{TypeUnderTest}{Scenario}{ExpectedResult}` (PascalCase, no underscores).
- [ ] `[TestClass]` / `[TestMethod]` (MSTest), `[DataRow]` instead of in-test branching for
      multiple inputs.
- [ ] Arrange / Act / Assert, separated by blank lines, one act per test.
- [ ] Every `Assert.*` call includes an explanatory message.
- [ ] No mocking library introduced — real objects, real `ServiceProvider`/`HttpClient`, or a
      hand-written fake/stub implementing the relevant `Core.Abstractions` interface instead.
- [ ] Timing-sensitive logic driven through an injected `TimeProvider`, not `Thread.Sleep` or the
      real clock.
- [ ] Shared setup factored into a private/static helper method, not a constructor.
- [ ] `#region` layout and XML docs follow [`CLAUDE.md`](../CLAUDE.md) /
      [`.github/copilot-instructions.md`](../.github/copilot-instructions.md).
- [ ] `reihitsu-format ./` run before committing.
