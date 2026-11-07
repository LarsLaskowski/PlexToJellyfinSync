# 0031: An existing NFO file with a document type declaration is refused, not parsed

- **Status:** Proposed
- **Date:** 2026-11-07
- **Area:** Path mapping and NFO
- **Source:** Issue #273
- **Supersedes:** —

## Context

`NfoWriter` parses every existing `.nfo` file on the media volume before it updates the watch fields. Those files
do not all come from the operator: media downloaded from elsewhere often ships with its own `.nfo`, and other
tools write them too, so the parser reads untrusted XML. Until this decision the code called
`XDocument.Parse` without reader settings and relied on the framework's defaults, which no document stated and no
test held: a document type declaration (DTD) is parsed, external entities are not resolved, and entity expansion
is capped at 10,000,000 characters (measured on .NET 10, see the plan of issue #273).

Measured on that behavior, a DTD did harm even without a resolver: a reference to an external entity became
empty text and was lost when the file was written back, and internal entities were written back expanded, so a
small file could grow by up to ten million characters on its next update. Both break the guarantee of
[0003](0003-nfo-only-watch-fields-touched.md) that an existing file changes only in its watch fields.

Jellyfin's own NFO reader uses the framework's default reader settings, which prohibit DTDs, so a file with a
DTD is not read by Jellyfin either.

## Options considered

1. **Refuse every DTD** (`DtdProcessing.Prohibit`, no resolver): a file with a document type declaration in any
   form is handled like a malformed file — skipped and left untouched. Nothing of a DTD is ever processed, so
   neither external entities nor expansion can occur; the cost is that such a file receives no watch state, and
   Jellyfin does not read it anyway.
2. **Parse the DTD with no resolver and an explicit expansion bound** (today's defaults, made explicit): keeps
   updating files that carry a DTD, but keeps the content loss and the growth described above, and needs a
   bound whose value is a further decision.
3. **Ignore the DTD** (`DtdProcessing.Ignore`): drops the declaration on write-back, which loses content, and an
   entity reference it declared then fails as undeclared — the file is skipped anyway, after a silent loss risk.
4. **A dedicated warning for a refused DTD** instead of the malformed-file warning: the parser reports a
   prohibited DTD only through the text of an `XmlException`, so telling it apart needs either that message or a
   pre-scan of the content; the malformed-file warning already carries the parser's message, which names the DTD.

## Decision

Option 1: an existing `.nfo` file is parsed by a reader that prohibits DTDs and has no resolver, and a file the
reader refuses for its DTD takes the malformed-file path. The rule is stated in
[Path mapping and NFO](../areas/path-mapping-and-nfo.md#limits-and-errors).

## Consequences

- External entities and entity expansion cannot reach the parser, whatever the framework's defaults become.
- A file that carries a DTD is never changed by the application and never gets watch state; the operator sees
  the malformed-file warning with the parser's message for it on every write of that item.
- Predefined entities (`&amp;`, `&lt;`, …) and character references need no DTD and are read as before.
- Revisiting this — accepting DTDs again — needs a new bound and a way to keep entity references on write-back,
  and with them a change to 0003's guarantee.
