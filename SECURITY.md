# Security Policy

## Supported Versions

Only the latest release of PlexToJellyfin Sync receives security fixes.

| Version | Supported |
| ------- | --------- |
| latest  | Yes       |
| older   | No        |

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Report vulnerabilities by e-mail to:

**Development@e-networld.de**

Include in your report:

- A clear description of the vulnerability
- Steps to reproduce
- Potential impact
- Any suggested fix (optional)

You will receive an acknowledgement within **5 business days**. We aim to release a fix or mitigation within **30 days** for confirmed vulnerabilities. We will keep you informed of progress throughout the process.

## Deployment Security Considerations

PlexToJellyfin Sync is designed for **home-network use or deployment behind a trusted reverse proxy**. The optional `TokenAuthMiddleware` provides basic token-based protection for the dashboard, but it is not a substitute for network-level controls.

Before exposing the application to any network:

- Enable `TokenAuthMiddleware` and use a strong, randomly generated token.
- Run the container behind a reverse proxy (Nginx, Caddy, Traefik) with authentication enabled.
- Restrict access at the network level (firewall, VLAN) to trusted clients only.
- Store the Plex token and the Jellyfin API key exclusively in environment variables or secrets — never hard-code them.
- Mount the Jellyfin media directory with the **minimum required permissions** (read/write only for the configured media path; no unnecessary host access).
- Do not expose the dashboard port directly to the internet.

## Scope

The following are considered in scope for vulnerability reports:

- Server-side request handling (Blazor Server hub, ASP.NET middleware)
- Path traversal or `.nfo` file writes outside the configured media path
- Leakage of the Plex token or Jellyfin API key through logs, API responses, or error messages
- Dependency vulnerabilities in NuGet packages consumed by the project

The following are **out of scope**:

- Attacks that require local system access or physical access to the host
- Issues arising from misconfiguration of the deployment environment (e.g. world-readable secrets)
- Denial-of-service through resource exhaustion on the local network
- Missing dashboard authentication when `TokenAuthMiddleware` is deliberately disabled (this is a design decision; use a reverse proxy or restrict network access)
