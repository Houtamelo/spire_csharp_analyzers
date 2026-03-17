**Project Context**

# Spire.Analyzers

Roslyn-based C# analyzer

- **Package**: `Spire.Analyzers`
- **Rule prefix**: `SPIRE` (SPIRE001, SPIRE002, ...)
- **No code fixes** — analyzers only, no `CodeFixProvider`

## Build Commands

```
dotnet restore
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SPIRE001"   # single rule
dotnet run --project tools/SyntaxTreeViewer -- <file> # print AST
```

## Project Structure

```
src/Spire.Analyzers/              # Analyzer (netstandard2.0)
  Rules/                          # One file per rule
  Descriptors.cs                  # Central DiagnosticDescriptor registry
  RequireInitializationAttribute.cs
src/Spire.Analyzers.Utils/        # Shared utilities (netstandard2.0)
tests/Spire.Analyzers.Tests/      # xUnit tests (net10.0, C# 14)
  AnalyzerTestBase.cs              # Base class for all analyzer tests (discovery, parsing, verification)
  Verifiers.cs                    # CSharpAnalyzerVerifier wrapper (legacy, kept for reference)
  {RuleId}/                       # One folder per rule
    {RuleId}Tests.cs              # Test runner (inherits AnalyzerTestBase)
    cases/
      _shared.cs                  # Shared preamble (types, usings)
      {CaseName}.cs               # One file per test case (excluded from compilation)
tools/SyntaxTreeViewer/           # AST printer (net10.0)
docs/rules/                      # Per-rule docs (SPIRE001.md, ...)
plans/                            # Design plans (read before implementing)
```

## Documentation Style

- Concise, clear, pragmatic. No fluff, no emojis.
- Code is self-documenting — comments explain **why**, not **what**.
- Only public API gets XML doc tags (`<summary>`, `<param>`, etc.). Internal code uses plain `///` without XML tags.
- Markdown: minimal formatting, avoid excessive headers/sections. Keep token count low.
- Full style guidelines are at `docs/style-guide.md`.

## Analyzer Conventions

See `.claude/rules/analyzer-conventions.md` for the full list of conventions, constraints, and the descriptor pattern.

Key points:
- **Analyzer targets `netstandard2.0`** — Roslyn requirement
- **Tests target `net10.0` with LangVersion 14** (C# 14)
- **Use `IOperation` API** as primary detection mechanism
- **No code fixes** — diagnostics only

### File naming

| Type | Pattern | Example |
|------|---------|---------|
| Analyzer | `Rules/{RuleId}{ShortName}Analyzer.cs` | `SPIRE001ArrayOfMustBeInitStructAnalyzer.cs` |
| Test folder | `{RuleId}/` | `SPIRE001/` |
| Test runner | `{RuleId}/{RuleId}Tests.cs` | `SPIRE001/SPIRE001Tests.cs` |
| Test case | `{RuleId}/cases/{CaseName}.cs` | `SPIRE001/cases/NonEmptyArray.cs` |
| Shared preamble | `{RuleId}/cases/_shared.cs` | `SPIRE001/cases/_shared.cs` |
| Docs | `docs/rules/{RuleId}.md` | `SPIRE001.md` |
| Descriptor | Field in `Descriptors.cs` | `SPIRE001_ArrayOfNonDefaultableStruct` |

## Test Conventions

See `.claude/rules/test-conventions.md` for the full test conventions (TDD ordering, file format, test runner structure, edge cases).

Test case file format is documented in `docs/test-case-format.md`.

## Adding a New Rule

1. **Design the rule** — Before writing anything, understand what the rule should do. Research the C# semantics involved. Build the Flagged/NOT flagged spec tables mentally. Identify edge cases, ambiguous scenarios, and out-of-scope items. **Ask the user questions** to clarify concerns: severity level, boundary cases, whether specific patterns should be flagged or not. Make suggestions if you spot cases the user may not have considered. Do not proceed until the design is clear.
2. **Write the rule plan** in `plans/` using the template at `.claude/skills/new-rule/templates/PlanTemplate.md`. Include: Flagged/NOT flagged spec tables, detection strategy, severity, message format, out-of-scope items.
3. Add descriptor to `Descriptors.cs`
4. Create attribute/marker type if needed (e.g. `RequireInitializationAttribute.cs`)
5. Scaffold test folder, shared preamble, and test runner (inheriting `AnalyzerTestBase<TAnalyzer>`).
6. **Spawn `test-researcher` agent** to produce a coverage matrix at `tests/.../{RuleId}/coverage-matrix.md`. Review the matrix — add/remove cases as needed.
7. **Spawn `test-case-writer` agents** — one per category in the matrix, in parallel. Each writer receives its category's case list.
8. Run `dotnet test` — confirm detection tests fail, false-positive tests pass
9. **Spawn `analyzer-implementer` agent** to create the analyzer in `src/Spire.Analyzers/Rules/`
10. When the implementer reports completion, **ask verification questions** derived from the plan (see plan 009 § Questioning Protocol)
11. Run `dotnet test` — confirm **ALL** tests pass
12. **Spawn `code-reviewer` agent** — provide the rule description, implementation file paths, and test folder path. The reviewer produces a one-shot audit report (it does NOT edit files).
13. Review the audit report. If any points are valid, relay them to the implementer for fixes, then re-run tests.
14. Run `/verify-rule {RuleId}` — confirm completeness
15. Create docs in `docs/rules/`

## Agent Budget

When a spawned agent stops because it ran out of turns/budget before finishing its task, **spawn a new agent to continue the remaining work**. Tell the new agent what was already completed and what remains. Do NOT do the agent's remaining work inline.

## When You're Stuck — IMPORTANT

**This applies to ALL agents (spawned or lead). Do NOT skip this.**

- **If you are a spawned agent**: message the lead explaining the problem. Do not improvise workarounds or run arbitrary commands to unblock yourself.
- **If you are the lead**: explain your problem to the user and ask for guidance. Do not guess or run arbitrary commands hoping something works.
- **Never** work around a problem by running arbitrary commands, installing tools, or guessing. Ask first.

## Reference

- `plans/` — design plans and research. **Do NOT read unless the user explicitly asks you to.** Plans may be outdated or abandoned; reading them unprompted can lead to following stale instructions.
- `docs/roslyn-api/` — Roslyn XML docs and curated reference guides (when available)
- `tools/SyntaxTreeViewer` — run on C# snippets to discover AST node types
