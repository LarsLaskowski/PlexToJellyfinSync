# Spec: Schema version in the state file

Source: Issue #61
Status: Draft

## Problem

The state file `state.json` in `State:Directory` holds one value, `HighWaterMark`, and nothing that says which
format it was written in (`src/PlexToJellyfinSync.Service/State/SyncStateFile.cs`). Reading it today tolerates
missing and unknown values (`StateStore.ReadAsync` deserializes with default `System.Text.Json` options, which
skip unknown properties; pinned by `StateStoreWithUnknownValueReadsMark`), so a later release that changes the
file's shape could not tell an old file from a new one and would silently misread it or drop what it does not
know. The operator affected is the one who upgrades or downgrades the container on an existing `/config`
volume: a misread file costs the high-water mark or, worse, yields a wrong one. While the file is still trivial,
it should start to record its format version, so that a future format change can recognize and migrate the
older files on purpose and an application can recognize a file it does not understand instead of guessing.

Claims of the issue checked against the code: `SyncStateFile` has only `HighWaterMark` (confirmed, lines 6-15);
`System.Text.Json` tolerates missing and unknown properties with the options `StateStore` uses (confirmed: the
options set only `WriteIndented`, and the existing test reads a mark from a file that also carries
`"SchemaVersion": 2`); there is no version marker anywhere in the state code (confirmed by search).

## Scenarios

- S1: Given any start of the application, when a history run writes the high-water mark, then `state.json`
  holds a JSON object with the values `SchemaVersion` and `HighWaterMark`, in this order, `SchemaVersion` being
  the whole number `1` and `HighWaterMark` the mark as today, e.g.
  `{ "SchemaVersion": 1, "HighWaterMark": "2026-02-03T04:05:06+00:00" }` (indented as today).
- S2: Given a `state.json` written by an earlier release, holding `HighWaterMark` without `SchemaVersion`,
  when the application starts and the history run reads the mark, then the file is read as version 1: the
  stored mark is used, nothing is logged about the version, and the file is not moved.
- S3: Given the file of S2, when the next mark is written, then the file holds `SchemaVersion` `1` and the new
  mark (S1): it is upgraded by the ordinary write, with no separate migration step.
- S4: Given a `state.json` holding `"SchemaVersion": 1` and a mark, when the history run reads the mark, then
  the stored mark is used, as today.
- S5: Given no `state.json`, when the application starts, then behavior is unchanged ("no mark": the run seeds
  the mark to the current time), and the file it then writes is the one of S1.

## Errors and edge cases

- E1: `state.json` holds a `SchemaVersion` that is a whole number other than `1` (a file of a newer release
  after a downgrade, or a hand-edited `0` or `-1`). The file is not interpreted: it is moved to
  `state.json.unsupported` (replacing an earlier one), the error
  `State file <path> has unsupported schema version <version>, preserved as <path>.unsupported; starting fresh with no high-water mark`
  is logged, `<path>` being the full path of `state.json` and `<version>` the number read, and the run continues
  with "no mark" (the history run seeds the mark to the current time; the next full reconcile covers the gap).
  The mark the file may carry is not used, even when it looks readable.
- E2: As E1, but moving the file fails (for example a read-only directory). The error
  `State file <path> has unsupported schema version <version> and could not be preserved; starting fresh with no high-water mark`
  is logged and the run continues with "no mark". As for a corrupt file, the file stays in place, every
  following read tries the move again and logs the error again while the move keeps failing, and the next
  successful mark write replaces the file with a version-1 file (S1).
- E3: `SchemaVersion` is present but not a whole number in the JSON number range of a 32-bit integer (a string
  such as `"1"`, a fraction such as `1.5`, `true`, an object, a number beyond 2147483647). The file counts as
  corrupt and is handled exactly as a corrupt file today: moved to `state.json.corrupt` with the existing error
  texts and the parse failure attached, run continues with "no mark".
- E4: `SchemaVersion` is JSON `null`. Treated as a missing version, i.e. version 1 (S2).
- E5: A file holding `"SchemaVersion": 1` without `HighWaterMark` (or with `HighWaterMark` `null`) means "no
  mark", as a file without a mark does today; nothing is logged and the file is not moved.
- E6: Other existing cases are unchanged: a file holding JSON `null`, an empty or unparsable file, a mark that is
  not a time, an unreadable file, and unknown additional values next to a supported version (still ignored, and
  dropped when the file is next written).
- E7: Concurrent reads and writes within the process stay serialized: a write that first reads an unsupported
  file (E1) moves it aside, logs E1 once for that read, and then writes the version-1 file.
- E8: Downgrade to a release before this feature: that release reads the version-1 file, uses its mark and
  ignores `SchemaVersion` (it skips unknown values), and drops `SchemaVersion` on its next write; after a later
  upgrade the file is read as S2. The mark survives both directions.

## Non-functional requirements

- Compatibility: every `state.json` written by a released version (v1.0.0 to v1.6.0, without `SchemaVersion`)
  keeps its mark after the upgrade (S2); files of this version keep their mark after a downgrade (E8). No
  operator action is needed.
- Security and privacy: the new log lines carry only the file path (already logged today) and an integer read
  from the file; no other file content reaches the log. `StateStore` is the security area *State on disk*
  (`.squad/project.md`); the preserved `state.json.unsupported` is a file in the same directory, written only by
  a move of `state.json`, like `state.json.corrupt`.
- The atomic replace, the kept Unix file mode and the in-process serialization of reads and writes are
  unchanged.
- Time and size: no measurable change; the file grows by one value.
- New configuration: none.
- Logs: the two error lines of E1 and E2 at level error, without an attached exception; no new line for S1 to
  S5.

## Acceptance criteria

- [ ] AC1: Every write of the mark produces a JSON object with exactly two values, `SchemaVersion` first with the
  number `1`, then `HighWaterMark` as a string that parses back to the written mark to the tick, whether or not
  a file existed before and whatever version-1 file it replaces (S1, S3, S5).
- [ ] AC2: A file with `HighWaterMark` and without `SchemaVersion`, or with `SchemaVersion` `null`, returns the
  stored mark, logs nothing and is left in place; the next write turns it into the file of AC1 (S2, S3, E4).
- [ ] AC3: A file with `"SchemaVersion": 1` and a mark returns that mark; with `"SchemaVersion": 1` and no mark
  or a `null` mark it returns no mark; in both cases nothing is logged and the file stays in place (S4, E5).
- [ ] AC4: A file whose `SchemaVersion` is a whole number other than `1` (tested with `2`, `0` and `-1`) returns
  no mark even though it carries a readable mark, is moved to `state.json.unsupported` with its content
  unchanged, replacing an earlier `state.json.unsupported`, and logs exactly one error with the first E1 text,
  the full path and the version, and no exception; a mark write that finds such a file logs that error once,
  moves the file and then writes the file of AC1 (E1, E7).
- [ ] AC5: When the move of AC4 fails, the read returns no mark, the file stays in place with its content, and
  exactly one error with the E2 text is logged per read; the next write replaces the file with the file of AC1
  (E2).
- [ ] AC6: A file whose `SchemaVersion` is a string, a fraction, a boolean or a number beyond the 32-bit range is
  handled as a corrupt file: no mark, moved to `state.json.corrupt`, the existing corrupt error with the parse
  failure attached (E3).
- [ ] AC7: The existing state-file behavior of E6 is unchanged: the existing `StateStoreTests` pass, the one that
  reads a mark next to `"SchemaVersion": 2` adapted to an unknown value that is not `SchemaVersion` (E6).
- [ ] AC8: `docs/areas/sync-pipeline.md`, *State file*, states the two values, the version rule (missing or
  `null` is 1, any other whole number is unsupported) and the E1/E2 texts (S1, E1, E2).

## Out of scope

- A migration to a second format version: none exists; the version rule only prepares for one.
- E8 needs no criterion: it is the behavior of released versions, which this change cannot alter, and it
  follows from AC1 and AC2.
- Recovering the mark from an unsupported file (E1) or carrying unknown values over into the rewritten file.
- Showing the schema version on the dashboard or in `/health`.

## Open questions

- Should a file of an unknown, newer version be read best-effort (use its `HighWaterMark` if present) instead
  of being set aside?
  Answered: no. The purpose of the version is that an application does not guess at a format it does not know;
  a newer format may have changed the meaning of `HighWaterMark`, and a wrong mark skips history entries
  silently, while "no mark" is the documented safe state (decision 0005: the mark is seeded and the full
  reconcile covers the gap; *State file* in `docs/areas/sync-pipeline.md`). Preserving the file, as a corrupt
  file is preserved, keeps it for the operator who upgrades again.
- Why `state.json.unsupported` and not `state.json.corrupt`?
  Answered: the file is not damaged, and a separate name keeps a corrupt file and an unsupported one from
  replacing each other; the log text names the path either way.
- Should `0` and negative numbers count as corrupt rather than unsupported?
  Answered: no; one rule (any whole number other than `1` is unsupported) is simpler to state and test, and the
  log line names the number read, so the operator sees what was found.
- Does the change need a decision record?
  Answered: yes, for the plan: decision 0005, released in v1.0.0 and later tags, states under *Consequences*
  that "`state.json` holds exactly one value"; this becomes untrue, so the plan adds a record that amends 0005
  (0005 itself stays unedited). Settled in the plan, not here.

## Area documents

- `docs/areas/sync-pipeline.md`, *State file*: the file holds `SchemaVersion` (currently `1`) and
  `HighWaterMark`; a file without a version, or with `null`, is a version-1 file of an earlier release and is
  upgraded by the next write; any other whole number is unsupported, moved to `state.json.unsupported` with the
  E1/E2 error texts, and means "no mark"; a version that is not a whole number makes the file corrupt. The
  sentence "The high-water mark is the only persisted state. It is kept as a JSON document with one value"
  is reworded accordingly, and the "no mark" list at the end of the section includes the unsupported case.
- `docs/ARCHITECTURE.md`, component 7 (`StateStore`): already links *State file* for the handling of a
  missing or unreadable file; the plan checks whether it needs a word on the version, no guarantee changes.

## Challenge notes

Minor objections of the spec challenge (verbatim):

1. **minor - AC7 contradicts AC1 on the existing tests.** AC7 says "the existing `StateStoreTests` pass" and names only `StateStoreWithUnknownValueReadsMark` as adapted. But `StateStoreWritesMarkInDocumentedFormat` (`tests/PlexToJellyfinSync.Tests/StateStoreTests.cs:399-422`) asserts `Assert.HasCount(1, properties, "The state file should hold exactly one value!")` and `properties[0].Name == "HighWaterMark"`, so it must fail once AC1 holds. Instruction: in AC7, also name `StateStoreWritesMarkInDocumentedFormat` as rewritten to AC1 (two values, `SchemaVersion` first). Its XML summary ("the single value HighWaterMark") needs updating too. Why minor: the Tester would notice anyway, but the criterion as written cannot be met.
2. **minor - "whole number" is not what the deserializer accepts.** E1/E3 sort values by "a whole number" in the 32-bit range. `System.Text.Json` reads an `int`/`int?` only from an integer literal, so `2.0`, `1.0` or `1e0` (mathematically whole) throw `JsonException` and become corrupt, not unsupported or version 1. Instruction: define the version as "a JSON integer literal without fraction or exponent in the `Int32` range". Move `1.0`, `2.0` and `1e0` explicitly into E3 (corrupt), and add one of them to the AC6 test values. Why minor: it only pins down the edge-case wording; the planned behavior does not change.
3. **minor - property-name case and duplicate names are not specified.** The default options are case-sensitive (`StateStore.cs:18-21`). A hand-edited `"schemaversion": 2` is therefore skipped as an unknown value, and the file is read as version 1 with its mark used. That runs against the spec's goal of not guessing at an unknown format. A duplicated `SchemaVersion` key resolves last-wins under default options. Instruction: say in E6 (or a new edge case) that value names are matched case-sensitively, as `HighWaterMark` is today, so a differently cased `SchemaVersion` is an unknown value and the file counts as version 1. Also say a duplicated `SchemaVersion` uses the last occurrence, or decide otherwise in the plan; either way, pin the choice with one test. Why minor: it is about hand-edited files and needs at most one sentence and one test, not a design change.
4. **minor - E2's example contradicts E2's own recovery path.** E2 gives "a read-only directory" as the cause of a failed move, then says "the next successful mark write replaces the file with a version-1 file". In a read-only directory the atomic write (`state.json.tmp` plus move, `StateStore.cs:212-218`) fails too, so no such write happens. The existing corrupt-file tests block the move with a non-empty directory at the target path instead (`StateStoreTests.cs:573-581`). Instruction: replace the example with "for example a directory in the way at `state.json.unsupported`", and test AC5 that way, mirroring `CreateUnmovableCorruptFileAsync`. Why minor: wording and test setup only.
