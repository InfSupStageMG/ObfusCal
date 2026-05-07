FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ObfusCal.slnx .
COPY ObfusCal.Domain/ObfusCal.Domain.csproj ObfusCal.Domain/
COPY ObfusCal.Application/ObfusCal.Application.csproj ObfusCal.Application/
COPY ObfusCal.Infrastructure/ObfusCal.Infrastructure.csproj ObfusCal.Infrastructure/
COPY ObfusCal.Api/ObfusCal.Api.csproj ObfusCal.Api/
COPY ObfusCal.Plugins.ICloudCalendar/ObfusCal.Plugins.ICloudCalendar.csproj ObfusCal.Plugins.ICloudCalendar/
COPY ObfusCal.Tests/ObfusCal.Tests.csproj ObfusCal.Tests/

RUN dotnet restore

COPY . .
RUN dotnet publish ObfusCal.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL maintainer="Matthias Hendrickx - Gijs Pennings @ InfoSupport"
LABEL org.opencontainers.image.description="ObfusCal API"

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Create directory for DataProtection keys (must be mounted as persistent volume)
# See: docker-compose.yaml and docs/07-deployment-view.md
RUN mkdir -p /dataprotection/keys && chmod 700 /dataprotection/keys

COPY --from=build /app/publish .
EXPOSE 8443

# Set default DataProtection key path
ENV DATAPROTECTION_KEYS_PATH=/dataprotection/keys

RUN chown -R 1000:1000 /app && chown -R 1000:1000 /dataprotection/keys
USER 1000

ENTRYPOINT ["dotnet", "ObfusCal.Api.dll"]
