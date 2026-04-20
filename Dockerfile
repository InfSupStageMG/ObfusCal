FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ObfusCal.slnx .
COPY ObfusCal.Api/ObfusCal.Api.csproj ObfusCal.Api/
COPY ObfusCal.Core/ObfusCal.Core.csproj ObfusCal.Core/
COPY ObfusCal.Infrastructure/ObfusCal.Infrastructure.csproj ObfusCal.Infrastructure/
COPY ObfusCal.Sync/ObfusCal.Sync.csproj ObfusCal.Sync/
COPY ObfusCal.Tests/ObfusCal.Tests.csproj ObfusCal.Tests/

RUN dotnet restore

COPY . .
RUN dotnet publish ObfusCal.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL maintainer="Matthias Hendrickx - Gijs Pennings @ InfoSupport"
LABEL org.opencontainers.image.description="ObfusCal API"

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ObfusCal.Api.dll"]