# syntax=docker/dockerfile:1

# DOCS: docs/wiki/modules/server-build-and-artifacts.md#server-standalone
# DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
# DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json ./
COPY src/Device.Client.Abstractions/Device.Client.Abstractions.csproj src/Device.Client.Abstractions/
COPY src/Device.Protocol/Device.Protocol.csproj src/Device.Protocol/
COPY src/Device.Server.Abstractions/Device.Server.Abstractions.csproj src/Device.Server.Abstractions/
COPY src/Device.Server/Device.Server.csproj src/Device.Server/
COPY src/MicaAudio.Core/MicaAudio.Core.csproj src/MicaAudio.Core/
COPY src/MicaAudio.Panels/MicaAudio.Panels.csproj src/MicaAudio.Panels/
COPY src/MicaAudio.Server/MicaAudio.Server.csproj src/MicaAudio.Server/
RUN dotnet restore src/MicaAudio.Server/MicaAudio.Server.csproj

COPY src/MicaAudio.Core/ src/MicaAudio.Core/
COPY src/MicaAudio.Panels/ src/MicaAudio.Panels/
COPY src/Device.Client.Abstractions/ src/Device.Client.Abstractions/
COPY src/Device.Protocol/ src/Device.Protocol/
COPY src/Device.Server.Abstractions/ src/Device.Server.Abstractions/
COPY src/Device.Server/ src/Device.Server/
COPY src/MicaAudio.Server/ src/MicaAudio.Server/
RUN dotnet publish src/MicaAudio.Server/MicaAudio.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080 5273 5274/udp 5275/udp
ENV MICA_SERVER__LISTENHOST=0.0.0.0
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MicaAudio.Server.dll"]
