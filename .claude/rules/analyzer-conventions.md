---
paths:
  - "src/Spire.Analyzers/**/*.cs"
---

# Analyzer Conventions

- Every analyzer inherits `DiagnosticAnalyzer` with `[DiagnosticAnalyzer(LanguageNames.CSharp)]`
- Descriptors are defined in `Descriptors.cs` as `public static readonly DiagnosticDescriptor` — never inline
- Descriptor field naming: `{RuleId}_{ShortName}` (e.g., `SPIRE001_ArrayOfNonDefaultableStruct`)
- Descriptor pattern:
  ```csharp
  // In Descriptors.cs — add in sequential order
  public static readonly DiagnosticDescriptor SPIRE001_ShortName = new(
      id: "SPIRE001",
      title: "...",
      messageFormat: "...",
      category: "Correctness",  // or "Performance"
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true,
      description: "...",
      helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE001.md"
  );
  ```
- File naming: `Rules/{RuleId}{ShortName}Analyzer.cs`
- Call `context.EnableConcurrentExecution()` in `Initialize()`
- Call `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` in `Initialize()`
- Use `CompilationStartAction` + `GetTypeByMetadataName` when resolving custom types/attributes — cache in the closure
- Use `SymbolEqualityComparer.Default` for all symbol comparisons
- Prefer `RegisterOperationAction` (IOperation API) over `RegisterSyntaxNodeAction` — operations have semantic info pre-resolved
- **Exception: `stackalloc`** has no `OperationKind` in Roslyn (verified up to 5.0.0). Use `RegisterSyntaxNodeAction` with `SyntaxKind.StackAllocArrayCreationExpression` and `StackAllocArrayCreationExpressionSyntax` instead.
- Register the narrowest action type possible
- Target is `netstandard2.0` — PolySharp provides syntax polyfills, but avoid runtime APIs not available on netstandard2.0
- All package dependencies must use `PrivateAssets="all"`
- No code fixes — this project provides diagnostics only
