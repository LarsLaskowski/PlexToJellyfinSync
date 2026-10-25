# Plan: Schema version in the state file

Source: Issue #61 | [spec.md](spec.md)
Status: Draft
Tier: standard — a behavior change of the state file (a new value, a new "unsupported" read path) that creates, changes or removes no security control.
Security areas touched: State on disk (`StateStore`, a new preserved file `state.json.unsupported`); Logging of external data (one new log value, an integer read from the file)

## Problem / root cause

See [spec.md](spec.md), *Problem*: `SyncStateFile` carries no format marker, so a later format cannot recognize
older files and no release can recognize a newer one.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `SyncStateFile` has only `HighWaterMark` | `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs:6-15` | confirmed |
| 2 | `StateStore` deserializes with options that set only `WriteIndented`, so unknown values are skipped and names are case-sensitive | `StateStore.cs:18-21`; `StateStoreWithUnknownValueReadsMark` (`StateStoreTests.cs:378-391`) reads a mark next to `"SchemaVersion": 2` | confirmed |
| 3 | `ReadAsync` opens the file with `await using var stream` inside the `try`; the corrupt-file move runs in the `catch`, after the stream is disposed | `StateStore.cs:104-128` | confirmed |
| 4 | Every write reads the file first through `ReadAsync`, under the same gate | `StateStore.cs:175-212` | confirmed |
| 5 | `StateStoreWritesMarkInDocumentedFormat` asserts exactly one value named `HighWaterMark` | `StateStoreTests.cs:394-422` | confirmed |
| 6 | The unmovable-file tests block the move with a non-empty directory at the target and run on Unix only | `StateStoreTests.cs:468-516`, `CreateUnmovableCorruptFileAsync` at 573-581 | confirmed |
| 7 | `System.Text.Json` reads an `int?` only from an integer literal in range; `"1"`, `1.5`, `1.0`, `true` and `2147483648` throw `JsonException` | to verify: the AC6 test `StateStoreWithNonIntegerSchemaVersionPreservesItAsCorrupt` passes after step 5; Tester writes it in step 4 (source: spec challenge note 2) | unverified |
| 8 | With default options a duplicated property is accepted and the last occurrence wins (.NET 10 `AllowDuplicateProperties` defaults to `true`) | to verify: the AC9 test `StateStoreWithDuplicateSchemaVersionUsesLastOccurrence` passes after step 5; Tester writes it in step 4 (source: spec challenge note 3) | unverified |
| 9 | Properties are serialized in declaration order, so declaring `SchemaVersion` first writes it first | to verify: the rewritten AC1 test `StateStoreWritesMarkInDocumentedFormat` passes after step 5; Tester writes it in step 4 | unverified |
| 10 | Decision 0005 is released and states "`state.json` holds exactly one value" | `git ls-remote --tags origin` lists v1.0.0 to v1.6.0; `docs/decisions/0005-incremental-history-plus-full-reconcile.md`, *Consequences* | confirmed |
| 11 | No other code reads or writes `state.json`; `IStateStore` exposes only the mark | `grep -rn state.json src`; `src/PlexToJellyfinSync.Core/Abstractions/IStateStore.cs` | confirmed |

## Acceptance criteria

The criteria are AC1–AC9 of [spec.md](spec.md).

## Tests per criterion

All in `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs`, real temporary directory and `RecordingStateStoreLogger`
as the existing tests use. The move-failure tests carry `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`
and `[UnsupportedOSPlatform("windows")]` like their corrupt-file counterparts, with a helper
`CreateUnmovableUnsupportedFileAsync` mirroring `CreateUnmovableCorruptFileAsync`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `StateStoreWritesMarkInDocumentedFormat` (rewritten: two values, `SchemaVersion` first with number `1`, then `HighWaterMark` parsing back to the tick, no prior file); `StateStoreWithVersionOneFileWritesSchemaVersionOnNextWrite` | `StateStoreTests.cs` |
| AC2 | `StateStoreWithoutSchemaVersionReadsMarkAndKeepsFile` (mark returned, no log entry, file unchanged); `StateStoreWithNullSchemaVersionReadsMark`; `StateStoreWithoutSchemaVersionIsUpgradedByNextWrite` (`DataRow`: no `SchemaVersion`, `"SchemaVersion": null`; file then equals the AC1 layout); `StateStoreWithByteOrderMarkReadsMarkAndKeepsFile` (`DataRow`: version 1, no version; a BOM-prefixed file reads its mark and stays in place) | `StateStoreTests.cs` |
| AC3 | `StateStoreWithSchemaVersionOneReadsMark`; `StateStoreWithSchemaVersionOneWithoutMarkReturnsNull` (`DataRow`: no `HighWaterMark`, `HighWaterMark` `null`; no log entry, file in place) | `StateStoreTests.cs` |
| AC4 | `StateStoreWithUnsupportedSchemaVersionPreservesItAsUnsupported` (`DataRow` `2`, `0`, `-1`, each with a readable mark: no mark, `state.json` gone, `state.json.unsupported` with the original content, one error with the exact E1 text and no exception); `StateStoreWithUnsupportedSchemaVersionAndNonTimeMarkPreservesItAsUnsupported` (`DataRow`: a number, an object, a non-time string as `HighWaterMark` next to version `2`: handled as unsupported, not corrupt); `StateStoreWithByteOrderMarkAndUnsupportedSchemaVersionPreservesItAsUnsupported`; `StateStoreWithUnsupportedSchemaVersionReplacesEarlierUnsupportedCopy`; `StateStoreWithUnsupportedSchemaVersionIsReplacedByNextWrite` (one error logged by the write's read, then the AC1 file) | `StateStoreTests.cs` |
| AC5 | `StateStoreWithUnmovableUnsupportedFileKeepsItAndLogsError` (no mark, file and content in place, one error with the exact E2 text); `StateStoreWithUnmovableUnsupportedFileIsOverwrittenByNextWrite` (read then write: two E2 errors, then the new mark reads back and the file has the AC1 layout) | `StateStoreTests.cs` |
| AC6 | `StateStoreWithNonIntegerSchemaVersionPreservesItAsCorrupt` (`DataRow` `"1"`, `1.5`, `1.0`, `true`, `2147483648`: no mark, moved to `state.json.corrupt`, the existing corrupt error text, `JsonException` attached); `StateStoreWithSchemaVersionOneAndNonTimeMarkPreservesItAsCorrupt` (version 1 with a mark that is not a time stays corrupt) | `StateStoreTests.cs` |
| AC7 | the existing `StateStoreTests`, with `StateStoreWithUnknownValueReadsMark` adapted (unknown value `"Comment": "x"` instead of `"SchemaVersion": 2`) and `StateStoreWritesMarkInDocumentedFormat` rewritten (row AC1, summary included) | `StateStoreTests.cs` |
| AC8 | no test: the Reviewer reads the *State file* section against AC8 and the final code in step 8; the Lead's `docs` task checks every statement against the code | `docs/areas/sync-pipeline.md` |
| AC9 | `StateStoreWithDifferentlyCasedSchemaVersionReadsMark` (`"schemaversion": 2` plus mark: mark returned, no log entry, file in place); `StateStoreWithDuplicateSchemaVersionUsesLastOccurrence` (`"SchemaVersion": 1`, then `"SchemaVersion": 2`, plus mark: handled as AC4) | `StateStoreTests.cs` |

Expected texts the tests compare (`<path>` = full path of `state.json`):

- E1: `State file <path> has unsupported schema version <version>, preserved as <path>.unsupported; starting fresh with no high-water mark`
- E2: `State file <path> has unsupported schema version <version> and could not be preserved; starting fresh with no high-water mark`

The guard that fails when a test input lost its quotes or escapes: the AC6 `DataRow` `"1"` must reach the file as
the JSON string `"1"` (the C# literal `"\"1\""`); written without the inner quotes it becomes the integer `1`
and the test fails on "moved to `state.json.corrupt`", because a version-1 file stays in place.

## Approach

1. `SyncStateFile` gains `CurrentSchemaVersion` (`1`) and a nullable `SchemaVersion`, declared before
   `HighWaterMark` so it is written first.
2. `SetHighWaterMarkAsync` sets `state.SchemaVersion = SyncStateFile.CurrentSchemaVersion` next to the mark,
   so every write produces the AC1 file whatever it read.
3. `ReadAsync` deserializes through a new helper `DeserializeFileAsync`, which reads the whole file into bytes
   with `File.ReadAllBytesAsync`, so no handle is open before any move (fact 3 — the move must not run with the
   file still open), and skips a leading UTF-8 byte order mark as the stream deserializer did. It first binds
   the bytes to the internal type `SchemaVersionProbe` (only `SchemaVersion`) with the same `_jsonOptions`: a
   JSON `null` returns `null`; a version other than `null` or `1` returns a `SyncStateFile` carrying only that
   version, without binding the rest of the file; otherwise it binds `SyncStateFile` from the same bytes.
   After the helper returns: a `null` result or a `SchemaVersion` of `null` or `1` returns the state as today;
   any other value moves the file with the renamed `PreserveFile` to `_filePath + ".unsupported"`, logs E1 or
   E2 at level error with no exception (message template placeholders `{Path}`, `{SchemaVersion}`,
   `{PreservedPath}`), and returns `new SyncStateFile()`. So a file of an unsupported version is unsupported
   whatever its other values hold, and a version-1 file whose mark is not a time stays corrupt. The
   `JsonException` and generic catches stay as they are and keep wrapping the helper call, so E3 and E6 are
   unchanged.
4. `PreserveCorruptFile(string corruptPath)` becomes `PreserveFile(string preservedPath)`; its body is
   unchanged, the corrupt path calls it with `_filePath + ".corrupt"`.

Layout of the user-visible output: the file layout is fixed by spec S1 (indented as today, `SchemaVersion`
first); the log texts are the exact E1/E2 texts above.

## Options considered

- Behavior for an unknown version: best-effort read, move aside as "no mark" (taken), or stop the application;
  weighed in [decision 0027](../../docs/decisions/0027-state-file-schema-version.md).
- Reuse `state.json.corrupt` for an unsupported file: lost, because a corrupt and an unsupported file would
  replace each other and the file is not damaged (spec, *Open questions*).
- A check on the bound `int?` after binding the whole file: lost in review, because a newer file whose
  `HighWaterMark` no longer binds would count as corrupt instead of unsupported; the version is read first
  through `SchemaVersionProbe` with the same options, and `SyncStateFile` is bound only for version `null` or
  `1`.
- Default `SchemaVersion` to `1` in the property initializer instead of setting it on write: lost, because an
  explicit `null` in the file would then overwrite it and the write would emit `null`; setting it on write
  keeps AC1 independent of what was read.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `State/SyncStateFile.cs` | constant and `SchemaVersion` property |
| `PlexToJellyfinSync.Service` | `State/SchemaVersionProbe.cs` | new internal type holding only `SchemaVersion`, read before binding |
| `PlexToJellyfinSync.Service` | `State/StateStore.cs` | version check in `ReadAsync`, `DeserializeFileAsync`, `PreserveFile` rename, version set on write |
| `PlexToJellyfinSync.Tests` | `StateStoreTests.cs` | new tests, two adapted |

## Signatures (for the Dev's skeleton)

In src/PlexToJellyfinSync.Service/State/SyncStateFile.cs (rewritten by the skeleton; a Constants region before the
Properties region):

- `public const int CurrentSchemaVersion = 1;` — summary "Schema version written by this release"
- `public int? SchemaVersion { get; set; }` — declared before `HighWaterMark`; summary "Schema version of the
  file; <c>null</c> for a file of a release before the version was introduced"

In src/PlexToJellyfinSync.Service/State/StateStore.cs (changed by the skeleton, Methods region):

- `private async Task<SyncStateFile?> DeserializeFileAsync(CancellationToken cancellationToken)` — reads
  state.json into bytes, probes the version, binds SyncStateFile only for version null or 1; throws what
  the deserializer or the file system throws

In src/PlexToJellyfinSync.Service/State/SchemaVersionProbe.cs (new file; its own file because RH2101 forbids
nested classes):

- internal sealed class `SchemaVersionProbe` with the property `public int? SchemaVersion { get; set; }`
- `private bool PreserveFile(string preservedPath)` — replaces PreserveCorruptFile(string corruptPath)

No change to IStateStore, FakeStateStore or any other public member of StateStore.

## Test files

- `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` — the only test file (one class per type under test).

Existing test code that calls a changed signature: none (the changed members are private or additive). The two
tests whose input a new criterion handles differently, `StateStoreWithUnknownValueReadsMark` and
`StateStoreWritesMarkInDocumentedFormat`, are adapted by the Tester in step 4 (AC7). All other `StateStoreTests`
use files without `SchemaVersion` or corrupt content and keep passing unchanged; tests that read back a
written file compare only the mark.

Files of the change no unit test can cover: none.

## Areas

- Sync pipeline (`docs/areas/sync-pipeline.md`), *State file*: the two values, the version rule, the
  unsupported case with its texts, case-sensitivity and duplicate names, the extended "no mark" list; *Related
  decisions*: 0027.

## Documentation updates

Sweep for enumerations and "only/exactly one" statements about the state file (`grep -rn "state.json\|state file\|high-water" docs README.md SECURITY.md .squad/project.md`):

- `docs/areas/sync-pipeline.md`, *State file* ("The high-water mark is the only persisted state … one value",
  the "no mark" cases, the corrupt-file bullet extended by the unsupported bullet) and *Related decisions* —
  owner Lead (task group G4).
- `docs/ARCHITECTURE.md`, component 7 `StateStore`: "the handling of a missing or unreadable file" becomes
  "a missing, unreadable or unsupported file" — owner Lead (G4). No guarantee or flow changes, so no record
  link is needed there.
- `docs/decisions/0005-incremental-history-plus-full-reconcile.md`: released, stays unedited; amended by 0027.
- Checked and still true: `README.md:45` ("stores `state.json` (the incremental high-water mark)"),
  `docs/areas/configuration.md:45,84`, `.squad/project.md` (*State on disk*), `docs/UNIT_TESTS.md:229-231`.

## Architecture check

No guarantee of `.squad/project.md` is weakened. The seeding guarantee (0005) is kept and is what makes the
unsupported case safe: "no mark" seeds the mark to now and the reconcile covers the gap. The atomic replace,
the kept file mode and the in-process gate are unchanged; the new move happens under the same gate as the
corrupt move.

## Security considerations

- [x] No limits: the file size is not bounded today and the change adds none; the version is an `Int32`
  bound by the deserializer.
- [x] Exceptions reaching a user-visible message: none new. The unsupported path logs a fixed text with the
  path (already logged today) and the integer; no file content other than that integer reaches the log or
  the dashboard. `state.json.unsupported` is created only by a move of `state.json` within `State:Directory`,
  replacing an earlier file of that name, as `state.json.corrupt` is: the same file-system exposure as the
  existing corrupt path, no new write target outside `State:Directory`.

## Decision records

- `docs/decisions/0027-state-file-schema-version.md` (Proposed) — amends 0005's "exactly one value"; index row added.

## Out of scope / follow-ups

As in [spec.md](spec.md), *Out of scope*. No follow-up issue.
## Security notes

Non-blocking findings of the Security plan review (verbatim):

- Non-blocking: the Lead can mark facts 7, 8 and 9 in plan.md as confirmed, with this measurement as evidence (.NET SDK 10.0.402, options `WriteIndented` only: `"1"`, `1.5`, `1.0`, `1e0`, `true`, `{}`, `[1]`, `2147483648`, `-2147483649` throw `JsonException`; duplicate `SchemaVersion` gives the last value; `"schemaversion"` is ignored; declaring `SchemaVersion` first writes it first and the const is not serialized). The Tester's AC1, AC6 and AC9 tests still pin the behavior.
- Non-blocking, for information: `-0` binds to 0, so it is handled as "unsupported" (E1), not "corrupt". This matches the spec's "integer literal with optional leading minus" rule, so nothing needs to change.
