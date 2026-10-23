# Sync pipeline

## Scope

When the application reads the watch state from Plex and when it hands an item to the NFO writer: the polling
cadence, the incremental history run with its persisted high-water mark, the full reconcile, the restriction to
the Plex owner, the requests made to Plex, the parallelism, the season and series aggregation rule, the isolation
of failures and the run status the application reports. Operators rely on it to know when a change in Plex reaches
the `.nfo` files and what a failure costs; the dashboard and `/health` rely on the status it maintains.

## Behavior

### Polling and schedule

- The application only polls Plex; it receives no webhooks and offers Plex no endpoint to call.
- On startup it runs one full reconcile. It then repeats a cycle: wait `Sync:PollIntervalSeconds`, run the
  incremental history run, and run a full reconcile when at least `Sync:FullReconcileIntervalHours` have passed
  since the previous reconcile ended.
- The wait starts when the previous run has ended, so the time between two history runs is the interval plus the
  duration of the run before it. A due reconcile starts right after the history run of the cycle in which it
  became due.
- Runs never overlap: a history run and a reconcile are never in progress at the same time, and a run never
  starts while the previous one is still in progress.
- The reconcile interval is measured from the end of the previous reconcile, whether that reconcile succeeded or
  failed; a failed reconcile is not retried before the interval has passed again.
- Defaults and allowed ranges of both intervals are in [Configuration](configuration.md).

### Incremental history run

- The history run reads the persisted high-water mark — the newest viewed-at time already processed — and asks
  Plex for the owner's history entries viewed strictly after it.
- Without a mark (the first start, or any case under *State file* below that yields no mark), the run sets the
  mark to the current time of the application host, processes no history entry and makes no request to Plex.
  Plays before that moment reach the `.nfo` files only through a full reconcile.
- History is read newest first in pages of 500 entries. Paging stops at the first page that contains an entry at
  or before the mark, at a page shorter than 500 entries, at an empty page, or at a page whose first entry equals
  the first entry of the previous page (logged as a warning, see *Limits and errors*).
- An entry without a viewed-at time or without a rating key is ignored. An entry without an account id counts as
  the owner's.
- The entries are processed one after another, oldest first. For each entry the item is read from Plex
  individually and handed to the NFO writer; an entry played several times is processed once per play.
- After the entries, the season and series aggregates are written for every show of which an episode was
  processed (see *Season and series aggregates*), then the mark is set to the newest viewed-at time among the
  entries, if that is newer than the stored mark.
- The mark advances past an entry whose item failed to process; that item's watch state reaches its `.nfo` file
  only through the next full reconcile.

### Full reconcile

- A reconcile lists the Plex libraries and processes every movie library and every show library; libraries of
  any other kind are ignored. When `Plex:Libraries` lists library keys, only libraries whose key is listed are
  processed; an empty list means all libraries.
- In a movie library every movie is handed to the NFO writer, regardless of its watch state. In a show library
  every episode of every show is handed to the NFO writer, followed by the show's season and series aggregates
  when they are enabled.
- Library contents and the episodes of a show are read page by page, 200 items per page. A further page is
  requested only after the items of the pages before it have been processed, so a change in Plex during a long
  reconcile may be seen by one page and missed by another until the next reconcile.
- The reconcile is the catch-up path: it writes changes that produce no history entry (for example an item marked
  watched by hand) and the items whose processing failed in an earlier run.

### Owner only

- Only the Plex server owner's watch state is synchronized; the history request asks for the owner's account,
  and an entry of another account is dropped.
- The owner's account id is `Plex:OwnerAccountId` when it is set. Otherwise it is resolved from the Plex account
  list as the lowest positive account id. When that request fails (except with `401` or `403`) or its answer
  cannot be read, a warning is logged and the id `1` is used; an answer without a usable account also yields `1`.
- A `401` or `403` answer or a timeout while resolving the owner is not replaced by the fallback: the run fails
  (see *Limits and errors*) and the owner is resolved again on the next run.
- The resolved id is kept for the following runs until a run fails; after a failed run it is resolved again,
  so a fallback id resolved during a failing run is not kept.
- The run that only sets a missing mark resolves no owner.
- The watch values written for an item (watched, play count, last played) are the values Plex reports for that
  item to the requests made with `Plex:Token`; the application does not filter them by account.

### What is read from Plex

- Every request goes to `Plex:BaseUrl`, asks for JSON and carries the `X-Plex-Token` header when `Plex:Token` is
  set; whether the token is required is in [Configuration](configuration.md).
- Every request times out after 30 seconds. An answer with a status other than success counts as a failed
  request.
- An item counts as watched when its Plex view count is greater than zero; its play count is the view count
  (`0` when absent); its last-played time is Plex's last-viewed time, absent when Plex reports none or a value
  of zero or less.
- An item whose rating key is missing, longer than 64 characters or contains whitespace or a control character is
  skipped with a warning. An episode's reference to its show that fails the same rule is dropped with a warning,
  so the episode contributes to no aggregate in a history run.
- A file path longer than 4096 characters or containing a character that is not allowed in a path (on Linux, the
  NUL character) is treated as missing, with a warning; an item without a file path is not written (see
  [Path mapping and NFO](path-mapping-and-nfo.md)).
- Title, original title, sort title, studio and show title are cut to 512 characters, the plot to 4000
  characters, never inside a surrogate pair.

### Parallelism

- Up to `Sync:LibraryReconcileParallelism` libraries are reconciled at the same time.
- Within a library, movies and shows are processed one at a time. The episodes of one show are written with up to
  `Sync:EpisodeReconcileParallelism` writes at the same time.
- Episodes that share one file (a multi-episode file) are written one after another, never concurrently with each
  other.
- History entries and the shows whose aggregates a history run writes are processed one at a time.
- Minimums and defaults of both parallelism settings are in [Configuration](configuration.md).

### Season and series aggregates

- Aggregates are written only while `Sync:WriteSeriesSeasonAggregates` is `true`, after a history run (for the
  shows it touched) and in a reconcile (for every show of a show library).
- An aggregate counts only the show's episodes that have a file path and a matching path mapping. A show without
  such an episode gets no aggregate.
- In a history run the episodes of each touched show are listed from Plex again, so the aggregate reflects every
  episode of the show, not only the played ones. In a reconcile the aggregate uses the same episode listing as
  the episode writes of that show.
- A season aggregate covers the counted episodes with the same season number; a series aggregate covers all
  counted episodes of the show.
- An aggregate is watched only when every counted episode is watched. Its play count is then `1` and its last
  played time is the newest last-played time of its episodes; otherwise it is unwatched, with play count `0` and
  no last-played time.
- The season aggregate is written in the directory of the season's first counted episode, the series aggregate in
  the parent of the directory of the show's first counted episode. The series aggregate takes the show's
  metadata from Plex; when Plex returns none, it uses the show title of the first counted episode. The file
  names, the root check and what changes in an existing file are in
  [Path mapping and NFO](path-mapping-and-nfo.md).

### State file

- The high-water mark is the only persisted state. It is kept as a JSON document with one value,
  `HighWaterMark`, in the file `state.json` in the directory `State:Directory`; the directory is created when it
  is missing.
- The file is replaced atomically: the new content is written to `state.json.tmp` in the same directory and
  then moved over `state.json`, keeping the existing file's Unix file mode. A failed write deletes the temporary
  file and leaves `state.json` unchanged.
- Reads and writes of the file are serialized within the process.
- A missing file, or a file without a mark, means "no mark".
- A file that cannot be parsed is moved to `state.json.corrupt` (replacing an earlier one) when that move
  succeeds, and an error is logged either way; the run continues with "no mark". A file that could not be moved
  is overwritten when the next mark is written.
- Any other read error is logged as a warning and treated as "no mark"; the file is not moved.
- In every "no mark" case the next history run sets the mark to the current time and processes nothing, so plays
  between the lost mark and that moment reach the `.nfo` files only through the next full reconcile.

### Run status

The application keeps one in-memory status, reset when the process starts. Its values, as the dashboard and
`/health` show them (see [Dashboard and auth](dashboard-and-auth.md) for where and to whom):

| Value | Meaning |
| ----- | ------- |
| `plexConnected` | `true` once a reconcile's library list or a history run's history request succeeded, and after a run that only set a missing mark; `false` at start and after a failed run. A failing item does not change it. |
| `isRunning` | `true` while a history run or a reconcile is in progress. |
| `lastPollAt` | End of the last history run that completed without failing, including a run that only set the mark. |
| `lastReconcileAt` | End of the last reconcile that completed without failing. |
| `errors` | Number of failed runs plus failed items and shows since the process started. |
| Next poll | When the next history run is due; set at the start of each wait. |
| High-water mark | The mark after the last run that set it. |
| Items processed | Movies and episodes handed to the NFO writer since the process started; in a history run only items Plex returned. |
| NFO files created / updated | Writes that created or changed a file since the process started. |
| Last error | The message of the most recent failure, of a run or of an item. |
| Started | When the process started. |

## Limits and errors

- **A failed run.** A run fails when a request it cannot do without fails: the owner resolution with `401`,
  `403` or a timeout, the history request (any of its pages), the library list, any page of a library's
  contents, or writing the mark. The run then stops, logs the error `Synchronization run failed`, counts one
  error, records the message as the last error and sets `plexConnected` to `false`. A failed history run leaves
  the mark unchanged, so its entries are read again by the next run. A failed reconcile does not reconcile the
  libraries it has not finished; items written before the failure stay written.
- **A failed item.** A failure while reading or writing one item (a history entry's item, a movie, an episode) or
  while listing a show's episodes or writing its aggregates is isolated: it is logged as the error
  `Processing item <rating key> failed, skipping it`, counted in `errors` and recorded as the last error, and the
  run continues with the next item or show. A per-item request that times out counts as a failed item.
- **The worker never stops on an error.** Any error that reaches the worker is logged and the next cycle runs on
  schedule. Stopping the application cancels the run in progress without counting an error; a history run
  cancelled this way does not advance the mark, so its entries are processed again after the restart.
- **Plex paging that does not advance.** When Plex returns the same first entry for two pages in a row, paging
  stops with the warning `Plex server returned a non-advancing page …` (`… non-advancing history page …` for
  history); the items of the pages read so far are processed.
- **History and `Plex:Libraries`.** The library filter applies to the reconcile only. The history run processes
  the owner's plays in every library, and it does not filter by item kind: a played item of another kind whose
  file lies under a mapped path is handed to the NFO writer like a video.
- **Clock and timestamp resolution.** The mark set on a run without a mark comes from the application host's
  clock, the viewed-at times from Plex's; a difference between the two clocks moves the point from which history
  is read. Viewed-at times have a resolution of one second, and an entry viewed exactly at the mark is not read
  again.
- **State directory not writable.** Setting the mark then fails on every history run, so each one fails and
  history reaches the files only through the reconcile.
- **Status is not persisted.** All counters and times of the run status start again from zero at every start.

## Related decisions

- [`decisions/0001-polling-instead-of-webhooks.md`](../decisions/0001-polling-instead-of-webhooks.md) — Plex is
  polled; no webhooks.
- [`decisions/0002-single-user-plex-owner.md`](../decisions/0002-single-user-plex-owner.md) — only the Plex
  owner's watch state is synchronized.
- [`decisions/0005-incremental-history-plus-full-reconcile.md`](../decisions/0005-incremental-history-plus-full-reconcile.md)
  — a cheap incremental history run plus a periodic full reconcile, seeded to "now" on the first start.
- [`decisions/0006-item-level-error-isolation.md`](../decisions/0006-item-level-error-isolation.md) — a failure
  is isolated per item and never stops the worker.
- [`decisions/0008-named-httpclient-for-singleton-plexclient.md`](../decisions/0008-named-httpclient-for-singleton-plexclient.md)
  — how the long-lived Plex client obtains its HTTP connections.
- [`decisions/0012-worker-reconcile-guard-kept-as-defense-in-depth.md`](../decisions/0012-worker-reconcile-guard-kept-as-defense-in-depth.md)
  — superseded by 0013.
- [`decisions/0013-worker-guards-both-orchestrator-calls.md`](../decisions/0013-worker-guards-both-orchestrator-calls.md)
  — the worker guards both the history run and the reconcile against any error.
- [`decisions/0018-sync-orchestrator-split-into-collaborators.md`](../decisions/0018-sync-orchestrator-split-into-collaborators.md)
  — the pipeline's split into collaborators and the single episode listing per show and run.

The rules above are implemented in `src/PlexToJellyfinSync/Worker.cs` and, under
`src/PlexToJellyfinSync.Service/`, in `SyncOrchestrator.cs`, `LibraryReconciler.cs`, `SeriesAggregateWriter.cs`,
`WatchAggregator.cs`, `PlexClient.cs`, `State/StateStore.cs` and `SyncStatusService.cs`.

## Not here

- Where an item's `.nfo` file is, what a write changes in it and how it is written: [Path mapping and
  NFO](path-mapping-and-nfo.md).
- Setting keys, environment variables, defaults, allowed ranges and startup validation, including the volume
  that holds `state.json`: [Configuration](configuration.md).
- How the status is shown, `/health` access and the live log: [Dashboard and auth](dashboard-and-auth.md).
- The components, their responsibilities and how they are wired: [`ARCHITECTURE.md`](../ARCHITECTURE.md#sync-pipeline).