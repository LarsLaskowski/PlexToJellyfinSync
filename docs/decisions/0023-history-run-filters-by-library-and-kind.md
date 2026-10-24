# 0023: The history run filters by library on the history entry and by kind on the item

- **Status:** Proposed
- **Date:** 2026-10-24
- **Area:** Sync pipeline
- **Source:** Issue #233
- **Supersedes:** —

## Context

`Plex:Libraries` restricted only the full reconcile; the incremental history run (0005) read the owner's plays
in every library and handed every item Plex returned to the NFO writer. An item that is neither a movie nor an
episode (Plex maps its type to an unknown kind) was written as `<file>.nfo` with a `movie` root, because the
NFO writer falls back to that for an unknown kind. An excluded library's episode also made its show's season
and series aggregates be written.

The real Plex samples attached to the issue show that a history entry carries `librarySectionID` as a JSON
string (`"7"`) and that `/library/metadata/{key}` carries it as a JSON number (`7`) on each item, so the shared
metadata DTO must read both forms.

## Options considered

1. **Filter the library on the history entry, the kind on the fetched item (chosen)** — the history entry
   already names its library, so an entry of an excluded library costs no item request and never reaches the
   writer; the kind is checked on the item that is actually written, with the same mapping the NFO writer uses.
2. **Send `librarySectionID` in the history request** — Plex's history endpoint takes one section per
   request, so a filter of several libraries needs one paged history query per library and a merge of their
   order; the server-side behavior is not verified, and a check on the client would still be needed for
   entries without a section.
3. **Filter the library on the fetched item** — needs a new field on the domain item and still makes one
   request per excluded play; the entry's own field, shown by the sample, makes it unnecessary.
4. **Filter the kind on the history entry's `type`** — would save one request per non-video play, but
   duplicates the kind mapping and checks a value other than the one that decides the NFO root; the Product
   Manager called non-video plays irrelevant for this server, so the saving does not pay for the second check.
5. **Guard the kind in the item writer or make the NFO writer reject an unknown kind** — would also cover the
   reconcile, which only ever lists movies, shows and episodes, but would leave the history run counting the
   skipped item as processed.

## Decision

The history run skips an entry whose library is not listed in a non-empty `Plex:Libraries` before reading its
item, an entry without a library included; it then skips an item whose kind is neither movie nor episode before
counting or writing it. Both skips still advance the high-water mark. The behavior is described in the
[Sync pipeline](../areas/sync-pipeline.md) area document.

## Consequences

- An entry without `librarySectionID` is skipped while `Plex:Libraries` is set (it cannot be shown to belong
  to a listed library); with an empty filter it is processed as before.
- A non-numeric `librarySectionID` fails the deserialization of whatever Plex response carries it, as a
  non-numeric `accountID` already does: a history page fails the run (the mark stays, the reconcile remains the
  catch-up path), a library page fails the reconcile of that library, a metadata response is a failed item.
- NFO files that earlier versions wrote for non-video items are left untouched; no cleanup is added.
- A non-video play still costs one item request; revisiting option 4 would remove it.
