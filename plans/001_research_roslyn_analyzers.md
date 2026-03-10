# Plan 001: Research — Roslyn Analyzer Design Patterns

**Status**: Complete
**Goal**: Research well-maintained Roslyn analyzers to inform the design of our struct analyzer.

---

## 1. Design Philosophies of Well-Maintained Roslyn Analyzers

**Analyzed**: Meziantou.Analyzer (139+ rules), StyleCop.Analyzers (200+ rules), Roslynator (500+ rules), Microsoft.CodeAnalysis.NetAnalyzers (200+ rules), ErrorProne.NET (27 rules)

### Common patterns across all:

- **DiagnosticDescriptor as the unit of work**: Each rule defines a `DiagnosticDescriptor` with ID, title, message format, category, default severity, and help link URI. These are static readonly fields.
- **Action registration in `Initialize()`**: Analyzers register via `context.RegisterSyntaxNodeAction`, `context.RegisterOperationAction`, `context.RegisterSymbolAction`, or `context.RegisterCompilationStartAction` depending on what they need to inspect.
- **Zero external dependencies**: Only Microsoft.CodeAnalysis. This keeps the package lightweight.
- **Code fixes paired with analyzers**: Most rules include a `CodeFixProvider` (decorated with `[ExportCodeFixProvider]`) for automatic remediation.
- **Configuration via EditorConfig**: All modern analyzers support `.editorconfig` for severity customization. Some also support MSBuild properties (Meziantou's `<MeziantouAnalysisMode>`) or custom JSON config (StyleCop's `stylecop.json`).
- **Category-based organization**: Rules grouped by concern (Design, Performance, Usage, Correctness, etc.)
- **Per-rule documentation**: Individual `.md` files per rule with rationale, examples, and fix guidance.

### Per-project highlights:

**Meziantou.Analyzer** (most relevant model for a focused analyzer):
- Sequential rule IDs with a short prefix (MA0001, MA0002, ...)
- Centralized descriptor management
- Blog: https://www.meziantou.net/roslyn-analyzers-how-to.htm

**StyleCop.Analyzers**:
- Category-based folder structure (`DocumentationRules/`, `LayoutRules/`, etc.)
- Each analyzer in its own file, named after the rule

**Roslynator**:
- Ships its own testing framework (`Roslynator.Testing.CSharp.Xunit`)
- Multi-package distribution (separate packages for analyzers, formatting, code analysis)

---

## 2. Test Suite Design

### Frameworks used:

| Framework | Used By |
|-----------|---------|
| xUnit | StyleCop, Roslynator |
| Microsoft.CodeAnalysis.Testing | Microsoft, Meziantou |
| Custom testing utilities | Roslynator (`Roslynator.Testing.CSharp.Xunit`) |

### Test structure across all projects:

- **Positive tests**: Code that should NOT trigger diagnostics (no false positives)
- **Negative tests**: Code that SHOULD trigger diagnostics (correct detection)
- **Code fix tests**: Validate that the fix transforms code correctly (before/after)
- **Verifier pattern**: Helper classes (`DiagnosticVerifier`, `CodeFixVerifier`, `CSharpAnalyzerVerifier<T>`) abstract testing infrastructure
- **Inline source code**: Test cases embed C# source as strings, pass them to the verifier, and assert on expected diagnostics (location, ID, message)

### Microsoft.CodeAnalysis.Testing (official Roslyn test framework):

- `CSharpAnalyzerVerifier<TAnalyzer>` for analyzer tests
- `CSharpCodeFixVerifier<TAnalyzer, TCodeFix>` for code fix tests
- Markup syntax for expected diagnostic locations: `{|DiagnosticId:code|}` or `[|code|]`
- Cross-framework testing support

---

## 3. Other Notable Learnings

- **NuGet packaging**: Analyzer DLLs go in `analyzers/dotnet/cs/` folder. Package must set `developmentDependency=true` and consumers use `PrivateAssets="all"`.
- **Rule ID conventions**: Short prefix + sequential number (MA0001, SA1642, CA1822, EPS01). The prefix should be unique to avoid collisions.
- **Severity defaults**: Most analyzers default to Warning or Info. Errors are rare and reserved for critical correctness issues.
- **Compilation caching**: Use `CompilationStartAction` to resolve types once and share across the compilation, rather than resolving in each node visit.
- **Version compatibility**: Ship multiple analyzer DLLs for different Roslyn versions in version-specific folders.

---

## ErrorProne.NET.Structs — What We're Replacing

### Current EPS Rules (11 total):

| ID | Description |
|----|-------------|
| EPS01 | Struct can be made readonly |
| EPS02 | Non-readonly struct passed as `in` parameter |
| EPS03 | Non-readonly struct returned by ref readonly |
| EPS04 | Non-readonly struct stored in ref readonly local |
| EPS05 | Struct could be passed using `in` modifier |
| EPS06 | Defensive copy detected |
| EPS07 | Struct without Equals/GetHashCode used as dictionary key |
| EPS08 | Default ValueType.Equals or GetHashCode used |
| EPS09 | `in` modifier can be explicitly specified |
| EPS10 | Non-defaultable struct constructed via `new T` or `default` |
| EPS11 | Non-defaultable struct embedded in defaultable struct |

### Why it's poorly maintained:

- Crashes on record structs (C# 10+)
- `InvalidCastException` in `MakeStructReadOnlyAnalyzer`
- EPS06 (defensive copy) has known accuracy issues
- Stuck in beta (0.4.0-beta.1)
- Multiple unresolved bugs, no recent stable releases
- Missing ValueTask support

### Struct pitfalls NOT covered by ErrorProne.NET:

- Mutable struct value semantics gotchas (lost mutations)
- Large struct detection (copy overhead >16-32 bytes)
- Boxing detection (interface assignment, string interpolation, non-generic collections)
- Struct captured in closures/LINQ (unexpected copies or boxing)
- Reference type members in structs (partial immutability illusion in readonly structs)
- IDisposable on structs without proper disposal patterns
- Thread safety of shared mutable structs
