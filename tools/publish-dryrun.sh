#!/usr/bin/env bash
set -euo pipefail

PROJECTS=(
    "src/Houtamelo.Spire/Houtamelo.Spire.csproj"
    "src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj"
    "src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj"
)
SOLUTION="Spire.Analyzers.slnx"

# Validate repo root
for CSPROJ in "${PROJECTS[@]}"; do
    if [ ! -f "$CSPROJ" ]; then
        echo "ERROR: $CSPROJ not found. Run from repository root."
        exit 1
    fi
done
if [ ! -f "$SOLUTION" ]; then
    echo "ERROR: $SOLUTION not found. Run from repository root."
    exit 1
fi

# Extract version
VERSION=$(dotnet msbuild "${PROJECTS[0]}" -getProperty:Version 2>/dev/null || \
          sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "${PROJECTS[0]}")
bash tools/check-tag-version.sh "v${VERSION}"
echo "=== Houtamelo.Spire v${VERSION} — dry run ==="

echo ""
echo "--- restore ---"
dotnet restore "$SOLUTION"

echo ""
echo "--- build (Release) ---"
dotnet build "$SOLUTION" -c Release --no-restore

echo ""
echo "--- test (Release) ---"
dotnet test "$SOLUTION" -c Release --no-build --no-restore --logger trx

echo ""
echo "--- pack (Release) ---"
for CSPROJ in "${PROJECTS[@]}"; do
    dotnet pack "$CSPROJ" -c Release --no-build --no-restore

    PACKAGE_NAME=$(basename "$CSPROJ" .csproj)
    NUPKG="${CSPROJ%/*}/bin/Release/${PACKAGE_NAME}.${VERSION}.nupkg"

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
    echo "Package:  ${PACKAGE_NAME}"
    echo "Version:  ${VERSION}"
    echo "Size:     ${SIZE} bytes"
    echo "Path:     ${NUPKG}"
done
echo ""
echo "Dry run complete. To publish:"
echo "git push origin main"
echo "git tag v${VERSION}"
echo "git push origin v${VERSION}"
