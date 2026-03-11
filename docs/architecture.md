# Architecture — Spire.Analyzers

## Overview

Spire.Analyzers is a Roslyn `DiagnosticAnalyzer` package focused on C# struct correctness and performance pitfalls. It ships as a NuGet package that integrates directly into the compiler pipeline.

## Runtime Model

The analyzer DLL is loaded by the Roslyn compiler host (VS, `dotnet build`, Rider). It runs **inside the compiler process**, which constrains it to `netstandard2.0`. PolySharp provides compile-time polyfills for modern C# syntax.

```
Compiler (csc / VS / Rider)
  └── Loads Spire.Analyzers.dll (netstandard2.0)
        └── Registers analysis callbacks
              └── Callbacks fire per syntax node / operation / symbol
                    └── Reports Diagnostic if rule violated
```

## Key Components

### `Descriptors.cs`

Central registry of all `DiagnosticDescriptor` instances. Every rule's metadata (ID, title, message, category, severity) lives here. Analyzers reference descriptors from this file — they never define their own.

### Analyzers (`src/Spire.Analyzers/Analyzers/`)

One file per rule. Each analyzer:
1. Inherits `DiagnosticAnalyzer`
2. Declares `SupportedDiagnostics` referencing `Descriptors`
3. Registers callbacks in `Initialize()`

### Analysis Strategy

**Preferred**: `IOperation` API via `RegisterOperationAction`. Operations are higher-level than syntax, already have semantic info resolved, and are language-agnostic within C#.

**When syntax is needed**: `RegisterSyntaxNodeAction` for cases where the syntax structure itself is the concern (e.g., modifier presence).

**Type resolution**: `RegisterCompilationStartAction` + `GetTypeByMetadataName` to resolve attribute/interface types once per compilation, then pass them to per-node callbacks via closures.

## Package Layout

```
Spire.Analyzers.nupkg
  └── analyzers/dotnet/cs/Spire.Analyzers.dll
```

The DLL goes in `analyzers/dotnet/cs/` — the standard NuGet convention for Roslyn analyzers. `IncludeBuildOutput=false` prevents it from appearing in `lib/`.

## Rule ID Scheme

- Prefix: `SPIRE`
- Format: `SPIRE001`, `SPIRE002`, ..., `SPIRE999`
- Sequential, never reused
