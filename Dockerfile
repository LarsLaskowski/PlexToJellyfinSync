ARG BASE_SDK="mcr.microsoft.com/dotnet/sdk:10.0-alpine"
ARG BASE_RUNTIME="mcr.microsoft.com/dotnet/aspnet:10.0-alpine"
ARG BASE_RUNTIME_DIGEST

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

FROM ${BASE_RUNTIME}
ARG BASE_RUNTIME
ARG BASE_RUNTIME_DIGEST

LABEL org.opencontainers.image.base.name="${BASE_RUNTIME}"
LABEL org.opencontainers.image.base.digest="${BASE_RUNTIME_DIGEST}"
LABEL org.opencontainers.image.source="https://github.com/LarsLaskowski/PlexToJellyfinSync"
LABEL org.opencontainers.image.description="Cyclically syncs Plex watch state into Jellyfin NFO files"
LABEL org.opencontainers.image.licenses="MIT"

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlexToJellyfinSync.dll"]
