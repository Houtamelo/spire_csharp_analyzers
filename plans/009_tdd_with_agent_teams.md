# Plan 009: Introducing TDD Philosophy with Agent Teams

## Context

This project is entirely agent-orchestrated. Two observations from early sessions motivate this plan:

1. **Agents produce better results when given concrete, verifiable targets.** "Make these tests pass" is sharper than "implement this rule according to the plan."
2. **Agents don't always verify their own work.** But simply asking them "did you actually test X?" often triggers self-correction — the question itself is the intervention.

This plan introduces test-driven development (TDD) as the standard workflow for rule implementation, and designs an agent team structure where the lead enforces quality through pointed questioning and automated gates.

## Why TDD Fits This Project

### The testing boundary is ideal

Roslyn analyzer tests have a perfectly clean input/output boundary:
- **Input**: a string of C# source code
- **Output**: a list of diagnostics with IDs, messages, and locations
- No mocking, no I/O, no external dependencies
- The existing `AnalyzerVerifier<T>` infrastructure handles all the plumbing

### Tests encode the spec

Plan 005 already defines SAS001's behavior as two tables: "Flagged" and "NOT flagged." Each row maps directly to a test case. Writing tests first is a mechanical translation of the spec — no design ambiguity.

### Edge cases are surfaced early

Struct analysis involves subtle cases: record structs, generics, nested types, multidimensional arrays, stackalloc, collection expressions. Writing tests first forces enumeration of these cases before the implementer gets tunnel vision on the happy path.

## The TDD Workflow

### Steps for implementing a single rule

```
1. Human writes/approves the rule plan (plans/005, etc.)
2. Lead writes the tests based on the plan's spec tables
3. Lead adds the descriptor to Descriptors.cs
4. Lead creates the attribute/marker type if needed
5. Run `dotnet test` → detection tests FAIL, false-positive tests PASS
   - Detection tests (ShouldReport) fail because no analyzer exists yet
   - False-positive tests (ShouldNotReport) pass because no diagnostic is emitted
   - This confirms the tests are wired correctly
6. Lead spawns an implementer teammate to write the analyzer
7. Implementer's only job: make ALL tests pass
8. TaskCompleted hook runs `dotnet test --filter SAS00X` before accepting
9. Lead asks verification questions
10. Lead runs /verify-rule to confirm completeness
11. Write docs
```

Steps 2-5 happen before any analyzer code exists. The tests define the contract.

## Agent Team Structure

### Roles

```
Lead (Opus): Coordination, test writing, verification questioning, reviews
└── Implementer (Sonnet): SAS00X analyzer — make all tests pass
```

One rule, one team, one implementer. The lead writes tests and descriptors before spawning the implementer. This means:
- The implementer starts with a clear, runnable target
- The lead retains ownership of shared files (`Descriptors.cs`, test files)
- The implementer only touches the analyzer file — no file conflicts

### Implementer Constraints

The implementer's spawn prompt must include these constraints:

- **Do NOT edit test files** — Tests are the contract. If a test fails, fix the analyzer, not the test.
- **Do NOT edit `Descriptors.cs`** — The lead already added the descriptor.
- **Do NOT edit files outside `src/Spire.Analyzers/`** — Your scope is analyzer implementation only.
- If you believe a test is wrong, message the lead explaining why — do not modify it yourself.

### Lead Responsibilities

1. **Write tests first** — Translate spec tables into test cases using `AnalyzerVerifier<T>` and `{|DiagnosticId:code|}` markup.
2. **Add descriptors** — Add entries to `Descriptors.cs` before spawning the implementer.
3. **Create attributes/markers** — If the rule needs a new attribute, create it before spawning.
4. **Verify expected test state** — Run `dotnet test` to confirm detection tests fail and false-positive tests pass.
5. **Question teammate on completion** — When the implementer marks a task as done, ask pointed questions (see below).
6. **Run /verify-rule** — Final completeness check.

### The Questioning Protocol

When the implementer reports completion, the lead asks questions drawn from the task's acceptance criteria. The purpose is not to catch lies — it's to trigger self-reflection. Examples:

- "Your task says the analyzer should handle record structs. Which test covers that?"
- "Does your implementation use `CompilationStartAction` to resolve the attribute type?"
- "What happens if the array has a constant zero size — does your analyzer skip it?"
- "Did the build complete with zero warnings?"

The lead doesn't need domain expertise to ask these — the questions come from the task description and the project's conventions (CLAUDE.md, `.claude/rules/`).

If the implementer realizes a gap while answering, it fixes the issue before the lead accepts. If the implementer confidently answers but the `TaskCompleted` hook's `dotnet test` fails, the mechanical gate catches it.

### Automated Quality Gates

#### TaskCompleted hook

Runs the full test suite before allowing a task to be marked complete:

```bash
#!/bin/bash
INPUT=$(cat)
TASK_SUBJECT=$(echo "$INPUT" | jq -r '.task_subject')

if ! dotnet test 2>&1; then
  echo "Test suite is not passing. Fix all failing tests before completing: $TASK_SUBJECT" >&2
  exit 2
fi

exit 0
```

This runs **all** tests, not just the current rule's. If the implementer breaks an existing rule's tests (e.g., by modifying a shared utility), this catches it. No amount of confident self-reporting can bypass a red test suite.

#### TeammateIdle hook (optional)

Could verify that the build is clean before allowing a teammate to go idle:

```bash
#!/bin/bash
if ! dotnet build --no-restore 2>&1 | tail -5; then
  echo "Build has errors. Fix before stopping." >&2
  exit 2
fi
exit 0
```

## Test Case File Structure

Each rule gets its own folder in the test project. Test cases are real `.cs` files, not string literals.

### Directory layout

```
tests/Spire.Analyzers.Tests/
  SAS001/
    SAS001Tests.cs          ← test runner: lists cases and expected behavior
    cases/
      _shared.cs            ← shared preamble (attribute, struct definitions, usings)
      NonEmptyArray.cs      ← one case per file
      VariableSizedArray.cs
      MultidimensionalArray.cs
      ZeroLengthArray.cs
      ArrayWithInitializer.cs
      ...
  SAS002/
    SAS002Tests.cs
    cases/
      _shared.cs
      ...
```

### Excluding case files from compilation

Case files are test inputs, not code to compile. Exclude them in the test `.csproj`:

```xml
<ItemGroup>
  <Compile Remove="**/cases/**" />
  <None Include="**/cases/**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

This ensures case files are copied to the output directory (so tests can read them at runtime) but are not compiled as part of the test project.

### Shared preamble

Each rule's `cases/_shared.cs` contains the types and usings needed by that rule's cases:

```csharp
// cases/_shared.cs for SAS001
using System;
using Spire.Analyzers;

[RequireInitialization]
public struct MarkedStruct
{
    public int Value;
    public MarkedStruct(int value) { Value = value; }
}

public struct UnmarkedStruct
{
    public int Value;
}
```

The test runner prepends this preamble to each case file's content before passing it to the verifier.

### Case files

Each case file is a standalone `.cs` file containing a single scenario. It uses `{|SAS001:code|}` markup for expected diagnostic locations:

```csharp
// cases/NonEmptyArray.cs — detection case (ShouldReport)
class Test
{
    void M()
    {
        var arr = {|SAS001:new MarkedStruct[5]|};
    }
}
```

```csharp
// cases/ZeroLengthArray.cs — false-positive case (ShouldNotReport)
class Test
{
    void M()
    {
        var arr = new MarkedStruct[0];
    }
}
```

Case files have full IDE support: syntax highlighting, error squiggles (for the C# portion — the markup syntax won't be recognized, but that's minimal noise), and autocomplete.

### Test runner

The test file lists cases and their expected behavior:

```csharp
// SAS001/SAS001Tests.cs
using Xunit;

namespace Spire.Analyzers.Tests;

public class SAS001Tests
{
    // Cases that SHOULD trigger SAS001
    [Theory]
    [InlineData("NonEmptyArray")]
    [InlineData("VariableSizedArray")]
    [InlineData("MultidimensionalArray")]
    [InlineData("ConstantNonZeroSize")]
    [InlineData("Stackalloc")]
    public async Task ShouldReportSAS001(string caseName)
    {
        var source = LoadCase("SAS001", caseName);
        await AnalyzerVerifier<SAS001ArrayOfNonDefaultableStructAnalyzer>
            .VerifyAnalyzerAsync(source);
    }

    // Cases that should NOT trigger SAS001
    [Theory]
    [InlineData("ZeroLengthArray")]
    [InlineData("ConstantZeroSize")]
    [InlineData("ArrayWithInitializer")]
    [InlineData("ImplicitlyTypedArray")]
    [InlineData("JaggedArray")]
    [InlineData("UnmarkedStruct")]
    [InlineData("EmptyCollectionExpression")]
    public async Task ShouldNotReportSAS001(string caseName)
    {
        var source = LoadCase("SAS001", caseName);
        await AnalyzerVerifier<SAS001ArrayOfNonDefaultableStructAnalyzer>
            .VerifyAnalyzerAsync(source);
    }

    private static string LoadCase(string ruleId, string caseName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, ruleId, "cases");
        var shared = File.ReadAllText(Path.Combine(dir, "_shared.cs"));
        var caseSource = File.ReadAllText(Path.Combine(dir, $"{caseName}.cs"));
        return shared + Environment.NewLine + caseSource;
    }
}
```

### Why this structure works

- **Each case is a real `.cs` file** — IDE support, reviewable independently, easy to add.
- **Shared preamble avoids duplication** — attribute and struct definitions written once per rule.
- **Test file is a manifest** — two lists (ShouldReport, ShouldNotReport) make the contract visible at a glance.
- **Adding a case = creating a file + adding one `[InlineData]` line** — minimal friction.
- **Agent-friendly** — the implementer can read individual case files to understand exactly what to handle.

## What TDD Does and Doesn't Solve

### What it solves

| Problem | How TDD addresses it |
|---------|---------------------|
| Agent implements wrong behavior | Tests define correct behavior upfront — implementation must match |
| Agent skips edge cases | Edge cases are enumerated as test cases before implementation starts |
| Agent claims completion without verification | `TaskCompleted` hook runs tests mechanically — can't be bypassed |
| Agent loses track of requirements | Tests ARE the requirements — always available, always runnable |
| Vague success criteria | "All tests pass" is unambiguous |

### What it doesn't solve

| Problem | Why TDD doesn't help | Mitigation |
|---------|---------------------|------------|
| Tests themselves are wrong | Tests can encode incorrect expectations | Human reviews test cases before implementation; lead questions test assumptions |
| Missing test cases | Can't catch what isn't tested | Minimum thresholds (3 positive, 3 negative per rule); lead prompts for edge cases |
| Non-functional issues (performance, code style) | Tests only verify behavior | `/verify-rule` checks structural conventions; code review |
| Docs quality | Not testable | Separate docs task after implementation |

## Changes to Existing Conventions

### CLAUDE.md

Update the "Adding a New Rule" section to reflect TDD order:

```
1. Read the rule's plan in plans/ (if one exists)
2. Add descriptor to Descriptors.cs
3. Create test folder, shared preamble, case files, and test runner  ← BEFORE analyzer
4. Run dotnet test — confirm detection tests fail, false-positive tests pass
5. Create analyzer in src/Spire.Analyzers/Analyzers/
6. Run dotnet test — confirm ALL tests pass
7. Create docs in docs/rules/
```

Update the "Project Structure" section to include test case layout:

```
tests/Spire.Analyzers.Tests/
  {RuleId}/
    {RuleId}Tests.cs              # Test runner with case lists
    cases/
      _shared.cs                  # Shared preamble (types, usings)
      {CaseName}.cs               # One file per test case
```

### .claude/rules/test-conventions.md

Rewrite to reflect file-based test cases and TDD ordering:

```markdown
- Tests are written BEFORE the analyzer implementation (TDD)
- After writing tests, run `dotnet test`:
  - Detection tests (ShouldReport) should FAIL — no analyzer exists yet
  - False-positive tests (ShouldNotReport) should PASS — no diagnostic is emitted
- Only then implement the analyzer to make all tests pass
- Each rule gets its own folder: `{RuleId}/` with a `cases/` subdirectory
- Each test case is a standalone `.cs` file in `cases/`
- Shared type definitions go in `cases/_shared.cs` (prepended to each case at runtime)
- Use `{|DiagnosticId:code|}` markup in case files for expected diagnostic spans
- The test runner uses `[Theory]` + `[InlineData("CaseName")]` to list cases
- Two theory methods per rule: `ShouldReport{RuleId}` and `ShouldNotReport{RuleId}`
- Minimum: 3 detection cases (ShouldReport) and 3 false-positive cases (ShouldNotReport)
```

### .claude/settings.json

Add `TaskCompleted` hook for test verification (when agent teams are used):

```json
{
  "hooks": {
    "TaskCompleted": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash .claude/hooks/verify-task-tests.sh"
          }
        ]
      }
    ]
  }
}
```

## Implementation Checklist

- [ ] Update "Adding a New Rule" and "Project Structure" sections in CLAUDE.md
- [ ] Rewrite `.claude/rules/test-conventions.md` for file-based cases and TDD ordering
- [ ] Add `<Compile Remove>` and `<None Include>` to test `.csproj` for `**/cases/**`
- [ ] Create a shared `LoadCase` helper (in `Verifiers.cs` or a new test utility)
- [ ] Create `.claude/hooks/verify-task-tests.sh` (TaskCompleted hook)
- [ ] Optionally create `.claude/hooks/verify-build-clean.sh` (TeammateIdle hook)
- [ ] Add hooks to `.claude/settings.json`
- [ ] Test the workflow manually with SAS001 (write tests + cases first, then implement)
