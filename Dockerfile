# Both base images are pinned by tag and multi-architecture index digest in the FROM line itself, the only place
# Dependabot's docker updates read (decision 0034).
FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine@sha256:52556c0fb9d0f609ae9aeab9efeeba96f4acc2f7038f7696f2235ccbdbb8e290 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props PlexToJellyfinSync.slnx ./
COPY src/PlexToJellyfinSync/PlexToJellyfinSync.csproj src/PlexToJellyfinSync/
COPY src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj src/PlexToJellyfinSync.Core/
COPY src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj src/PlexToJellyfinSync.Data/
COPY src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj src/PlexToJellyfinSync.Service/
RUN dotnet restore src/PlexToJellyfinSync/PlexToJellyfinSync.csproj

COPY . .
RUN dotnet publish src/PlexToJellyfinSync/PlexToJellyfinSync.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine@sha256:d39b72ad1534fa0faf52b488bffb0762df7ba269d120f0338713d4dda2a41bf5

# The label names the image and tag of the runtime FROM line above and is edited together with it.
LABEL org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:11.0-alpine"
LABEL org.opencontainers.image.description="Cyclically syncs Plex watch state into Jellyfin NFO files"
LABEL org.opencontainers.image.licenses="MIT"

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_URLS=http://+:8081

COPY --from=build /app/publish .

# COPY --from leaves everything owned by root; give the non-root user below read/write
# access to its own app directory before switching to it.
RUN chown -R "$APP_UID:$APP_UID" /app

# Run as the base image's predefined non-root user instead of root; the mounted media
# and /config volumes must be writable by this UID (check it with `docker run --rm <image> id`).
USER 1000

ENTRYPOINT ["dotnet", "PlexToJellyfinSync.dll"]
