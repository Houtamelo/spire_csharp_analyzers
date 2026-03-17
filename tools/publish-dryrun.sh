#!/usr/bin/env bash
set -euo pipefail

CSPROJ="src/Spire.Analyzers/Spire.Analyzers.csproj"

# Validate repo root
if [ ! -f "$CSPROJ" ]; then
    echo "ERROR: $CSPROJ not found. Run from repo root."
    exit 1
fi

# Extract version
VERSION=$(dotnet msbuild "$CSPROJ" -getProperty:Version 2>/dev/null || \
          sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ")
echo "=== Spire.Analyzers v${VERSION} — dry run ==="

echo ""
echo "--- restore ---"
dotnet restore

echo ""
echo "--- build (Release) ---"
dotnet build -c Release

echo ""
echo "--- test (Release) ---"
dotnet test -c Release

echo ""
echo "--- pack (Release) ---"
dotnet pack src/Spire.Analyzers/ -c Release --no-build

NUPKG="src/Spire.Analyzers/bin/Release/Spire.Analyzers.${VERSION}.nupkg"

if [ ! -f "$NUPKG" ]; then
    echo "ERROR: Expected $NUPKG not found"
    exit 1
fi

echo ""
echo "--- package contents ---"
unzip -l "$NUPKG"

echo ""
echo "--- summary ---"
SIZE=$(stat --printf="%s" "$NUPKG" 2>/dev/null || stat -f "%z" "$NUPKG")
echo "Package:  Spire.Analyzers"
echo "Version:  ${VERSION}"
echo "Size:     ${SIZE} bytes"
echo "Path:     ${NUPKG}"
echo ""
echo "Dry run complete. To publish, push a tag: git tag v${VERSION} && git push --tags"
