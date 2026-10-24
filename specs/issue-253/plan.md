# Plan: NfoWriter leaves `<target>.tmp` in the media folder when the replace fails

Source: Issue #253
Status: Draft
Tier: standard — the cleanup of `<target>.tmp` moves into the failure path of the file-mode copy and the move, a change in error behavior and control flow of `NfoWriter.SaveAsync` that creates, changes or removes no security control.
Security areas touched: Path mapping and file writes (`NfoWriter` file writes); `.nfo` / XML handling (`NfoWriter` saving)

## Problem / root cause

`NfoWriter.SaveAsync` (`src/PlexToJellyfinSync.Service/NfoWriter.cs`) wraps only the creation and
serialization of `<target>.tmp` (lines 200–207) in the `try` whose `catch` (lines 208–213) calls
`TryDeleteTempFile`. `PreserveFileMode(tempPath, path)` (line 215) and `File.Move(tempPath, path, overwrite: true)`
(line 217) run after that block, so an exception from either leaves the fully written `<target>.tmp` next to the
`.nfo` in the user's media library. The area document states this as current behavior
(`docs/areas/path-mapping-and-nfo.md`, *Limits and errors*, lines 153–156). `StateStore` had the same shape and
was fixed in #254 (commit 7273c58) by moving both calls into the `try` and the serialization into a helper.

Decision asked for by the issue: yes, `NfoWriter` deletes its temporary file on every failure of the save, as
`StateStore` does — see *Options considered*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `SaveAsync` deletes `<target>.tmp` only when creating or serializing it fails. | `NfoWriter.cs` lines 200–213: the `try` holds the `FileStream`, `XmlWriter`, `document.SaveAsync` and `FlushAsync`; the `catch` calls `TryDeleteTempFile` and rethrows. | confirmed |
| 2 | A failure of `PreserveFileMode` or `File.Move` leaves the temp file behind. | `NfoWriter.cs` lines 215 and 217 sit outside the `try`; nothing else deletes `tempPath`. | confirmed |
| 3 | The area document describes the leftover as today's behavior. | `docs/areas/path-mapping-and-nfo.md` lines 155–156: "one left by a failure after its serialization (copying the file mode, the rename) stays next to the target and is overwritten by the next write to that target." | confirmed |
| 4 | #243 / #254 changed `StateStore` to delete its temp file on every failure. | `git show 7273c58`: `PreserveFileMode` and `File.Move` moved into the `try`, serialization extracted to `WriteTempFileAsync` so the stream is disposed before the move; `docs/areas/sync-pipeline.md` *State file* reworded; tests `StateStoreSetHighWaterMarkAsyncMoveFailureDeletesTempFile` and `...MoveFailureRethrowsOriginalException`. | confirmed |
| 5 | The `await using` stream and writer must be disposed before `PreserveFileMode` and `File.Move` run; putting the two calls into the existing `try` block without a helper would run them with the temp file still open. | `NfoWriter.cs` lines 202–203: `await using var` declarations are scoped to the `try` block and are disposed only when it ends. | confirmed |
| 6 | A target path that is an existing directory makes `File.Move(temp, target, overwrite: true)` throw `IOException` on Linux and macOS, after the temp file was written. | `StateStoreTests.StateStoreSetHighWaterMarkAsyncMoveFailureDeletesTempFile` uses exactly this and passes on main (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`). For `NfoWriter`: `WriteAsync` line 486 `File.Exists(targetPath)` is `false` for a directory, so with `CreateMissingNfo = true` the creation path runs (`Directory.Exists(directory)` true, line 524), `PreserveFileMode` returns early (line 228, `File.Exists(path)` false) and the move throws (confirmed by reading; the Tester's run in step 4 shows the exception type). | confirmed |
| 7 | `TryDeleteTempFile` swallows only `IOException` and `UnauthorizedAccessException`, so a failing cleanup does not mask the original exception, and `throw;` rethrows it unchanged. | `NfoWriter.cs` lines 240–250 and 212; covered today by `NfoWriterTempFileCleanupFailureSurfacesOriginalError`. | confirmed |
| 8 | Deleting `tempPath` after a failed move can never delete the target: the two paths differ (`path + ".tmp"`), and a move within one directory is a single `rename`, which either replaced the target (no exception) or left the temp file in place. | `NfoWriter.cs` line 198; same directory by construction. | confirmed |
| 9 | No test double or other caller depends on the leftover temp file. | `grep -rn "\.tmp" tests/` hits only `NfoWriterTests` and `StateStoreTests`, all asserting the temp file is absent. | confirmed |
| 10 | Whether the two existing `[OSCondition(OperatingSystems.Linux \| OperatingSystems.OSX)]` attributes in `NfoWriterTests.cs` (lines 371, 407) raise an analyzer diagnostic once the file is changed — #254 replaced the same form in `StateStoreTests.cs` with `ConditionMode.Exclude, OperatingSystems.Windows` without stating why. | to verify: *Analyzer gate* on `NfoWriterTests.cs`, Code Officer, step 6; if it reports them, the Code Officer rewrites them to the `Exclude, Windows` form. | unverified |

## Acceptance criteria

- [ ] AC1: When `File.Move` of `<target>.tmp` onto the target fails, `NfoWriter.WriteAsync` lets the original
  exception reach the caller unchanged in type (`IOException` in the test), `<target>.tmp` no longer exists, and
  the target is unchanged. The same `catch` covers a failure of `PreserveFileMode` (see *Approach*).
- [ ] AC2: After such a failed move, the next write to the same target succeeds once the cause is removed: it
  returns `NfoWriteOutcome.Created`, the target is a file with the item's watch state, and no `<target>.tmp`
  remains.
- [ ] AC3: The existing behavior stays: a serialization failure leaves the existing target's content unchanged
  and deletes `<target>.tmp`; a temp file that cannot be deleted does not mask the original exception; a
  successful write leaves no `<target>.tmp`.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `NfoWriterMoveFailureDeletesTempFile` (new): `CreateWriter(createMissing: true)`, movie path `Heat (1995).mkv` in the temp directory, `Directory.CreateDirectory(nfoPath)` where `nfoPath = Path.ChangeExtension(moviePath, ".nfo")`; `Assert.ThrowsExactlyAsync<IOException>` on `WriteAsync`; then `Assert.IsFalse(File.Exists(nfoPath + ".tmp"))` and `Assert.IsTrue(Directory.Exists(nfoPath))`. `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, `[UnsupportedOSPlatform("windows")]`. Fails on today's code (the temp file stays). | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC2 | `NfoWriterMoveFailureRethrowsOriginalExceptionAndNextWriteSucceeds` (new): same setup and `ThrowsExactlyAsync<IOException>`; then `Directory.Delete(nfoPath)`, a second `WriteAsync` of the same item; assert the outcome is `Created`, `File.Exists(nfoPath)`, its content contains `<watched>true</watched>` (item with `Watched = true`), and no `nfoPath + ".tmp"`. Same OS attributes. A regression guard: it passes on today's code too. | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC3 | Existing, unchanged: `NfoWriterExistingNfoFailedWritePreservesContent`, `NfoWriterTempFileCleanupFailureSurfacesOriginalError`, `NfoWriterConcurrentWritesToSameTargetNeverRace`, `NfoWriterMalformedExistingNfoSkips`. | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |

## Approach

Mirror the `StateStore` fix of #254 in `NfoWriter`:

1. Extract the temp-file write (today lines 202–206: `FileStream` with `FileMode.Create`, `XmlWriter.Create`,
   `document.SaveAsync`, `writer.FlushAsync`) into a new private static `WriteTempFileAsync`, so the `await using`
   stream and writer are disposed when it returns.
2. In `SaveAsync`, keep building `settings` and `tempPath` as today, then one `try` that calls
   `WriteTempFileAsync`, `PreserveFileMode(tempPath, path)` and `File.Move(tempPath, path, overwrite: true)` in
   that order; the existing `catch { TryDeleteTempFile(tempPath); throw; }` stays as the only handler. No new
   exception type, no wrapping, no log line: the exception reaches `MediaItemWriter` / the item-level isolation
   exactly as today.
3. Update the XML doc of `TryDeleteTempFile` only if its wording no longer fits (it does today: "Delete a leftover
   temp file without masking the write failure that triggered the cleanup").

A failure of `PreserveFileMode` (`File.GetUnixFileMode` on a target that vanished after `File.Exists`,
`File.SetUnixFileMode` on the temp file) cannot be provoked deterministically without a fault-injection seam; it
runs inside the same `try` and is covered by the same `catch` that AC1 exercises. No seam is added for it.

No new user-visible output.

## Options considered

- **Delete the temp file on every failure of the save (chosen).** The leftover sits in the user's media library,
  where a media server or a file manager shows it, and it outlives the run until the next write to the same
  target, which may never come (an item removed from Plex, a mapping changed). Deleting it is best effort and
  cannot touch the target (fact 8). It also keeps `NfoWriter` and `StateStore` on the same replace pattern, which
  `docs/ARCHITECTURE.md` (item 7) describes as "the same way `NfoWriter` does".
- **Keep today's behavior and leave the temp file (rejected).** Its only merit is that a leftover could help
  diagnose a failed rename; the error log line already names the target and the exception, and the next write
  overwrites the leftover anyway, so it carries no information the log lacks.
- **A second `try`/`catch` around `PreserveFileMode` and `File.Move` only, without the helper (rejected).** It
  works, but duplicates the cleanup handler and diverges from the shape `StateStore` now has.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `NfoWriter` — `src/PlexToJellyfinSync.Service/NfoWriter.cs` | New private `WriteTempFileAsync`; `SaveAsync` moves `PreserveFileMode` and `File.Move` into its `try`. |
| `PlexToJellyfinSync.Tests` | `NfoWriterTests` — `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` | Two new tests (AC1, AC2). |

## Signatures (for the Dev's skeleton)

New, in the `#region Methods` block next to `SaveAsync`, with XML docs:

```csharp
/// <summary>
/// Serialize an NFO document to the temp file
/// </summary>
/// <param name="document">Document to serialize</param>
/// <param name="tempPath">Temp file path</param>
/// <param name="settings">XML writer settings, including encoding and indentation</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Task</returns>
private static async Task WriteTempFileAsync(XDocument document, string tempPath, XmlWriterSettings settings, CancellationToken cancellationToken)
```

Unchanged: `private static async Task SaveAsync(XDocument document, string path, bool indent, Encoding encoding, CancellationToken cancellationToken)`,
`PreserveFileMode`, `TryDeleteTempFile`, and the public `INfoWriter.WriteAsync`.

## Test files

- `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` (existing; `using System.Runtime.Versioning;` is already
  present).

Existing test code that calls a changed signature: none (only a private member is added).

Files of the change no unit test can cover: none.

## Areas

- [Path mapping and NFO](../../docs/areas/path-mapping-and-nfo.md) — *Limits and errors*: a failed save deletes
  the temporary file in every case, not only after a serialization failure.

## Documentation updates

- `docs/areas/path-mapping-and-nfo.md`, *Limits and errors*, the bullet "A failure to read, create a folder, …"
  (lines 153–156) — owner **Dev**. Replace the two sentences "A temporary file whose serialization failed is
  deleted when possible; one left by a failure after its serialization (copying the file mode, the rename) stays
  next to the target and is overwritten by the next write to that target." with exactly:
  "A failure of the save — creating or serializing the temporary file, copying the file mode or the rename —
  deletes the temporary file when possible; one that cannot be deleted stays next to the target and is
  overwritten by the next write to that target."
- Sweep (done): `docs/ARCHITECTURE.md` items 6 and 7 ("saves every write to a sibling temporary file that replaces
  the target", "the same way `NfoWriter` does") stay true and are not changed; *Atomic replace* in the area
  document describes only the successful path and stays; `docs/areas/sync-pipeline.md` (*State file*) already has
  the `StateStore` wording; `README.md` says nothing about temporary files; decision 0003 (released) states only
  that every write goes through `<name>.tmp`, which stays true.

## Architecture check

The guarantees of `.squad/project.md` (*Guarantees*) on `.nfo` files — only the three watch elements change, an
unchanged value skips the write, encoding and indentation rules — are untouched: the content written, the
skip decision and the atomic rename are the same. Root containment (`IsUnderMappedLocalRoot`) runs before
`SaveAsync` and is unchanged; the only new file operation is the deletion of `<target>.tmp`, a path derived from
an already contained target in the same directory, which today's code deletes on serialization failure already.
The item-level error isolation (decision 0006) is unaffected: the same exception reaches the caller.

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding — no limits.
- [x] Every exception that can reach a user-visible failure reason or message is listed — none new: the
  `IOException` / `UnauthorizedAccessException` of `File.Move` or `PreserveFileMode` reaches the item-level
  handler and the dashboard's *Last error* exactly as today; only the cleanup now runs before the rethrow.

## Decision records

- none: no decision beyond the obvious fix — the change aligns `NfoWriter` with the cleanup rule #254 gave
  `StateStore` (also without a record), touches no guarantee or architecture, and the behavior is written once in
  the area document; the rejected option is stated in this plan and goes into the PR description.

## Out of scope / follow-ups

- A fault-injection seam for `PreserveFileMode` failures (see *Approach*).
- Rewriting the `OSCondition` form of existing tests in `NfoWriterTests.cs` beyond what the *Analyzer gate*
  requires (fact 10).
