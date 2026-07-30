#!/usr/bin/env bash
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

configuration="${1:-Debug}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

dotnet restore LT1Diagnostics.sln --locked-mode --configfile NuGet.Config
dotnet build LT1Diagnostics.sln --configuration "$configuration" --no-restore
