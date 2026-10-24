# Plan: The run that only sets a missing mark leaves the Plex connection state unchanged

Source: Issue #236
Status: Draft
Tier: standard — the fix changes what the status reports after a seed-only history run (a behavior change in production code) and creates, changes or removes no security control.
Security areas touched: none

## Problem / root cause

`SyncOrchestrator.ProcessHistoryAsync` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`, lines 273-286)
handles a missing high-water mark by persisting "now" and returning early. Its status update sets
`s.PlexConnected = true` (line 282) although this branch sends no request to Plex: the owner resolution
(line 288) and the history request (line 289) come after the early `return` (line 285). The dashboard card
(`Dashboard.razor`, lines 8-10) and `/health` (`Program.cs`, line 91) read that flag through
`SyncStatusService.GetSnapshot` and therefore report a connection no request in that run confirmed — after a
failed startup reconcile, for example, the flag flips from `false` to `true` with Plex still unreachable.

Related defect found on the way (out of scope, see *Out of scope / follow-ups*): `HandleError` (line 107) sets
`PlexConnected = false` for every failed run, also when the failure is not a Plex request at all (writing the
mark, line 277 or 302, with a state directory that is not writable).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A history run without a mark sets the mark to "now" and `PlexConnected` to `true` | `SyncOrchestrator.cs` lines 273-286, assignment on line 282 | confirmed |
| 2 | That branch sends no request to Plex | early `return` on line 285 precedes `ResolveOwnerAsync` (288) and `GetHistorySinceAsync` (289); test `SyncOrchestratorFirstRunSeedsHighWaterMark` asserts `LastHistorySince` is null and `OwnerAccountIdCalls` is 0 | confirmed |
| 3 | The issue's location "around lines 246-251" | the code moved with #240; the branch is now at lines 273-286 (the substance, facts 1 and 2, is confirmed) | refuted |
| 4 | The dashboard and `/health` show the flag | `Dashboard.razor` lines 8-10, `Program.cs` line 91, `SyncStatusService.cs` line 54 | confirmed |
| 5 | `docs/areas/sync-pipeline.md` documents the current behavior, from #230 | `sync-pipeline.md` line 158 ("and after a run that only set a missing mark"); `git log -S` names commit 4c1178b, "[Docs] Introduce area documents (#230)" | confirmed |
| 6 | A seed-only run is always preceded in the same process by a reconcile that did contact Plex | `Worker.cs` line 109 runs `SafeReconcileAsync` before the loop that calls `SafeProcessHistoryAsync` (line 128); `ReconcileAsync` sets the flag after `GetLibrariesAsync` (`SyncOrchestrator.cs` line 331) or clears it in `HandleError` | confirmed |
| 7 | No other code sets the flag | `grep PlexConnected src/`: only `SyncOrchestrator.cs` lines 107, 282, 291, 331 write it | confirmed |
| 8 | An existing test asserts the defect | `SyncOrchestratorTests.cs` line 82: `Assert.IsTrue(snapshot.PlexConnected, "The first run should mark Plex as connected!")` | confirmed |

## Acceptance criteria

- [ ] AC1: A history run without a high-water mark leaves `PlexConnected` at `false` when it was `false` before the run
  (the decision is the status update in the missing-mark branch of `SyncOrchestrator.ProcessHistoryAsync`).
- [ ] AC2: A history run without a high-water mark leaves `PlexConnected` at `true` when it was `true` before the run (the
  fix leaves the value alone and does not set it to `false` either).
- [ ] AC3: The rest of the missing-mark run is unchanged: the mark is persisted once and reported as `HighWaterMark`,
  `LastPollAt` is set, no owner is resolved, no history is read and no NFO is written.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `SyncOrchestratorFirstRunKeepsPlexDisconnected` — fresh `SyncStatusService` (flag `false`), empty `FakeStateStore`, one `ProcessHistoryAsync`; asserts `PlexConnected` is `false` and `OwnerAccountIdCalls` is 0 | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC2 | `SyncOrchestratorFirstRunKeepsPlexConnected` — `_status.Update(s => s.PlexConnected = true)` before the run, empty `FakeStateStore`; asserts `PlexConnected` is still `true`. It passes on today's code, so it is deferred to step 6 (`.squad/routing.md`, step 5) and guards against a fix that clears the flag | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC3 | existing `SyncOrchestratorFirstRunSeedsHighWaterMark`, with its assertion on line 82 removed (it encodes the defect); add `Assert.AreEqual(_stateStore.HighWaterMark, snapshot.HighWaterMark, …)` so the reported mark is covered | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |

## Approach

- Remove the line `s.PlexConnected = true;` from the status update of the missing-mark branch
  (`SyncOrchestrator.cs` line 282). `HighWaterMark` and `LastPollAt` are still set in the same `Update` call.
- Nothing else in the orchestrator changes: the history request still sets the flag (line 291), the reconcile's
  library list too (line 331), and a failed run still clears it (line 107).
- No new user-visible output: the dashboard card and `/health` keep their text and fields; only the value after a
  seed-only run changes.

## Options considered

1. **Leave the previous connection state unchanged (chosen).** One deleted assignment; the flag then means what
   the area document says for every other case — the last request that decides it succeeded or failed. The
   value a seed-only run inherits is fresh: the worker always runs the startup reconcile, which contacts Plex,
   before the first history run (fact 6), and a later seed run (lost or corrupt `state.json`) inherits the
   result of the previous history run or reconcile.
2. **Check the connection in the seed run** (e.g. resolve the owner or list the libraries before reporting it).
   Lost: it adds a request to a run the area document and decision 0005 define as "sets the mark, processes
   nothing, makes no request to Plex"; a failing check would then also have to decide whether the mark is still
   seeded (today it is), which changes the failure behavior of the seed run; and the information it would add is
   at most one poll interval newer than the reconcile that ran just before.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `SyncOrchestrator` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`) | drop `s.PlexConnected = true` from the missing-mark status update |
| `PlexToJellyfinSync.Tests` | `SyncOrchestratorTests` | two new tests, one assertion removed and one added in an existing test |
| docs | `docs/areas/sync-pipeline.md` | *Run status* row `plexConnected` |

## Signatures (for the Dev's skeleton)

none — no new or changed member; step 4 does not apply.

## Test files

- `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs`

Existing test code that calls a changed signature: none. The existing test `SyncOrchestratorFirstRunSeedsHighWaterMark`
changes its expectation (line 82); the Tester edits it in step 5.

Files of the change no unit test can cover: none.

## Areas

- [Sync pipeline](../../docs/areas/sync-pipeline.md): *Run status*, row `plexConnected` — the seed-only run no longer
  sets it. The *Incremental history run* bullet (lines 32-33, "makes no request to Plex") and *Owner only*
  (line 82) already hold and stay.

## Documentation updates

Sweep: `grep -rni "connected"` over `docs/areas/`, `docs/ARCHITECTURE.md`, `README.md` and `SECURITY.md` — the only
statement on when the flag is set is `docs/areas/sync-pipeline.md` line 158; `dashboard-and-auth.md` (lines 24-26,
175) links to it without restating it; `docs/decisions/0006` mentions the flag only for item failures (unchanged).

- `docs/areas/sync-pipeline.md` line 158 — owner: **Dev**. The row's meaning reads exactly:
  `` `true` once a reconcile's library list or a history run's history request succeeded; `false` at start and after a failed run. A run that only sets a missing mark makes no request to Plex and leaves it unchanged, as does a failing item. ``

No other documentation changes; `docs/ARCHITECTURE.md` is untouched.

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` is touched. The status stays in-memory and is
reset at start; polling-only (0001), the seed-to-"now" first run (0005) and per-item isolation (0006) are kept —
the seed run still makes no request to Plex.

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding — no limits.
- [x] Every exception that can reach a user-visible failure reason or message is listed — none: the change adds
  no exception path.

## Decision records

- none: no decision beyond the obvious fix — the issue names the chosen behavior as one of its two expectations,
  it follows the existing rule that the seed run makes no request to Plex (0005, area document), and the meaning
  of `plexConnected` is stated once in the area document.

## Out of scope / follow-ups

- Proposed follow-up issue (related defect, not changed here): title "A failed mark write reports Plex as
  disconnected"; body: "`SyncOrchestrator.HandleError` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`,
  line 107) sets `plexConnected` to `false` for every failed run, including one that failed only because the
  high-water mark could not be written (`IStateStore.SetHighWaterMarkAsync`, e.g. a state directory that is not
  writable) after Plex answered. The dashboard and `/health` then show a disconnected Plex server that answered
  in that run. Expected: only a failed Plex request clears the flag. Documented as-is in
  `docs/areas/sync-pipeline.md` (*Limits and errors*, *A failed run*), which should change with the fix."
