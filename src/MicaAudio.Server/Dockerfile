# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json ./
COPY src/Device.Client.Abstractions/Device.Client.Abstractions.csproj src/Device.Client.Abstractions/
COPY src/Device.Protocol/Device.Protocol.csproj src/Device.Protocol/
COPY src/Device.Server.Abstractions/Device.Server.Abstractions.csproj src/Device.Server.Abstractions/
COPY src/Device.Server/Device.Server.csproj src/Device.Server/
COPY src/MicaAudio.Server/MicaAudio.Server.csproj src/MicaAudio.Server/
RUN dotnet restore src/MicaAudio.Server/MicaAudio.Server.csproj

COPY src/Device.Client.Abstractions/ src/Device.Client.Abstractions/
COPY src/Device.Protocol/ src/Device.Protocol/
COPY src/Device.Server.Abstractions/ src/Device.Server.Abstractions/
COPY src/Device.Server/ src/Device.Server/
COPY src/MicaAudio.Server/ src/MicaAudio.Server/
RUN dotnet publish src/MicaAudio.Server/MicaAudio.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV MICA_SERVER__LISTENHOST=0.0.0.0
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MicaAudio.Server.dll"]
