# 0017: The Plex token stays optional

- **Status:** Accepted
- **Date:** 2026-10-02
- **Source:** Issue #31
- **Supersedes:** —

## Context

Issue #31 calls `Plex:BaseUrl` and `Plex:Token` "de-facto mandatory" and asks for both to be validated at startup
(see [0016](0016-options-validated-at-startup.md)). Most installations do need a token. Plex, however, can be
configured to allow unauthenticated access from listed networks ("List of IP addresses and networks that are allowed
without auth"); in that setup requests without `X-Plex-Token` succeed. The `HttpClient` registration already omits
the header when the token is empty, and a token that is missing where Plex needs one already surfaces as a `401` that
is recorded as a sync failure (`PlexConnected = false`) on every poll. The README never listed the token as
required.

## Options considered

1. **`[Required]` on `Plex:Token`** — catches the common mistake at startup, but stops working, previously
   supported installations that rely on Plex's unauthenticated-network setting.
2. **Leave `Plex:Token` unvalidated** — keeps every working setup working; a missing token in a setup that needs
   one is still reported, at the first poll, as an authentication failure.

## Decision

Option 2. `PlexOptions.Token` has no validation attribute. Startup validation rejects only settings without which
the application can never work (`Plex:BaseUrl`), not settings that some valid installations leave empty.

## Consequences

- A forgotten token is not caught at startup; it shows as a `401` sync failure on the dashboard and in the logs.
- The README describes the token as required unless Plex allows unauthenticated access from the container's network.
- Revisit if the unauthenticated-network setup is dropped as a supported configuration.