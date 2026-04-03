# SpireEnum Utility Methods + SPIRE016 Code Fix

Safe integer-to-enum conversion utilities and code fixes for `[EnforceInitialization]` enums.

## 1. SpireEnum<TEnum> Static Class

Lives in `src/Houtamelo.Spire/`, namespace `Houtamelo.Spire`.

```csharp
public static class SpireEnum<TEnum> where TEnum : struct, Enum
```

### Methods

Every method that takes a numeric value has 8 overloads: `int`, `long`, `short`, `sbyte`, `uint`, `ulong`, `ushort`, `byte`.

| Method | Returns | Behavior |
|--------|---------|----------|
| `From(T value)` | `TEnum` | Throws `ArgumentOutOfRangeException` if value is not a named member |
| `FromFlags(T value)` | `TEnum` | Throws if any bit is not covered by named members |
| `TryFrom(T value, out TEnum result)` | `bool` | Returns false + default if not a named member |
| `TryFromFlags(T value, out TEnum result)` | `bool` | Returns false + default if bits not covered |
| `FromOrFallback(T value)` | `TEnum` | Returns cast if valid, else `Fallback()` |
| `FromFlagsOrFallback(T value)` | `TEnum` | Returns cast if valid flags composite, else `Fallback()` |
| `Fallback()` | `TEnum` | First value from `Enum.GetValues(typeof(TEnum))` |

Total: 48 overloaded methods + 1 `Fallback` = 49 public methods.

### Validation Logic

**Non-flags (`TryFrom`):** delegates to `Enum.IsDefined(typeof(TEnum), value)` after converting the input to the enum's underlying type.

**Flags (`TryFromFlags`):** runtime bit-coverage check — collects all named member values, verifies `(value & ~allNamedBits) == 0`. Zero is valid only if a zero-valued member exists. Negative values fall back to exact named-member match. Mirrors the analyzer's `IsValidFlagsCombination` logic.

**`Fallback()`:** `(TEnum)Enum.GetValues(typeof(TEnum)).GetValue(0)`. Throws `InvalidOperationException` if the enum has no members (degenerate case).

### Internal Structure

All 8 overloads per method delegate to shared internal helpers that normalize to `long`/`ulong`. The overloads exist to avoid boxing at the call site.

```
From(int)       -> FromCore(long)    -> TryFromCore(long) ? cast : throw
From(uint)      -> FromCore(long)
From(ulong)     -> FromCoreUlong(ulong)
...etc
TryFrom(int)    -> TryFromCore(long)
TryFromFlags(int) -> TryFromFlagsCore(long)
```

## 2. Code Fix: SPIRE016 Non-Constant Cast

**Provider:** `CodeFixProvider` in `src/Houtamelo.Spire.CodeFixes/`, fixes `SPIRE016`.

**Applies to:** non-constant casts only. Constant invalid casts (e.g., `(Status)42`) get no code fix — the diagnostic message tells the user the value is invalid.

**Transformation:**

```csharp
// Before
Status s = (Status)variable;

// After (non-flags)
Status s = SpireEnum<Status>.From(variable);

// After ([Flags] target)
Status s = SpireEnum<Status>.FromFlags(variable);
```

The fix replaces the cast expression node with a `SpireEnum<TEnum>.From(operand)` invocation. The expression shape stays the same (single expression producing `TEnum`), so it works in all contexts: local assignment, return, argument, ternary, etc.

**Flags detection:** the analyzer must attach an `IsFlags` property to the diagnostic (`"true"` / `"false"`). The code fix reads this property to choose `From` vs `FromFlags`.

### Diagnostic Property Addition

Add to SPIRE016 analyzer's `ReportDiagnostic` call:

```csharp
properties: ImmutableDictionary<string, string?>.Empty
    .Add("IsFlags", hasFlagsAttribute ? "true" : "false")
    .Add("IsConstant", constantValue.HasValue ? "true" : "false")
```

The code fix skips diagnostics where `IsConstant == "true"`.

## 3. Code Refactoring: Enum.IsDefined -> TryFrom

**Provider:** `CodeRefactoringProvider` in `src/Houtamelo.Spire.CodeFixes/`, no diagnostic needed.

**Triggers on:** `Enum.IsDefined` calls where the type argument is an enum.

**Transformation:**

```csharp
// Before
if (Enum.IsDefined<Status>(variable)) { ... }

// After
if (SpireEnum<Status>.TryFrom(variable, out var result)) { ... }
```

Handles both generic overload `Enum.IsDefined<TEnum>(value)` and non-generic `Enum.IsDefined(typeof(TEnum), value)`.

## 4. Diagnostic Message Update

Update SPIRE016 message format to include the actual value for constant casts:

- Non-constant: `"{0} to '{1}' may produce a value that is not a named member"` (keep current style)
- Constant: `"{0} to '{1}' — value {2} does not map to a valid member"`

This requires either a second descriptor or conditional message formatting. Two descriptors is cleaner:
- `SPIRE016_InvalidEnforceInitializationEnumValue` — non-constant (existing, message updated)
- `SPIRE016_InvalidEnforceInitializationEnumConstantValue` — constant, includes value in message

Both use ID `"SPIRE016"` (same rule, different message formats). This is standard Roslyn practice — multiple descriptors can share an ID.

## 5. Files Changed / Created

| File | Action |
|------|--------|
| `src/Houtamelo.Spire/SpireEnum.cs` | Create — utility class |
| `src/Houtamelo.Spire.CodeFixes/FromEnumCodeFix.cs` | Create — SPIRE016 code fix |
| `src/Houtamelo.Spire.CodeFixes/IsDefinedRefactoring.cs` | Create — Enum.IsDefined refactoring |
| `src/Houtamelo.Spire.Analyzers/Descriptors.cs` | Edit — add constant-value descriptor |
| `src/Houtamelo.Spire.Analyzers/Rules/SPIRE016...Analyzer.cs` | Edit — attach properties, use new descriptor for constants |
| `docs/rules/SPIRE016.md` | Edit — document code fix and updated messages |
