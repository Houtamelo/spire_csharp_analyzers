---
paths:
  - "tests/**/*.cs"
---

# Test Conventions

## TDD Ordering

- Tests are written BEFORE the analyzer implementation
- The lead scaffolds the test runner, descriptor, and shared preamble
- The lead **spawns the test-case-writer agent** to populate case files (keeps the lead's context clean)
- After test cases are written, run `dotnet test`:
  - Detection tests (ShouldFail) should FAIL — no analyzer exists yet
  - False-positive tests (ShouldPass) should PASS — no diagnostic is emitted
- Only then spawn the analyzer-implementer agent to make all tests pass

## Self-Contained Test Cases

- Each test case is a **single `.cs` file** in `{RuleId}/cases/` — adding a test = adding one file
- No `[InlineData]` entries, no test runner edits needed
- Cases are discovered at runtime by `AnalyzerTestBase<TAnalyzer>`
- Shared type definitions and `global using` directives go in `cases/_shared.cs` (compiled as a separate syntax tree in the same compilation)
- Usings in `_shared.cs` must be `global using` so they apply to all case files
- Case files can have their own `using` directives freely
- Case files are excluded from project compilation, read at runtime by the test framework

### File format

See `docs/test-case-format.md` for the full format reference (headers, error markers, file naming, validation rules).

## Test Runner Structure

The test runner inherits `AnalyzerTestBase<TAnalyzer>` — all logic is in the base class:

```csharp
public class {RuleId}Tests : AnalyzerTestBase<{AnalyzerType}>
{
    protected override string RuleId => "{RuleId}";
}
```

- Minimum: 3 detection cases (should_fail) and 3 false-positive cases (should_pass)

## Edge Cases

- Test against both `struct` and `record struct` where applicable
- Include edge cases: generics, nested structs, partial classes, ref structs
