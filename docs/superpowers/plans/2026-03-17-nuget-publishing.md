# NuGet Publishing Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish `Spire.Analyzers` to NuGet via GitHub Actions on tag push, with local dry-run verification.

**Architecture:** Tag-triggered CI workflow validates version match, builds, tests, packs, and pushes to NuGet. Local script provides dry-run verification. Shared version-check script used by both CI and optional git hook.

**Tech Stack:** GitHub Actions, dotnet CLI, bash

**Spec:** `docs/superpowers/specs/2026-03-17-nuget-publishing-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `LICENSE` | Create | MIT license text |
| `README.md` | Create | Package readme — what Spire.Analyzers is, install instructions, link to docs |
| `src/Spire.Analyzers/Spire.Analyzers.csproj` | Modify | Add metadata, remove `GeneratePackageOnBuild` |
| `tools/check-tag-version.sh` | Create | Compare git tag version to .csproj version |
| `tools/publish-dryrun.sh` | Create | Local build/test/pack/inspect (no push) |
| `.github/workflows/publish.yml` | Create | CI workflow — tag trigger, validate, build, test, pack, push |
| `tools/git-hooks/pre-push` | Create | Optional local hook calling check-tag-version.sh |

---

## Task 1: LICENSE and README

**Files:**
- Create: `LICENSE`
- Create: `README.md`

- [ ] **Step 1: Create LICENSE**

Standard MIT license text with `Copyright (c) 2026 houtamelo`.

```
MIT License

Copyright (c) 2026 houtamelo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 2: Create README.md**

Minimal readme: project name, one-line description, install command, link to rules docs.

```markdown
# Spire.Analyzers

Roslyn analyzers for C# struct correctness and performance pitfalls.

## Installation

```shell
dotnet add package Spire.Analyzers
```

## Rules

| Rule | Description |
|------|-------------|
| [SPIRE001](docs/rules/SPIRE001.md) | Array allocation of `[MustBeInit]` struct |
| [SPIRE002](docs/rules/SPIRE002.md) | `[MustBeInit]` on fieldless type |
| [SPIRE003](docs/rules/SPIRE003.md) | `default(T)` of `[MustBeInit]` struct |
| [SPIRE004](docs/rules/SPIRE004.md) | `new T()` of `[MustBeInit]` struct without parameterless ctor |
| [SPIRE005](docs/rules/SPIRE005.md) | `Activator.CreateInstance` of `[MustBeInit]` struct |
| [SPIRE006](docs/rules/SPIRE006.md) | `Array.Clear`/`Span<T>.Clear` on `[MustBeInit]` struct |
| [SPIRE007](docs/rules/SPIRE007.md) | `Unsafe.SkipInit` on `[MustBeInit]` struct |
| [SPIRE008](docs/rules/SPIRE008.md) | `RuntimeHelpers.GetUninitializedObject` on `[MustBeInit]` struct |

## License

[MIT](LICENSE)
```

- [ ] **Step 3: Verify files**

```bash
head -1 LICENSE        # Expected: "MIT License"
head -1 README.md      # Expected: "# Spire.Analyzers"
```

- [ ] **Step 4: Commit**

```bash
git add LICENSE README.md
git commit -m "Add MIT license and package README"
```

---

## Task 2: Update .csproj metadata

**Files:**
- Modify: `src/Spire.Analyzers/Spire.Analyzers.csproj`

- [ ] **Step 1: Modify PropertyGroup and add README item group**

The `.csproj` PropertyGroup already contains `PackageId`, `Description`, `PackageTags`, `NoPackageAnalysis`. Remove `GeneratePackageOnBuild`, add new metadata after existing fields. Also add a README item group.

Target state for the full `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>

    <!-- NuGet package metadata -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <PackageId>Spire.Analyzers</PackageId>
    <Version>1.0.0</Version>
    <Authors>houtamelo</Authors>
    <Copyright>Copyright (c) 2026 houtamelo</Copyright>
    <Description>Roslyn analyzers for C# struct correctness and performance pitfalls.</Description>
    <PackageTags>roslyn;analyzer;struct;csharp;code-analysis</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <NoPackageAnalysis>true</NoPackageAnalysis>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Spire.Analyzers.Utils\Spire.Analyzers.Utils.csproj" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="AnalyzerReleases.Shipped.md" />
    <AdditionalFiles Include="AnalyzerReleases.Unshipped.md" />
  </ItemGroup>

  <!-- Place analyzer and utility DLLs in the correct NuGet path -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          PackagePath="analyzers/dotnet/cs"
          Pack="true"
          Visible="false" />
    <None Include="$(OutputPath)\Spire.Analyzers.Utils.dll"
          PackagePath="analyzers/dotnet/cs"
          Pack="true"
          Visible="false" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../../README.md" PackagePath="/" Pack="true" />
  </ItemGroup>
</Project>
```

Changes from current state:
- Removed `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`
- Added `Version`, `Authors`, `Copyright`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageReadmeFile`
- Added README item group
- Existing `PackageId`, `Description`, `PackageTags`, `NoPackageAnalysis` preserved

- [ ] **Step 4: Verify build and pack**

```bash
dotnet build src/Spire.Analyzers/ -c Release
dotnet pack src/Spire.Analyzers/ -c Release --no-build
```

Expected: build succeeds, `.nupkg` produced at `src/Spire.Analyzers/bin/Release/Spire.Analyzers.1.0.0.nupkg`.

- [ ] **Step 5: Verify package contents**

```bash
unzip -l src/Spire.Analyzers/bin/Release/Spire.Analyzers.1.0.0.nupkg
```

Expected contents include:
- `analyzers/dotnet/cs/Spire.Analyzers.dll`
- `analyzers/dotnet/cs/Spire.Analyzers.Utils.dll`
- `README.md`
- No `lib/` directory (IncludeBuildOutput is false)

- [ ] **Step 6: Commit**

```bash
git add src/Spire.Analyzers/Spire.Analyzers.csproj
git commit -m "Add NuGet package metadata, remove GeneratePackageOnBuild"
```

---

## Task 3: Version validation script

**Files:**
- Create: `tools/check-tag-version.sh`

- [ ] **Step 1: Create the script**

```bash
#!/usr/bin/env bash
set -euo pipefail

CSPROJ="src/Spire.Analyzers/Spire.Analyzers.csproj"

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

# Extract version from .csproj — prefer MSBuild if available, fall back to grep
if command -v dotnet &>/dev/null; then
    CSPROJ_VERSION=$(dotnet msbuild "$CSPROJ" -getProperty:Version 2>/dev/null || true)
fi

if [ -z "${CSPROJ_VERSION:-}" ]; then
    CSPROJ_VERSION=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ")
fi

if [ -z "${CSPROJ_VERSION:-}" ]; then
    echo "ERROR: Could not extract <Version> from $CSPROJ"
    exit 1
fi

if [ "$TAG_VERSION" != "$CSPROJ_VERSION" ]; then
    echo "ERROR: Tag version '$TAG_VERSION' does not match .csproj version '$CSPROJ_VERSION'"
    exit 1
fi

echo "OK: Tag version '$TAG_VERSION' matches .csproj version '$CSPROJ_VERSION'"
```

- [ ] **Step 2: Make executable**

```bash
chmod +x tools/check-tag-version.sh
```

- [ ] **Step 3: Test with matching version**

```bash
./tools/check-tag-version.sh v1.0.0
```

Expected: `OK: Tag version '1.0.0' matches .csproj version '1.0.0'`

- [ ] **Step 4: Test with mismatched version**

```bash
./tools/check-tag-version.sh v9.9.9
```

Expected: exit 1, `ERROR: Tag version '9.9.9' does not match .csproj version '1.0.0'`

- [ ] **Step 5: Commit**

```bash
git add tools/check-tag-version.sh
git commit -m "Add tag-version validation script"
```

---

## Task 4: Local dry-run script

**Files:**
- Create: `tools/publish-dryrun.sh`

- [ ] **Step 1: Create the script**

```bash
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
          grep -oP '(?<=<Version>)[^<]+' "$CSPROJ")
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
```

- [ ] **Step 2: Make executable**

```bash
chmod +x tools/publish-dryrun.sh
```

- [ ] **Step 3: Run the dry-run script**

```bash
./tools/publish-dryrun.sh
```

Expected: full build/test/pack cycle completes, package contents shown, summary printed.

- [ ] **Step 4: Commit**

```bash
git add tools/publish-dryrun.sh
git commit -m "Add local publish dry-run script"
```

---

## Task 5: GitHub Actions workflow

**Files:**
- Create: `.github/workflows/publish.yml`

- [ ] **Step 1: Create workflow file**

```yaml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      contents: read

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      # Install .NET 10+ SDK. Remove this step once runners ship with it.
      # If .NET 10 is still in preview, add: include-prerelease: true
      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Validate tag matches .csproj version
        run: ./tools/check-tag-version.sh "${GITHUB_REF_NAME}"

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test
        run: dotnet test -c Release --no-build

      - name: Pack
        run: dotnet pack src/Spire.Analyzers/ -c Release --no-build

      - name: Push to NuGet
        run: >
          dotnet nuget push
          src/Spire.Analyzers/bin/Release/Spire.Analyzers.*.nupkg
          --source https://api.nuget.org/v3/index.json
          --api-key ${{ secrets.NUGET_API_KEY }}
          --skip-duplicate
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/publish.yml
git commit -m "Add GitHub Actions NuGet publish workflow"
```

---

## Task 6: Optional pre-push hook

**Files:**
- Create: `tools/git-hooks/pre-push`

- [ ] **Step 1: Create the hook**

```bash
#!/usr/bin/env bash
# Optional pre-push hook — validates tag version matches .csproj.
# Install: git config core.hooksPath tools/git-hooks

SCRIPT_DIR="$(cd "$(dirname "$0")" && cd ../.. && pwd)"

while read local_ref local_sha remote_ref remote_sha; do
    # Only check tag pushes
    if [[ "$local_ref" == refs/tags/v* ]]; then
        TAG_NAME="${local_ref#refs/tags/}"
        echo "Validating tag $TAG_NAME..."
        "$SCRIPT_DIR/tools/check-tag-version.sh" "$TAG_NAME"
    fi
done
```

- [ ] **Step 2: Make executable**

```bash
chmod +x tools/git-hooks/pre-push
```

- [ ] **Step 3: Test the hook with piped input**

```bash
echo "refs/tags/v1.0.0 abc123 refs/tags/v1.0.0 abc123" | ./tools/git-hooks/pre-push
```

Expected: `OK: Tag version '1.0.0' matches .csproj version '1.0.0'`

```bash
echo "refs/tags/v9.9.9 abc123 refs/tags/v9.9.9 abc123" | ./tools/git-hooks/pre-push
```

Expected: exit 1, version mismatch error.

```bash
echo "refs/heads/main abc123 refs/heads/main abc123" | ./tools/git-hooks/pre-push
```

Expected: no output (branch push, no-op).

- [ ] **Step 4: Commit**

```bash
git add tools/git-hooks/pre-push
git commit -m "Add optional pre-push hook for tag-version validation"
```

---

## Task 7: Final verification

- [ ] **Step 1: Run full dry-run**

```bash
./tools/publish-dryrun.sh
```

Expected: all steps pass, package contents correct.

- [ ] **Step 2: Test version validation**

```bash
./tools/check-tag-version.sh v1.0.0   # should pass
./tools/check-tag-version.sh v0.0.0   # should fail
```

- [ ] **Step 3: Verify all tests still pass**

```bash
dotnet test
```

Expected: all existing tests pass.
