---
paths:
  - ".claude/**"
  - "CLAUDE.md"
  - "docs/**"
---

# Cross-Reference Check

When modifying any file in `.claude/`, `CLAUDE.md`, or `docs/`, check all other files in the same group below for consistency. Do not skip this step.

## Test case format
How test case files are structured, discovered, and validated.
- `docs/test-case-format.md` — canonical format reference
- `.claude/rules/test-conventions.md` — rule loaded when editing test files
- `.claude/agents/test-case-writer.md` — references format doc
- `.claude/agents/edge-case-finder.md` — references format doc
- `.claude/agents/test-researcher.md` — coverage matrix references case format
- `.claude/skills/write-test-cases/SKILL.md` — orchestrates researcher + writers
- `.claude/skills/new-rule/templates/TestTemplate.cs` — test runner template
- `tests/Houtamelo.Spire.Analyzers.Tests/AnalyzerTestBase.cs` — runtime implementation of format
- `CLAUDE.md` — "Test Conventions" section

## Analyzer conventions
How analyzers are structured, named, and implemented.
- `.claude/rules/analyzer-conventions.md` — rule loaded when editing analyzer files
- `.claude/agents/analyzer-implementer.md` — follows conventions
- `.claude/agents/code-reviewer.md` — checks conventions
- `.claude/skills/new-rule/templates/AnalyzerTemplate.cs` — analyzer template
- `.claude/skills/verify-rule/SKILL.md` — validates conventions
- `src/Houtamelo.Spire.Analyzers/Descriptors.cs` — descriptor registry
- `CLAUDE.md` — "Analyzer Conventions" section

## Rule creation workflow
The step-by-step process for adding a new rule.
- `CLAUDE.md` — "Adding a New Rule" section (steps 1–15)
- `.claude/skills/new-rule/SKILL.md` — scaffolding skill
- `.claude/skills/new-rule/templates/PlanTemplate.md` — plan template
- `.claude/skills/new-rule/templates/TestTemplate.cs` — test runner template
- `.claude/skills/new-rule/templates/AnalyzerTemplate.cs` — analyzer template
- `.claude/skills/new-rule/templates/DocTemplate.md` — docs template
- `.claude/agents/researcher.md` — step 1 (research)
- `.claude/agents/test-researcher.md` — step 6
- `.claude/agents/test-case-writer.md` — step 7
- `.claude/agents/analyzer-implementer.md` — step 9
- `.claude/agents/code-reviewer.md` — step 12
- `.claude/skills/verify-rule/SKILL.md` — step 14
- `.claude/skills/test/SKILL.md` — steps 8, 11 (run tests)
- `.claude/skills/syntax-tree/SKILL.md` — AST investigation (uses `dev-tools` MCP server)
- `tools/DevTools/` — MCP server: parse_syntax_tree, list_files, create_directory, remove, remove_recursive, copy, move
- `.mcp.json` — MCP server config: dev-tools, git (cyanheads/git-mcp-server), dotnet (Community.Mcp.DotNet), sherlock

## Documentation style
How all documentation (code comments, markdown, rule docs) should be written.
- `docs/style-guide.md` — canonical style guide (all agents must read this)
- `CLAUDE.md` — "Documentation Style" section (summary for agent context)
- `.claude/rules/documentation-conventions.md` — rule loaded when editing docs/rules/
- `.claude/agents/analyzer-implementer.md` — reads style guide in step 1
- `.claude/agents/code-reviewer.md` — enforces style guide in convention checks
- `.claude/agents/test-case-writer.md` — reads style guide in step 1
- `.claude/agents/doc-maintainer.md` — reads style guide in step 1
- `.claude/skills/new-rule/SKILL.md` — references style guide in step 9
- `.claude/skills/new-rule/templates/DocTemplate.md` — rule docs template

## Documentation maintenance and propagation
Session capture, feedback processing, doc updates, cross-file consistency, change history.
- `.claude/rules/cross-reference-check.md` — this file
- `.claude/skills/update-docs/SKILL.md` — propagates changes across related files
- `.claude/skills/update-history/SKILL.md` — triggers history-keeper agent
- `.claude/hooks/capture-session.sh` — captures transcripts/summaries
- `.claude/hooks/log-skill-usage.sh` — logs skill invocations
- `.claude/agents/doc-maintainer.md` — processes feedback
- `.claude/agents/history-keeper.md` — maintains `.claude/HISTORY.md`
- `.claude/skills/maintain-docs/SKILL.md` — orchestrates doc maintenance
- `.claude/HISTORY.md` — audit trail for infrastructure changes
- `feedback/` — summaries, transcripts, issue tracker, skill usage log

## Session and reflection
Session metadata and reflection workflow.
- `.claude/hooks/save-session-metadata.sh` — SessionStart hook
- `.claude/skills/reflect/SKILL.md` — reflection skill
- `.claude/settings.json` — hook configuration
- `session-reviews/` — reflection output files

## Source generator emitter workflow
How emitters are developed, tested (snapshot + behavioral), and implemented.
- `.claude/skills/new-emitter/SKILL.md` — scaffolding skill
- `.claude/agents/emitter-test-researcher.md` — coverage matrix
- `.claude/agents/emitter-snapshot-writer.md` — snapshot test pairs
- `.claude/agents/emitter-behavioral-writer.md` — behavioral types + tests
- `.claude/agents/emitter-implementer.md` — emitter implementation
- `tests/Houtamelo.Spire.SourceGenerators.Tests/GeneratorSnapshotTestBase.cs` — snapshot test framework
- `tests/Houtamelo.Spire.BehavioralTests/` — behavioral test project

## Pattern exhaustiveness analysis
Recursive pattern exhaustiveness checking via Maranget algorithm.
- `src/Houtamelo.Spire.PatternAnalysis/` — standalone library (domains, algorithm, resolution)
- `src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs` — entry point
- `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs` — matrix construction + pattern conversion
- `src/Houtamelo.Spire.PatternAnalysis/Algorithm/DecisionTreeBuilder.cs` — Maranget core loop
- `src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs` — type-to-domain mapping
- `src/Houtamelo.Spire.PatternAnalysis/Resolution/TypeHierarchyResolver.cs` — assembly walk for [EnforceExhaustiveness]
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs` — SPIRE009, delegates to ExhaustivenessChecker
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/CS8509Suppressor.cs` — delegates to ExhaustivenessChecker
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs` — uses ExhaustivenessChecker.CollectVariants
- `tests/Houtamelo.Spire.PatternAnalysis.Tests/ExhaustivenessTestBase.cs` — file-based test discovery with //~ markers
- `docs/superpowers/specs/2026-03-25-recursive-pattern-exhaustiveness-design.md` — design spec

## Generator-coupled analyzer workflow
How analyzers that run on generator output are developed and tested.
- `.claude/skills/new-coupled-analyzer/SKILL.md` — scaffolding skill
- `.claude/agents/coupled-analyzer-test-researcher.md` — coverage matrix
- `.claude/agents/coupled-analyzer-test-case-writer.md` — test cases
- `.claude/agents/coupled-analyzer-implementer.md` — analyzer implementation
- `tests/Houtamelo.Spire.SourceGenerators.Tests/GeneratorAnalyzerTestBase.cs` — test framework
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/AnalyzerDescriptors.cs` — descriptor registry

## Code fix workflow
How code fix providers are developed and tested.
- `.claude/skills/new-codefix/SKILL.md` — scaffolding skill
- `.claude/agents/codefix-test-researcher.md` — coverage matrix
- `.claude/agents/codefix-test-case-writer.md` — before/after pairs
- `.claude/agents/codefix-implementer.md` — code fix implementation
- `tests/Houtamelo.Spire.SourceGenerators.Tests/CodeFixTestBase.cs` — test framework
- `src/Houtamelo.Spire.CodeFixes/` — code fix providers

## Benchmark infrastructure
How benchmarks are structured, run, and results generated.
- `benchmarks/Houtamelo.Spire.Benchmarks/Program.cs` — entry point, RESULTS.md generation
- `benchmarks/Houtamelo.Spire.Benchmarks/Helpers/BenchmarkConstants.cs` — shared N constant
- `benchmarks/Houtamelo.Spire.Benchmarks/Types/` — union type declarations ([BenchmarkUnion] + hand-written)
- `benchmarks/Houtamelo.Spire.Benchmarks/Benchmarks/` — hand-written benchmark classes
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/BenchmarkUnionGenerator.cs` — [BenchmarkUnion] generator
- `src/Houtamelo.Spire.Analyzers/SourceGenerators/Attributes/BenchmarkAttributeSource.cs` — attribute source
- `docs/benchmark-results/` — auto-generated RESULTS_{job}.md files
- `README.md` — layout strategy comparison table
- `CLAUDE.md` — benchmark build commands

## Project structure and build
Project layout, build configuration, conventions.
- `CLAUDE.md` — "Project Structure", "Analyzer Conventions", "Build Commands" sections
- `.claude/rules/analyzer-conventions.md` — target framework, dependencies
- `.claude/rules/test-conventions.md` — test framework, target
- `src/Houtamelo.Spire/Houtamelo.Spire.csproj`
- `src/Houtamelo.Spire.Core/Houtamelo.Spire.Core.csproj`
- `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj`
- `src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj`
- `src/Houtamelo.Spire.PatternAnalysis/Houtamelo.Spire.PatternAnalysis.csproj`
- `tests/Houtamelo.Spire.Analyzers.Tests/Houtamelo.Spire.Analyzers.Tests.csproj`
- `tests/Houtamelo.Spire.PatternAnalysis.Tests/Houtamelo.Spire.PatternAnalysis.Tests.csproj`
- `tests/Houtamelo.Spire.SourceGenerators.Tests/Houtamelo.Spire.SourceGenerators.Tests.csproj`
- `tests/Houtamelo.Spire.BehavioralTests/Spire.BehavioralTests.csproj`
- `benchmarks/Houtamelo.Spire.Benchmarks/Houtamelo.Spire.Benchmarks.csproj`
