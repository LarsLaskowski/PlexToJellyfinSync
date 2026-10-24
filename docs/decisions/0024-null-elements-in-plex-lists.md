# 0024: Plex list elements are declared nullable; a null element is handled like an empty one

- **Status:** Accepted
- **Date:** 2026-10-24
- **Area:** Sync pipeline
- **Source:** Issue #246
- **Supersedes:** —

## Context

System.Text.Json deserializes a JSON `null` inside an array into a `null` element of the target `List<T>`; the
nullable annotation `List<PlexAccount>` does not stop it. The Plex DTOs in `PlexToJellyfinSync.Data.Plex` declared
their list elements non-nullable, so `PlexClient` dereferenced every element without a check, and a Plex answer such
as `{"MediaContainer":{"Account":[null]}}` threw a `NullReferenceException` that no handler in `PlexClient` expects:
the run failed with the raw runtime message instead of the documented owner fallback, and the same held for the
library list, history pages, library and episode pages and an item's genres and external ids. Plex is an untrusted
source of JSON (`.squad/project.md`, *HTTP calls to Plex*).

## Options considered

1. **Declare every list element of the Plex DTOs nullable and handle `null` at each projection in `PlexClient`**
   (chosen) — the annotations say what the deserializer really produces, so the compiler flags every dereference
   today and every new projection or DTO list later; the handling stays visible in the mapping code. Costs a
   public type change in `PlexToJellyfinSync.Data`, which only `PlexClient` uses.
2. **Null checks in `PlexClient` only, DTOs unchanged** — the smallest diff, but the annotations keep claiming
   non-null elements, the compiler finds no future site, and a check against a non-nullable element reads as
   redundant to analyzers and reviewers.
3. **Remove `null` elements centrally while deserializing** (a contract modifier in `PlexJsonOptions`, or
   `IJsonOnDeserialized` on each DTO) — one mechanism for all lists, but hidden from the mapping code, more code
   than the checks it replaces, and it changes the shared deserialization behavior of a security-area type.
4. **Catch `NullReferenceException` in `GetOwnerAccountIdAsync`** — fixes the one site the issue names, masks real
   programming errors and leaves every other site throwing.

## Decision

Every `List<T>` element type of the Plex DTOs is nullable (`List<PlexAccount?>?`), and `PlexClient` handles a `null`
element exactly like an element of that list without any field. The resulting behavior is in
[Sync pipeline](../areas/sync-pipeline.md#what-is-read-from-plex).

## Consequences

- A new list in a Plex DTO is declared with a nullable element type, so its consumer has to decide what a `null`
  element means; the rule above is the default.
- A `null` element still counts as an entry of its page for the paging rules (page length, first entry of a page),
  like an entry without rating key and viewed-at time. Two consecutive pages that both start with such an entry
  therefore count as a page that does not advance, for library contents and a show's episodes as for history
  (issue #248), so two genuinely different pages that both start with one end paging early. Comparing the rating
  keys of the whole page would tell them apart, but costs a key list per page, still sees two pages without any
  keyed entry as equal, and would split the rule from history paging; it was not taken.
- No real Plex response with a `null` element is known; the tests use hand-written answers for this variant.
