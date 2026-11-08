#!/usr/bin/env bash
set -euo pipefail

SOLUTION="tests/Spire.CoreCLR.slnx"
if [ ! -f "$SOLUTION" ]; then
    echo "ERROR: $SOLUTION not found. Run from repository root."
    exit 1
fi

dotnet restore "$SOLUTION" -p:EnableSourceControlManagerQueries=false
dotnet build "$SOLUTION" -c Release --no-restore -p:EnableSourceControlManagerQueries=false
dotnet test "$SOLUTION" -c Release --no-build --no-restore \
    -p:EnableSourceControlManagerQueries=false --filter 'Environment!=Unity' --logger trx
