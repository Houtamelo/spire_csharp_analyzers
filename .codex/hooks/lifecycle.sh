#!/usr/bin/env bash
set -euo pipefail
export CI=true GIT_TERMINAL_PROMPT=0
export NUGET_HTTP_CACHE_PATH=${NUGET_HTTP_CACHE_PATH:-${TMPDIR:-/tmp}/spire-nuget-http-cache}

spire_root=$(git rev-parse --show-toplevel)
spire_dotnet=${SPIRE_DOTNET_HOST:-${DOTNET_HOST_PATH:-dotnet}}
spire_project="$spire_root/tools/Spire.Tool/Spire.Tool.csproj"
spire_cli="$spire_root/tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll"

# Hook stdout is reserved for the event response JSON.
"$spire_dotnet" build "$spire_project" -c Release --nologo --verbosity quiet >&2
exec "$spire_dotnet" "$spire_cli" --root "$spire_root" hook
