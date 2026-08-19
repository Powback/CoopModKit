#!/usr/bin/env bash
# Decompile a Unity/Mono game's assemblies for reference (never redistribute).
# Usage: decompile.sh <game_Data/Managed dir> [assemblies...]
set -euo pipefail
MANAGED="$1"; shift
ASMS=("${@:-Assembly-CSharp}")
mkdir -p refs decomp
cp "$MANAGED"/*.dll refs/
podman run --rm -v "$PWD":/w:z -w /w mcr.microsoft.com/dotnet/sdk:9.0 bash -c "
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 PATH=\$PATH:/root/.dotnet/tools DOTNET_ROLL_FORWARD=Major
  # Newer ilspycmd versions fail to install in-container; this one works.
  dotnet tool install -g ilspycmd --version 9.0.0.7889 >/dev/null 2>&1
  for a in ${ASMS[*]}; do ilspycmd -p -o decomp/\$a -r refs refs/\$a.dll; done"
