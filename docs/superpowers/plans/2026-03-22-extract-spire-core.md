# Extract Spire.Core + Meta-Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract user-facing API (attributes, future utilities/LINQ extensions) into `Spire.Core`, restructure NuGet packaging into four independent packages + one meta-package.

**Architecture:** `Spire.Core` is a standalone `lib/` package (no Roslyn dependency). Analyzer/generator/codefix projects remain independent — they resolve types by `GetTypeByMetadataName`, not compile-time references. Phantom `ProjectReference` chains between analyzer projects are removed. A `Spire` meta-package depends on all four sub-packages.

**Tech Stack:** .NET (netstandard2.0), Roslyn, NuGet packaging, xUnit

---

## Package Structure (After)

```
Spire (meta-package — empty, depends on all four below)
├── Spire.Core            lib/netstandard2.0    attributes, utilities, LINQ extensions
├── Spire.Analyzers       analyzers/dotnet/cs   SPIRE001–015 struct correctness
├── Spire.SourceGenerators analyzers/dotnet/cs   discriminated union generator + coupled analyzers
└── Spire.CodeFixes       analyzers/dotnet/cs   code fixes + refactorings
```

No compile-time references between the four sub-packages. Each is independently installable. The meta-package bundles all four.

## Internal Project Graph (After)

```
Spire.Core                no dependencies (standalone lib)
Spire.Analyzers.Utils  →  Microsoft.CodeAnalysis.CSharp (Roslyn helpers, internal)
Spire.Analyzers        →  Spire.Analyzers.Utils (ships Utils.dll alongside)
Spire.SourceGenerators →  Microsoft.CodeAnalysis.CSharp (standalone)
Spire.CodeFixes        →  Microsoft.CodeAnalysis.CSharp.Workspaces (standalone)
```

## Change Summary

| Area | What changes |
|------|-------------|
| New project | `src/Spire.Core/` — attributes moved here, namespace `Spire` |
| New project | `src/Spire/` — meta-package (no code) |
| Namespace | `Spire.Analyzers.MustBeInitAttribute` → `Spire.MustBeInitAttribute` |
| Namespace | `Spire.Analyzers.EnforceExhaustivenessAttribute` → `Spire.EnforceExhaustivenessAttribute` |
| Metadata strings | 8 analyzers: `GetTypeByMetadataName` updated |
| Emitter strings | 5 emitters: `[global::Spire.Analyzers.MustBeInit]` → `[global::Spire.MustBeInit]` |
| Phantom refs | `Spire.SourceGenerators → Spire.Analyzers` removed |
| Phantom refs | `Spire.CodeFixes → Spire.SourceGenerators` removed |
| Test infra | `AnalyzerTestBase`, `GeneratorTestHelper`, `BehavioralTestBase` updated |
| Test cases | 9 `_shared.cs` files: `global using Spire.Analyzers` → `global using Spire` |
| Snapshots | 27 `output.cs` files: `Spire.Analyzers.MustBeInit` → `Spire.MustBeInit` |
| Consumer projects | `BehavioralTests`, `Benchmarks`: ref `Spire.Core` instead of `Spire.Analyzers` |
| NuGet packaging | All 5 projects get proper pack metadata |
| Solution | `Spire.Analyzers.slnx` updated |

---

### Task 1: Create Spire.Core project

**Files:**
- Create: `src/Spire.Core/Spire.Core.csproj`
- Create: `src/Spire.Core/MustBeInitAttribute.cs`
- Create: `src/Spire.Core/EnforceExhaustivenessAttribute.cs`

- [ ] **Step 1: Create project directory**

Run: `mkdir -p src/Spire.Core`

- [ ] **Step 2: Create Spire.Core.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>

    <!-- NuGet package metadata -->
    <PackageId>Spire.Core</PackageId>
    <Version>1.0.0</Version>
    <Authors>houtamelo</Authors>
    <Copyright>Copyright (c) 2026 houtamelo</Copyright>
    <Description>Core types, attributes, and utilities for the Spire analyzer suite.</Description>
    <PackageTags>spire;attributes;csharp</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create MustBeInitAttribute.cs**

```csharp
using System;

namespace Spire;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum)]
public class MustBeInitAttribute : Attribute
{
}
```

- [ ] **Step 4: Create EnforceExhaustivenessAttribute.cs**

```csharp
using System;

namespace Spire;

[AttributeUsage(AttributeTargets.Enum)]
public sealed class EnforceExhaustivenessAttribute : MustBeInitAttribute
{
}
```

- [ ] **Step 5: Verify Spire.Core builds**

Run: `dotnet build src/Spire.Core/Spire.Core.csproj`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```
git add src/Spire.Core/
git commit -m "feat: create Spire.Core project with MustBeInit and EnforceExhaustiveness attributes"
```

---

### Task 2: Remove attributes from Spire.Analyzers and remove phantom ProjectReferences

**Files:**
- Delete: `src/Spire.Analyzers/MustBeInitAttribute.cs`
- Delete: `src/Spire.Analyzers/EnforceExhaustivenessAttribute.cs`
- Modify: `src/Spire.SourceGenerators/Spire.SourceGenerators.csproj` (remove phantom ref)
- Modify: `src/Spire.CodeFixes/Spire.CodeFixes.csproj` (remove phantom ref)

- [ ] **Step 1: Delete attribute files from Spire.Analyzers**

Run:
```
rm src/Spire.Analyzers/MustBeInitAttribute.cs
rm src/Spire.Analyzers/EnforceExhaustivenessAttribute.cs
```

- [ ] **Step 2: Remove phantom ProjectReference from Spire.SourceGenerators.csproj**

Remove this block:
```xml
  <ItemGroup>
    <ProjectReference Include="..\Spire.Analyzers\Spire.Analyzers.csproj" PrivateAssets="all" />
  </ItemGroup>
```

- [ ] **Step 3: Remove phantom ProjectReference from Spire.CodeFixes.csproj**

Remove this block:
```xml
  <ItemGroup>
    <ProjectReference Include="..\Spire.SourceGenerators\Spire.SourceGenerators.csproj" PrivateAssets="all" />
  </ItemGroup>
```

- [ ] **Step 4: Verify Spire.SourceGenerators builds**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`
Expected: Build succeeded (no types from Spire.Analyzers were actually used)

- [ ] **Step 5: Verify Spire.CodeFixes builds**

Run: `dotnet build src/Spire.CodeFixes/Spire.CodeFixes.csproj`
Expected: Build succeeded (no types from Spire.SourceGenerators were actually used)

- [ ] **Step 6: Verify Spire.Analyzers still builds**

Run: `dotnet build src/Spire.Analyzers/Spire.Analyzers.csproj`
Expected: Build succeeded — no analyzer file has a compile-time dependency on the attribute types (only string literals in `GetTypeByMetadataName`)

- [ ] **Step 7: Commit**

```
git rm src/Spire.Analyzers/MustBeInitAttribute.cs src/Spire.Analyzers/EnforceExhaustivenessAttribute.cs
git add src/Spire.SourceGenerators/Spire.SourceGenerators.csproj src/Spire.CodeFixes/Spire.CodeFixes.csproj
git commit -m "refactor: remove attributes from Spire.Analyzers, remove phantom ProjectReferences"
```

---

### Task 3: Update analyzer metadata name strings

All analyzers resolve attributes via `GetTypeByMetadataName`. Namespace changed from `Spire.Analyzers` to `Spire`.

**Files (all in `src/Spire.Analyzers/Rules/`):**
- `SPIRE001ArrayOfMustBeInitStructAnalyzer.cs`
- `SPIRE002MustBeInitOnFieldlessTypeAnalyzer.cs`
- `SPIRE003DefaultOfMustBeInitStructAnalyzer.cs`
- `SPIRE004NewOfMustBeInitStructWithoutCtorAnalyzer.cs`
- `SPIRE005ActivatorCreateInstanceOfMustBeInitStructAnalyzer.cs`
- `SPIRE006ClearOfMustBeInitElementsAnalyzer.cs`
- `SPIRE007UnsafeSkipInitOfMustBeInitStructAnalyzer.cs`
- `SPIRE008GetUninitializedObjectOfMustBeInitStructAnalyzer.cs`
- `SPIRE015ExhaustiveEnumSwitchAnalyzer.cs`

- [ ] **Step 1: Replace all MustBeInitAttribute metadata names**

In each of the 8 files listed above (SPIRE001–008), replace:
```csharp
.GetTypeByMetadataName("Spire.Analyzers.MustBeInitAttribute");
```
with:
```csharp
.GetTypeByMetadataName("Spire.MustBeInitAttribute");
```

- [ ] **Step 2: Replace EnforceExhaustivenessAttribute metadata name**

In `SPIRE015ExhaustiveEnumSwitchAnalyzer.cs`, replace:
```csharp
"Spire.Analyzers.EnforceExhaustivenessAttribute");
```
with:
```csharp
"Spire.EnforceExhaustivenessAttribute");
```

- [ ] **Step 3: Verify Spire.Analyzers builds**

Run: `dotnet build src/Spire.Analyzers/Spire.Analyzers.csproj`
Expected: Build succeeded (analyzers don't import the attribute types, only reference by string)

- [ ] **Step 4: Commit**

```
git add src/Spire.Analyzers/Rules/
git commit -m "refactor: update GetTypeByMetadataName strings to Spire namespace"
```

---

### Task 4: Update source generator emitter strings

**Files (all in `src/Spire.SourceGenerators/Emit/`):**
- `AdditiveEmitter.cs`
- `BoxedFieldsEmitter.cs`
- `BoxedTupleEmitter.cs`
- `OverlapEmitter.cs`
- `UnsafeOverlapEmitter.cs`

- [ ] **Step 1: Replace attribute references in all 5 emitters**

In each file, replace:
```csharp
sb.AppendLine("[global::Spire.Analyzers.MustBeInit]");
```
with:
```csharp
sb.AppendLine("[global::Spire.MustBeInit]");
```

- [ ] **Step 2: Verify Spire.SourceGenerators builds**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
git add src/Spire.SourceGenerators/Emit/
git commit -m "refactor: update emitted MustBeInit attribute to Spire namespace"
```

---

### Task 5: Update test infrastructure and project references

**Files:**
- Modify: `tests/Spire.Analyzers.Tests/Spire.Analyzers.Tests.csproj`
- Modify: `tests/Spire.Analyzers.Tests/AnalyzerTestBase.cs`
- Modify: `tests/Spire.SourceGenerators.Tests/Spire.SourceGenerators.Tests.csproj`
- Modify: `tests/Spire.SourceGenerators.Tests/GeneratorTestHelper.cs`
- Modify: `tests/Spire.SourceGenerators.Tests/Behavioral/BehavioralTestBase.cs`
- Modify: `tests/Spire.BehavioralTests/Spire.BehavioralTests.csproj`
- Modify: `benchmarks/Spire.Benchmarks/Spire.Benchmarks.csproj`

- [ ] **Step 1: Add Spire.Core reference to Spire.Analyzers.Tests.csproj**

Keep the existing `Spire.Analyzers` ProjectReference (provides analyzer types for `TAnalyzer`). Add `Spire.Core` alongside it:
```xml
<ProjectReference Include="..\..\src\Spire.Core\Spire.Core.csproj" />
```

- [ ] **Step 2: Update AnalyzerTestBase.cs — add CoreAssemblyReference**

After the existing `AnalyzerAssemblyReference` field (line 26-27), add:
```csharp
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Spire.MustBeInitAttribute).Assembly.Location);
```

Update `ResolveReferencesAsync` (line 184-189) — change the return to include both references:
```csharp
    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference);
    }
```

- [ ] **Step 3: Update Spire.SourceGenerators.Tests.csproj**

Replace the `Spire.Analyzers` ProjectReference with `Spire.Core`:
```xml
<!-- Before -->
<ProjectReference Include="..\..\src\Spire.Analyzers\Spire.Analyzers.csproj" />
<!-- After -->
<ProjectReference Include="..\..\src\Spire.Core\Spire.Core.csproj" />
```

Keep the `Spire.SourceGenerators` and `Spire.CodeFixes` ProjectReferences unchanged.

- [ ] **Step 4: Update GeneratorTestHelper.cs**

Replace (line 13-14):
```csharp
    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Spire.Analyzers.MustBeInitAttribute).Assembly.Location);
```
with:
```csharp
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Spire.MustBeInitAttribute).Assembly.Location);
```

Also update line 28 where it's appended to references:
```csharp
// Before
.Append(AnalyzerAssemblyReference)
// After
.Append(CoreAssemblyReference)
```

- [ ] **Step 5: Update BehavioralTestBase.cs**

Replace (lines 28-30):
```csharp
        // Spire.Analyzers — provides [MustBeInit] and other marker attributes
        refs.Add(MetadataReference.CreateFromFile(
            typeof(Spire.Analyzers.MustBeInitAttribute).Assembly.Location));
```
with:
```csharp
        // Spire.Core — provides [MustBeInit] and other marker attributes
        refs.Add(MetadataReference.CreateFromFile(
            typeof(Spire.MustBeInitAttribute).Assembly.Location));
```

- [ ] **Step 6: Update Spire.BehavioralTests.csproj**

Replace the `Spire.Analyzers` ProjectReference with `Spire.Core`:
```xml
<!-- Before -->
<ProjectReference Include="..\..\src\Spire.Analyzers\Spire.Analyzers.csproj" />
<!-- After -->
<ProjectReference Include="..\..\src\Spire.Core\Spire.Core.csproj" />
```

Keep the `Spire.SourceGenerators` analyzer reference as-is.

- [ ] **Step 7: Update Spire.Benchmarks.csproj**

Replace the `Spire.Analyzers` ProjectReference with `Spire.Core`:
```xml
<!-- Before -->
<ProjectReference Include="..\..\src\Spire.Analyzers\Spire.Analyzers.csproj" />
<!-- After -->
<ProjectReference Include="..\..\src\Spire.Core\Spire.Core.csproj" />
```

Keep the `Spire.SourceGenerators` analyzer reference as-is.

- [ ] **Step 8: Commit**

```
git add tests/ benchmarks/
git commit -m "refactor: update test infrastructure and project references for Spire.Core"
```

---

### Task 6: Update test case files

**Files:**
- 9 `_shared.cs` files in `tests/Spire.Analyzers.Tests/SPIRE*/cases/`
- 27 `output.cs` snapshot files in `tests/Spire.SourceGenerators.Tests/cases/`

- [ ] **Step 1: Update all _shared.cs files**

In all 9 files, replace:
```csharp
global using Spire.Analyzers;
```
with:
```csharp
global using Spire;
```

Files:
- `SPIRE001/cases/_shared.cs`
- `SPIRE002/cases/_shared.cs`
- `SPIRE003/cases/_shared.cs`
- `SPIRE004/cases/_shared.cs`
- `SPIRE005/cases/_shared.cs`
- `SPIRE006/cases/_shared.cs`
- `SPIRE007/cases/_shared.cs`
- `SPIRE008/cases/_shared.cs`
- `SPIRE015/cases/_shared.cs`

- [ ] **Step 2: Update all snapshot output.cs files**

In all 27 output.cs files under `tests/Spire.SourceGenerators.Tests/cases/`, replace:
```
Spire.Analyzers.MustBeInit
```
with:
```
Spire.MustBeInit
```

Use find+sed for efficiency:
```bash
find tests/Spire.SourceGenerators.Tests/cases -name "output.cs" \
  -exec sed -i 's/Spire\.Analyzers\.MustBeInit/Spire.MustBeInit/g' {} +
```

- [ ] **Step 3: Verify no stale references remain**

Run: `grep -r "Spire\.Analyzers\.MustBeInit\|Spire\.Analyzers\.EnforceExhaustiveness" tests/`
Expected: No matches

- [ ] **Step 4: Commit**

```
git add tests/
git commit -m "refactor: update test cases and snapshots to Spire namespace"
```

---

### Task 7: Update solution file and verify full build

**Files:**
- Modify: `Spire.Analyzers.slnx`

- [ ] **Step 1: Add Spire.Core to solution**

Update `Spire.Analyzers.slnx` — add `Spire.Core` to the `/src/` folder:
```xml
<Folder Name="/src/">
  <Project Path="src/Spire.Core/Spire.Core.csproj" />
  <Project Path="src/Spire.Analyzers/Spire.Analyzers.csproj" />
  <Project Path="src/Spire.Analyzers.Utils/Spire.Analyzers.Utils.csproj" />
  <Project Path="src/Spire.SourceGenerators/Spire.SourceGenerators.csproj" />
  <Project Path="src/Spire.CodeFixes/Spire.CodeFixes.csproj" />
</Folder>
```

- [ ] **Step 2: Restore and build entire solution**

Run: `dotnet restore && dotnet build`
Expected: Build succeeded (0 errors)

- [ ] **Step 3: Run all tests**

Run: `dotnet test`
Expected: All tests pass

- [ ] **Step 4: Verify no stale references remain anywhere**

Run: `grep -r "Spire\.Analyzers\.MustBeInit\|Spire\.Analyzers\.EnforceExhaustiveness" src/ tests/ benchmarks/`
Expected: No matches (only in git history)

- [ ] **Step 5: Commit**

```
git add Spire.Analyzers.slnx
git commit -m "refactor: add Spire.Core to solution file"
```

---

### Task 8: NuGet packaging for Spire.Analyzers

**Files:**
- Modify: `src/Spire.Analyzers/Spire.Analyzers.csproj`

- [ ] **Step 1: Update Spire.Analyzers.csproj packaging**

The project already has NuGet metadata and `IncludeBuildOutput=false`. Three changes:

1. Add `<DevelopmentDependency>true</DevelopmentDependency>` after `<IncludeBuildOutput>false</IncludeBuildOutput>`
2. Remove `<PackageReadmeFile>README.md</PackageReadmeFile>` (moves to meta-package)
3. Remove the README `<None Include="../../README.md" .../>` ItemGroup (moves to meta-package)

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Spire.Analyzers/Spire.Analyzers.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
git add src/Spire.Analyzers/Spire.Analyzers.csproj
git commit -m "chore: update Spire.Analyzers NuGet packaging"
```

---

### Task 9: NuGet packaging for Spire.SourceGenerators

**Files:**
- Modify: `src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`

- [ ] **Step 1: Add NuGet packaging to Spire.SourceGenerators.csproj**

Add to PropertyGroup:
```xml
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <PackageId>Spire.SourceGenerators</PackageId>
    <Version>1.0.0</Version>
    <Authors>houtamelo</Authors>
    <Copyright>Copyright (c) 2026 houtamelo</Copyright>
    <Description>Discriminated union source generator for C# structs, classes, and records.</Description>
    <PackageTags>roslyn;source-generator;discriminated-union;csharp</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <NoPackageAnalysis>true</NoPackageAnalysis>
```

Add packaging items:
```xml
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          PackagePath="analyzers/dotnet/cs"
          Pack="true"
          Visible="false" />
  </ItemGroup>
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
git add src/Spire.SourceGenerators/Spire.SourceGenerators.csproj
git commit -m "chore: add NuGet packaging to Spire.SourceGenerators"
```

---

### Task 10: NuGet packaging for Spire.CodeFixes

**Files:**
- Modify: `src/Spire.CodeFixes/Spire.CodeFixes.csproj`

- [ ] **Step 1: Add NuGet packaging to Spire.CodeFixes.csproj**

Add to PropertyGroup:
```xml
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <PackageId>Spire.CodeFixes</PackageId>
    <Version>1.0.0</Version>
    <Authors>houtamelo</Authors>
    <Copyright>Copyright (c) 2026 houtamelo</Copyright>
    <Description>Code fixes and refactorings for Spire analyzers and source generators.</Description>
    <PackageTags>roslyn;code-fix;csharp</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <NoPackageAnalysis>true</NoPackageAnalysis>
```

Add packaging items:
```xml
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          PackagePath="analyzers/dotnet/cs"
          Pack="true"
          Visible="false" />
  </ItemGroup>
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Spire.CodeFixes/Spire.CodeFixes.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
git add src/Spire.CodeFixes/Spire.CodeFixes.csproj
git commit -m "chore: add NuGet packaging to Spire.CodeFixes"
```

---

### Task 11: Create Spire meta-package

**Files:**
- Create: `src/Spire/Spire.csproj`

- [ ] **Step 1: Create meta-package directory**

Run: `mkdir -p src/Spire`

- [ ] **Step 2: Create Spire.csproj**

The meta-package bundles all analyzer/generator DLLs directly in its own `analyzers/dotnet/cs/`
folder, rather than relying on transitive NuGet dependencies. This avoids the problem where
`DevelopmentDependency=true` packages get `PrivateAssets="all"` when consumed transitively,
which would prevent analyzers from activating.

`Spire.Core` is the only dependency declared in the NuGet — it flows to consumers as a `lib/`
reference. The analyzer DLLs are vendored into the meta-package.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>

    <!-- NuGet package metadata -->
    <PackageId>Spire</PackageId>
    <Version>1.0.0</Version>
    <Authors>houtamelo</Authors>
    <Copyright>Copyright (c) 2026 houtamelo</Copyright>
    <Description>Meta-package: installs Spire.Core (attributes/utilities), Spire.Analyzers (struct correctness), Spire.SourceGenerators (discriminated unions), and Spire.CodeFixes.</Description>
    <PackageTags>spire;roslyn;analyzer;source-generator;csharp</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Houtamelo/spire_csharp_analyzers</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Houtamelo/spire_csharp_analyzers</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <!-- Spire.Core flows as a lib dependency to consumers -->
  <ItemGroup>
    <ProjectReference Include="..\Spire.Core\Spire.Core.csproj" PrivateAssets="none" />
  </ItemGroup>

  <!--
    Build-only references to produce the analyzer DLLs.
    NOT declared as NuGet dependencies — DLLs are vendored below.
  -->
  <ItemGroup>
    <ProjectReference Include="..\Spire.Analyzers\Spire.Analyzers.csproj" PrivateAssets="all" />
    <ProjectReference Include="..\Spire.SourceGenerators\Spire.SourceGenerators.csproj" PrivateAssets="all" />
    <ProjectReference Include="..\Spire.CodeFixes\Spire.CodeFixes.csproj" PrivateAssets="all" />
  </ItemGroup>

  <!-- Vendor all analyzer/generator/codefix DLLs into the package -->
  <ItemGroup>
    <None Include="$(OutputPath)\Spire.Analyzers.dll"
          PackagePath="analyzers/dotnet/cs" Pack="true" Visible="false" />
    <None Include="$(OutputPath)\Spire.Analyzers.Utils.dll"
          PackagePath="analyzers/dotnet/cs" Pack="true" Visible="false" />
    <None Include="$(OutputPath)\Spire.SourceGenerators.dll"
          PackagePath="analyzers/dotnet/cs" Pack="true" Visible="false" />
    <None Include="$(OutputPath)\Spire.CodeFixes.dll"
          PackagePath="analyzers/dotnet/cs" Pack="true" Visible="false" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../../README.md" PackagePath="/" Pack="true" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add to solution**

Update `Spire.Analyzers.slnx` — add to `/src/` folder:
```xml
<Project Path="src/Spire/Spire.csproj" />
```

- [ ] **Step 4: Verify meta-package builds**

Run: `dotnet build src/Spire/Spire.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
git add src/Spire/ Spire.Analyzers.slnx
git commit -m "feat: create Spire meta-package"
```

---

### Task 12: Final verification

- [ ] **Step 1: Clean and rebuild entire solution**

Run: `dotnet clean && dotnet build`
Expected: Build succeeded (0 errors)

- [ ] **Step 2: Run all tests**

Run: `dotnet test`
Expected: All tests pass

- [ ] **Step 3: Verify no stale namespace references**

Run: `grep -rn "Spire\.Analyzers\.MustBeInit\|Spire\.Analyzers\.EnforceExhaustiveness" src/ tests/ benchmarks/`
Expected: No matches

- [ ] **Step 4: Verify pack succeeds for each project**

Run (sequentially):
```
dotnet pack src/Spire.Core/Spire.Core.csproj --no-build -o ./tmp/nupkg
dotnet pack src/Spire.Analyzers/Spire.Analyzers.csproj --no-build -o ./tmp/nupkg
dotnet pack src/Spire.SourceGenerators/Spire.SourceGenerators.csproj --no-build -o ./tmp/nupkg
dotnet pack src/Spire.CodeFixes/Spire.CodeFixes.csproj --no-build -o ./tmp/nupkg
dotnet pack src/Spire/Spire.csproj --no-build -o ./tmp/nupkg
```
Expected: 5 .nupkg files in `./tmp/nupkg/`

- [ ] **Step 5: Inspect meta-package contents**

Verify the Spire meta-package contains vendored analyzer DLLs and a dependency on Spire.Core:
```
unzip -l ./tmp/nupkg/Spire.1.0.0.nupkg | grep -E "analyzers/|\.nuspec"
```
Expected: `analyzers/dotnet/cs/Spire.Analyzers.dll`, `Spire.Analyzers.Utils.dll`, `Spire.SourceGenerators.dll`, `Spire.CodeFixes.dll`

Then check nuspec for Spire.Core dependency:
```
unzip -p ./tmp/nupkg/Spire.1.0.0.nupkg Spire.nuspec
```
Expected: `<dependency id="Spire.Core" .../>` present. No dependencies on `Spire.Analyzers`, `Spire.SourceGenerators`, or `Spire.CodeFixes` (those are vendored, not declared as deps).

- [ ] **Step 6: Clean up tmp**

Run: `rm -rf ./tmp/nupkg`

- [ ] **Step 7: Commit (if any fixups were needed)**

Only if earlier tasks required adjustments during verification.
