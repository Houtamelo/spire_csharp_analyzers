# Spire.Analyzers

Roslyn-based C# analyzer

- **Packages**: `Houtamelo.Spire` (attributes + utilities + vendored analyzers), `Houtamelo.Spire.Analyzers` (analyzers + source generator), `Houtamelo.Spire.CodeFixes`, `Houtamelo.Spire.PatternAnalysis`
- **Rule prefix**: `SPIRE` (SPIRE001, SPIRE002, ...)
- **User-facing API** in `Houtamelo.Spire` (namespace `Houtamelo.Spire`) — `EnforceInitializationAttribute`, `EnforceExhaustivenessAttribute`, `DiscriminatedUnionAttribute`, `VariantAttribute`, `Layout`, `GenerateDeconstruct`, `JsonLibrary`, `JsonNameAttribute`, `IDiscriminatedUnion<TEnum>`, `SpireLINQ.OfKind`, `SpireEnum<TEnum>` (safe integer-to-enum conversions), `InlinerStructAttribute`, `InlinableAttribute`, `IActionInliner` / `IActionInliner<T1..T8>`, `IFuncInliner<TR>` / `IFuncInliner<T1..T8, TR>` (JIT-monomorphizable dispatch)
- **Global config** via MSBuild properties (`CompilerVisibleProperty` in `build/Houtamelo.Spire.props`) — DU defaults (`Spire_DU_Default{Layout,GenerateDeconstruct,Json,JsonDiscriminator}`) and analyzer enforcement (`Spire_EnforceExhaustivenessOnAllEnumTypes`)
- **Code fixes** in separate `Houtamelo.Spire.CodeFixes` project (standalone, no inter-project dependencies)

## Build Commands

```
dotnet restore
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SPIRE001"   # single rule
dotnet run -c Release --project benchmarks/Houtamelo.Spire.Benchmarks/ -- --filter '*' --job Dry   # benchmarks (25s)
dotnet run -c Release --project benchmarks/Houtamelo.Spire.Benchmarks/ -- --filter '*'              # benchmarks (full, ~2h)
# The `dev-tools` MCP server also exposes `parse_syntax_tree` when configured.
```

## Project Structure

```
src/Houtamelo.Spire/                        # User-facing API: attributes, utilities, vendors analyzer DLLs (netstandard2.0)
  build/Houtamelo.Spire.props               # CompilerVisibleProperty declarations (packed into NuGet)
src/Houtamelo.Spire.Analyzers/              # Analyzers + source generator (netstandard2.0)
  Rules/                                    # One file per rule
  Descriptors.cs                            # Central DiagnosticDescriptor registry
  Utils/                                    # Shared utilities (EnforceInitializationChecks, OperationUtilities, etc.)
    FlowAnalysis/                           # CFG-based flow analysis (InitState, KindState, NullState tracking)
  SourceGenerators/                         # Source generators + coupled analyzers
    Emit/                                   # Per-strategy DU emitters (Additive, Overlap, BoxedFields, etc.)
    Analyzers/                              # Generator-coupled analyzers — delegates to PatternAnalysis
    Model/                                  # Union declaration model types
    Parsing/                                # Attribute parsing
    Performance/                            # Closure inliner generator ([InlinerStruct]/[Inlinable])
      Emit/                                 # InlinerStructEmitter, InlinableTwinEmitter, InlinableBodyRewriter
      Analyzers/                            # InlinableUsageAnalyzer (SPIRE021-026)
      Model/                                # InlinerStructDecl, InlinableHostDecl, diagnostics
      Parsing/                              # InlinerStructParser, InlinableParser
src/Houtamelo.Spire.CodeFixes/              # Code fixes (standalone, no inter-project deps)
src/Houtamelo.Spire.PatternAnalysis/        # Recursive pattern exhaustiveness analysis (netstandard2.0)
  Domains/                                  # Value domains (Bool, Enum, Numeric, Nullable, Structural, etc.)
  Algorithm/                                # Maranget decision-tree builder + pattern matrix
  Resolution/                               # Type hierarchy resolver for [EnforceExhaustiveness]
tests/Houtamelo.Spire.Analyzers.Tests/      # Analyzer xUnit tests (net10.0, C# preview)
  AnalyzerTestBase.cs                       # Base class for all analyzer tests (discovery, parsing, verification)
  FlowAnalysis/                             # Flow analysis infrastructure unit + integration tests
  {RuleId}/                                 # One folder per rule
    {RuleId}Tests.cs                        # Test runner (inherits AnalyzerTestBase)
    cases/
      _shared.cs                            # Shared preamble (types, usings)
      {CaseName}.cs                         # One file per test case (excluded from compilation)
tests/Houtamelo.Spire.SourceGenerators.Tests/ # Generator snapshot + analyzer tests (net10.0)
  Behavioral/                               # Reflection-based behavioral tests (compile-emit-load pipeline)
  cases/                                    # Snapshot test cases (input.cs/output.cs pairs)
tests/Houtamelo.Spire.PatternAnalysis.Tests/  # Pattern exhaustiveness tests (unit + file-based integration)
tests/DevTools.Tests/                      # DevTools filesystem integration tests
tests/Houtamelo.Spire.BehavioralTests/      # Compile-time behavioral tests (generator runs at build time)
  Types/                                    # Union definitions per strategy
  Tests/                                    # Type-safe tests with real switch/pattern matching
benchmarks/Houtamelo.Spire.Benchmarks/      # BenchmarkDotNet performance tests
  Types/                                    # Union type declarations ([BenchmarkUnion] + hand-written)
  Benchmarks/                               # Hand-written benchmark classes (UpdateLoop, Match, Micro, JSON, etc.)
  Helpers/                                  # ArrayFiller, Distribution, BenchN constant
docs/benchmark-results/                     # Auto-generated RESULTS_{job}.md from benchmark runs
tools/DevTools/                             # MCP server (parse_syntax_tree, filesystem tools, dotnet_build, dotnet_test, dotnet_restore)
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

See `.codex/rules/analyzer-conventions.md` for the full list of conventions, constraints, and the descriptor pattern.

Key points:
- **Analyzer targets `netstandard2.0`** — Roslyn requirement
- **Tests target `net10.0` with LangVersion preview**
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

See `.codex/rules/test-conventions.md` for the full test conventions (TDD ordering, file format, test runner structure, edge cases).

Test case file format is documented in `docs/test-case-format.md`.

## Adding a New Rule

1. **Design the rule** — Before writing anything, understand what the rule should do. Research the C# semantics involved. Build the Flagged/NOT flagged spec tables mentally. Identify edge cases, ambiguous scenarios, and out-of-scope items. **Ask the user questions** to clarify concerns: severity level, boundary cases, whether specific patterns should be flagged or not. Make suggestions if you spot cases the user may not have considered. Do not proceed until the design is clear.
2. **Write the rule plan** in `plans/` using the template at `.agents/skills/new-rule/templates/PlanTemplate.md`. Include: Flagged/NOT flagged spec tables, detection strategy, severity, message format, out-of-scope items.
3. Add descriptor to `Descriptors.cs`
4. Create attribute/marker type if needed in `src/Houtamelo.Spire/` (namespace `Houtamelo.Spire`)
5. Scaffold test folder, shared preamble, and test runner (inheriting `AnalyzerTestBase<TAnalyzer>`).
6. **Spawn `test_researcher` agent** to produce a coverage matrix at `tests/.../{RuleId}/coverage-matrix.md`. Review the matrix — add/remove cases as needed.
7. **Spawn `test_case_writer` agents** — one per category in the matrix, in parallel. Each writer receives its category's case list.
8. Run `dotnet test` — confirm detection tests fail, false-positive tests pass
9. **Spawn `analyzer_implementer` agent** to create the analyzer in `src/Houtamelo.Spire.Analyzers/Rules/`
10. When the implementer reports completion, **ask verification questions** derived from the plan (see plan 009 § Questioning Protocol)
11. Run `dotnet test` — confirm **ALL** tests pass
12. **Spawn `code_reviewer` agent** — provide the rule description, implementation file paths, and test folder path. The reviewer produces a one-shot audit report (it does NOT edit files).
13. Review the audit report. If any points are valid, relay them to the implementer for fixes, then re-run tests.
14. Run `$verify-rule {RuleId}` — confirm completeness
15. Create docs in `docs/rules/`

## Lead Delegation — IMPORTANT

The lead agent **must not** write test cases or analyzer implementations directly. Always delegate to specialized sub-agents:
- **Test cases** → `test_case_writer` agents (one per coverage matrix category)
- **Analyzer implementation** → `analyzer_implementer` agent
- **Coverage research** → `test_researcher` agent

The lead orchestrates, reviews, and makes decisions — it does not write `.cs` files in `tests/*/cases/` or `src/*/Rules/`.

When an agent runs out of budget, give the remaining scoped work to another agent with a clear handoff. Investigate ordinary failures using available evidence; ask for guidance only when a decision cannot be made from the task and repository.

## Reference

- `plans/` — design plans and research. **Do NOT read unless the user explicitly asks you to.** Plans may be outdated or abandoned; reading them unprompted can lead to following stale instructions.
- `docs/roslyn-api/` — Roslyn XML docs and curated reference guides (when available)
- `tools/DevTools` — MCP server with `parse_syntax_tree`, filesystem tools, and dotnet tools
- `.codex/config.toml` — project MCP servers: `git`, `dotnet`, `sherlock`, `dev-tools`, `microsoft-learn`

## MCP Setup — IMPORTANT

**Before using any `mcp__git__` tool**, call `mcp__git__git_set_working_dir` with the current repository root. This must be done once per session — the git MCP server has no default working directory.

## Codex setup

Codex discovers task skills in `.agents/skills/` and named agents in `.codex/agents/`. Use a skill when its description matches the user's task; invoke it explicitly as `$skill-name` when requested. The specialist agents are available for the rule, generator, code fix, review, and documentation workflows above. Project hooks are in `.codex/hooks.json` and require local trust before Codex runs them. The existing `.claude/` setup and `.mcp.json` remain for Claude compatibility; Codex uses the new locations.

When editing agent guidance or documentation, consult `.codex/rules/cross-reference-check.md` for affected references. Keep public documentation aligned with changed behavior. After editing C# files, format the changed files with `dotnet format --include` and run the relevant real test projects. Report validation results.

When changing a package version, keep the three package project versions synchronized; see `.codex/rules/csproj-version-sync.md`.
