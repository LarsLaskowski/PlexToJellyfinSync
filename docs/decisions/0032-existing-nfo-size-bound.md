# 0032: An existing NFO file is read only up to a fixed size bound

- **Status:** Proposed
- **Date:** 2026-11-07
- **Area:** Path mapping and NFO
- **Source:** Issue #275
- **Supersedes:** —

## Context

`NfoWriter` reads an existing `.nfo` file whole into a string and then parses it, on every write of its item. The
files on the media volume do not all come from the operator (see
[0031](0031-nfo-with-a-dtd-is-refused.md)), and nothing bounded their size: the reader had no limit, and the
parser's `MaxCharactersInDocument` was 0 (unlimited). A very large file — or a target that is no regular file at
all, such as a symbolic link to a device that never reaches its end — was read in full into memory each time, and
several writes run in parallel (the reconcile parallelism settings). Real NFO files are small: Jellyfin, Kodi and
tinyMediaManager write metadata, actor lists and links, never embedded images, so even a long-running series'
`tvshow.nfo` stays in the tens or low hundreds of kilobytes. Issue #273 fixed DTD and entity handling and
deliberately left the size out.

## Options considered

1. **A fixed byte bound, enforced by the read itself**: at most one byte more than the bound is ever taken from
   the file; a file that has more is skipped and left untouched. Holds for files that grow while being read and
   for targets without a length; no new setting.
2. **The same bound as a setting** (`Nfo:*`): lets an operator with an unusually large file raise it, but adds a
   key, its validation, documentation and tests for a case no one has reported; it can be added later without
   changing this decision's behavior.
3. **A check of the file length before reading only**: cheap, but the length is read at one moment and the file
   at another, and a device or pipe reports no length (or none that bounds the read), so the read itself stays
   unbounded.
4. **A character bound on the parser only** (`XmlReaderSettings.MaxCharactersInDocument`): the parser runs on the
   string the read has already produced, so the memory of the read is not bounded at all.
5. **A smaller bound (256 KiB) or a larger one (10 MiB)**: the smaller one comes close to large real files and
   would silently stop their watch state; the larger one multiplies the memory each parallel write may take
   (the decoded string takes two bytes per character, the parsed document more) for no file that needs it.

## Decision

Option 1: an existing `.nfo` file is read through a bounded read, and a file larger than the bound is skipped with
a warning of its own, never read further or parsed. The bound and its rules are stated in
[Path mapping and NFO](../areas/path-mapping-and-nfo.md#limits-and-errors).

## Consequences

- The memory one write of an existing file takes is bounded by the bound, whatever the file is; the total still
  scales with the reconcile parallelism.
- A larger file never receives watch state; the operator sees the oversized-file warning on every write of that
  item, and shrinking the file is the only remedy.
- A parser character bound is not added: after a byte bound, without DTDs (0031), the decoded document cannot
  have more characters than the file has bytes.
- Making the bound configurable later (option 2) needs only a setting, not a change of this decision.
