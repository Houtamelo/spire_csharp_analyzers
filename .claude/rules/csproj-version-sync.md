---
paths:
  - "**/*.csproj"
---

# Version Sync

All three package projects must have the same `<Version>` value. CI fails in the version check phase if they diverge.

When changing the version in any `.csproj`, apply the same change to all three:
- `src/Houtamelo.Spire/Houtamelo.Spire.csproj`
- `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj`
- `src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj`
