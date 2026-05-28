FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.slnx ./
COPY _nuget.config nuget.config

# Host
COPY src/DonkeyWork.Recordings.Api/*.csproj src/DonkeyWork.Recordings.Api/

# Common
COPY src/common/DonkeyWork.Recordings.Persistence/*.csproj src/common/DonkeyWork.Recordings.Persistence/

# Identity
COPY src/identity/DonkeyWork.Recordings.Identity.Api/*.csproj src/identity/DonkeyWork.Recordings.Identity.Api/
COPY src/identity/DonkeyWork.Recordings.Identity.Contracts/*.csproj src/identity/DonkeyWork.Recordings.Identity.Contracts/
COPY src/identity/DonkeyWork.Recordings.Identity.Core/*.csproj src/identity/DonkeyWork.Recordings.Identity.Core/

# Storage
COPY src/storage/DonkeyWork.Recordings.Storage.Api/*.csproj src/storage/DonkeyWork.Recordings.Storage.Api/
COPY src/storage/DonkeyWork.Recordings.Storage.Contracts/*.csproj src/storage/DonkeyWork.Recordings.Storage.Contracts/
COPY src/storage/DonkeyWork.Recordings.Storage.Core/*.csproj src/storage/DonkeyWork.Recordings.Storage.Core/

# Audio
COPY src/audio/DonkeyWork.Recordings.Audio.Api/*.csproj src/audio/DonkeyWork.Recordings.Audio.Api/
COPY src/audio/DonkeyWork.Recordings.Audio.Contracts/*.csproj src/audio/DonkeyWork.Recordings.Audio.Contracts/
COPY src/audio/DonkeyWork.Recordings.Audio.Core/*.csproj src/audio/DonkeyWork.Recordings.Audio.Core/

# Mcp
COPY src/mcp/DonkeyWork.Recordings.Mcp.Api/*.csproj src/mcp/DonkeyWork.Recordings.Mcp.Api/
COPY src/mcp/DonkeyWork.Recordings.Mcp.Contracts/*.csproj src/mcp/DonkeyWork.Recordings.Mcp.Contracts/
COPY src/mcp/DonkeyWork.Recordings.Mcp.Core/*.csproj src/mcp/DonkeyWork.Recordings.Mcp.Core/

ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0
ENV NUGET_HTTP_TIMEOUT=300
RUN dotnet restore src/DonkeyWork.Recordings.Api/DonkeyWork.Recordings.Api.csproj \
    --disable-parallel --verbosity minimal \
    || (sleep 30 && dotnet restore src/DonkeyWork.Recordings.Api/DonkeyWork.Recordings.Api.csproj --disable-parallel --verbosity minimal)

COPY src/ src/

RUN dotnet publish src/DonkeyWork.Recordings.Api/DonkeyWork.Recordings.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV DOTNET_ROLL_FORWARD=LatestMajor
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DonkeyWork.Recordings.Api.dll"]
