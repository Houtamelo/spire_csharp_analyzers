#!/usr/bin/env bash
set -euo pipefail

# Extract Roslyn XML documentation from NuGet cache into docs/roslyn-api/xml/
# Run after `dotnet restore`. Re-run when bumping Microsoft.CodeAnalysis version.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTDIR="$PROJECT_ROOT/docs/roslyn-api/xml"
mkdir -p "$OUTDIR"

NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

# Analyzer project uses 4.12.0, DevTools uses 5.0.0
# Extract docs from both versions
for VERSION in "4.12.0" "5.0.0"; do
  for PKG in "microsoft.codeanalysis.common" "microsoft.codeanalysis.csharp"; do
    XML_SOURCE="$NUGET_CACHE/$PKG/$VERSION/lib/netstandard2.0"
    if [ -d "$XML_SOURCE" ]; then
      for XML_FILE in "$XML_SOURCE"/*.xml; do
        [ -f "$XML_FILE" ] || continue
        BASENAME="$(basename "$XML_FILE" .xml)"
        DEST="$OUTDIR/${BASENAME}_${VERSION}.xml"
        cp "$XML_FILE" "$DEST"
        echo "Extracted: $DEST"
      done
    fi
  done
done

echo ""
echo "Done. Files in $OUTDIR/:"
ls -la "$OUTDIR/"
