# Plan: Dashboard.razor mutates component state outside the InvokeAsync callback

Source: Issue #78
Status: Approved
Tier: standard — the fix moves a state mutation onto the renderer's dispatcher, which changes the threading/control flow of a component (not just wording), so `trivial` does not fit; it touches no token, auth, file write, path mapping, XML parsing, Plex HTTP call or dependency, so `security` does not apply.

## Problem / root cause

`SyncStatusService.Update` (`src/PlexToJellyfinSync.Service/SyncStatusService.cs`, lines 70–95) raises
`Changed` synchronously on the caller's thread — in production the `Worker`/`SyncOrchestrator` background
thread. `Dashboard.OnChanged` (`src/PlexToJellyfinSync/Components/Pages/Dashboard.razor.cs`, lines 47–51)
handles it with

```csharp
_status = StatusProvider.GetSnapshot();   // line 49: component field written on the worker thread
InvokeAsync(StateHasChanged);             // line 50: only the re-render is marshalled
```

so the component field `_status` is written on a foreign thread while the renderer may concurrently read
it during a render on the circuit's dispatcher. Blazor's threading contract requires component state to be
mutated only on the renderer's dispatcher. `Logs.OnEntryAdded`
(`src/PlexToJellyfinSync/Components/Pages/Logs.razor.cs`, lines 65–79) already does it correctly by
performing the mutation inside the `InvokeAsync` delegate. Benign today (a reference assignment is atomic
and the snapshot is an immutable copy), but it violates the contract and is inconsistent with `Logs.razor`.

## Acceptance criteria

- [ ] AC1: When `ISyncStatusProvider.Changed` is raised from a thread other than the renderer's dispatcher,
  `Dashboard` calls `ISyncStatusProvider.GetSnapshot()` (and therefore assigns `_status`) only while on the
  renderer's dispatcher (`Dispatcher.CheckAccess()` is `true` inside `GetSnapshot` for every call made in
  response to the event).
- [ ] AC2: After `Changed` is raised with an updated status, the dashboard re-renders and its output shows
  the new snapshot (e.g. a changed `ItemsProcessed` / `LastError` value appears in the rendered HTML).
- [ ] AC3: The initial render (`OnInitialized`) still shows the snapshot current at initialization.
- [ ] AC4: After the component is disposed, raising `Changed` no longer calls `GetSnapshot()` (the
  subscription is removed in `Dispose`).

Test approach for the Tester (no bUnit, no mocking library allowed): render `Dashboard` with the framework's
public `Microsoft.AspNetCore.Components.Web.HtmlRenderer` (available via the test project's existing
`FrameworkReference` to `Microsoft.AspNetCore.App`), backed by a `ServiceCollection` that registers a new
hand-written fake `ISyncStatusProvider` (e.g. `FakeSyncStatusProvider` in `tests/PlexToJellyfinSync.Tests`).
The fake exposes `RaiseChanged()`, a settable snapshot, and records `renderer.Dispatcher.CheckAccess()` on
every `GetSnapshot()` call plus signals a `TaskCompletionSource` so the test can await the asynchronous
re-render. Raise the event from `Task.Run(...)` to guarantee a foreign thread. Render and read HTML via
`renderer.Dispatcher.InvokeAsync(...)` (`RenderComponentAsync<Dashboard>()`, `ToHtmlString()`); dispose via
`renderer.DisposeAsync()` for AC4. AC1 must fail on the current code (the first `GetSnapshot` call from the
event is made off-dispatcher). Test class: `DashboardTests`, methods e.g.
`DashboardChangedFromBackgroundThreadReadsSnapshotOnDispatcher`.

## Approach

Mirror `Logs.razor.cs`: move the snapshot read and field assignment into the `InvokeAsync` delegate.

```csharp
private void OnChanged()
{
    InvokeAsync(() =>
                {
                    _status = StatusProvider.GetSnapshot();
                    StateHasChanged();
                });
}
```

Keep the fire-and-forget style used by `Logs.OnEntryAdded` (the event is `Action`, so it cannot be awaited);
the Code Officer decides whether the analyzers require a `_ =` discard. Update the XML summary of
`OnChanged` to say the snapshot is refreshed on the renderer's dispatcher.

Side effect (acceptable, arguably better): an exception from `GetSnapshot()` now surfaces in the
dispatcher's task instead of being caught and logged by `SyncStatusService.Update`'s per-subscriber
isolation (#181); in either case the sync cycle is not disturbed.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync` | `Components/Pages/Dashboard.razor.cs` — `Dashboard.OnChanged` | Body change: mutation moved inside `InvokeAsync`; XML doc wording |
| `PlexToJellyfinSync.Tests` | `DashboardTests.cs` (new), `FakeSyncStatusProvider.cs` (new) | Tests for AC1–AC4, hand-written fake |
| docs | `docs/ARCHITECTURE.md` (dashboard paragraph, ~line 289) | One-sentence clarification, see below |

## Signatures (for the Dev's skeleton)

None — no public or internal member is added or changed. `private void Dashboard.OnChanged()` keeps its
signature; only its body changes. The skeleton step (4) is skipped.

## Documentation updates

- `docs/ARCHITECTURE.md`, *Web host & dashboard*, the `Dashboard.razor` bullet (~lines 289–291): change
  "re-rendering via `InvokeAsync(StateHasChanged)`" to state that the handler re-reads the snapshot and
  re-renders inside `InvokeAsync`, i.e. component state is only mutated on the renderer's dispatcher
  because `Changed` is raised on the sync worker's thread. No behavior or guarantee change.
- `README.md`: none (no configuration change).

## Architecture check

Touches only the dashboard's push-update flow. The dashboard stays push-updated (no polling), subscribes in
`OnInitialized` and unsubscribes in `Dispose` as documented. No guarantee from `docs/ARCHITECTURE.md` (NFO
watch-fields only, unmapped paths skipped, dashboard auth model) is affected. Consistent with decision
record 0006 (item-level isolation / worker stays alive): the worker thread now does even less work inside
the event handler.

## Security considerations

No security area is touched (no tokens, auth, file I/O, parsing, HTTP, logging of external data,
dependencies). The fix removes a potential data race on component state; it does not change what data the
dashboard shows.

## Decision records

- none: no decision beyond the obvious fix (mirrors the existing `Logs.razor` pattern; no alternative was
  seriously weighed).

## Out of scope / follow-ups

- No other component subscribes to `ISyncStatusProvider.Changed` or `ILogStore.EntryAdded` (checked:
  only `Dashboard` and `Logs`), so no further instances to fix.
