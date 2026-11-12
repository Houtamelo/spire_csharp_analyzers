# Version Sync

All four package projects must have the same `<Version>` value. CI fails in the version check phase if they diverge.

When changing the version in any package `.csproj`, apply the same change to all four:
- `src/Houtamelo.Spire/Houtamelo.Spire.csproj`
- `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj`
- `src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj`
- `src/Houtamelo.Spire.Functional/Houtamelo.Spire.Functional.csproj`
