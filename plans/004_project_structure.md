# Plan 004: Project Structure

**Status**: Superseded
**Superseded by**: Plan 009 (TDD with Agent Teams) — test structure changed to per-rule folders with file-based cases. CLAUDE.md and the actual project files are the source of truth for current structure.
**Goal**: Define the complete folder hierarchy and project configuration for Spire.Analyzers.

---

## Naming Conventions

- **Namespace**: `Spire.Analyzers`
- **NuGet package**: `Spire.Analyzers`
- **Rule ID prefix**: `SAS` (Spire Analyzer Struct)
- **Rule ID format**: `SAS001`, `SAS002`, ..., `SAS999`
- **No code fixes** — analyzers only

---

## Target Framework Notes

- **Analyzer project** (`Spire.Analyzers`): Must target `netstandard2.0`. This is a hard Roslyn requirement — the analyzer DLL runs inside the compiler/IDE host, which loads `netstandard2.0` assemblies. The analyzer can still *analyze* C# 14 code; the target framework only constrains the analyzer's own runtime.
- **Test project** (`Spire.Analyzers.Tests`): Targets `net10.0` with `LangVersion` set to `14` (C# 14). Tests exercise the analyzer against C# 14 source code.

---

## Folder Hierarchy

```
csharp_analyzer/
│
├── Spire.Analyzers.sln                     # Solution file
├── Directory.Build.props                   # Shared build properties
├── .editorconfig                           # Code style for the repo itself
├── .gitignore
├── CLAUDE.md                               # Agent instructions (loaded every session)
│
├── src/
│   └── Spire.Analyzers/
│       ├── Spire.Analyzers.csproj          # netstandard2.0, analyzer package
│       │
│       ├── Analyzers/                      # One file per rule
│       │   ├── SAS001MakeStructReadonlyAnalyzer.cs
│       │   ├── SAS002NonReadonlyStructInParameterAnalyzer.cs
│       │   └── ...
│       │
│       ├── Descriptors.cs                  # Central registry of all DiagnosticDescriptors
│       │
│       └── Helpers/                        # Shared utilities (extension methods, type checks)
│           └── StructAnalysisHelpers.cs
│
├── tests/
│   └── Spire.Analyzers.Tests/
│       ├── Spire.Analyzers.Tests.csproj    # net10.0, LangVersion 14
│       │
│       ├── Verifiers.cs                    # CSharpAnalyzerVerifier wrapper
│       │
│       ├── SAS001Tests.cs                  # Tests for SAS001
│       ├── SAS002Tests.cs                  # Tests for SAS002
│       └── ...
│
├── docs/
│   ├── rules/
│   │   ├── SAS001.md                       # Per-rule documentation
│   │   ├── SAS002.md
│   │   └── ...
│   ├── roslyn-api/
│   │   ├── xml/                            # Full XML docs extracted from NuGet
│   │   │   ├── Microsoft.CodeAnalysis.xml
│   │   │   ├── Microsoft.CodeAnalysis.CSharp.xml
│   │   │   └── ...
│   │   └── reference/                      # Curated markdown guides by category
│   │       ├── struct-nodes.md             # StructDeclarationSyntax, record struct, etc.
│   │       ├── type-declarations.md        # ClassDeclaration, InterfaceDeclaration, etc.
│   │       ├── members.md                  # FieldDeclaration, MethodDeclaration, PropertyDeclaration
│   │       ├── expressions.md              # InvocationExpression, MemberAccess, Assignment, etc.
│   │       ├── statements.md              # LocalDeclaration, Return, ForEach, etc.
│   │       ├── symbols.md                  # INamedTypeSymbol, IFieldSymbol, IMethodSymbol, etc.
│   │       ├── modifiers.md                # readonly, ref, in, out, static, etc.
│   │       └── common-patterns.md          # Reusable analysis patterns for analyzers
│   ├── architecture.md                     # Internal design overview
│   └── contributing.md                     # How to add new rules
│
├── tools/
│   └── SyntaxTreeViewer/
│       ├── SyntaxTreeViewer.csproj          # net10.0 console app
│       └── Program.cs                       # Reads C# source, prints full AST
│
├── plans/                                  # Planning docs (created before implementation)
│   ├── 001_research_roslyn_analyzers.md
│   ├── 002_agent_tooling_setup.md
│   ├── 003_research_agent_feedback_loops.md
│   └── 004_project_structure.md
│
├── feedback/                               # Automatic session capture
│   ├── issue-tracker.md                    # Tracked in git
│   ├── transcripts/                        # Gitignored (local-only JSONL files)
│   └── archived/                           # Gitignored (processed reports)
│
└── .claude/
    ├── settings.json                       # Permissions, hooks
    │
    ├── hooks/
    │   └── capture-session.sh              # Transcript capture script
    │
    ├── rules/
    │   ├── analyzer-conventions.md         # Activates on src/Spire.Analyzers/**/*.cs
    │   ├── test-conventions.md             # Activates on tests/**/*.cs
    │   └── documentation-conventions.md    # Activates on docs/rules/**/*.md
    │
    ├── skills/
    │   ├── new-rule/
    │   │   ├── SKILL.md                    # Skill definition
    │   │   └── templates/
    │   │       ├── AnalyzerTemplate.cs
    │   │       ├── TestTemplate.cs
    │   │       └── DocTemplate.md
    │   ├── test/
    │   │   └── SKILL.md
    │   ├── verify-rule/
    │   │   └── SKILL.md
    │   ├── syntax-tree/
    │   │   └── SKILL.md                    # Invokes SyntaxTreeViewer tool
    │   └── maintain-docs/
    │       └── SKILL.md
    │
    └── agents/
        ├── analyzer-implementer.md
        ├── test-writer.md
        ├── researcher.md
        └── doc-maintainer.md
```

---

## Key Files

### `Spire.Analyzers.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>

    <!-- NuGet package metadata -->
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <PackageId>Spire.Analyzers</PackageId>
    <Description>Roslyn analyzers for C# struct correctness and performance pitfalls.</Description>
    <PackageTags>roslyn;analyzer;struct;csharp;code-analysis</PackageTags>
    <NoPackageAnalysis>true</NoPackageAnalysis>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>

  <!-- Place analyzer DLL in the correct NuGet path -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          PackagePath="analyzers/dotnet/cs"
          Pack="true"
          Visible="false" />
  </ItemGroup>
</Project>
```

### `Spire.Analyzers.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Spire.Analyzers\Spire.Analyzers.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Analyzer.Testing" Version="1.1.3" />
  </ItemGroup>
</Project>
```

### `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### `Descriptors.cs` (Central Descriptor Registry)

```csharp
using Microsoft.CodeAnalysis;

namespace Spire.Analyzers;

internal static class Descriptors
{
    // SAS001: Struct can be made readonly
    public static readonly DiagnosticDescriptor SAS001_MakeStructReadonly = new(
        id: "SAS001",
        title: "Struct can be made readonly",
        messageFormat: "Struct '{0}' can be declared as readonly",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/TODO/docs/rules/SAS001.md"
    );

    // Add new descriptors here in sequential order.
}
```

### `Verifiers.cs` (Test Helper)

```csharp
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Spire.Analyzers.Tests;

public static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static async Task VerifyAsync(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        await test.RunAsync();
    }

    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);
}
```

---

## File Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Analyzer | `{RuleId}{ShortName}Analyzer.cs` | `SAS001MakeStructReadonlyAnalyzer.cs` |
| Test | `{RuleId}Tests.cs` | `SAS001Tests.cs` |
| Rule doc | `{RuleId}.md` | `SAS001.md` |
| Descriptor | Central `Descriptors.cs` | `Descriptors.SAS001_MakeStructReadonly` |

---

## What's NOT in the structure

- **No code fix projects** — we provide diagnostics only, no auto-fixes
- **No VSIX project** — distribution via NuGet only
- **No separate packaging project** — `GeneratePackageOnBuild` in the analyzer .csproj handles it
- **No `global.json`** — agents use whatever SDK is installed; `Directory.Build.props` handles shared settings

---

## Roslyn API Reference Setup

Three layers of Roslyn documentation available to agents, from most detailed to most practical.

### Layer 1: Full XML Docs (`docs/roslyn-api/xml/`)

The complete API documentation extracted from the Microsoft.CodeAnalysis NuGet packages. These are the XML doc files that ship with the packages (containing every type, method, property, and parameter description).

**Extraction script** (`tools/extract-roslyn-docs.sh`):

```bash
#!/usr/bin/env bash
set -euo pipefail

# Run dotnet restore first to populate the NuGet cache
dotnet restore

OUTDIR="docs/roslyn-api/xml"
mkdir -p "$OUTDIR"

# Find the XML docs from the NuGet cache for our referenced version
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
ROSLYN_VERSION="4.12.0"

for PKG in "microsoft.codeanalysis.common" "microsoft.codeanalysis.csharp"; do
  XML_SOURCE="$NUGET_CACHE/$PKG/$ROSLYN_VERSION/lib/netstandard2.0"
  if [ -d "$XML_SOURCE" ]; then
    cp "$XML_SOURCE"/*.xml "$OUTDIR/" 2>/dev/null || true
  fi
done

echo "Extracted XML docs to $OUTDIR/"
ls -la "$OUTDIR/"
```

Run once during project setup, commit the XML files. They only change when we bump the Microsoft.CodeAnalysis version.

**Files produced:**
- `Microsoft.CodeAnalysis.xml` — Core API (SyntaxNode, SyntaxToken, SemanticModel, Compilation, symbols)
- `Microsoft.CodeAnalysis.CSharp.xml` — C#-specific API (CSharpSyntaxNode subclasses, SyntaxKind, CSharpCompilation)

### Layer 2: Curated Reference Guides (`docs/roslyn-api/reference/`)

Markdown files organized by category, written for agents implementing struct analyzers. Each file lists the relevant types with:
- Type name and namespace
- Key properties and methods
- A short C# code example showing what source code produces that node
- When to use it in an analyzer

#### `struct-nodes.md` — Struct-specific syntax nodes
Covers: `StructDeclarationSyntax`, `RecordDeclarationSyntax` (with `record struct` kind), `ReadOnlyKeyword`, `RefKeyword`, `InKeyword` modifier, `ParameterSyntax` with `in`/`ref readonly` modifiers, `RefTypeSyntax`.

#### `type-declarations.md` — All type declaration nodes
Covers: `ClassDeclarationSyntax`, `StructDeclarationSyntax`, `RecordDeclarationSyntax`, `InterfaceDeclarationSyntax`, `EnumDeclarationSyntax`, `TypeParameterSyntax`, `BaseListSyntax`.

#### `members.md` — Member declaration nodes
Covers: `FieldDeclarationSyntax`, `PropertyDeclarationSyntax`, `MethodDeclarationSyntax`, `ConstructorDeclarationSyntax`, `OperatorDeclarationSyntax`, `EventDeclarationSyntax`, `IndexerDeclarationSyntax`.

#### `expressions.md` — Expression nodes
Covers: `InvocationExpressionSyntax`, `MemberAccessExpressionSyntax`, `ObjectCreationExpressionSyntax`, `DefaultExpressionSyntax`, `AssignmentExpressionSyntax`, `CastExpressionSyntax`, `BinaryExpressionSyntax`, `IdentifierNameSyntax`.

#### `statements.md` — Statement nodes
Covers: `LocalDeclarationStatementSyntax`, `ReturnStatementSyntax`, `ExpressionStatementSyntax`, `ForEachStatementSyntax`, `UsingStatementSyntax`, `BlockSyntax`.

#### `symbols.md` — Semantic model and symbol API
Covers: `SemanticModel`, `INamedTypeSymbol`, `IFieldSymbol`, `IMethodSymbol`, `IPropertySymbol`, `IParameterSymbol`, `ILocalSymbol`, `ITypeSymbol.IsValueType`, `ITypeSymbol.IsReadOnly`, `GetDeclaredSymbol()`, `GetSymbolInfo()`, `GetTypeInfo()`.

#### `modifiers.md` — Modifier tokens and how to check them
Covers: `SyntaxKind.ReadOnlyKeyword`, `SyntaxKind.RefKeyword`, `SyntaxKind.InKeyword`, `SyntaxKind.StaticKeyword`, `SyntaxKind.PartialKeyword`, checking `Modifiers` on declaration syntax, checking `IsReadOnly` on symbols.

#### `common-patterns.md` — Reusable patterns for analyzer implementations
Covers:
- How to register a `SyntaxNodeAction` for struct declarations
- How to get the `INamedTypeSymbol` from a `StructDeclarationSyntax`
- How to check if a struct is `readonly`
- How to walk all members of a struct
- How to detect defensive copies
- How to check if a type implements an interface
- How to use `CompilationStartAction` for type caching

### Layer 3: `/syntax-tree` Skill

A skill that runs a console tool to show the exact AST for any C# code snippet. This is the most practical tool — instead of memorizing the API, agents inspect the tree for their specific case.

#### `/syntax-tree` Skill Definition

```yaml
---
name: syntax-tree
description: Print the Roslyn syntax tree for a C# code snippet. Use this to discover which SyntaxNode types and properties to match in your analyzer.
user-invocable: true
allowed-tools: Bash, Read, Write
argument-hint: <C# code or path to .cs file>
---
```

Workflow:
1. If `$ARGUMENTS` is a file path, pass it to the tool
2. If `$ARGUMENTS` is inline code, write it to a temp file
3. Run: `dotnet run --project tools/SyntaxTreeViewer -- <path>`
4. Return the formatted AST output

#### `SyntaxTreeViewer` Console Project

**`tools/SyntaxTreeViewer/SyntaxTreeViewer.csproj`**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  </ItemGroup>
</Project>
```

**`tools/SyntaxTreeViewer/Program.cs`** — Parses C# source and prints the full syntax tree with node types, kinds, spans, and token values:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: SyntaxTreeViewer <file.cs | --stdin>");
    return 1;
}

string source;
if (args[0] == "--stdin")
{
    source = Console.In.ReadToEnd();
}
else
{
    source = File.ReadAllText(args[0]);
}

var tree = CSharpSyntaxTree.ParseText(source,
    new CSharpParseOptions(LanguageVersion.CSharp14));
var root = tree.GetRoot();

PrintNode(root, indent: 0);
return 0;

static void PrintNode(SyntaxNodeOrToken nodeOrToken, int indent)
{
    var prefix = new string(' ', indent * 2);

    if (nodeOrToken.IsNode)
    {
        var node = nodeOrToken.AsNode()!;
        Console.WriteLine($"{prefix}{node.GetType().Name} [{node.Kind()}] {node.Span}");
        foreach (var child in node.ChildNodesAndTokens())
        {
            PrintNode(child, indent + 1);
        }
    }
    else
    {
        var token = nodeOrToken.AsToken();
        var valueStr = string.IsNullOrWhiteSpace(token.ValueText) ? "" : $" \"{token.ValueText}\"";
        Console.WriteLine($"{prefix}Token [{token.Kind()}]{valueStr} {token.Span}");

        // Print leading/trailing trivia if present
        foreach (var trivia in token.LeadingTrivia)
        {
            if (trivia.Kind() != SyntaxKind.WhitespaceTrivia &&
                trivia.Kind() != SyntaxKind.EndOfLineTrivia)
            {
                Console.WriteLine($"{prefix}  LeadingTrivia [{trivia.Kind()}]");
            }
        }
    }
}
```

**Example usage by an agent:**

```
$ dotnet run --project tools/SyntaxTreeViewer -- sample.cs

CompilationUnit [CompilationUnit] [0..89)
  StructDeclaration [StructDeclaration] [0..89)
    Token [PublicKeyword] "public" [0..6)
    Token [ReadOnlyKeyword] "readonly" [7..15)
    Token [StructKeyword] "struct" [16..22)
    Token [IdentifierToken] "Point" [23..28)
    Token [OpenBraceToken] "{" [29..30)
    FieldDeclaration [FieldDeclaration] [35..63)
      Token [PublicKeyword] "public" [35..41)
      Token [ReadOnlyKeyword] "readonly" [42..50)
      VariableDeclaration [VariableDeclaration] [51..62)
        PredefinedType [PredefinedType] [51..54)
          Token [IntKeyword] "int" [51..54)
        VariableDeclarator [VariableDeclarator] [55..56)
          Token [IdentifierToken] "X" [55..56)
      Token [SemicolonToken] ";" [56..57)
    ...
```

This tells the agent exactly which node types to match and what the tree structure looks like.
