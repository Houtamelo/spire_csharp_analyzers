**Project Context**

# Spire.Analyzers

Roslyn-based C# analyzer

- **Packages**: `Houtamelo.Spire` (meta-package), `Houtamelo.Spire.Core` (attributes/utilities), `Houtamelo.Spire.Analyzers` (analyzers + source generator), `Houtamelo.Spire.CodeFixes`, `Houtamelo.Spire.PatternAnalysis`
- **Rule prefix**: `SPIRE` (SPIRE001, SPIRE002, ...)
- **User-facing API** in `Houtamelo.Spire.Core` (namespace `Houtamelo.Spire.Core`) — `EnforceInitializationAttribute`, `EnforceExhaustivenessAttribute`, `IDiscriminatedUnion<TEnum>`, `SpireLINQ.OfKind`
- **Code fixes** in separate `Houtamelo.Spire.CodeFixes` project (standalone, no inter-project dependencies)

## Build Commands

```
dotnet restore
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SPIRE001"   # single rule
dotnet run -c Release --project benchmarks/Houtamelo.Spire.Benchmarks/ -- --filter '*' --job Dry   # benchmarks (25s)
dotnet run -c Release --project benchmarks/Houtamelo.Spire.Benchmarks/ -- --filter '*'              # benchmarks (full, ~2h)
# MCP tool `parse_syntax_tree` also available — preferred for agents
```

## Project Structure

```
src/Houtamelo.Spire/                        # Meta-package (no code, depends on all below)
src/Houtamelo.Spire.Core/                   # User-facing API: attributes, utilities (netstandard2.0)
src/Houtamelo.Spire.Analyzers/              # Analyzers + source generator (netstandard2.0)
  Rules/                                    # One file per rule
  Descriptors.cs                            # Central DiagnosticDescriptor registry
  Utils/                                    # Shared utilities (EnforceInitializationChecks, OperationUtilities, etc.)
    FlowAnalysis/                           # CFG-based flow analysis (InitState, KindState, NullState tracking)
  SourceGenerators/                         # Discriminated union source generator
    Emit/                                   # Per-strategy emitters (Additive, Overlap, BoxedFields, etc.)
    Analyzers/                              # Generator-coupled analyzers — delegates to PatternAnalysis
    Model/                                  # Union declaration model types
    Parsing/                                # Attribute parsing
src/Houtamelo.Spire.CodeFixes/              # Code fixes (standalone, no inter-project deps)
src/Houtamelo.Spire.PatternAnalysis/        # Recursive pattern exhaustiveness analysis (netstandard2.0)
  Domains/                                  # Value domains (Bool, Enum, Numeric, Nullable, Structural, etc.)
  Algorithm/                                # Maranget decision-tree builder + pattern matrix
  Resolution/                               # Type hierarchy resolver for [EnforceExhaustiveness]
tests/Houtamelo.Spire.Analyzers.Tests/      # Analyzer xUnit tests (net11.0, C# preview)
  AnalyzerTestBase.cs                       # Base class for all analyzer tests (discovery, parsing, verification)
  FlowAnalysis/                             # Flow analysis infrastructure unit + integration tests
  {RuleId}/                                 # One folder per rule
    {RuleId}Tests.cs                        # Test runner (inherits AnalyzerTestBase)
    cases/
      _shared.cs                            # Shared preamble (types, usings)
      {CaseName}.cs                         # One file per test case (excluded from compilation)
tests/Houtamelo.Spire.SourceGenerators.Tests/ # Generator snapshot + analyzer tests (net11.0)
  Behavioral/                               # Reflection-based behavioral tests (compile-emit-load pipeline)
  cases/                                    # Snapshot test cases (input.cs/output.cs pairs)
tests/Houtamelo.Spire.PatternAnalysis.Tests/  # Pattern exhaustiveness tests (unit + file-based integration)
tests/Houtamelo.Spire.BehavioralTests/      # Compile-time behavioral tests (generator runs at build time)
  Types/                                    # Union definitions per strategy
  Tests/                                    # Type-safe tests with real switch/pattern matching
benchmarks/Houtamelo.Spire.Benchmarks/      # BenchmarkDotNet performance tests
  Types/                                    # Union type declarations ([BenchmarkUnion] + hand-written)
  Benchmarks/                               # Hand-written benchmark classes (UpdateLoop, Match, Micro, JSON, etc.)
  Helpers/                                  # ArrayFiller, Distribution, BenchN constant
docs/benchmark-results/                     # Auto-generated RESULTS_{job}.md from benchmark runs
tools/DevTools/                             # MCP server (parse_syntax_tree, filesystem tools)
docs/rules/                                 # Per-rule docs (SPIRE001.md, ...)
plans/                                      # Design plans (read before implementing)
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
- **Tests target `net11.0` with LangVersion preview** (C# 15)
- **Use `IOperation` API** as primary detection mechanism
- **Code fixes** live in `src/Houtamelo.Spire.CodeFixes/` (separate project)

### File naming

| Type | Pattern | Example |
|------|---------|---------|
| Analyzer | `Rules/{RuleId}{ShortName}Analyzer.cs` | `SPIRE001ArrayOfEnforceInitializationStructAnalyzer.cs` |
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
4. Create attribute/marker type if needed in `src/Houtamelo.Spire.Core/` (namespace `Houtamelo.Spire.Core`)
5. Scaffold test folder, shared preamble, and test runner (inheriting `AnalyzerTestBase<TAnalyzer>`).
6. **Spawn `test-researcher` agent** to produce a coverage matrix at `tests/.../{RuleId}/coverage-matrix.md`. Review the matrix — add/remove cases as needed.
7. **Spawn `test-case-writer` agents** — one per category in the matrix, in parallel. Each writer receives its category's case list.
8. Run `dotnet test` — confirm detection tests fail, false-positive tests pass
9. **Spawn `analyzer-implementer` agent** to create the analyzer in `src/Houtamelo.Spire.Analyzers/Rules/`
10. When the implementer reports completion, **ask verification questions** derived from the plan (see plan 009 § Questioning Protocol)
11. Run `dotnet test` — confirm **ALL** tests pass
12. **Spawn `code-reviewer` agent** — provide the rule description, implementation file paths, and test folder path. The reviewer produces a one-shot audit report (it does NOT edit files).
13. Review the audit report. If any points are valid, relay them to the implementer for fixes, then re-run tests.
14. Run `/verify-rule {RuleId}` — confirm completeness
15. Create docs in `docs/rules/`

## Lead Delegation — IMPORTANT

The lead agent **must not** write test cases or analyzer implementations directly. Always delegate to specialized sub-agents:
- **Test cases** → `test-case-writer` agents (one per coverage matrix category)
- **Analyzer implementation** → `analyzer-implementer` agent
- **Coverage research** → `test-researcher` agent

The lead orchestrates, reviews, and makes decisions — it does not write `.cs` files in `tests/*/cases/` or `src/*/Rules/`.

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
- `tools/DevTools` — MCP server with `parse_syntax_tree` and filesystem tools (list_files, create_directory, remove, copy, move)
- `.mcp.json` — MCP servers: `git` (cyanheads, 28 tools), `dotnet` (Community.Mcp.DotNet), `sherlock`, `dev-tools`

## MCP Setup — IMPORTANT

**Before using any `mcp__git__` tool**, call `mcp__git__git_set_working_dir` with path `/home/houtamelo/Documents/projects/libraries/csharp_analyzer`. This must be done once per session — the git MCP server has no default working directory.
