---
name: new-codefix
description: Scaffold a code fix provider and test structure for an existing diagnostic. Use when adding a code fix for a diagnostic that already has passing tests.
disable-model-invocation: true
user-invocable: true
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: [RuleId]
hooks:
  Stop:
    - command: "bash .claude/hooks/log-skill-usage.sh new-codefix"
---

# Scaffold a New Code Fix Provider

Arguments: `$ARGUMENTS`
Example: `/new-codefix SPIRE009`

## Validation

1. Parse `$1` as the rule ID — reject if missing
2. Verify the diagnostic exists:
   - Check `src/Houtamelo.Spire.Analyzers/Descriptors.cs` for `id: "$1"` OR
   - Check `src/Houtamelo.Spire.Analyzers/SourceGenerators/AnalyzerDescriptors.cs` for `id: "$1"`
   - Abort if not found in either location
3. Verify no existing code fix already handles this rule:
   - Grep `src/Houtamelo.Spire.CodeFixes/` for `"$1"` in `FixableDiagnosticIds` — warn if found (may be adding to existing fix)

## Scaffolding

4. Create code fix stub: `src/Houtamelo.Spire.CodeFixes/{RuleId}{ShortName}CodeFix.cs`
   - ShortName derived from the diagnostic's title
   - Minimal `CodeFixProvider` with `FixableDiagnosticIds` returning `{RuleId}`
   - Empty `RegisterCodeFixesAsync` — to be implemented by the codefix-implementer
5. Register the code fix in the test runner:
   - Edit `tests/Houtamelo.Spire.SourceGenerators.Tests/CodeFix/CodeFixTests.cs`
   - Add the new code fix to `GetCodeFixes()` return value
   - Add the relevant analyzer to `GetAnalyzers()` if not already present
6. Create template test case folder: `tests/Houtamelo.Spire.SourceGenerators.Tests/CodeFix/cases/{RuleId}_Example/`
   - Create placeholder `before.cs` and `after.cs` with TODO comments

## Verify

7. Run `dotnet build` — must succeed
8. Report all created files

## What this skill does NOT do

- **Does NOT implement the code fix logic** — that is the implementer's job
- **Does NOT write real test cases** — the lead orchestrates writers
- Only scaffolds the structure for the pipeline

## Constraints

- Do NOT implement fix logic — the stub has an empty `RegisterCodeFixesAsync`
- Do NOT modify the diagnostic/analyzer — only create the code fix provider
- Do NOT proceed if validation fails — report the error and stop
