# Plan 012: Self-Contained Test Cases

**Status**: Ready for implementation
**Goal**: Make each test case a fully self-contained file — no `[InlineData]` entries, no test runner edits. Adding a test = adding one `.cs` file.

---

## Problem

The current test system requires two steps to add a test case:
1. Create a `.cs` file in `{RuleId}/cases/`
2. Add an `[InlineData("CaseName")]` entry to the test runner

This coupling causes:
- Silent gaps when a file exists but the `[InlineData]` entry is missing (happened during SPIRE001)
- Agent budget waste — each case requires a `Write` + `Edit` (two tool calls instead of one)
- Merge conflicts when multiple agents write to the same test runner file
- Prevents parallel test-case-writer agents (all need to edit the same file)

## Design

### Test case file format

Every `.cs` file in `{RuleId}/cases/` (except `_shared.cs`) is a test case. The file must start with two header comments:

```csharp
//@ should_fail
// Detection: new T[n] allocates array of [MustBeInit] struct without initialization
public class Detect_1DConstantSize_LocalVariable
{
    public void Method()
    {
        var arr = new MustInitStruct[5]; //~ ERROR
    }
}
```

```csharp
//@ should_pass
// NoReport: array with explicit initializer — all elements are provided
public class NoReport_1DWithInitializer
{
    public void Method()
    {
        var arr = new MustInitStruct[] { new(1), new(2) };
    }
}
```

**Line 1**: `//@ should_fail` or `//@ should_pass` — required, determines test expectation.
**Line 2**: `// {Description}` — required, explains why the test should pass/fail.

### Error location markers (should_fail cases only)

| Marker | Meaning |
|--------|---------|
| `//~ ERROR` | Diagnostic expected on **this line** |
| `//~^ ERROR` | Diagnostic expected on the **previous line** |

The marker indicates that the analyzer should report a diagnostic whose span includes the marked line. The column/span is determined by finding the diagnostic whose location falls on that line.

### Validation rules

The test infrastructure must enforce:

1. **should_pass + error comments** → Test throws exception: `"File {name} is marked should_pass but contains error markers"`
2. **should_fail + no error comments** → Test throws exception: `"File {name} is marked should_fail but has no error markers"`
3. **should_fail + multiple error comments + any unflagged** → Test throws exception: `"File {name} has {n} error markers but only {m} diagnostics were reported"`
   - i.e., every `//~ ERROR` / `//~^ ERROR` must correspond to an actual diagnostic. If any marker did not produce a diagnostic, the test fails.
4. **No header** → Test throws exception: `"File {name} is missing //@ should_fail or //@ should_pass header"`

### Test discovery

The test runner discovers cases at runtime by globbing `{RuleId}/cases/*.cs`, excluding `_shared.cs`. No `[InlineData]` needed.

```csharp
public class SPIRE001Tests : AnalyzerTestBase<SPIRE001ArrayOfMustBeInitStructAnalyzer>
{
    protected override string RuleId => "SPIRE001";
}
```

That's the entire test runner file for a rule. All logic lives in the base class.

### Base class: `AnalyzerTestBase<TAnalyzer>`

Located in `tests/Spire.Analyzers.Tests/AnalyzerTestBase.cs`. Responsibilities:

1. **Discover cases**: Glob `{RuleId}/cases/*.cs`, exclude `_shared.cs`
2. **Parse headers**: Extract `should_fail`/`should_pass` and description from lines 1-2
3. **Parse error markers**: Find all `//~ ERROR` and `//~^ ERROR` comments, compute expected diagnostic lines
4. **Strip all comments**: After parsing headers and error markers, remove **all** comments (single-line `//...` and multi-line `/*...*/`) from the combined source before passing it to Roslyn. Replace each comment's content with spaces to preserve line numbers and column offsets. This prevents analyzers from reading `//~ ERROR` markers or any other comment content.
6. **Build expected diagnostics**: For `should_fail` cases, create `DiagnosticResult` entries with the expected line numbers
7. **Run verification**: Use the Roslyn testing framework with the comment-stripped source
8. **Validate**: Apply the validation rules above

Provides two `[Theory]` methods via `MemberData`:
- `ShouldFail(string caseName)` — runs all `should_fail` cases
- `ShouldPass(string caseName)` — runs all `should_pass` cases

The `MemberData` source is a static method that discovers and classifies files at runtime.

### Shared preamble (`_shared.cs`)

Still prepended to every case file. No change here — `_shared.cs` does not have header comments and is not a test case.

### Line number calculation

Since `_shared.cs` is prepended, the line numbers in error markers refer to lines **within the case file**, but the actual source passed to Roslyn has the shared preamble prepended. The base class must offset error marker line numbers by the number of lines in `_shared.cs`.

Example:
- `_shared.cs` is 24 lines
- Case file line 7 has `//~ ERROR`
- Expected diagnostic line in combined source: 24 + 1 (newline separator) + 7 = **32**

### Migration from current format

The existing `{|SPIRE001:code|}` markup and `[InlineData]` pattern must be migrated for SPIRE001:

1. Each case file gets `//@ should_fail` or `//@ should_pass` + description as lines 1-2
2. `{|SPIRE001:code|}` markup is replaced with plain code + `//~ ERROR` on the same line
3. `[InlineData]` entries are removed from the test runner
4. The test runner is replaced with the 4-line version inheriting `AnalyzerTestBase`

---

## Files to create/modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Spire.Analyzers.Tests/AnalyzerTestBase.cs` | Create | Base class with discovery, parsing, validation, verification |
| `tests/Spire.Analyzers.Tests/SPIRE001/SPIRE001Tests.cs` | Rewrite | 4-line class inheriting `AnalyzerTestBase` |
| `tests/Spire.Analyzers.Tests/SPIRE001/cases/*.cs` | Migrate | Add headers, replace `{|..|}` markup with `//~ ERROR` |
| `tests/Spire.Analyzers.Tests/Verifiers.cs` | Keep | `AnalyzerVerifier` still used internally by `AnalyzerTestBase` |
| `tests/Spire.Analyzers.Tests/TestCaseLoader.cs` | Remove or keep | `TestCaseLoader.LoadCase` may be absorbed into `AnalyzerTestBase` |
| `.claude/skills/new-rule/templates/TestTemplate.cs` | Rewrite | New minimal template |
| `CLAUDE.md` | Update | Test conventions section |
| `.claude/agents/test-case-writer.md` | Update | New file format, no `[InlineData]` step |
| `.claude/agents/analyzer-implementer.md` | Update | Reference new format |
| `.claude/agents/code-reviewer.md` | Update | Reference new format |
| `.claude/agents/test-writer.md` | Update | Reference new format |
| `.claude/rules/test-conventions.md` | Update | New format |
| `.claude/skills/verify-rule/SKILL.md` | Update | No `[InlineData]` count check, check for headers instead |
| `.claude/skills/write-test-cases/SKILL.md` | Update | New format, no test runner editing |

---

## Implementation order

1. Create `AnalyzerTestBase.cs` with discovery, parsing, validation, and verification logic
2. Migrate one SPIRE001 case file (e.g. `Detect_1DConstantSize_LocalVariable.cs`) as a proof of concept
3. Rewrite `SPIRE001Tests.cs` to use the new base class
4. Run tests — verify the one migrated case passes
5. Migrate all remaining SPIRE001 case files
6. Run full test suite — all 87+ tests pass
7. Update templates, agents, skills, rules, and CLAUDE.md
8. Remove `TestCaseLoader` if fully replaced

---

## Example: before and after

### Before (current)

**Test runner** (`SPIRE001Tests.cs`):
```csharp
[Theory]
[InlineData("Detect_1DConstantSize_LocalVariable")]
[InlineData("Detect_1DConstantSize_FieldInitializer")]
// ... 60+ more entries ...
public async Task ShouldReportSPIRE001(string caseName)
{
    var source = TestCaseLoader.LoadCase("SPIRE001", caseName);
    await AnalyzerVerifier<SPIRE001ArrayOfMustBeInitStructAnalyzer>.VerifyAsync(source);
}
```

**Case file**:
```csharp
public class Detect_1DConstantSize_LocalVariable
{
    public void Method()
    {
        var arr = {|SPIRE001:new MustInitStruct[5]|};
    }
}
```

### After (new)

**Test runner** (`SPIRE001Tests.cs`):
```csharp
using Spire.Analyzers.Rules;

public class SPIRE001Tests : AnalyzerTestBase<SPIRE001ArrayOfMustBeInitStructAnalyzer>
{
    protected override string RuleId => "SPIRE001";
}
```

**Case file**:
```csharp
//@ should_fail
// new T[n] allocates array of [MustBeInit] struct without initialization
public class Detect_1DConstantSize_LocalVariable
{
    public void Method()
    {
        var arr = new MustInitStruct[5]; //~ ERROR
    }
}
```

---

## Implementation notes

### Current infrastructure (what exists today)

- `tests/Spire.Analyzers.Tests/Verifiers.cs` contains:
  - `AnalyzerVerifier<TAnalyzer>` — wraps `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>`, adds `AnalyzerAssemblyReference` (so test code can resolve `Spire.Analyzers` types like `[MustBeInit]`), uses `ReferenceAssemblies.Net.Net80`
  - `TestCaseLoader` — reads `_shared.cs` + case file, concatenates them with `Environment.NewLine`
- `AnalyzerVerifier.VerifyAsync(string source)` takes source with `{|DiagnosticId:code|}` markup — Roslyn's `CSharpAnalyzerTest` parses that markup internally
- Test project targets `net10.0` with LangVersion 14, has `<NoWarn>NU1701;AD0001;xUnit1003</NoWarn>`
- Case files are excluded from compilation via the `.csproj` (they're read as content at runtime via `AppContext.BaseDirectory`)
- SPIRE001 has 87 case files (65 should_fail + 22 should_pass, approximate split based on Detect_ vs NoReport_ prefixes)

### Comment stripping approach

Use Roslyn's own lexer to strip comments reliably (don't regex-parse C# comments):
1. Parse the combined source with `CSharpSyntaxTree.ParseText()`
2. Walk all trivia, replace `SingleLineCommentTrivia`, `MultiLineCommentTrivia`, `SingleLineDocumentationCommentTrivia`, `MultiLineDocumentationCommentTrivia` with equivalent-length whitespace
3. Reconstruct the source string

### AnalyzerTestBase design sketch

```csharp
public abstract class AnalyzerTestBase<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    protected abstract string RuleId { get; }

    // xUnit discovers these via [Theory] + [MemberData]
    public static IEnumerable<object[]> ShouldFailCases() => DiscoverCases("should_fail");
    public static IEnumerable<object[]> ShouldPassCases() => DiscoverCases("should_pass");

    [Theory]
    [MemberData(nameof(ShouldFailCases))]
    public async Task ShouldFail(string caseName) { /* ... */ }

    [Theory]
    [MemberData(nameof(ShouldPassCases))]
    public async Task ShouldPass(string caseName) { /* ... */ }
}
```

**Problem**: `MemberData` calling a static method can't access the abstract `RuleId` property. Options:
- Use `[MemberData]` with a static method that takes `RuleId` as a parameter — but xUnit `MemberData` doesn't support instance context
- Use `ClassData` with a custom enumerator
- Use xUnit's `IClassFixture<T>` pattern
- Discover in the constructor and store, use `[MemberData(nameof(...))]` pointing to a static field populated by a derived-class static initializer
- **Simplest**: Make `ShouldFailCases`/`ShouldPassCases` abstract or virtual in the derived class, or use a convention-based approach where the derived class sets a static `RuleId` field

This needs careful design — investigate xUnit's `MemberData` with inheritance. The key constraint is that xUnit resolves `MemberData` on the concrete test class, so if `ShouldFailCases()` is defined on the base class, it needs access to `RuleId` somehow.

### Key gotcha: _shared.cs comment stripping

`_shared.cs` contains `using` directives and type definitions. If we strip all comments from the combined source (shared + case), any comments in `_shared.cs` are also stripped. This is fine — `_shared.cs` shouldn't contain comments that affect compilation. But if someone adds a `// TODO` or documentation comment to `_shared.cs`, it will be silently stripped. This is acceptable.

### Migration: Detect_ prefix → should_fail, NoReport_ prefix → should_pass

Current naming convention uses `Detect_` for should_fail and `NoReport_` for should_pass. The file names don't need to change — the `//@ should_*` header is authoritative. The filename prefix becomes just a human-readable convention.

## Resolved decisions

1. **Column precision**: Line-only. No column or span verification.
2. **Multiple diagnostics on one line**: One `//~ ERROR` marker = one expected diagnostic on that line. If the analyzer happens to emit two diagnostics on the same line, that's fine — we only assert at least one matches.
3. **Diagnostic ID in marker**: Omitted. Cases are already organized per-rule.
