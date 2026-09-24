# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config Directory.Build.props ./
COPY local-nuget-feed/ local-nuget-feed/
COPY protos/ protos/
COPY src/BuildingBlocks/ src/BuildingBlocks/
RUN dotnet pack src/BuildingBlocks/Contracts/Contracts.csproj -c Release -o local-nuget-feed
COPY src/Services/Partners/ src/Services/Partners/
RUN dotnet publish src/Services/Partners/Partners.Api/Partners.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Partners.Api.dll"]
