ARG BASE_SDK="mcr.microsoft.com/dotnet/sdk:10.0-alpine"
ARG BASE_RUNTIME_IMAGE="mcr.microsoft.com/dotnet/aspnet"
ARG BASE_RUNTIME_TAG="10.0-alpine"
ARG BASE_RUNTIME_DIGEST="sha256:27b6b84beeede74fd16886177d360799c8e4299ceadfbd64eef57bafead7878a"

FROM ${BASE_SDK} AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props PlexToJellyfinSync.slnx ./
COPY src/PlexToJellyfinSync/PlexToJellyfinSync.csproj src/PlexToJellyfinSync/
COPY src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj src/PlexToJellyfinSync.Core/
COPY src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj src/PlexToJellyfinSync.Data/
COPY src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj src/PlexToJellyfinSync.Service/
RUN dotnet restore src/PlexToJellyfinSync/PlexToJellyfinSync.csproj

COPY . .
RUN dotnet publish src/PlexToJellyfinSync/PlexToJellyfinSync.csproj -c Release -o /app/publish

FROM ${BASE_RUNTIME_IMAGE}@${BASE_RUNTIME_DIGEST}
ARG BASE_RUNTIME_IMAGE
ARG BASE_RUNTIME_TAG
ARG BASE_RUNTIME_DIGEST

LABEL org.opencontainers.image.base.name="${BASE_RUNTIME_IMAGE}:${BASE_RUNTIME_TAG}"
LABEL org.opencontainers.image.base.digest="${BASE_RUNTIME_DIGEST}"
LABEL org.opencontainers.image.description="Cyclically syncs Plex watch state into Jellyfin NFO files"
LABEL org.opencontainers.image.licenses="MIT"

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# COPY --from leaves everything owned by root; give the non-root user below read/write
# access to its own app directory before switching to it.
RUN chown -R $APP_UID:$APP_UID /app

# Run as the base image's predefined non-root user instead of root; the mounted media
# and /config volumes must be writable by this UID (check it with `docker run --rm <image> id`).
USER $APP_UID

ENTRYPOINT ["dotnet", "PlexToJellyfinSync.dll"]
