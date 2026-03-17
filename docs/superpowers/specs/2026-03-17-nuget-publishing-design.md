# NuGet Publishing — Design Spec

## Overview

Publish `Spire.Analyzers` to NuGet via GitHub Actions on tag push, with a local dry-run script for pre-release verification.

## Decisions

- **Versioning:** `<Version>` in `.csproj` is source of truth. Git tag must match.
- **CI trigger:** Push tag `v*` to GitHub.
- **CI platform:** GitHub Actions, `ubuntu-latest`.
- **NuGet auth:** Repository secret `NUGET_API_KEY`.
- **Local script:** Dry-run only — build, test, pack, inspect. Never pushes.
- **Tag-version validation:** Shared script used by CI (hard gate) and optional local pre-push hook.
- **License:** MIT.

## Package Metadata

Add to `src/Spire.Analyzers/Spire.Analyzers.csproj`:

```xml
<Version>1.0.0</Version>
<Authors>houtamelo</Authors>
<Copyright>Copyright (c) 2026 houtamelo</Copyright>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
<RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

Add README to package:

```xml
<None Include="../../README.md" PackagePath="/" Pack="true" />
```

## GitHub Actions Workflow

File: `.github/workflows/publish.yml`

Trigger: tag push matching `v*`.

Steps:
1. Checkout
2. Setup .NET SDK
3. Run `tools/check-tag-version.sh` — fail if tag version != `.csproj` version
4. `dotnet restore`
5. `dotnet build -c Release`
6. `dotnet test -c Release`
7. `dotnet pack -c Release --no-build`
8. `dotnet nuget push` to `nuget.org` using `NUGET_API_KEY` secret

## Version Validation Script

File: `tools/check-tag-version.sh`

Takes tag name as argument (e.g., `v1.2.3`):
1. Strip `v` prefix → `1.2.3`
2. Extract `<Version>` from `src/Spire.Analyzers/Spire.Analyzers.csproj`
3. Compare. Exit 1 on mismatch, printing both values.

Used by CI workflow and optionally by local pre-push hook.

## Local Dry-Run Script

File: `tools/publish-dryrun.sh`

Steps:
1. Validate repo root (check `.csproj` path)
2. Extract and print version from `.csproj`
3. `dotnet restore`
4. `dotnet build -c Release`
5. `dotnet test -c Release`
6. `dotnet pack -c Release --no-build`
7. Inspect `.nupkg` contents (verify both DLLs in `analyzers/dotnet/cs/`, README at root)
8. Print summary: package name, version, file size, path

Never pushes to NuGet.

## Optional Local Git Hook

File: `tools/git-hooks/pre-push`

Detects tag pushes and calls `check-tag-version.sh`. No-op for branch pushes.

Setup: `git config core.hooksPath tools/git-hooks`

## New Files

| File | Purpose |
|------|---------|
| `.github/workflows/publish.yml` | CI publish workflow |
| `tools/check-tag-version.sh` | Tag-version validation (shared) |
| `tools/publish-dryrun.sh` | Local dry-run |
| `tools/git-hooks/pre-push` | Optional local hook |
| `README.md` | Package readme (bundled in .nupkg) |
| `LICENSE` | MIT license text |

## Modified Files

| File | Change |
|------|--------|
| `src/Spire.Analyzers/Spire.Analyzers.csproj` | Add version, metadata, README item group |

## Release Workflow

1. Bump `<Version>` in `.csproj`
2. Commit, push to `main`
3. `git tag v1.2.3 && git push --tags`
4. CI validates → builds → tests → publishes to NuGet
