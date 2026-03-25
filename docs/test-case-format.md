# Test Case File Format

Each test case is a self-contained `.cs` file in `tests/Spire.Analyzers.Tests/{RuleId}/cases/`.
Cases are discovered automatically at runtime — **no test runner edits needed**.

## Compilation model

`_shared.cs` and each case file are compiled as **separate syntax trees** in the same Roslyn compilation.
This means:

- Types defined in `_shared.cs` are visible to case files (same compilation).
- `using` directives in `_shared.cs` must be `global using` so they apply to all syntax trees.
- Case files **can** have their own `using` directives — they won't conflict with `_shared.cs`.
- Diagnostics are only checked in the case file, not in `_shared.cs`.
- Error marker line numbers are relative to the case file (no offset computation needed).
- **Compilation errors fail the test immediately** — if the combined code (case + `_shared.cs`) has C# compiler errors, the test fails with a clear message listing the errors instead of silently producing no diagnostics. This prevents false passes from invalid test code.

## should_fail (detection) case

```csharp
//@ should_fail
// Ensure that {RuleId} IS triggered when {scenario description}.
public class Detect_NodeType_Context
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5]; //~ ERROR
    }
}
```

## should_pass (false-positive) case

```csharp
//@ should_pass
// Ensure that {RuleId} is NOT triggered when {scenario description}.
public class NoReport_NodeType_Context
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[0];
    }
}
```

## Headers

- **Line 1**: `//@ should_fail` or `//@ should_pass` — required.
- **Line 2**: `// Ensure that {RuleId} IS/is NOT triggered when {scenario}.` — required.

## Error markers (should_fail cases only)

| Marker | Meaning |
|--------|---------|
| `//~ ERROR` | Diagnostic expected on **this line** |
| `//~^ ERROR` | Diagnostic expected on the **previous line** |

Place the marker at the end of the line containing the code that should trigger the diagnostic.

## File naming

- Detection cases: `Detect_{NodeType}_{Context}.cs`
- False-positive cases: `NoReport_{Reason}_{Context}.cs`
- PascalCase with underscores separating logical parts.
