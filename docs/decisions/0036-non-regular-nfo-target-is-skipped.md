# 0036: An existing NFO target that is not a regular file is skipped before it is opened

- **Status:** Accepted
- **Date:** 2026-11-28
- **Area:** Path mapping and NFO
- **Source:** Issue #278
- **Supersedes:** —

## Context

`NfoWriter` treats every existing path as a file (`File.Exists`, which is true for a FIFO, a device, a socket and
a dangling symbolic link) and opens it for reading. Opening a FIFO for reading blocks until some process opens it
for writing; the open cannot be cancelled, so the write never finishes, keeps its target lock, and — since runs
never overlap ([Sync pipeline](../areas/sync-pipeline.md)) — no later run starts until the application is
restarted, and stopping the application waits on it. A device can block in the same way (a terminal), and a socket
fails the open. [0032](0032-existing-nfo-size-bound.md) bounds the read of such a target once it is open, but it
cannot help with an open that never returns. The base class library exposes no file type on Unix (attributes and
`UnixFileMode` look the same for a FIFO and a regular file), so a check needs the operating system's `stat` family.
The application runs as a Linux container on `linux/amd64` and `linux/arm64` (Alpine, musl).

## Options considered

1. **Skip a non-regular target, checked with `statx` through `LibraryImport`** — one call, a struct layout that is
   the same on every architecture, in glibc and in musl; needs `AllowUnsafeBlocks` for the generated interop code
   and covers Linux only.
2. **The same check with `stat`** — the struct layout differs between `x86_64` and `arm64`, and glibc before 2.33
   exports no `stat` symbol.
3. **The runtime's own `SystemNative_Stat`** — normalized for every Unix, but an internal interface of the runtime
   whose struct may change with any major version, which this project follows
   ([0033](0033-follow-the-current-dotnet-major.md)).
4. **A package (`Mono.Posix`, `Tmds.LibC`)** — a dependency with native assets for one call.
5. **Open with a time limit on another thread** — the blocked open cannot be cancelled: every stalled write keeps a
   thread and its target lock for good.
6. **A file size of zero as the sign** — the size is no file type: an empty regular file would change its warning,
   and a device may report a size.
7. **Document the risk only, like symbolic links** — one FIFO stops every later run, a much heavier consequence than
   one skipped file.

## Decision

Option 1: on Linux, an existing target whose file type, after following symbolic links, is not a regular file is
skipped with a warning before it is opened; a failure to read the type fails the item. The rules are stated in
[Path mapping and NFO](../areas/path-mapping-and-nfo.md#limits-and-errors).

## Consequences

- A FIFO, device or socket in the media folders no longer stalls or fails a run; the operator sees the warning on
  every write of that item, and removing or renaming the entry is the remedy.
- The check and the open are two steps: an entry replaced by a FIFO between them can still block one write. Making
  that possible needs write access to the media folders, which already allows worse.
- On Windows and other Unix systems the check is not made; only the Linux container is protected.
- The service project allows unsafe code for the generated interop; the project's own sources hold none.
- The temporary file `<target>.tmp` is still opened without such a check (follow-up issue).
