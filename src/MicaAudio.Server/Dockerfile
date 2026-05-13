# syntax=docker/dockerfile:1

# DOCS: docs/wiki/modules/server-build-and-artifacts.md#server-standalone
# DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json ./
COPY src/Device.Client.Abstractions/Device.Client.Abstractions.csproj src/Device.Client.Abstractions/
COPY src/Device.Protocol/Device.Protocol.csproj src/Device.Protocol/
COPY src/Device.Server.Abstractions/Device.Server.Abstractions.csproj src/Device.Server.Abstractions/
COPY src/Device.Server/Device.Server.csproj src/Device.Server/
COPY src/MicaAudio.Core/MicaAudio.Core.csproj src/MicaAudio.Core/
COPY src/Panels.Composition/Panels.Composition.csproj src/Panels.Composition/
COPY src/MicaAudio.Server/MicaAudio.Server.csproj src/MicaAudio.Server/
RUN dotnet restore src/MicaAudio.Server/MicaAudio.Server.csproj

COPY src/Device.Client.Abstractions/ src/Device.Client.Abstractions/
COPY src/Device.Protocol/ src/Device.Protocol/
COPY src/Device.Server.Abstractions/ src/Device.Server.Abstractions/
COPY src/Device.Server/ src/Device.Server/
COPY src/MicaAudio.Core/ src/MicaAudio.Core/
COPY src/Panels.Composition/ src/Panels.Composition/
COPY src/MicaAudio.Server/ src/MicaAudio.Server/
RUN dotnet publish src/MicaAudio.Server/MicaAudio.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080 5273 5274/udp
ENV MICA_SERVER__LISTENHOST=0.0.0.0
# Persistent data root — panels JSON + uploaded media (GIF/image).
# Must match the volume mount point in docker-compose.yml.
# Override with MICA_SERVER__STORAGEROOT if using a different mount.
ENV MICA_SERVER__STORAGEROOT=/app/data
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MicaAudio.Server.dll"]
