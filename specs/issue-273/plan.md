# Plan: State and enforce how the NFO parser handles DTDs and entities

Source: Issue #273
Tier: security — the change replaces the parser's implicit framework defaults with an explicit guard (DTDs prohibited, no resolver) that decides what a hostile `.nfo` file can make the parser reach or expand.
Security areas touched: `.nfo` / XML handling (`NfoWriter`)

## Problem / root cause

`NfoWriter.WriteAsync` parses an existing file with `XDocument.Parse(xml, LoadOptions.PreserveWhitespace)`
(`src/PlexToJellyfinSync.Service/NfoWriter.cs:507`) and sets no reader settings, so the DTD rule is whatever
`XNode`'s internal settings are: DTDs parsed, no external resolution, expansion capped at 10,000,000 characters.
No document states it, no test holds it. Measured on that behavior, a DTD already causes harm without any
resolver: an external entity reference becomes empty text and is lost on write-back, and internal entities are
written back expanded (a 1,000,000-character title from a few hundred bytes) — both break guarantee 0003.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `.squad/project.md` says the parser is protected against external entities and entity expansion and links the area document | `.squad/project.md`, *Security areas*, `.nfo` / XML handling | confirmed |
| 2 | The area document says nothing about DTDs or entities | `grep -i "dtd\|entit\|xxe"` over `docs/areas`, `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md`: no hit | confirmed |
| 3 | The parser is `XDocument.Parse(xml, LoadOptions.PreserveWhitespace)` without reader settings | `NfoWriter.cs:507`; `XmlException` caught at :509, warning at :511 | confirmed |
| 4 | Nothing in `src` mentions `DtdProcessing` or `XmlResolver` | `grep -rn "DtdProcessing\|XmlResolver\|XmlReader" src tests`: no hit | confirmed |
| 5 | `XDocument.Parse` uses `DtdProcessing.Parse`, `MaxCharactersFromEntities` = 10,000,000, `MaxCharactersInDocument` = 0 | scratch probe (SDK 10.0.403), reflection on `XNode.GetXmlReaderSettings(LoadOptions.PreserveWhitespace)` | confirmed |
| 6 | Today an external general entity (`file://` URL to a readable file) is not resolved; the reference parses as empty text | probe: an entity `x` declared `SYSTEM` with the `file://` URL of a scratch `secret.txt`, referenced as the content of `title` → parse succeeds, title length 0, DOCTYPE node kept | confirmed |
| 7 | Today an external parameter entity and an external DTD subset parse without error and without resolution | probe: `<!ENTITY % p SYSTEM "file://…"> %p;` and `<!DOCTYPE movie SYSTEM "file://…">` → parse succeeds | confirmed |
| 8 | Today expansion of 1,000,000 characters succeeds; 1,000,000,000 throws `XmlException` (MaxCharactersFromEntities) | probe: 10-way nesting, 5 levels → title length 1,000,000; 8 levels → exception | confirmed |
| 9 | With `DtdProcessing.Prohibit` every one of these inputs, a bare `<!DOCTYPE movie>` and an internal-only entity throw `XmlException` "For security reasons DTD is prohibited in this XML document. …" | probe, same seven inputs | confirmed |
| 10 | `XDocument.Load(XmlReader.Create(new StringReader(xml), settings), LoadOptions.PreserveWhitespace)` with `DtdProcessing.Prohibit`, `XmlResolver = null` yields the same document as `XDocument.Parse` for a DTD-free file (declaration with `standalone`, comment, whitespace, `&amp;`, `&#233;`, attributes) | probe: serialized output of both identical | confirmed |
| 11 | Jellyfin's NFO reader uses `XmlReaderSettings` without setting `DtdProcessing` (default `Prohibit`), so it does not read an NFO with a DTD either | `jellyfin/jellyfin` at 797999f, `MediaBrowser.XbmcMetadata/Parsers/BaseNfoParser.cs:1000-1007` (both parse paths use it) | confirmed |
| 12 | An existing NFO is decoded to a string before parsing (`ReadExistingAsync`), so a UTF-16/32 file reaches the same parser | `NfoWriter.cs:169-177`, :501 | confirmed |
| 13 | `NfoWriterTests` builds the writer with `NullLogger<NfoWriter>`; no logger double for `NfoWriter` exists | `NfoWriterTests.cs:666`; `.squad/project.md`, *Test doubles* | confirmed |

## Acceptance criteria

"Refused" below means: `WriteAsync` returns `NfoWriteOutcome.Skipped`, the file's bytes are unchanged, and no
`<target>.tmp` is left — the malformed-file path at `NfoWriter.cs:509-514`.

- AC1: An existing `.nfo` whose DTD declares an external entity — a general entity referenced in an element, an
  external parameter entity referenced in the DTD, or an external DTD subset (`SYSTEM`) — pointing at a readable
  file is refused; the file's content never appears in the target.
- AC2: An existing `.nfo` whose DTD declares nested internal entities (entity-expansion payload) is refused, both
  below the framework's 10,000,000-character bound (5 levels of 10, 1,000,000 characters) and above it (8 levels).
- AC3: An existing `.nfo` with a document type declaration in any other form is refused: a bare
  `<!DOCTYPE movie>`, an internal subset with one internal entity referenced in `title`, a `PUBLIC` identifier, a
  DOCTYPE after a comment in the prolog, and a DOCTYPE in a UTF-16 LE file with byte-order mark.
- AC4: An existing `.nfo` without a DTD that uses predefined entities and character references (`&amp;`, `&lt;`,
  `&#233;`) is still updated (`Updated`), and its `title` keeps the same text.

## Tests per criterion

All in `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs`, built with `CreateWriter(createMissing: false)`; the
payloads are literal XML in the test (no escapes, no control characters). AC1 writes the target file of its
entity (a known marker string) into `_tempDirectory` and builds its `file://` URL with `new Uri(path).AbsoluteUri`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `NfoWriterExistingNfoWithExternalEntitySkips` — `[DataRow]` per form: general entity, parameter entity, external subset; asserts outcome, unchanged content, no `.tmp`, marker absent | `NfoWriterTests.cs` |
| AC2 | `NfoWriterExistingNfoWithEntityExpansionSkips` — `[DataRow(5)]`, `[DataRow(8)]` nesting levels | `NfoWriterTests.cs` |
| AC3 | `NfoWriterExistingNfoWithDocumentTypeSkips` — `[DataRow]` per form; the UTF-16 row writes with `CreateEncodingWithBom(1200, true)` and compares bytes | `NfoWriterTests.cs` |
| AC4 | `NfoWriterExistingNfoWithPredefinedEntitiesUpdates` — passes on today's code: deferred to step 6 (`.squad/routing.md`, step 5) | `NfoWriterTests.cs` |

Rows that fail on today's code: AC1 all three, AC2 level 5, AC3 every row (each parses and is updated today).
AC2 level 8 passes today and stays as the bound's pin.

## Approach

1. Add `ParseExisting` to `NfoWriter` (*Signatures*): a new `XmlReaderSettings` per call with
   `DtdProcessing = DtdProcessing.Prohibit` and `XmlResolver = null`, everything else at its default;
   `XmlReader.Create(new StringReader(xml), settings)`; return
   `XDocument.Load(reader, LoadOptions.PreserveWhitespace)`. Fact 10 shows the result equals today's for files
   without a DTD.
2. Replace `XDocument.Parse(xml, LoadOptions.PreserveWhitespace)` at `NfoWriter.cs:507` with `ParseExisting(xml)`.
   The existing `catch (XmlException)` and its warning stay unchanged; a refused DTD takes that path.

No new user-visible output: the existing warning text is unchanged and carries the parser's message (fact 9).
`NfoWriter.BuildDocument` (new files) parses nothing and is untouched.

## Options considered

Refuse every DTD (chosen); parse the DTD with no resolver and an explicit bound; ignore the DTD; a dedicated
warning for a refused DTD. Why each other one lost is in
[0031](../../docs/decisions/0031-nfo-with-a-dtd-is-refused.md) (the issue names the first two).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `NfoWriter.cs` | new private `ParseExisting`, call site at :507 |
| `PlexToJellyfinSync.Tests` | `NfoWriterTests.cs` | four new tests |

## Signatures (for the Dev's skeleton)

No public API changes, so no skeleton (step 4). New private member, in `#region Static methods`:

```csharp
/// <summary>
/// Parse an existing NFO file's content with a reader that prohibits document type declarations and resolves nothing
/// </summary>
private static XDocument ParseExisting(string xml)
```

## Test files

`tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` (existing).

Existing test call sites of a changed signature: none. Existing tests kept unchanged, with no input a criterion
handles differently: `NfoWriterMalformedExistingNfoSkips`, `NfoWriterExistingNfoPreservesOtherNodes`,
`NfoWriterExistingNfoPreservesOriginalEncoding`, the `lastplayed` removal tests — they are also the regression
check that the new reader keeps whitespace, declaration and encoding.

Files no unit test can cover: none.

## Areas

Path mapping and NFO — the DTD and entity rule under *Limits and errors*, and the scope line.

## Documentation updates

All for the Dev, written once the code is green:

- `docs/areas/path-mapping-and-nfo.md`: under *Limits and errors*, after the malformed-file bullet, a bullet
  stating the rule — an existing file with a document type declaration in any form is treated like a malformed
  file (skipped, untouched, same warning, which carries the parser's message about the prohibited DTD); external
  entities are never resolved and no entity is expanded; predefined entities and character references need no
  DTD and are read normally. *Scope*: add "document type declarations and entities" after "malformed files".
  *Related decisions*: add 0031. *Not here* unchanged.
- `docs/areas/README.md`: the *Path mapping and NFO* row's scope adds "DTDs and entities" after "malformed files".
- `docs/ARCHITECTURE.md`, item 6 (`NfoWriter`): "through `XDocument` (`LoadOptions.PreserveWhitespace`)" becomes
  a reader that prohibits DTDs and resolves nothing, and the closing link reads "malformed files and DTDs under
  *Limits and errors*"; link [0031](../../docs/decisions/0031-nfo-with-a-dtd-is-refused.md).
- `SECURITY.md`, *Scope* (in-scope list): add "XML external entity or entity-expansion attacks through existing
  `.nfo` files".
- `.squad/project.md`: none — its `.nfo` / XML handling line becomes true as written. `README.md`: none (its
  promise on existing files still holds; a refused file is not changed). No changelog file exists; the PR
  template's documentation and record items are covered above.

## Architecture check

Guarantee 0003 (existing files change only in their watch fields) is strengthened: a DTD file is no longer
rewritten with lost or expanded entity content. Per-item error isolation (0006) is unaffected — the refusal is
the existing caught `XmlException`. No flow or component changes.

## Security considerations

- [x] No limits: a DTD is refused before any expansion, so no bound on parsed input is added; the existing
  unbounded read of the file (`ReadExistingAsync`) is unchanged (*Out of scope*).
- [x] Exceptions reaching user-visible text: the `XmlException` of a refused DTD reaches only the existing
  warning log line as its attached exception (dashboard log), message "For security reasons DTD is prohibited in
  this XML document. …"; it names no file content. No other new exception.

Guard forms (the parser's accepted DTD forms, from the XML 1.0 prolog grammar, and the guard's behavior):
internal subset with general or parameter entities, external subset via `SYSTEM` or `PUBLIC`, both combined,
DOCTYPE after the XML declaration, comments, PIs or whitespace — the reader throws at the `<!DOCTYPE` token in
every case (fact 9; AC1–AC3 rows). A DOCTYPE after the root element is not well-formed and already malformed.
An entity reference without a DTD other than the five predefined ones is an undeclared entity, already
`XmlException`. Decoding happens before parsing (fact 12), so an encoding form cannot hide the declaration
(AC3 UTF-16 row).

## Decision records

- `docs/decisions/0031-nfo-with-a-dtd-is-refused.md` (Proposed)

## Out of scope / follow-ups

- Proposed follow-up issue — title "Bound the size of an existing .nfo file before it is read"; body:
  "`NfoWriter.ReadExistingAsync` reads an existing `.nfo` whole into memory and `XmlReaderSettings`
  `MaxCharactersInDocument` is 0, so a very large file on the media volume is read and parsed in full on every
  write of its item. Decide a size bound, state it in *Path mapping and NFO*, *Limits and errors*, and test it."
- Asserting the warning line itself needs a logger double for `NfoWriter` (fact 13); the refusal reuses the
  existing, unchanged warning path, so no double is added here.
