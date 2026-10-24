# Path mapping and NFO

## Scope

How a file path reported by Plex becomes a path in this application's file system, and what is written there:
the prefix mapping, the rejection of traversal forms, the containment check against the configured local roots,
the `.nfo` target name per kind of item, the fields an update may change, the content of a new file, the
encoding, the atomic replace, malformed files and concurrent writers on one target. Users rely on it because
their `.nfo` files are shared with Jellyfin and often with other tools; operators rely on it to know which part
of the media volume the application may write.

## Behavior

### Path mapping

- A file path is used only through a configured mapping (`PathMappings:N:Plex` → `PathMappings:N:Local`). A
  path that no mapping matches is never passed through unchanged: the item is skipped and the warning
  `No path mapping for <path>, skipping` is logged, with the path as Plex reported it. An identity mapping
  (`Plex` equal to `Local`) has to be configured explicitly when both sides see the same path (see
  [`README.md`](../../README.md#path-mappings)).
- An item without a file path is skipped with the warning `Item <rating key> (<title>) has no file path,
  skipping`. Plex reports the path of the first part of the item's first media; a path that Plex reports in a
  form the application does not accept is treated as missing ([Sync pipeline](sync-pipeline.md)).
- Before matching, every backslash in the Plex path and in both sides of each mapping is read as `/`, and
  trailing `/` of a mapping's prefixes are ignored.
- A mapping matches when the path equals its `Plex` prefix or continues it with `/`, compared case-sensitively
  and character by character: `/data/Movies` matches `/data/Movies/A.mkv`, not `/data/MoviesOld/A.mkv` and not
  `/data/movies/A.mkv`. When several mappings match, the longest `Plex` prefix wins.
- A mapping whose `Plex` prefix is empty after trimming (an empty value or `/` alone) matches nothing.
- The mapped path is the matching mapping's `Local` prefix followed by the rest of the Plex path unchanged.

### Traversal rejection

- A Plex path is rejected before any mapping is tried when, after the backslash conversion, it contains `/../`,
  ends with `/..`, starts with `../` or is exactly `..`. A rejected path is handled like an unmapped one
  (skipped, `No path mapping for <path>, skipping`).
- Other forms (`.` segments, repeated `/`, names such as `...` or `..x`) are not rejected here; the containment
  check below is the guard for every target.

### Root containment

- Every target is checked before any file is read, created or replaced, independently of the mapping and of the
  traversal rejection: the target path is normalized (made absolute, with `.` and `..` segments and repeated
  separators resolved as text), and it is written only when it equals, or lies below, the normalized `Local`
  value of at least one configured mapping (not necessarily the mapping that produced it). A mapping with an
  empty `Local` value contains nothing.
- The check is lexical: it compares the normalized path strings case-sensitively, character by character, with
  a separator after the root, so `/media/Movies` contains `/media/Movies/A.nfo` but not
  `/media/MoviesOld/A.nfo`. It does not resolve symbolic links: a symbolic link inside a root that points
  outside it is not detected, and a write through it lands where the link points. The check is not
  symlink-safe; the operator keeps links that leave the media roots out of the mapped trees.
- A `Local` value of `/` contains every path.
- A target outside every root is not written; the warning `Refusing to write NFO for <path>: resolved target
  <target> escapes every mapped local root` is logged and the item counts as neither created nor updated.
- Season and series targets lie above the episode files (see *Targets*). A `Local` root that points below a
  series or season folder therefore makes that series' `tvshow.nfo` or that season's `season.nfo` refused.

### Targets

| Item | Target |
| ---- | ------ |
| Movie, `Nfo:MovieFilenameStrategy` = `PreferExistingMovieNfo` (default) | `movie.nfo` in the video's folder when that file exists at the time of the write, else the video's name with the extension `.nfo` |
| Movie, `VideoFileName` | the video's name with the extension `.nfo` |
| Movie, `MovieNfo` | `movie.nfo` in the video's folder |
| Episode | the video's name with the extension `.nfo` |
| Season | `season.nfo` in the folder of the season's first mapped episode file |
| Series | `tvshow.nfo` in the parent folder of the folder of the series' first mapped episode file |

- Only episodes with a file path and a matching mapping take part in a season or series target; a series
  without any such episode gets no `season.nfo` and no `tvshow.nfo`. Episodes without a season number form one
  season of their own.
- Several items can resolve to one target (episodes sharing one multi-episode file, two movies in one folder
  that both use `movie.nfo`, two libraries sharing a folder); each write applies that item's watch state to the
  same file.
- An item of an unknown kind is written like an episode target (`<video>.nfo`) with the root element `movie`. Neither the history run nor the reconcile hands such an item to the writer (see
  [Sync pipeline](sync-pipeline.md#incremental-history-run)); NFO files written for such items before that change
  are left untouched.

### Updating an existing file

- An existing file changes only in the elements `watched`, `playcount` and `lastplayed` directly below its root
  element (the first element of each name); every other element, attribute, comment and text is kept and is
  never refreshed from Plex.
- `watched` is written as `true` or `false`, `playcount` as a decimal integer, and `lastplayed` as the local time
  of the application process in the format `Nfo:DateTimeFormat` (default `yyyy-MM-dd HH:mm:ss`). A missing
  element is appended as the last child of the root element.
- When the item has no last-played time, an existing `lastplayed` element is removed together with the
  whitespace directly before it, so a value never stays behind next to `watched` = `false`.
- Each value is compared as text with the value already in the file; when all three are unchanged, nothing is
  written (no temporary file, no replace, no change of the modification time). A changed `Nfo:DateTimeFormat`
  therefore rewrites `lastplayed` in every file that has one at its next write.
- Whitespace in the file is kept as it is; the file is not re-indented.
- The encoding is the one identified by the file's byte-order mark and is written back with the same mark:
  UTF-8 with a mark, UTF-16 or UTF-32 in either byte order. A file without a byte-order mark is read and
  written as UTF-8 without one; its XML declaration's `encoding` value is not consulted.
- The file is written back through an XML serializer, so its lexical form is normalized even where its content
  is kept: the XML declaration is written (added when missing) with `version="1.0"`, the `encoding` name of
  the encoding written and a `standalone` value the file had; line breaks become the platform's line break
  (LF in the container); attribute values are quoted with `"`; character references such as `&#233;` are
  written as the character; an empty element is written as `<name />`.
- A successful update logs `Updated watch state in <target>` at level Information and counts as updated in the
  run status ([Sync pipeline](sync-pipeline.md)).

### Creating a new file

- A missing file is created only while `Sync:CreateMissingNfo` is `true` (default `true`); otherwise the item is
  skipped without a log line. Missing folders up to the target are created.
- A new file is UTF-8 without a byte-order mark, starts with `<?xml version="1.0" encoding="utf-8"?>` and is
  indented. Its root element is `movie`, `episodedetails`, `season` or `tvshow` by kind, with these children in
  this order, each only when Plex supplies a non-empty value:
  1. `title`, `originaltitle`, `sorttitle`;
  2. episodes: `season` and `episode`; seasons: `seasonnumber`;
  3. `year`, `plot`, `runtime` (whole minutes), one `genre` per genre, `studio`;
  4. `premiered` (`aired` for an episode) as `yyyy-MM-dd`;
  5. one `uniqueid` per identifier, with its `type` attribute and `default="true"` on the default one;
  6. movies: `dateadded` as `yyyy-MM-dd HH:mm:ss` in the local time of the application process;
  7. `watched`, `playcount` and `lastplayed` as for an update.
- A season file carries the title `Season <number>` (`Season` without a number) and the aggregated watch state;
  a series file carries the series' metadata from Plex, or only its title when Plex returns none, and the
  aggregated watch state. How the aggregate is derived is stated in [Sync pipeline](sync-pipeline.md).
- A successful creation logs `Created NFO <target>` at level Information and counts as created in the run
  status.

### Atomic replace

- Every write, update or creation, is first serialized to the sibling file `<target>.tmp` (an existing file of
  that name is overwritten) and then moved over the target in one rename, so a reader sees either the old or
  the new file, never a partial one.
- On a Unix file system the new file takes the file mode of the file it replaces; a newly created file gets the
  default mode of the application's user. Owner and group of a replaced file are not carried over: the rename
  installs a new file created by the application's user.
- The rename replaces the directory entry: a hard link to the old file keeps the old content, and a target that
  is a symbolic link is replaced by a regular file.

### Concurrent writers

- Writes to one target are serialized within the application: the read, the comparison and the replace of one
  writer finish before the next writer to the same normalized target path (compared case-sensitively) starts.
  Writes to different targets may run in parallel.
- There is no coordination with other processes: a tool that edits the same file at the same moment, or a
  second instance of the application on the same volume, can have its change overwritten or overwrite this one.
- The application keeps one lock per distinct target path written since it started; the number grows with the
  number of items in the libraries and is released only on restart.

## Limits and errors

- A malformed existing file (content that is not well-formed XML, including a UTF-16 or UTF-32 file without a
  byte-order mark) is skipped and left untouched — never rebuilt or repaired — with the warning `Skipping
  malformed NFO <target>: existing content could not be parsed as XML`; it counts as neither created nor
  updated, and the same item is tried again on its next write.
- A file without a byte-order mark that is not UTF-8 (for example ISO-8859-1) is decoded as UTF-8: each invalid
  byte sequence becomes U+FFFD, and the file is written back as UTF-8 when one of the three values changes.
- A failure to read, create a folder, write the temporary file or replace the target (a read-only volume, a
  missing permission, a full disk) leaves the target unchanged and is reported as an error of that item; it
  does not stop the run ([Sync pipeline](sync-pipeline.md)). A failure of the save — creating or serializing the
  temporary file, copying the file mode or the rename — deletes the temporary file when possible; one that
  cannot be deleted stays next to the target and is overwritten by the next write to that target.
- The log lines of this area name the Plex path and the local target path in full. The malformed-file warning
  carries the XML parser's message, which can quote a name or character from the file and its line and
  position; no line carries the file's content beyond that.
- Containment is lexical and case-sensitive and does not follow symbolic links (see *Root containment*): it is
  no protection against links inside a root.
- The kept content of an updated file is its XML content, not its bytes (see *Updating an existing file*).

## Related decisions

- [`decisions/0003-nfo-only-watch-fields-touched.md`](../decisions/0003-nfo-only-watch-fields-touched.md) —
  existing files change only in `watched`, `playcount` and `lastplayed`; new files only when enabled.
- [`decisions/0004-unmapped-paths-are-skipped.md`](../decisions/0004-unmapped-paths-are-skipped.md) — a matching
  mapping is mandatory, traversal forms are rejected and every target is checked against the local roots.

## Not here

- When an item is written, which items a run covers, the per-item error isolation, the aggregation rule for
  seasons and series and the run status: [Sync pipeline](sync-pipeline.md).
- The `PathMappings`, `Nfo:*` and `Sync:CreateMissingNfo` keys with their environment variables, defaults and
  the fact that they are not validated at startup, and the writable media volume of the container:
  [Configuration](configuration.md).
- Where it is implemented: `src/PlexToJellyfinSync.Service/PathMapper.cs` (mapping, traversal rejection),
  `MediaItemWriter.cs` (skip of unmapped items), `SeriesAggregateWriter.cs` (season and series folders) and
  `NfoWriter.cs` (containment, targets, update, creation, replace, locks) in the same folder.