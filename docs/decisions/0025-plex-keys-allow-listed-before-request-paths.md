# 0025: Plex keys are checked against an allow-list before they reach a request path

- **Status:** Proposed
- **Date:** 2026-10-24
- **Area:** Sync pipeline
- **Source:** Issue #234
- **Supersedes:** —

## Context

`PlexClient` builds three request paths from keys that Plex itself reported: `/library/metadata/{key}`,
`/library/metadata/{key}/allLeaves` and `/library/sections/{key}/all`. A history entry's rating key and a
library's key were only checked for being non-blank, and the rule applied to item and show rating keys (at most
64 characters, no whitespace or control character) still let through every character that changes the structure
of a URL: `/` and `\` (which the URL parser treats as `/`) add path segments, `.` and `..` segments are removed
together with the segment before them, `?` starts a query that can override the paging parameters, `#` cuts off
the rest of the path and the paging bounds, `%` starts a percent-encoding. Unvalidated history rating keys also
reached log lines in the history run, control characters included.

The request paths start with `/library/`, so a key cannot change the scheme, host or port: every request still goes
to `Plex:BaseUrl` with the configured token. The risk is a hostile or broken Plex server, or an attacker between
the application and an `http://` server, steering the application's requests to other paths of that server and
around the paging bounds. Plex is an untrusted source of JSON (`.squad/project.md`, *HTTP calls to Plex*). All
keys seen in real Plex answers (the samples under `tests/PlexToJellyfinSync.Tests/TestData/Plex/`, decision 0023)
are decimal numbers.

## Options considered

1. **One allow-list rule for every key, applied where the key enters from Plex and again where it is put into a
   request path (chosen)** — 1 to 64 characters, each an ASCII letter, ASCII digit, `-` or `_`: characters that
   are unreserved in a URL and never form a dot segment, so the guard does not depend on how the URL parser treats
   any other character. History entries and libraries that fail it are dropped with a warning that carries no part
   of the key; the check in front of the request path fails closed for any future caller.
2. **Percent-encode the key in the request path** — keeps every key Plex might send, but `.` and `..` are not
   encoded and still remove path segments, the behavior then rests on the URL parser's handling of encoded
   characters, and hostile values still reach the log lines.
3. **Apply the existing rule (length, whitespace, control characters) to history entries and libraries** — what
   the issue names literally, but it lets `/`, `?`, `#` and `..` through, so the request path can still be
   changed.
4. **Digits only** — matches every key seen so far and is no safer than option 1 for the URL; it would skip
   the items of a server or agent that uses letters in its keys, for no gain.

## Decision

Every Plex key that goes into a request path — an item's, a show's, a history entry's rating key and a library's
key — must consist of 1 to 64 ASCII letters, ASCII digits, `-` or `_`; a value that fails this is dropped where it
is read from Plex and rejected before any request is built from it. The rule and the warnings are stated in
[Sync pipeline](../areas/sync-pipeline.md#what-is-read-from-plex).

## Consequences

- An item, show reference, history entry or library whose key uses any other character is no longer synchronized.
  No such key is known from real Plex answers; a server that sends one shows the warnings in the dashboard log, and
  widening the allow-list then means checking each added character against the URL parser.
- A history entry dropped for its key does not advance the high-water mark; while it is the newest entry, every
  history run reads it again and repeats its warning.
- A key value never appears in these warnings or in the exception of the request-path check, so a hostile key
  cannot inject text into the log.
