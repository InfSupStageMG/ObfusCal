FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ObfusCal.slnx .
COPY ObfusCal.Api/ObfusCal.Api.csproj ObfusCal.Api/
COPY ObfusCal.Core/ObfusCal.Core.csproj ObfusCal.Core/
COPY ObfusCal.Infrastructure/ObfusCal.Infrastructure.csproj ObfusCal.Infrastructure/
COPY ObfusCal.Sync/ObfusCal.Sync.csproj ObfusCal.Sync/

RUN dotnet restore

COPY . .
RUN dotnet publish ObfusCal.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ObfusCal.Api.dll"]