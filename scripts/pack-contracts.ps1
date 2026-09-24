# Packs BuildingBlocks.Contracts into the local NuGet feed used by the services.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet pack "$root/src/BuildingBlocks/Contracts/Contracts.csproj" -c Release -o "$root/local-nuget-feed"
