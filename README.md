# PlexToJellyfinSync

Cyclically reads the watch state from a **Plex** media server and writes it into **Jellyfin** `.nfo`
files, so that everything you watch in Plex shows up as watched in Jellyfin. Existing `.nfo` files are
left untouched except for the watch fields (`watched`, `playcount`, `lastplayed`); missing `.nfo`
files are generated from the Plex metadata.

Built with **C# / .NET 10**, runs as a small container, and ships a **web dashboard** for status and
live logs.

> Inspired by [`2dee11/PlexXMLtoJellyfinNFO`](https://github.com/2dee11/PlexXMLtoJellyfinNFO) –
> rewritten in C#, running continuously instead of as a one-off, and supporting movies **and** series.

## Features

- **Polling based** – no Plex Pass required; also picks up items manually marked as watched.
- Incremental history poll (fast) plus a periodic full reconcile (catch-up).
- Writes movie, episode, season and series NFO files (aggregated watch state for seasons/series).
- **Single user** (the Plex server owner), matching Jellyfin's single-user NFO user-data import.
- Updates existing NFO files in place – only the watch fields are changed.
- Web dashboard (Blazor Server) with status cards and live logs, optionally protected by a token.

## Quick start (Docker)

```bash
docker run -d \
  --name plextojellyfinsync \
  -e PLEXSYNC__Plex__BaseUrl="http://plex:32400" \
  -e PLEXSYNC__Plex__Token="<your-plex-token>" \
  -e PLEXSYNC__PathMappings__0__Plex="/data/Movies" \
  -e PLEXSYNC__PathMappings__0__Local="/media/Movies" \
  -e PLEXSYNC__PathMappings__1__Plex="/data/Shows" \
  -e PLEXSYNC__PathMappings__1__Local="/media/Shows" \
  -v /path/to/media:/media \
  -v /path/to/config:/config \
  -p 8080:8080 \
  ghcr.io/larslaskowski/plextojellyfinsync:latest
```

- Dashboard: `http://<host>:8080/` – logs at `/logs`, health at `/health`.
- The media volume must be **writable** so NFO files can be written next to the media.
- The `/config` volume stores `state.json` (the incremental high-water mark).

## Path mappings

Plex reports file paths as the Plex server sees them (for example `/data/Movies/...`). This tool runs
in its own container and sees the same files under its own mount point (for example `/media/Movies/...`).
A path mapping rewrites the Plex path **prefix** into the local prefix; the rest of the path is kept
unchanged. Configure one mapping per library root so the tool can locate each file and write the `.nfo`
next to it.

> **The path that matters is the one Plex reports, not the folder you see in Plex or Jellyfin.** Even
> when Plex and Jellyfin point at the same physical folder, the path *string* Plex returns through its
> API can differ from the path this container has mounted. Compare the path Plex reports against the
> right-hand side of this container's volume mounts.

**A matching mapping is mandatory.** If no mapping prefix matches a file path, that item is **skipped**
(logged as `No path mapping for <path>, skipping`) – the path is never passed through unchanged. So even
when this container mounts the media under the *exact* path Plex reports, you still need an identity
mapping where `Plex` and `Local` are equal:

```json
"PathMappings": [
  { "Plex": "/data/Movies", "Local": "/data/Movies" },
  { "Plex": "/data/Shows",  "Local": "/data/Shows" }
]
```

To find the right values: the `Plex` prefix is what Plex shows under *Settings → Library → Manage
Folders* (and exactly what appears in the `No path mapping for …` log line if a mapping is missing); the
`Local` prefix is the right-hand side of this container's volume mounts (`-v /host/movies:/media/Movies`
→ `Local` = `/media/Movies`). When several mappings match, the longest matching `Plex` prefix wins.

## Configuration

All settings can be provided via `appsettings.json` or environment variables (prefix `PLEXSYNC__`,
double underscore for nesting).

| Key | Env var | Default | Description |
|---|---|---|---|
| `Plex:BaseUrl` | `PLEXSYNC__Plex__BaseUrl` | `http://plex:32400` | Plex base URL |
| `Plex:Token` | `PLEXSYNC__Plex__Token` | – | Plex auth token (`X-Plex-Token`) |
| `Plex:OwnerAccountId` | `PLEXSYNC__Plex__OwnerAccountId` | auto | Owner account id (auto-detected) |
| `Plex:Libraries` | `PLEXSYNC__Plex__Libraries__0` | all | Restrict to library section keys |
| `Sync:PollIntervalSeconds` | `PLEXSYNC__Sync__PollIntervalSeconds` | `60` | Incremental poll interval |
| `Sync:FullReconcileIntervalHours` | `PLEXSYNC__Sync__FullReconcileIntervalHours` | `24` | Full reconcile interval |
| `Sync:CreateMissingNfo` | `PLEXSYNC__Sync__CreateMissingNfo` | `true` | Create complete NFO if missing |
| `Sync:WriteSeriesSeasonAggregates` | `PLEXSYNC__Sync__WriteSeriesSeasonAggregates` | `true` | Write season/series aggregates |
| `PathMappings:N:Plex` / `:Local` | `PLEXSYNC__PathMappings__N__Plex` / `__Local` | – | Path prefix mapping |
| `Nfo:DateTimeFormat` | `PLEXSYNC__Nfo__DateTimeFormat` | `yyyy-MM-dd HH:mm:ss` | `lastplayed` format |
| `Nfo:MovieFilenameStrategy` | `PLEXSYNC__Nfo__MovieFilenameStrategy` | `PreferExistingMovieNfo` | Movie NFO naming |
| `State:Directory` | `PLEXSYNC__State__Directory` | `/config` | Where `state.json` is stored |
| `Dashboard:Enabled` | `PLEXSYNC__Dashboard__Enabled` | `true` | Enable the web dashboard |
| `Dashboard:Token` | `PLEXSYNC__Dashboard__Token` | – | Optional access token |
| `Dashboard:LogBufferSize` | `PLEXSYNC__Dashboard__LogBufferSize` | `1000` | In-memory log entries |

## Jellyfin setup

Jellyfin can only import NFO user-data (watched / playcount / lastplayed) for **one** user. In your
Jellyfin library settings enable the **NFO** metadata reader and set the user whose watch state
should be imported to the same user that corresponds to the Plex owner.

## Development

```bash
dotnet restore PlexToJellyfinSync.slnx
reihitsu-format ./                                   # format (dotnet tool install -g Reihitsu.Cli)
dotnet build PlexToJellyfinSync.slnx -c Release --no-restore
dotnet test PlexToJellyfinSync.slnx -c Release --no-build
```

## License

[MIT](LICENSE.md) © 2026 Lars Laskowski
