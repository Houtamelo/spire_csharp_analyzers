#!/usr/bin/env bash
set -euo pipefail

PROJECTS=(
    "src/Houtamelo.Spire.Core/Houtamelo.Spire.Core.csproj"
    "src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj"
    "src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj"
    "src/Houtamelo.Spire/Houtamelo.Spire.csproj"
)

if [ $# -lt 1 ]; then
    echo "Usage: $0 <tag>"
    echo "Example: $0 v1.2.3"
    exit 1
fi

TAG="$1"
TAG_VERSION="${TAG#v}"

# Validate tag format
if [[ ! "$TAG_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-.*)?$ ]]; then
    echo "ERROR: Tag '$TAG' does not match expected format v{major}.{minor}.{patch}[-prerelease]"
    exit 1
fi

FAILED=0

for CSPROJ in "${PROJECTS[@]}"; do
    CSPROJ_VERSION=""

    # Extract version — prefer MSBuild if available, fall back to sed
    if command -v dotnet &>/dev/null; then
        CSPROJ_VERSION=$(dotnet msbuild "$CSPROJ" -getProperty:Version 2>/dev/null || true)
    fi

    if [ -z "${CSPROJ_VERSION:-}" ]; then
        CSPROJ_VERSION=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ")
    fi

    if [ -z "${CSPROJ_VERSION:-}" ]; then
        echo "ERROR: Could not extract <Version> from $CSPROJ"
        FAILED=1
        continue
    fi

    if [ "$TAG_VERSION" != "$CSPROJ_VERSION" ]; then
        echo "ERROR: Tag version '$TAG_VERSION' does not match $CSPROJ version '$CSPROJ_VERSION'"
        FAILED=1
    else
        echo "OK: $CSPROJ version '$CSPROJ_VERSION' matches tag"
    fi
done

exit $FAILED
