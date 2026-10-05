---
name: verify-rule
description: Validate that a rule has all required files (analyzer, descriptor, test cases, docs) and passes tests. Use after implementing or modifying a rule to confirm completeness.
---

# Verify a Rule is Complete

Use the rule ID supplied with the skill invocation.

Run each check below and report pass/fail for each.

## 1. Analyzer file exists
- Find `src/Houtamelo.Spire.Analyzers/Rules/<RuleId>*Analyzer.cs`
- Verify it has `[DiagnosticAnalyzer(LanguageNames.CSharp)]`
- Verify it references a descriptor from `Descriptors.cs`

## 2. Descriptor registered
- Search `src/Houtamelo.Spire.Analyzers/Descriptors.cs` for `id: "<RuleId>"`

## 3. Test structure exists
- Verify test folder exists: `tests/Houtamelo.Spire.Analyzers.Tests/<RuleId>/`
- Verify test runner exists: `tests/Houtamelo.Spire.Analyzers.Tests/<RuleId>/<RuleId>Tests.cs`
- Verify test runner inherits `AnalyzerTestBase<...>` and overrides `RuleId`
- Verify case folder exists: `tests/Houtamelo.Spire.Analyzers.Tests/<RuleId>/cases/`
- Verify shared preamble exists: `tests/Houtamelo.Spire.Analyzers.Tests/<RuleId>/cases/_shared.cs`
- Count `should_fail` case files (need >= 3): files starting with `//@ should_fail`
- Count `should_pass` case files (need >= 3): files starting with `//@ should_pass`
- Verify all case files have proper headers (`//@ should_fail` or `//@ should_pass` on line 1)
- Verify all `should_fail` files have at least one `//~ ERROR` or `//~^ ERROR` marker

## 4. Documentation exists
- Look for `docs/rules/<RuleId>.md`
- Verify it has all 5 required sections per `.codex/rules/documentation-conventions.md`: Title, Property table, Description, Examples, When to Suppress

## 5. Tests pass
Use the `dev-tools` MCP `dotnet_test` tool with filter `FullyQualifiedName~<RuleId>`, or run the equivalent CLI command.

## 6. Summary
Print a pass/fail checklist with counts.

## Constraints

- Do NOT create missing files — only report what's missing
- Do NOT modify existing code or documentation
- This is a read-only verification — if checks fail, report them and stop
- Do NOT lower the thresholds (3 detection, 3 false-positive test cases minimum)
