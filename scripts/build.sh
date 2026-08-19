#!/usr/bin/env bash
# Containerized build; refs/ populated from the user's own game install.
set -euo pipefail
podman run --rm -v "$PWD":/w:z -w /w mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet build src/*.csproj -c Release -o dist
