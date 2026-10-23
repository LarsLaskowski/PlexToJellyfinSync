# Configuration

## Scope

What an operator sets and runs: where settings come from, every setting with its default and allowed values,
the check of the settings at startup, and the container contract — image, port, volumes and user. Operators rely
on it to install and configure the application; the other areas rely on it for the settings their behavior reads.
The [configuration table in `README.md`](../../README.md#configuration) is the users' setup reference and agrees
with this document.

## Behavior

### Sources

- Settings are read from `appsettings.json` next to the application (`/app/appsettings.json` in the image) and
  from environment variables.
- An environment variable carries the prefix `PLEXSYNC__` and uses a double underscore `__` for every level of
  nesting: `Plex:BaseUrl` is `PLEXSYNC__Plex__BaseUrl`. List entries take their zero-based index as a level:
  `PathMappings:0:Plex` is `PLEXSYNC__PathMappings__0__Plex`, `Plex:Libraries:0` is `PLEXSYNC__Plex__Libraries__0`.
- A `PLEXSYNC__` variable overrides the same setting from every other source. A list entry set this way replaces
  the entry of the same index and leaves the other entries of the list in place.
- Implemented in `src/PlexToJellyfinSync/Program.cs` (sources) and
  `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs` (binding and validation).

### Settings

Defaults are those of the shipped `appsettings.json`; they equal the built-in ones except for `PathMappings`, which
is empty without that file. "Not validated" means the startup check does not look at the setting.

| Key | Environment variable | Default | Allowed values |
| --- | -------------------- | ------- | -------------- |
| `Plex:BaseUrl` | `PLEXSYNC__Plex__BaseUrl` | empty | Required: an absolute `http` or `https` URL with a host, for example `http://plex:32400` |
| `Plex:Token` | `PLEXSYNC__Plex__Token` | empty | Any text, optional; sent to Plex as `X-Plex-Token` when not empty; not validated |
| `Plex:OwnerAccountId` | `PLEXSYNC__Plex__OwnerAccountId` | not set (resolved from Plex) | Not set, or a whole number of at least 1 |
| `Plex:Libraries` | `PLEXSYNC__Plex__Libraries__N` | empty list (all libraries) | Plex library section keys, compared exactly; not validated |
| `Sync:PollIntervalSeconds` | `PLEXSYNC__Sync__PollIntervalSeconds` | `60` | 5 to 86400 |
| `Sync:FullReconcileIntervalHours` | `PLEXSYNC__Sync__FullReconcileIntervalHours` | `24` | 1 to 8760 |
| `Sync:CreateMissingNfo` | `PLEXSYNC__Sync__CreateMissingNfo` | `true` | `true` or `false` |
| `Sync:WriteSeriesSeasonAggregates` | `PLEXSYNC__Sync__WriteSeriesSeasonAggregates` | `true` | `true` or `false` |
| `Sync:EpisodeReconcileParallelism` | `PLEXSYNC__Sync__EpisodeReconcileParallelism` | `4` | At least 1 |
| `Sync:LibraryReconcileParallelism` | `PLEXSYNC__Sync__LibraryReconcileParallelism` | `2` | At least 1 |
| `PathMappings:N:Plex` / `PathMappings:N:Local` | `PLEXSYNC__PathMappings__N__Plex` / `PLEXSYNC__PathMappings__N__Local` | one example entry, `/data/movies` → `/local/movies` | Pairs of path prefixes; an entry with an empty `Plex` prefix is ignored; not validated |
| `Nfo:DateTimeFormat` | `PLEXSYNC__Nfo__DateTimeFormat` | `yyyy-MM-dd HH:mm:ss` | A custom date and time format pattern for `lastplayed`; not validated |
| `Nfo:MovieFilenameStrategy` | `PLEXSYNC__Nfo__MovieFilenameStrategy` | `PreferExistingMovieNfo` | `PreferExistingMovieNfo`, `VideoFileName` or `MovieNfo`; not validated beyond being one of these |
| `State:Directory` | `PLEXSYNC__State__Directory` | `/config` | Required: not empty and not only white space; the directory that holds `state.json` |
| `Dashboard:Enabled` | `PLEXSYNC__Dashboard__Enabled` | `true` | `true` or `false` |
| `Dashboard:Token` | `PLEXSYNC__Dashboard__Token` | empty | Any text, optional; empty leaves the dashboard open |
| `Dashboard:LogBufferSize` | `PLEXSYNC__Dashboard__LogBufferSize` | `1000` | At least 1 |

What each setting controls is stated in the area that owns the behavior: the Plex settings, the `Sync` settings
and `State:Directory` in [Sync pipeline](sync-pipeline.md); `PathMappings`, the `Nfo` settings and the effect of
`Sync:CreateMissingNfo` on new files in [Path mapping and NFO](path-mapping-and-nfo.md); the `Dashboard` settings
in [Dashboard and auth](dashboard-and-auth.md).

The shipped `appsettings.json` also holds the web host's standard settings `Logging:LogLevel` (`Default`
`Information`, `Microsoft.AspNetCore` `Warning`) and `AllowedHosts` (`*`); they are not part of this table.

### Startup check

- The settings of the `Plex`, `Sync`, `State` and `Dashboard` sections are checked against the table when the
  application starts. A violation stops the application before the sync worker and the web host start; nothing
  is synchronized and neither the dashboard nor `/health` answers. Under a container restart policy the container
  restarts and stops again until the setting is fixed.
- The error message of a violated rule names the setting and its environment variable, never the configured value,
  and is written to the console output (`docker logs`).
- The check reads only the configuration: it makes no network request, so an unreachable Plex server never stops
  the start. A wrong but well-formed `Plex:BaseUrl` or a missing `Plex:Token` shows up only as a failed sync cycle
  ([Sync pipeline](sync-pipeline.md)).
- `Plex:Token` stays optional: Plex can allow requests without a token from listed networks, so an empty token is
  not rejected.
- `PathMappings` and the `Nfo` settings are not part of the check. With no matching mapping every item is skipped
  ([Path mapping and NFO](path-mapping-and-nfo.md)); the application still starts.

### Container

- **Image:** `networlddev/plextojellyfinsync` on Docker Hub, the only registry the project publishes to. Each
  release is tagged with its version without the leading `v` (for example `1.2.2`) and as `latest`, for
  `linux/amd64` and `linux/arm64`.
- **Port:** the application listens on plain HTTP on port `8080` inside the container (`http://+:8080`); the
  host maps it to any external port. The image itself serves no HTTPS.
- **Volumes:** a media volume, mounted at the local prefixes of the path mappings (for example `/media`), which
  must be writable because the `.nfo` files are written next to the media; and `/config`, the default
  `State:Directory`, which must be writable and keeps `state.json` across container restarts.
- **User:** the application runs as the base image's predefined non-root user, not as `root`. Both volumes must be
  writable by that user's UID (`docker run --rm networlddev/plextojellyfinsync:latest id` prints it). A custom
  `appsettings.json` bind-mounted over `/app/appsettings.json` must be readable by the same UID; a bind mount keeps
  the host file's owner and mode.
- The image defines no container health check; `/health` ([Dashboard and auth](dashboard-and-auth.md)) can serve
  as one.
- Implemented in the `Dockerfile` and `.github/workflows/release.yml`.

## Limits and errors

- A missing or malformed `Plex:BaseUrl`, an `OwnerAccountId` below 1, a `Sync` value outside its range, an empty
  `State:Directory` or a `Dashboard:LogBufferSize` below 1 stops the application at startup with the message of
  the violated rule (*Startup check*).
- A value that cannot be read as the setting's type — text for a number or a switch, an unknown
  `Nfo:MovieFilenameStrategy` — also stops the application at startup. That error names the setting and **does
  repeat the configured value**; only settings that are not plain text can fail this way, so the tokens and
  `Plex:BaseUrl` never appear in it.
- An `Nfo:DateTimeFormat` that is not a valid pattern is not caught at startup; it fails when a `lastplayed`
  value is written.
- A bind-mounted `appsettings.json` that the non-root user cannot read stops the application at startup with an
  access-denied error for `/app/appsettings.json`.
- A media volume or `/config` that the non-root user cannot write does not stop the start; the writes fail at run
  time as described in [Path mapping and NFO](path-mapping-and-nfo.md) and [Sync pipeline](sync-pipeline.md).
- The shipped example mapping `/data/movies` → `/local/movies` stays in effect at index 0 unless the configuration
  sets `PathMappings:0`; a deployment configures its own mappings from index 0.
- Secrets (`Plex:Token`, `Dashboard:Token`) belong in environment variables or a file readable only by the
  container's user; the dashboard's log masking of them is described in [Dashboard and auth](dashboard-and-auth.md)
  and does not cover the console output.

## Related decisions

- [`decisions/0015-container-images-on-docker-hub-only.md`](../decisions/0015-container-images-on-docker-hub-only.md)
  — release images go to Docker Hub only, under one set of coordinates.
- [`decisions/0016-options-validated-at-startup.md`](../decisions/0016-options-validated-at-startup.md) — an
  invalid configuration stops the start instead of failing on every sync; where the bounds come from.
- [`decisions/0017-plex-token-stays-optional.md`](../decisions/0017-plex-token-stays-optional.md) — the Plex token
  is not required, because Plex can allow unauthenticated networks.

## Not here

- What a setting does once it is read: [Sync pipeline](sync-pipeline.md), [Path mapping and NFO](path-mapping-and-nfo.md),
  [Dashboard and auth](dashboard-and-auth.md).
- How the image is built and released, CI and the development process: [`ARCHITECTURE.md`](../ARCHITECTURE.md#deployment)
  and [`CONTRIBUTING.md`](../CONTRIBUTING.md).
- Step-by-step setup with examples (`docker run`, finding the mapping prefixes): [`README.md`](../../README.md).