# Contributing — Adding a New Rule

## Prerequisites

- Read the rule's plan in `plans/` (if one exists)
- Run `dotnet build` to verify the project compiles before starting

## Step-by-Step

### 1. Define the descriptor

Add a `DiagnosticDescriptor` to `src/Spire.Analyzers/Descriptors.cs`:

```csharp
public static readonly DiagnosticDescriptor SASxxx_ShortName = new(
    id: "SASxxx",
    title: "...",
    messageFormat: "...",
    category: "Correctness",  // or "Performance"
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "...",
    helpLinkUri: "https://github.com/TODO/docs/rules/SASxxx.md"
);
```

### 2. Create the analyzer

Create `src/Spire.Analyzers/Analyzers/SASxxx{ShortName}Analyzer.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Spire.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SASxxxShortNameAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SASxxx_ShortName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Use CompilationStartAction when resolving types
        // Use RegisterOperationAction for IOperation-based analysis
        // Use RegisterSyntaxNodeAction for syntax-based analysis
    }
}
```

### 3. Write tests

Create `tests/Spire.Analyzers.Tests/SASxxxTests.cs`:

- Method naming: `{Scenario}_Should{Not}Report{DiagnosticId}`
- Include **positive tests** (no false positives) and **negative tests** (correct detection)
- Test both `struct` and `record struct` where applicable
- Use `AnalyzerVerifier<TAnalyzer>` from `Verifiers.cs`
- Use `{|SASxxx:code|}` markup for expected diagnostic spans

### 4. Write documentation

Create `docs/rules/SASxxx.md` with: ID, Category, Severity, Description, Examples (violating + compliant), When to Suppress.

### 5. Verify

```
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SASxxx"
```

## Conventions

- **IOperation API** over syntax for detection logic
- **CompilationStartAction** for resolving types once per compilation
- **SymbolEqualityComparer.Default** for symbol comparisons
- **No code fixes** — this project provides diagnostics only
