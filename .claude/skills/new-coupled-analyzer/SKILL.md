---
name: new-coupled-analyzer
description: Scaffold descriptor, test runner, and test folder for a new generator-coupled analyzer (TDD — tests before analyzer). Use when adding a diagnostic that runs on generator output.
disable-model-invocation: true
user-invocable: true
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: [RuleId] [RuleTitle]
hooks:
  Stop:
    - command: "bash .claude/hooks/log-skill-usage.sh new-coupled-analyzer"
---

# Scaffold a New Generator-Coupled Analyzer (TDD)

Arguments: `$ARGUMENTS`
Example: `/new-coupled-analyzer SPIRE016 "Variant field type mismatch"`

## Validation

1. Parse `$1` as the rule ID and everything after as the title
2. Verify the rule ID matches `SPIRE` followed by a numeric ID — reject otherwise
3. Verify no existing descriptor matches `id: "$1"` in `src/Houtamelo.Spire.Analyzers/SourceGenerators/AnalyzerDescriptors.cs` — abort if duplicate

## Scaffolding (TDD order — tests and descriptor BEFORE analyzer)

4. Add descriptor to `src/Houtamelo.Spire.Analyzers/SourceGenerators/AnalyzerDescriptors.cs`
   - Field name: `{RuleId}_{TitlePascalCase}`
   - Follow existing descriptor pattern (see `SPIRE009`, `SPIRE011`, etc.)
5. Create category folder from the rule title: `tests/Houtamelo.Spire.SourceGenerators.Tests/{CategoryName}/`
   - CategoryName derived from title in PascalCase (e.g., "Variant field type mismatch" → `VariantFieldTypeMismatch`)
6. Create test case folder: `tests/Houtamelo.Spire.SourceGenerators.Tests/{CategoryName}/cases/`
   - Note: coupled analyzer test cases are **self-contained** — no `_shared.cs` preamble. Each case file includes its own `[DiscriminatedUnion]` declaration and usage code.
7. Create test runner: `tests/Houtamelo.Spire.SourceGenerators.Tests/{CategoryName}/{CategoryName}Tests.cs`
   - Inherits `GeneratorAnalyzerTestBase`
   - Override `Category` to return `"{CategoryName}"`
   - Override `GetAnalyzers()` to return the analyzer instance
   - Override `IsRelevantDiagnostic(d)` to filter by `{RuleId}`
   - Reference pattern: `tests/Houtamelo.Spire.SourceGenerators.Tests/Exhaustiveness/ExhaustivenessTests.cs`
8. Create docs: `docs/rules/{RuleId}.md`

## Verify

9. Run `dotnet build` — must succeed with zero warnings
10. Report all created files

## What this skill does NOT do

- **Does NOT create the analyzer file** — that is the implementer's job
- **Does NOT create test case files** — the lead orchestrates writers
- Only scaffolds the structure for the pipeline

## Constraints

- Do NOT implement detection logic — the analyzer file is not created at this stage
- Do NOT modify existing descriptors (only append the new one)
- Do NOT proceed if validation fails — report the error and stop
