# Architecture — Spire.Analyzers

Roslyn `DiagnosticAnalyzer` package for C# struct correctness.

## Runtime Model

The analyzer DLL is loaded by the Roslyn compiler host (VS, `dotnet build`, Rider). It runs inside the compiler process, constraining it to `netstandard2.0`. PolySharp provides compile-time polyfills for modern C# syntax.

```
Compiler (csc / VS / Rider)
  └── Loads Spire.Analyzers.dll (netstandard2.0)
        └── Registers analysis callbacks
              └── Callbacks fire per syntax node / operation / symbol
                    └── Reports Diagnostic if rule violated
```

## Key Components

`Descriptors.cs` — central registry of all `DiagnosticDescriptor` instances. Analyzers reference descriptors from this file; they never define their own.

Analyzers live in `src/Spire.Analyzers/Rules/`, one file per rule. Each inherits `DiagnosticAnalyzer`, declares `SupportedDiagnostics`, and registers callbacks in `Initialize()`. Detection strategy and conventions are in `.claude/rules/analyzer-conventions.md`.

## Package Layout

```
Spire.Analyzers.nupkg
  └── analyzers/dotnet/cs/Spire.Analyzers.dll
```

The DLL goes in `analyzers/dotnet/cs/` (standard NuGet convention for Roslyn analyzers). `IncludeBuildOutput=false` prevents it from appearing in `lib/`.
