# PlexToJellyfinSync

Cyclically reads the watch state from a **Plex** media server and writes it into **Jellyfin** `.nfo`
files, so that everything you watch in Plex shows up as watched in Jellyfin. In existing `.nfo` files
only the watch fields (`watched`, `playcount`, `lastplayed`) change, and every other element keeps its
content; missing `.nfo` files are generated from the Plex metadata.

Built with **C# / .NET 11**, runs as a small container, and ships a **web dashboard** for status and
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
  networlddev/plextojellyfinsync:latest
```

- Image: [`networlddev/plextojellyfinsync`](https://hub.docker.com/r/networlddev/plextojellyfinsync)
  on Docker Hub.
- Dashboard: `http://<host>:8080/` – logs at `/logs`, health at `/health`.
- Both volumes must be writable by the container's non-root user – the media volume because the `.nfo` files are
  written next to the media, `/config` because it keeps `state.json`. What the image expects (port, volumes, user,
  a bind-mounted `appsettings.json`) is stated in [*Container*](docs/areas/configuration.md#container).

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

**A matching mapping is mandatory**: an item whose path no mapping matches is skipped, never written under
the Plex path. So even when this container mounts the media under the *exact* path Plex reports, you still need
an identity mapping where `Plex` and `Local` are equal:

```json
"PathMappings": [
  { "Plex": "/data/Movies", "Local": "/data/Movies" },
  { "Plex": "/data/Shows",  "Local": "/data/Shows" }
]
```

To find the right values: the `Plex` prefix is what Plex shows under *Settings → Library → Manage
Folders* (and exactly what appears in the `No path mapping for …` log line if a mapping is missing); the
`Local` prefix is the right-hand side of this container's volume mounts (`-v /host/movies:/media/Movies`
→ `Local` = `/media/Movies`). How paths are matched, rejected and checked against the local roots is stated in
[Path mapping and NFO](docs/areas/path-mapping-and-nfo.md#path-mapping).

## Configuration

Settings come from `appsettings.json` or from environment variables with the prefix `PLEXSYNC__` (a double
underscore for every level of nesting, as in the quick start above). Every setting with its environment variable,
default and allowed values, and what happens when one is invalid, is listed in
[Configuration](docs/areas/configuration.md#settings).

## Jellyfin setup

Jellyfin can only import NFO user-data (watched / playcount / lastplayed) for **one** user. In your
Jellyfin library settings enable the **NFO** metadata reader and set the user whose watch state
should be imported to the same user that corresponds to the Plex owner.

## Development

```bash
dotnet restore PlexToJellyfinSync.slnx
dotnet tool run reihitsu-format --force ./           # format (after dotnet tool restore)
dotnet build PlexToJellyfinSync.slnx -c Release --no-restore
dotnet test PlexToJellyfinSync.slnx -c Release --no-build
```

See [`CONTRIBUTING.md`](docs/CONTRIBUTING.md) for the full contribution workflow,
[`docs/areas/README.md`](docs/areas/README.md) for what the software guarantees, area by area,
[`ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how the sync pipeline, dashboard and deployment are put
together, and [`UNIT_TESTS.md`](docs/UNIT_TESTS.md) for test conventions (unit tests are mandatory for
new code).

## License

[MIT](LICENSE.md) © 2026 Lars Laskowski
