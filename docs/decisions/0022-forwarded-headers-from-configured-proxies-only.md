# 0022: Forwarded headers are trusted only from configured proxies; the login throttle is bounded

- **Status:** Proposed
- **Date:** 2026-10-23
- **Area:** Dashboard and auth
- **Source:** Issue #231
- **Supersedes:** 0007 — amends its "exponential login backoff per client IP": the client address is the
  connection's own unless a configured proxy forwards it, and the throttle keeps a bounded number of records

## Context

`DashboardStartup.CreateForwardedHeadersOptions` cleared `KnownIPNetworks` and `KnownProxies`, because no reverse
proxy address is known upfront in a single-container deployment. ASP.NET Core's `ForwardedHeadersMiddleware` checks
the sender against those lists only when at least one of them is non-empty, so `X-Forwarded-For` and
`X-Forwarded-Proto` were applied from every sender. The login throttle keys on the address the request is attributed
to after that middleware, so a client that reaches the port directly chose its own throttle key, and with
`X-Forwarded-Proto` whether its request counts as HTTPS (and so the session cookie's `Secure` flag). The throttle's
records were pruned only of idle, unlocked entries, so a client varying its key added records without limit. The
project is meant for a home network (`SECURITY.md`): the client that reaches the port directly is the usual case,
not the exception.

## Options considered

1. **Trust forwarded headers only from configured proxies, plus loopback** — the operator lists the proxy's address
   or network; without a list only loopback is trusted. Closes the bypass for every sender; a deployment behind a
   proxy on a container network has to set the list once, or its clients share the proxy's throttle key and lose
   the forwarded scheme.
2. **Trust private networks by default** (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `fc00::/7`) — keeps proxy
   setups working without configuration, but the attacker on the home network, the case the issue is about, still
   spoofs the headers.
3. **Keep trusting every sender and key the throttle on the connection's own address** — fixes the key but not the
   scheme and the cookie, and behind a proxy every client shares one key, so one wrong-guessing client locks out
   the operator.
4. **One global throttle record for all clients** — bounded and immune to key variation, but any client locks out
   every other client after six wrong guesses.
5. **Two settings mirroring the framework (`KnownProxies`, `KnownNetworks`)** — two lists for one concept; one list
   whose entries are an address or a CIDR network says the same.
6. **Bound the throttle by evicting the oldest record** — memory is bounded, but every new key still gets its five
   free attempts, so varying the key keeps guessing unlimited.
7. **Bound the throttle with one shared overflow record** — once the per-client records are full and none can be
   pruned, every further client is counted on one shared record, so key variation beyond the bound is throttled
   like a single client. Costs a legitimate new client the shared lockout while such a flood lasts.
8. **Count IPv6 clients per /64 prefix** — narrows key variation within one IPv6 allocation, but option 7 already
   caps it, and it adds a key derivation that the forwarded and the direct address would both need.

## Decision

Options 1 and 7: `Dashboard:TrustedProxies` lists the addresses and CIDR networks whose forwarded headers are
applied, in addition to the framework's loopback defaults, and the login throttle holds at most 1024 per-client
records, counting every further client on one shared record that a successful login does not clear. The rules are
in [Dashboard and auth](../areas/dashboard-and-auth.md) (*Forwarded headers*, *Login throttle*) and the setting in
[Configuration](../areas/configuration.md).

## Consequences

- An upgrade behind a reverse proxy that does not connect from loopback needs `Dashboard:TrustedProxies`;
  without it the login throttle counts all clients under the proxy's address and the cookie is issued without
  `Secure` behind a TLS-terminating proxy. The pull request description names this step.
- An operator who wants the old behavior lists `0.0.0.0/0` and `::/0`, accepting the bypass knowingly.
- While more than 1024 clients with recent failures are tracked, a new client can be locked out by others' failures.
- The forwarded-header trust becomes part of the dashboard's security area (`.squad/project.md`).