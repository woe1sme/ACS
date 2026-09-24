#!/usr/bin/env bash
# Packs BuildingBlocks.Contracts into the local NuGet feed used by the services.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet pack "$root/src/BuildingBlocks/Contracts/Contracts.csproj" -c Release -o "$root/local-nuget-feed"
