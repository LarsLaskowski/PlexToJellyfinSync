# Plan: Skip an .nfo target that is not a regular file

Source: Issue #278
Tier: standard — a behavior change in input handling (a check that keeps a non-file entry from stalling the program), not a guard that confines what a path reaches: symbolic links are still followed as before.
Security areas touched: Path mapping and file writes (`NfoWriter`), `.nfo` / XML handling (the read of an existing file), build configuration (`AllowUnsafeBlocks` in `PlexToJellyfinSync.Service.csproj` for a native `libc` call)

## Problem / root cause

`NfoWriter.WriteAsync` (`src/PlexToJellyfinSync.Service/NfoWriter.cs:567`) takes the update path whenever
`File.Exists(targetPath)` is true, which it is for any entry that is not a directory. `ReadExistingAsync` then opens
it with `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)` (line 222). For a FIFO, `open(2)` with
`O_RDONLY` blocks until a writer opens the FIFO; the call is synchronous, ignores the cancellation token, and holds the
target lock (line 563). Runs never overlap (`docs/areas/sync-pipeline.md`, *Polling and schedule*), so the stalled
write stops every later run until restart. A socket fails the open (`ENXIO`, an item error); a device such as
`/dev/null` reads as empty and is logged as malformed, a terminal device blocks on read.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `File.Exists` is true for a FIFO, a socket, a character device, a symbolic link to a device and a dangling symbolic link; false for a directory | scratch probe (`dotnet run`, .NET 11.0.100, Linux): `fifo/sock/dev/broken: Exists=True`, `dir: Exists=False` | confirmed |
| 2 | Opening a FIFO read-only blocks until a writer opens it | `fifo(7)` / `open(2)`; the open is the synchronous `FileStream` constructor at `NfoWriter.cs:222` | confirmed |
| 3 | No BCL member tells a FIFO, socket or device from a regular file on Unix | probe: `File.GetAttributes` and `FileInfo.Attributes` are `Normal` for all of them, `UnixFileMode` holds permission bits only; no `File`/`FileSystemInfo` member names a type | confirmed |
| 4 | `statx` via `[LibraryImport("libc", EntryPoint = "statx")]` with `AT_FDCWD` (-100), flags 0, mask `STATX_TYPE` (1) and a 256-byte `[Out] byte[]` reports the type in `stx_mode` (UInt16 at offset 28): FIFO `0x1000`, character device `0x2000` (also through a link to `/dev/null`), directory `0x4000`, regular `0x8000`; a dangling link fails with errno 2 | scratch probe `…/scratchpad/probe2/Program.cs` on glibc 2.39, x86_64 | confirmed |
| 5 | The `statx` struct layout is the same on every architecture | `include/uapi/linux/stat.h` (fixed-width fields, `stx_mode` at offset 28) | confirmed |
| 6 | `LibraryImport` code compiles only with `AllowUnsafeBlocks` | probe: CS0227 in `LibraryImports.g.cs` without it | confirmed |
| 7 | The generator forwards `DefaultDllImportSearchPaths` to its `DllImport` | probe: generated file line 39 | confirmed |
| 8 | In the pinned runtime image (`aspnet:11.0-alpine`, musl), `libc` resolves and `statx` returns the same types | to verify: the orchestrator runs the probe (`…/scratchpad/probe2`, published framework-dependent) in the image of the Dockerfile's runtime `FROM` line on `linux/amd64` with a FIFO and a regular file, before Security's plan review (*A denied measurement*). Fails closed: a missing symbol throws, every existing-file write fails as an item error | unverified |
| 9 | An item error's exception message reaches the dashboard as the last error | `LibraryReconciler.cs:69`, `SyncOrchestrator.cs:85,106` (`s.LastError = ex.Message`) | confirmed |
| 10 | The existing test `NfoWriterExistingNfoWithoutEndSkips` expects the oversized warning for a link to `/dev/zero` | `NfoWriterTests.cs:425-452` | confirmed |
| 11 | Record 0032 is released | `git tag --contains` of its commit: `v1.6.1` | confirmed |

## Acceptance criteria

- AC1: On Linux, when `File.Exists(targetPath)` is true and `NativeMethods.IsRegularFile(targetPath)` returns false
  in `NfoWriter.WriteAsync` — the target, after following symbolic links, is a FIFO, a character or block device or
  a socket — the target is not opened, read, replaced or created over, whatever `Sync:CreateMissingNfo` is: the
  outcome is `Skipped`, the target entry is unchanged, no `<target>.tmp` exists, and exactly one log entry is
  written, the warning `Skipping NFO <target>: existing target is not a regular file` with the full normalized
  target path.
- AC2: A target that is a symbolic link to a regular file is updated as before (the check follows links).
- AC3: When the file type cannot be read (`statx` fails, for example for a dangling symbolic link), `WriteAsync`
  throws an `IOException` with the message `Could not read the file type of '<target>': <system error message>`
  (the message of `Marshal.GetPInvokeErrorMessage(errno)`), writes no log entry and creates nothing.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `NfoWriterExistingNfoLinkedToDeviceSkips` (DataRows `/dev/null`, `/dev/zero`), `NfoWriterExistingNfoFifoSkips`, `NfoWriterExistingNfoSocketSkips` | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC2 | `NfoWriterExistingNfoLinkedToRegularFileUpdates` (passes on today's code: deferred to step 6) | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |
| AC3 | `NfoWriterExistingNfoDanglingLinkThrows` | `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` |

Test notes for the Tester: all of them `[OSCondition(OperatingSystems.Linux)]`, writer with `createMissing: true`
for AC1 and AC3. The FIFO is created with `Process` on the absolute path `/usr/bin/mkfifo` (`ArgumentList`, no
shell, no PATH lookup), the socket by binding a `Socket` to a `UnixDomainSocketEndPoint` kept open for the test.
`NfoWriterExistingNfoFifoSkips` blocks forever on today's code, so it runs the write in `Task.Run` and bounds the wait
(`WaitAsync` with a generous limit and `TestContext.CancellationToken`) as a hang guard: on timeout it opens the FIFO
for writing and disposes it, which releases the blocked read, then fails. The AC3 test compares the whole message,
computing the system part with `Marshal.GetPInvokeErrorMessage(2)`.

## Approach

1. New `internal static partial class NativeMethods` (`src/PlexToJellyfinSync.Service/NativeMethods.cs`): a private
   `LibraryImport` of `statx` and an internal wrapper `IsRegularFile(string path)`. The wrapper allocates a 256-byte
   buffer, calls `statx(AT_FDCWD, path, 0, STATX_TYPE, buffer)`, and on -1 throws
   `new IOException($"Could not read the file type of '{path}': {Marshal.GetPInvokeErrorMessage(errno)}")` with
   `errno` from `Marshal.GetLastPInvokeError()`. Otherwise it returns whether `BitConverter.ToUInt16(buffer, 28) &
   0xF000` equals `0x8000`; a type the kernel did not fill in reads as 0, so it counts as not regular (fail closed).
   Constants for `AT_FDCWD`, `STATX_TYPE`, the buffer size, the offset and both mode masks, named in PascalCase. No
   struct type (S1104, CS0649), no `unsafe` in the source.
2. `NfoWriter`: a private static `IsRegularFile(string path)` returns `OperatingSystem.IsLinux() == false ||
   NativeMethods.IsRegularFile(path)`; inside the lock, right after `File.Exists(targetPath)` and before
   `ReadExistingAsync`, a false result logs `NfoWriterLog.NonRegularNfoSkipped` and returns `Skipped`. Nothing else
   in the write changes; `ResolveMoviePath`'s `File.Exists(movieNfo)` stays, so a non-regular `movie.nfo` is chosen
   and then skipped.
3. `NfoWriterLog`: event 6 `NonRegularNfoSkipped`, Warning, `Skipping NFO {Path}: existing target is not a regular
   file`.
4. `PlexToJellyfinSync.Service.csproj`: `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in a `PropertyGroup`.

Early draft PR: no

## Options considered

The seven options of [0036](../../docs/decisions/0036-non-regular-nfo-target-is-skipped.md) (`statx` chosen over
`stat`, the runtime-internal `SystemNative_Stat`, a package, a time-limited open, a zero-size heuristic, documenting
the risk only). Two choices inside option 1: a failed `statx` throws (an item error, like a failed open today)
rather than skipping with the new warning, whose text would be wrong for `EACCES`; and a directory at the target
stays out of the check, since `File.Exists` already sends it to the creation path, where it fails without blocking.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync.Service | `NativeMethods.cs` (new) | `statx` import and `IsRegularFile` |
| PlexToJellyfinSync.Service | `NfoWriter.cs` | type check before the read |
| PlexToJellyfinSync.Service | `NfoWriterLog.cs` | event 6 |
| PlexToJellyfinSync.Service | `PlexToJellyfinSync.Service.csproj` | `AllowUnsafeBlocks` |
| PlexToJellyfinSync.Tests | `NfoWriterTests.cs` | new tests, one replaced |

## Signatures (for the Dev's skeleton)

- `internal static partial class NativeMethods` (regions `Constants`, `Methods`)
- `[SupportedOSPlatform("linux")] internal static bool IsRegularFile(string path)` in `NativeMethods`
- `[LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]`
  `[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]`
  `private static partial int Statx(int directoryDescriptor, string path, int flags, uint mask, [Out] byte[] buffer);`
  in `NativeMethods` — the skeleton holds it for real (the generator implements it); private, wrapped (S4200),
  in a class named `NativeMethods` (CA1060), five parameters (S107)
- `[LoggerMessage(EventId = 6, EventName = "NonRegularNfoSkipped", Level = LogLevel.Warning, Message = "Skipping NFO {Path}: existing target is not a regular file")]`
  `public static partial void NonRegularNfoSkipped(ILogger logger, string path);` in `NfoWriterLog` — real in the
  skeleton

`NfoWriter.WriteAsync` keeps its signature. Final files read: `NfoWriterLog.cs` (only event 6 added).

## Test files

`tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` (it holds the `CreateWriter` and `CreateWatchedMovie` helpers;
a second class would copy them).

Existing tests: `NfoWriterExistingNfoWithoutEndSkips` (link to `/dev/zero`, oversized warning) has an input AC1
handles differently; the Tester deletes it in step 5, its case becoming the `/dev/zero` row of
`NfoWriterExistingNfoLinkedToDeviceSkips`. No call site of a changed signature.

Files no unit test can cover: none. The `OperatingSystem.IsLinux() == false` arm in `NfoWriter` is not reached on
the Linux CI; it is one branch condition and the gate's threshold allows it.

## Areas

`docs/areas/path-mapping-and-nfo.md` (+110 words):
- *Scope*: add "targets that are not regular files" to the enumeration.
- *Limits and errors*: a new bullet before the size bound — on Linux, an existing target that is not a regular
  file after following symbolic links (FIFO, device, socket) is skipped before it is opened with the warning of AC1,
  counts as neither created nor updated, is tried again on its next write, also while `Sync:CreateMissingNfo` is
  `true`; on other systems no check is made; a type that cannot be read fails the item with the text of AC3; an
  entry replaced between the check and the open is not caught. In the size-bound bullet, "or a target that never
  reaches its end (for example a symbolic link to `/dev/zero`)" becomes "or a regular file that grows past the
  bound while it is read". In the log-lines bullet, the new warning names the target in full and carries no content.
- *Related decisions*: 0036. *Not here*: `NativeMethods.cs` (the file type check).

## Documentation updates

All by the Dev, once the code is green:
- `docs/areas/path-mapping-and-nfo.md` as under *Areas*.
- `docs/ARCHITECTURE.md`, component 6 (`NfoWriter`): it skips an existing target that is not a regular file
  before opening it ([0036](../../docs/decisions/0036-non-regular-nfo-target-is-skipped.md)), and "non-regular
  targets" joins the list of rules under *Limits and errors* at the end of the paragraph.
- `docs/UNIT_TESTS.md`, *Testing against the filesystem and the clock* (Tester): one bullet — a test whose
  regression would block a thread for good (an open of a FIFO) bounds its wait with a generous real-time limit and
  fails on it; the limit is a hang guard, never the assertion of a duration (about +40 words).

Sweep: `README.md`, `SECURITY.md`, the other area documents and `docs/ARCHITECTURE.md` name no list of file kinds
or `/dev/zero`; the hits are the three in the area document above. No changelog file exists; the PR checklist asks
for unit tests, the area document and the record, all planned.

## Architecture check

The guarantees "existing files change only in their watch fields" and "writes only inside a configured local root"
are kept: the check only adds a skip before any read or write, and containment is untouched. Per-item isolation
(0006) carries AC3's exception. No guarantee of `.squad/project.md` is weakened.

## Security considerations

- [x] No new limit; the 1 MiB bound of 0032 still applies to regular files.
- [x] User-visible exception: AC3's `IOException`, whose message becomes the dashboard's last error and the item
  error's exception: `Could not read the file type of '<target>': <system error message>` — the target path, as
  .NET's own file exceptions already show, and the C library's error text, no file content.
- [x] Native call: one `statx` on a path string, a fixed 256-byte managed buffer the kernel fills at most
  `sizeof(struct statx)` (256) of; the library is looked up outside the application directory
  (`DllImportSearchPath.SafeDirectories`). Interop code exists only in generated code; Sonar's unsafe-code rule
  (S6640) does not look at generated files — unverified until the *Sonar gate* (step 11).
- [x] Check-then-open race: accepted in 0036.

## Decision records

- `docs/decisions/0036-non-regular-nfo-target-is-skipped.md` (Proposed), indexed.

## Out of scope / follow-ups

Follow-up issue for the orchestrator:
- **Title:** Do not open an existing entry at the NFO temporary path. **Body:** `NfoWriter.WriteTempFileAsync`
  opens `<target>.tmp` with `FileMode.Create`, which follows whatever exists there: a FIFO blocks the write as in
  #278, and a symbolic link makes the write land where it points, truncating that file, before the rename replaces
  the link. Decide whether an existing entry at the temporary path is deleted before a new file is created
  (`FileMode.CreateNew`), and state it in *Atomic replace* of Path mapping and NFO. Found while planning #278.

Not changed: a directory at the target path (fails the creation as an item error today, without blocking).

## Challenge notes

Devil's Advocate, mode `plan`, for `/home/user/PlexToJellyfinSync/specs/issue-278/plan.md`.

The plan holds up: no major objections. I checked these claims myself:
- **Root cause is right.** `NfoWriter.cs:567` takes the update path on `File.Exists(targetPath)`. The synchronous `FileStream` open at `NfoWriter.cs:222` runs inside the target lock (line 563).
- **No sibling path is missed.** `ResolveMoviePath` (`NfoWriter.cs:443`) is covered. `PreserveFileMode` (`NfoWriter.cs:321`) is reached only after the check. The `.tmp` open (line 299) is already a follow-up. `StateStore` reads only its own configured state file.
- **`AllowUnsafeBlocks` passes the build-structure check.** `.squad/tools/dotnet_config.py` does not police that property in a project file (it checks only REQUIRED, TargetFramework, package versions and suppressions).
- **`libc` resolves on a glibc host even when the `libc.so` linker script is present.** `/usr/lib/x86_64-linux-gnu/libc.so` exists here and Verified fact 4's probe still succeeded.
- **Tier `standard` fits** *Tiers* in `.squad/routing.md` (line 100): this check keeps a non-file entry from stalling the program; it does not confine what a path reaches.
- **The test fixture fits the plan.** `_tempDirectory` is `/tmp/pjftests<32 hex>` (`NfoWriterTests.cs:52`), so the socket path stays well under the 108-byte limit. The test to be replaced is at `NfoWriterTests.cs:425-452`, as fact 10 says.
- **Decision 0036 does not contradict the released record 0032.** It adds a check before the read; the bound stays for what is read.

Minor objections:

1. **minor: the size-bound rewrite in Path mapping and NFO drops a guarantee that still holds.** The plan (*Areas*) changes "or a target that never reaches its end (for example a symbolic link to `/dev/zero`)" (`docs/areas/path-mapping-and-nfo.md:157`) to "or a regular file that grows past the bound while it is read". On systems other than Linux no type check is made (Approach step 2, `OperatingSystem.IsLinux() == false ||`), so a link to `/dev/zero` still reaches the read and only the 1 MiB bound stops it. Instruction for the Dev: keep the endless-target case in that bullet and qualify its example, e.g. "or a target that never reaches its end (on systems other than Linux, for example a symbolic link to `/dev/zero`; on Linux such a target is skipped earlier, see above)". Why minor: the wording is a documentation detail, but as planned the area document would stop stating a guarantee the code still gives.

2. **minor: a host that refuses `statx` fails every update, and the plan only names the missing-symbol case.** Verified fact 8 checks the image's C library. But a container runtime whose seccomp profile predates `statx` makes the call fail with `EPERM`, not `ENOSYS`. The C library's built-in fallback to a plain `stat` covers only `ENOSYS`, so every existing target then fails as an item error under AC3. I have not run this; it comes from how seccomp profiles and the libc fallback behave. Instruction for the Dev: in the new *Limits and errors* bullet, say that a host that refuses the file-type call fails every update of an existing NFO with the AC3 message ("Could not read the file type of '<target>': <system error message>"). The operator then recognizes the cause. Why minor: it is a documentation addition; the fail-closed design itself stays.

3. **minor: the platform guard form may not satisfy CA1416.** Approach step 2 calls the `[SupportedOSPlatform("linux")]` method behind `OperatingSystem.IsLinux() == false || NativeMethods.IsRegularFile(path)`. Every existing platform guard in the service uses the positive form (`OperatingSystem.IsWindows() || ...` at `NfoWriter.cs:321` and `StateStore.cs:71`). Whether CA1416's flow analysis accepts the `== false ||` form is unchecked. Instruction for the Dev: if CA1416 reports the call, write the check at the call site as `if (OperatingSystem.IsLinux() && NativeMethods.IsRegularFile(targetPath) == false)` instead of the helper. Leave the analyzer settings alone. Why minor: the Dev or the Code Officer can do this without changing the plan.
