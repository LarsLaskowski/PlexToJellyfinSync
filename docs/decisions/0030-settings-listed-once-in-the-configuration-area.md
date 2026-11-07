# 0030: The settings are listed once, in the Configuration area

- **Status:** Accepted
- **Date:** 2026-11-07
- **Area:** Configuration
- **Source:** Squad-maintenance pull request after the refresh to template v2026.11.07
- **Supersedes:** [0021](0021-area-documents-and-area-assignment.md) — amends only its consequence that the settings are stated in the Configuration area and in a `README.md` table that change together; the rest of 0021 stands

## Context

Every setting was listed twice: normatively in the settings table of the Configuration area and, for users, in a
table in `README.md` with key, environment variable, default and range. Every option change had to update both,
and the two already differed in wording (the README called the Plex token "required unless …", the area
"optional"). The template's rules give every behavior one home and let other documents link it.

## Options considered

1. **Keep both tables** — users find the settings on the repository's front page; every option change edits two
   tables, and a missed one leaves a wrong default or range in front of users.
2. **README links the Configuration area** — one table to keep correct; the README keeps the quick start and the
   path-mapping walkthrough, and a user follows one link to the full list.

## Decision

Option 2. The settings are listed only in the [Configuration](../areas/configuration.md#settings) area;
`README.md` shows a setup by example and links there.

## Consequences

- An option change updates one table (`.squad/project.md`, *Integration surface*); `README.md` changes only when
  its example uses the key.
- A user reads the list of settings one click away from the front page.
- Bringing back a README table would revisit this decision.