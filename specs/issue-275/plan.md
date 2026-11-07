# Plan: Bound the size of an existing .nfo file before it is read

Source: Issue #275
Tier: standard — a behavior change (an oversized existing file is skipped instead of updated) whose check only keeps oversized input from exhausting memory: input handling, not a security control (`.squad/routing.md`, *Tiers*).
Security areas touched: `.nfo` / XML handling (`NfoWriter`)

## Problem / root cause

`src/PlexToJellyfinSync.Service/NfoWriter.cs:188-196` (`ReadExistingAsync`) reads the whole file with
`StreamReader.ReadToEndAsync` before anything looks at its size, and `ParseExisting` (`NfoWriter.cs:163-175`)
leaves `MaxCharactersInDocument` at its default 0 (unlimited). `WriteAsync` (`NfoWriter.cs:518-520`) calls the read
for every existing target, so a file of any size — or a target that never reaches end of file — is read into
memory in full on every write of its item, with up to `EpisodeReconcileParallelism` × `LibraryReconcileParallelism`
writes at once.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The issue: `ReadExistingAsync` reads the existing file whole into memory | `NfoWriter.cs:190-193`: `FileStream` + `StreamReader.ReadToEndAsync`, no length check | confirmed |
| 2 | The issue: `MaxCharactersInDocument` is 0 | `NfoWriter.cs:165-169` sets only `DtdProcessing` and `XmlResolver`; the framework default of `MaxCharactersInDocument` is 0 = no limit | confirmed |
| 3 | The issue: read and parsed in full on every write of its item | `NfoWriter.cs:518-526`, no cache; called from `MediaItemWriter` and `SeriesAggregateWriter` for each item | confirmed |
| 4 | Several writes run in parallel | `LibraryReconciler.cs:147-153` (`EpisodeReconcileParallelism`, default 4), `SyncOrchestrator.cs:333-339` (`LibraryReconcileParallelism`, default 2) | confirmed |
| 5 | Decoded characters never exceed the file's bytes for every encoding the reader accepts (UTF-8 with U+FFFD replacement, UTF-16, UTF-32 by byte-order mark), so a byte bound also bounds the parser's input and no `MaxCharactersInDocument` is needed | UTF-8: 1–3 bytes per BMP char, 4 bytes per surrogate pair, at most one U+FFFD per invalid byte; UTF-16: 2 bytes per char; UTF-32: 4 bytes per 1–2 chars; DTDs are prohibited (0031), so no entity expands | confirmed |
| 6 | A target that is a symbolic link to `/dev/zero` passes `File.Exists` and is read by `ReadToEndAsync` until memory runs out; `FileStream.Length` on it does not bound the read (non-seekable or 0) | to verify: the Tester's AC3 test in step 6 (Linux, after the fix only — on today's code it exhausts the test host's memory) | unverified |
| 7 | The existing-file tests use `NullLogger<NfoWriter>`, so no test sees a warning of `NfoWriter` today | `NfoWriterTests.cs:812` | confirmed |
| 8 | No real sample is needed: the change bounds the byte count of the read; decoding and parsing of the format are unchanged, and the tests generate files of exact byte lengths | `ParseExisting` and the `StreamReader` encoding detection stay as they are (*Approach*) | confirmed |
| 9 | Nothing else documents a limit on existing files: the sweep of `docs/areas/`, `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` for "size", "limit", "bound" and the NFO enumerations found only the hits under *Documentation updates* | `grep -n -i "size\|bound\|limit\|nfo"` over those files | confirmed |

## Acceptance criteria

The bound is **1,048,576 bytes** (1 MiB), counted on the file's bytes as stored, byte-order mark included, before
decoding.

- AC1: An existing `.nfo` of exactly 1,048,576 bytes that is well-formed XML is updated as today (outcome
  `Updated`), in UTF-8 without a byte-order mark and in UTF-16 LE with one.
- AC2: An existing `.nfo` of 1,048,577 bytes or more — well-formed XML, in UTF-8 without a byte-order mark and in
  UTF-16 LE with one — is skipped (outcome `Skipped`), its bytes stay unchanged, no `<target>.tmp` remains, and
  exactly one warning is logged with the text
  `Skipping oversized NFO <target>: existing file is larger than 1048576 bytes`, where `<target>` is the resolved
  full target path. The text is decided by the new size branch of `WriteAsync` (the result of the bounded read),
  not by the parser; the malformed-file warning is not logged for it.
- AC3: A target that delivers bytes without end (a symbolic link to `/dev/zero` inside the mapped root) is skipped
  with the AC2 warning, and the write completes — the read takes at most 1,048,577 bytes from it.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `NfoWriterExistingNfoAtSizeBoundUpdates` (DataRow: UTF-8 no BOM; UTF-16 LE with BOM) — passes on today's code, so deferred to step 6 | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC2 | `NfoWriterExistingNfoOverSizeBoundSkips` (DataRow: UTF-8 no BOM, 1,048,577 bytes; UTF-16 LE with BOM, 1,048,578 bytes; UTF-8, 4 MiB) — asserts outcome, bytes, no `.tmp`, the one warning's level and exact text through `RecordingNfoWriterLogger` | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC3 | `NfoWriterExistingNfoWithoutEndSkips` — `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, symbolic link `<movie>.nfo` → `/dev/zero`; written in step 6 and never run on the unfixed code (it would exhaust memory), deferred | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |

The Tester builds each file to the exact byte count (well-formed XML padded with whitespace or a comment inside the
root) and asserts the length on disk before the write, so a fixture off by one fails instead of testing the wrong
side of the bound.

## Approach

- `NfoWriter` gets the bound as a private constant, and `ReadExistingAsync` reads through a bounded read: it takes
  at most bound + 1 bytes from the stream (a read loop such as `Stream.ReadAtLeastAsync` with
  `throwOnEndOfStream: false` into a buffer of bound + 1 bytes; a pooled buffer from `ArrayPool<byte>.Shared`,
  returned in `finally`, so a reconcile does not allocate 1 MiB per item). More than bound bytes read → the
  result says "oversized" and nothing is decoded. Otherwise the bytes are decoded exactly as today: a
  `StreamReader` with `_utf8NoBom` and `detectEncodingFromByteOrderMarks: true` over a read-only `MemoryStream` of
  the bytes read, returning its content and `CurrentEncoding` — the encoding rules of *Updating an existing file*
  stay unchanged.
- No check of `FileStream.Length` (fact 6: it does not bound a device or a growing file; one mechanism, one
  place). No `MaxCharactersInDocument` (fact 5).
- `WriteAsync`: when the read reports "oversized", log the AC2 warning at level Warning and return
  `NfoWriteOutcome.Skipped`, before `ParseExisting`; the target lock is released by the existing `finally`.
- Read failures (permission, I/O) keep propagating as today.

## Options considered

Recorded with their reasons in [0032](../../docs/decisions/0032-existing-nfo-size-bound.md): a configurable bound
(no reported need, can be added later), a length pre-check only (does not bound the read), a parser character bound
only (the read is already done), a smaller or larger value.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `NfoWriter.cs` | constant, bounded `ReadExistingAsync`, oversized branch in `WriteAsync` |
| `PlexToJellyfinSync.Tests` | `NfoWriterTests.cs`, new `RecordingNfoWriterLogger.cs` | tests AC1–AC3; `CreateWriter` takes an optional logger |

## Signatures (for the Dev's skeleton)

No public API changes; no skeleton (step 4 skipped). Private members, for orientation:

- `private const int MaxExistingFileBytes = 1_048_576;` in `NfoWriter`
- `private static async Task<(string Content, Encoding Encoding)?> ReadExistingAsync(string path, CancellationToken cancellationToken)` — `null` when the file exceeds the bound

## Test files

- `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` (new tests)
- `tests/PlexToJellyfinSync.Tests/RecordingNfoWriterLogger.cs` (new double, `ILogger<NfoWriter>`, the pattern of
  `RecordingStateStoreLogger.cs`)

Existing test code: the helper `CreateWriter` (`NfoWriterTests.cs:793`) gains an optional
`ILogger<NfoWriter>? logger = null` parameter (default `NullLogger<NfoWriter>.Instance`); the Tester changes it in
step 5, existing callers stay unchanged. No existing test has an input the bound treats differently (all fixtures
are far below 1 MiB, the largest is the entity-expansion payload of `CreateEntityExpansionNfo`, under 1 KiB).

Files no unit test can cover: none.

## Areas

- [Path mapping and NFO](../../docs/areas/path-mapping-and-nfo.md): the size bound (*Limits and errors*), the
  scope line, the decision link.

## Documentation updates

All by the Dev once the code is green, against that code:

- `docs/areas/path-mapping-and-nfo.md`
  - *Scope*: the enumeration "malformed files, document type declarations and entities" gains the size bound of an
    existing file.
  - *Limits and errors*: a new bullet after the DTD bullet — the bound (1,048,576 bytes, counted on the bytes as
    stored, byte-order mark included, before decoding); a larger file, or a target that never ends, is skipped and
    left untouched, read no further than one byte past the bound and never parsed, with the AC2 warning; it counts
    as neither created nor updated and is tried again on its next write. The log-lines bullet names the new
    warning (path in full, no content).
  - *Related decisions*: a line for 0032.
- `docs/ARCHITECTURE.md`, `NfoWriter` item (line 144 ff.): "read by a reader that prohibits DTDs" becomes a bounded
  read plus that reader, linking [0032](../../docs/decisions/0032-existing-nfo-size-bound.md); "malformed files and
  DTDs under *Limits and errors*" adds the size bound.
- `.squad/project.md`, *Test doubles*: one line `RecordingNfoWriterLogger` (`RecordingNfoWriterLogger.cs`) —
  `ILogger<NfoWriter>`. Members may not write under `.squad/` (`.squad/team.md`, *Files*): the Tester reports it,
  the orchestrator applies it.
- `SECURITY.md`, `README.md`: no change (the sweep found no size statement; the scope list names entity attacks,
  not resource bounds).

## Architecture check

Guarantee "existing `.nfo` files change only in their watch fields" (0003): kept — an oversized file is not
changed at all. "Failures are isolated per item" (0006): the oversized case is a skip, not an exception. No flow
or component changes; `docs/ARCHITECTURE.md` gets the one sentence above.

## Security considerations

- [x] The bound applies before decoding, on the raw bytes, byte-order mark included; decoding cannot raise the
  character count above it (fact 5).
- [x] Exceptions reaching a message: none new. The oversized warning is a fixed text with the target path; read
  exceptions are unchanged.

## Decision records

- `docs/decisions/0032-existing-nfo-size-bound.md` (Proposed), indexed.

## Out of scope / follow-ups

- A target that is a named pipe (FIFO): opening it for reading blocks until a writer appears, before any read, so
  the bound does not help and the write of that item hangs while holding its target lock. Proposed follow-up
  issue — title "Skip an .nfo target that is not a regular file", body: "`NfoWriter.WriteAsync` treats any
  existing path as a file (`File.Exists`); a FIFO at a target path blocks `new FileStream(...)` until a writer
  opens it, stalling that write and the reconcile waiting on it. Decide whether non-regular files (FIFO, device,
  socket) are skipped before opening, and state it in *Path mapping and NFO*. Found while planning #275."

## Challenge notes

- (minor) Limit the rented buffer to bound + 1 bytes when reading. `ArrayPool<byte>.Shared.Rent(1_048_577)` returns an array of at least that size, normally the next power of two (2 MiB), and `Stream.ReadAtLeastAsync` can fill the whole buffer it is given. Call it as `ReadAtLeastAsync(buffer.AsMemory(0, MaxExistingFileBytes + 1), MaxExistingFileBytes + 1, throwOnEndOfStream: false, cancellationToken)` so AC3 holds (at most 1,048,577 bytes read).
- (minor) Decode only the bytes actually read. Build the decode stream as `new MemoryStream(buffer, 0, bytesRead, writable: false)`, never over the whole rented array; otherwise a small file picks up stale bytes from an earlier rental and fails as malformed or gets a corrupt write. AC1/AC2 would not reliably catch it, so the Reviewer checks it explicitly.
