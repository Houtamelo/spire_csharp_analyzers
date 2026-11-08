#!/usr/bin/env bash
set -euo pipefail

PROJECT="tests/Houtamelo.Spire.SourceGenerators.Tests/Houtamelo.Spire.SourceGenerators.Tests.csproj"
if [ ! -f "$PROJECT" ]; then
    echo "ERROR: $PROJECT not found. Run from repository root."
    exit 1
fi

dotnet test "$PROJECT" -c Release -p:EnableSourceControlManagerQueries=false \
    --filter 'Environment=Unity' --logger trx
