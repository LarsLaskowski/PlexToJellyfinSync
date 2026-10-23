# Dashboard and auth

## Scope

The web surface of the application: its routes, the switch that turns the dashboard off, the optional token
login with its sessions, cookie and login throttle, the security headers, the trust in forwarded headers, and
the live log buffer with its secret masking. The operator relies on it to watch the sync and to decide how far
the dashboard may be exposed; monitoring tools rely on `/health`.

## Behavior

### Routes

| Route | Method | Purpose | Mapped when |
| ----- | ------ | ------- | ----------- |
| `/health` | GET | Sync status as JSON | always |
| `/` | GET | Status page | `Dashboard:Enabled` is `true` |
| `/logs` | GET | Live log page | `Dashboard:Enabled` is `true` |
| `/login` | GET | Login form | `Dashboard:Enabled` is `true` |
| `/login` | POST | Login submission | `Dashboard:Enabled` is `true` |
| `/logout` | POST | Ends the session | `Dashboard:Enabled` is `true` |
| `/not-found`, `/Error` | GET | Not-found and error pages | `Dashboard:Enabled` is `true` |

- `/health` answers with a JSON object of the fields `status` (always `"ok"`), `plexConnected`, `isRunning`,
  `lastPollAt`, `lastReconcileAt` and `errors`. It never requires authentication, also when a dashboard token
  is set. What the values mean is stated in [Sync pipeline](sync-pipeline.md).
- With `Dashboard:Enabled` set to `false` only `/health` is mapped: no page, no login or logout endpoint, no
  token check and no antiforgery handling exist, and every other path is answered as not found.
- The status page shows the current status snapshot and is updated whenever the status changes; it does not
  poll. The log page shows the buffered entries newest first with time, level, category and message, and can
  filter by minimum level and by text and pause its live updates.
- Routes and the status page's content are implemented in `src/PlexToJellyfinSync/Program.cs` and
  `src/PlexToJellyfinSync/Components/Pages/`.

### Security headers

Every response, `/health` included and also with the dashboard switched off, carries:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- `Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self' wss: ws:; img-src 'self' data:`

`'unsafe-inline'` and `wss:` / `ws:` are what the interactive pages need for their bootstrap script and their
WebSocket connection; every other source is limited to the application's own origin.

### Without a dashboard token

- While `Dashboard:Token` is empty or whitespace, every route is reachable without authentication, the log
  page included, and no logout control is shown.
- When the dashboard is enabled without a token, a warning is logged at startup saying that the dashboard is
  reachable by anyone who can reach the host.

### With a dashboard token

- A request needs a valid session unless its path is public. Public are:
  - `/health` and `/login`, compared exactly and case-insensitively;
  - every path that begins with `/_framework/`, `/_content/` or `/_blazor` (the last one without a trailing
    slash), compared case-sensitively;
  - every path whose last segment ends in one of the extensions `.css`, `.js`, `.map`, `.png`, `.jpg`,
    `.jpeg`, `.svg`, `.ico`, `.woff`, `.woff2`, compared case-insensitively.
- A request without a valid session to any other path is redirected to `/login`.
- The login form posts the token to `/login`. The submitted token is compared with `Dashboard:Token` in
  constant time (both are hashed to a fixed length first, so neither content nor length shows in the timing).
- Login and logout are accepted only with a valid antiforgery token; without one the request is answered with
  `400` and the text `Invalid or missing antiforgery token.`, and no login attempt is counted.
- A correct token starts a session and redirects to `/`. A wrong token redirects to `/login?error=1`, where the
  form shows `Invalid access token.`.
- Implemented in `src/PlexToJellyfinSync/Security/` and `src/PlexToJellyfinSync.Service/Security/`.

### Sessions and cookie

- A session identifier is 32 random bytes (256 bits), Base64-encoded, and is valid only while the server holds
  it: sessions live in process memory, so a restart ends every session.
- A session lasts 8 hours from the login, absolute: activity does not extend it.
- The session cookie is `pjf_auth` with `HttpOnly`, `SameSite=Strict` and a maximum age of 8 hours. It carries
  `Secure` only when the login request counts as HTTPS (see *Forwarded headers*), so the cookie also works in a
  plain-HTTP setup.
- Logout removes the session on the server, deletes the cookie and redirects to `/login`; the identifier is
  useless afterwards even if it was copied.

### Login throttle

- Failed logins are counted per client address, the address the request is attributed to after forwarded
  headers are applied (see *Forwarded headers*). A request without any address counts under one shared key.
- The first 5 failures are free. Each further failure locks the client out, for 1 second after the 6th failure,
  doubling with every failure after that, capped at 5 minutes.
- While a client is locked out, every login attempt is answered with `429` and a `Retry-After` header holding
  the remaining lockout in whole seconds (rounded up). The token is not checked, and the attempt is neither
  counted as a failure nor extends the lockout.
- A failure more than 15 minutes after the client's previous one starts the count again from zero. A successful
  login clears the client's record.
- The records live in process memory and are lost on restart. Once more than 1024 client keys are tracked, each
  recorded failure removes the records that have been idle for more than 15 minutes and are not locked. Records
  that are recent or locked are kept, so the number of tracked clients, and the memory they use, has no upper
  bound.

### Forwarded headers

- `X-Forwarded-For` and `X-Forwarded-Proto` are honoured from any sender: no known proxy or proxy network is
  configured. Of a list of addresses in `X-Forwarded-For` the rightmost entry is used (one hop).
- When present, `X-Forwarded-For` decides the client address the login throttle counts by, and
  `X-Forwarded-Proto` decides whether a request counts as HTTPS and with it whether the session cookie carries
  `Secure`. Without them the connection's own address and scheme are used.
- Implemented in `src/PlexToJellyfinSync/Security/DashboardStartup.cs`.

### Live log buffer and secret masking

- The log page reads a ring buffer in process memory that holds the last `Dashboard:LogBufferSize` entries; the
  oldest entry is dropped when a new one arrives at a full buffer. The buffer is empty after a restart.
- Only entries of level Information and above enter the buffer, as far as the host's log-level configuration
  lets them through.
- Before an entry is stored, every literal, case-sensitive occurrence of the configured `Plex:Token` and
  `Dashboard:Token` in its message and exception text is replaced by `***REDACTED***`. The longer token is
  replaced first, so one token that contains the other is not left partly visible.
- Everything else is stored and shown verbatim; a secret that is not one of the two configured tokens, or a
  token in another form (encoded, split, other case), is not masked.
- Implemented in `src/PlexToJellyfinSync.Service/Logging/`.

## Limits and errors

- **Forwarded headers from any sender.** Because `X-Forwarded-For` and `X-Forwarded-Proto` are accepted from
  any sender, a client that reaches the application's port directly chooses the address the throttle counts it
  under, so the throttle does not limit it; each address it uses also adds a throttle record, which is not
  removed while it is recent (see *Login throttle*: no bound). Such a client likewise decides through
  `X-Forwarded-Proto` whether its request counts as HTTPS and with it the cookie's `Secure` flag; behind a
  TLS-terminating proxy that does not send the header, the cookie is issued without `Secure`.
  Mitigation for the operator:
  - put a reverse proxy in front that sets or appends `X-Forwarded-For` (the rightmost entry is used, one hop)
    and sets `X-Forwarded-Proto`;
  - keep the application's port off untrusted networks, so that only the proxy reaches it;
  - set a long, random `Dashboard:Token`.
- **Masking reach.** Masking covers only the dashboard's in-memory log buffer. Console and container output is
  not masked, and neither is the last error shown on the status page, which is the failing operation's message
  as reported.
- **Without a token** the status page, the log page and `/health` are open to everyone who reaches the port;
  only network controls protect them.
- **Login failures** are reported only as `Invalid access token.` (wrong token), `400` (antiforgery) or `429`
  with `Retry-After` (locked out); no response reveals the configured token or how close a guess was.
- **Restart** ends all sessions, clears the throttle records and empties the log buffer.
- **Unhandled errors** outside the development environment show the generic error page while the dashboard is
  enabled, with a request id when one exists and without exception details.

## Related decisions

- [`decisions/0007-dashboard-auth-model.md`](../decisions/0007-dashboard-auth-model.md) — the dashboard is open
  by default and optionally protected by a shared token with server-side sessions.

## Not here

- Validation of the dashboard settings at startup, their defaults and environment variables, and the container
  port: [Configuration](configuration.md).
- What the status values (`plexConnected`, `isRunning`, last poll and reconcile, errors) mean and when they
  change: [Sync pipeline](sync-pipeline.md).
- How to report a vulnerability: [`SECURITY.md`](../../SECURITY.md).