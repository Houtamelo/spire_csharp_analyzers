# SpireEnum Utility + SPIRE016 Code Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `SpireEnum<TEnum>` safe-conversion utilities to `Houtamelo.Spire`, a SPIRE016 code fix, and an `Enum.IsDefined` refactoring provider.

**Architecture:** `SpireEnum<TEnum>` is a public static generic class in `Houtamelo.Spire` with 8 int-type overloads per method. The SPIRE016 code fix replaces non-constant enum casts with `SpireEnum<TEnum>.From(value)`. A separate `CodeRefactoringProvider` replaces `Enum.IsDefined` calls with `SpireEnum<TEnum>.TryFrom`. The SPIRE016 analyzer gets diagnostic property additions (`IsFlags`, `IsConstant`) and a second descriptor for constant-value messages.

**Tech Stack:** C#, Roslyn (`Microsoft.CodeAnalysis`), xUnit, netstandard2.0 (utility + fixes), net10.0 (tests)

**Spec:** `docs/superpowers/specs/2026-04-02-spire-enum-utility-design.md`

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `src/Houtamelo.Spire/SpireEnum.cs` | Create | Utility class with all From/TryFrom/Fallback methods |
| `src/Houtamelo.Spire.Analyzers/Descriptors.cs` | Modify | Add constant-value descriptor |
| `src/Houtamelo.Spire.Analyzers/Rules/SPIRE016InvalidEnforceInitializationEnumValueAnalyzer.cs` | Modify | Attach `IsFlags`/`IsConstant` properties, use new descriptor |
| `src/Houtamelo.Spire.CodeFixes/FromEnumCodeFix.cs` | Create | SPIRE016 code fix: cast → `SpireEnum<T>.From(v)` |
| `src/Houtamelo.Spire.CodeFixes/IsDefinedRefactoring.cs` | Create | Refactoring: `Enum.IsDefined` → `SpireEnum<T>.TryFrom` |
| `tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs` | Create | Unit tests for SpireEnum validation logic |
| `tests/Houtamelo.Spire.Analyzers.Tests/AnalyzerCodeFixTestBase.cs` | Create | Non-generator code fix test base (before/after pairs) |
| `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/` | Create | Code fix test cases (before.cs/after.cs folders) |
| `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/SPIRE016CodeFixTests.cs` | Create | Test runner for code fix |
| `docs/rules/SPIRE016.md` | Modify | Document code fix and updated messages |

---

### Task 1: SpireEnum<TEnum> — Core Validation Logic

**Files:**
- Create: `src/Houtamelo.Spire/SpireEnum.cs`
- Test: `tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs`

- [ ] **Step 1: Create SpireEnum.cs with Fallback, TryFrom (int overload only), and From (int overload only)**

```csharp
// src/Houtamelo.Spire/SpireEnum.cs
using System;
using System.Linq;

namespace Houtamelo.Spire;

/// <summary>
/// Safe integer-to-enum conversion utilities.
/// Use <see cref="TryFrom"/> for non-[Flags] enums and <see cref="TryFromFlags"/> for [Flags] enums.
/// </summary>
public static class SpireEnum<TEnum> where TEnum : struct, Enum
{
    private static readonly TEnum[] AllValues = (TEnum[])Enum.GetValues(typeof(TEnum));

    /// <summary>
    /// Returns the first named member of <typeparamref name="TEnum"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The enum has no named members.</exception>
    public static TEnum Fallback()
    {
        if (AllValues.Length == 0)
            throw new InvalidOperationException(
                $"Enum type '{typeof(TEnum).Name}' has no named members.");

        return AllValues[0];
    }

    // ── Non-flags: TryFrom ──

    public static bool TryFrom(int value, out TEnum result)
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            result = (TEnum)(object)value;
            return true;
        }

        result = default;
        return false;
    }

    // ── Non-flags: From (throws) ──

    public static TEnum From(int value)
    {
        if (!TryFrom(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Value {value} does not map to a valid member of '{typeof(TEnum).Name}'.");

        return result;
    }

    // ── Non-flags: FromOrFallback ──

    public static TEnum FromOrFallback(int value)
    {
        return TryFrom(value, out var result) ? result : Fallback();
    }
}
```

- [ ] **Step 2: Write unit tests for Fallback, TryFrom(int), From(int), FromOrFallback(int)**

```csharp
// tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs
using Houtamelo.Spire;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests;

public enum TestStatus { Active = 1, Inactive = 2, Pending = 3 }

[Flags]
public enum TestFlags { None = 0, Read = 1, Write = 2, Execute = 4 }

[Flags]
public enum TestFlagsNoZero { Read = 1, Write = 2, Execute = 4 }

public class SpireEnumTests
{
    // ── Fallback ──

    [Fact]
    public void Fallback_ReturnsFirstMember()
    {
        Assert.Equal(TestStatus.Active, SpireEnum<TestStatus>.Fallback());
    }

    // ── TryFrom (int) ──

    [Fact]
    public void TryFrom_Int_ValidValue_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestStatus>.TryFrom(1, out var result));
        Assert.Equal(TestStatus.Active, result);
    }

    [Fact]
    public void TryFrom_Int_InvalidValue_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestStatus>.TryFrom(42, out var result));
        Assert.Equal(default(TestStatus), result);
    }

    [Fact]
    public void TryFrom_Int_Zero_NoZeroMember_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestStatus>.TryFrom(0, out _));
    }

    // ── From (int) ──

    [Fact]
    public void From_Int_ValidValue_ReturnsEnum()
    {
        Assert.Equal(TestStatus.Active, SpireEnum<TestStatus>.From(1));
    }

    [Fact]
    public void From_Int_InvalidValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<TestStatus>.From(42));
    }

    // ── FromOrFallback (int) ──

    [Fact]
    public void FromOrFallback_Int_ValidValue_ReturnsEnum()
    {
        Assert.Equal(TestStatus.Inactive, SpireEnum<TestStatus>.FromOrFallback(2));
    }

    [Fact]
    public void FromOrFallback_Int_InvalidValue_ReturnsFallback()
    {
        Assert.Equal(TestStatus.Active, SpireEnum<TestStatus>.FromOrFallback(99));
    }
}
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SpireEnumTests"`
Expected: all 7 tests PASS

- [ ] **Step 4: Commit**

```
feat: add SpireEnum<TEnum> with Fallback, TryFrom, From, FromOrFallback (int only)
```

---

### Task 2: SpireEnum<TEnum> — Flags Validation Logic

**Files:**
- Modify: `src/Houtamelo.Spire/SpireEnum.cs`
- Modify: `tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs`

- [ ] **Step 1: Add TryFromFlags(int), FromFlags(int), FromFlagsOrFallback(int) to SpireEnum.cs**

Add after the non-flags methods:

```csharp
    // ── Flags: TryFromFlags ──

    public static bool TryFromFlags(int value, out TEnum result)
    {
        long longValue = value;
        if (IsValidFlagsCombination(longValue))
        {
            result = (TEnum)(object)value;
            return true;
        }

        result = default;
        return false;
    }

    // ── Flags: FromFlags (throws) ──

    public static TEnum FromFlags(int value)
    {
        if (!TryFromFlags(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Value {value} does not map to a valid flags combination of '{typeof(TEnum).Name}'.");

        return result;
    }

    // ── Flags: FromFlagsOrFallback ──

    public static TEnum FromFlagsOrFallback(int value)
    {
        return TryFromFlags(value, out var result) ? result : Fallback();
    }

    // ── Flags validation internals ──

    private static bool IsValidFlagsCombination(long value)
    {
        if (value < 0)
            return Enum.IsDefined(typeof(TEnum), value);

        ulong bits = (ulong)value;
        ulong allNamedBits = 0;
        bool hasZeroMember = false;

        foreach (var member in AllValues)
        {
            ulong memberValue = ToUInt64(member);
            if (memberValue == 0)
                hasZeroMember = true;
            else
                allNamedBits |= memberValue;
        }

        if (bits == 0)
            return hasZeroMember;

        return (bits & ~allNamedBits) == 0;
    }

    private static ulong ToUInt64(TEnum value)
    {
        return Convert.ToUInt64(value);
    }
```

- [ ] **Step 2: Write flags unit tests**

Add to `SpireEnumTests.cs`:

```csharp
    // ── TryFromFlags (int) ──

    [Fact]
    public void TryFromFlags_Int_SingleNamedValue_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(1, out var result));
        Assert.Equal(TestFlags.Read, result);
    }

    [Fact]
    public void TryFromFlags_Int_ValidComposite_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(3, out var result));
        Assert.Equal(TestFlags.Read | TestFlags.Write, result);
    }

    [Fact]
    public void TryFromFlags_Int_AllBits_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(7, out var result));
        Assert.Equal(TestFlags.Read | TestFlags.Write | TestFlags.Execute, result);
    }

    [Fact]
    public void TryFromFlags_Int_InvalidBit_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestFlags>.TryFromFlags(8, out _));
    }

    [Fact]
    public void TryFromFlags_Int_MixedValidInvalid_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestFlags>.TryFromFlags(9, out _));
    }

    [Fact]
    public void TryFromFlags_Int_Zero_WithZeroMember_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(0, out var result));
        Assert.Equal(TestFlags.None, result);
    }

    [Fact]
    public void TryFromFlags_Int_Zero_NoZeroMember_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestFlagsNoZero>.TryFromFlags(0, out _));
    }

    // ── FromFlags (int) ──

    [Fact]
    public void FromFlags_Int_ValidComposite_ReturnsEnum()
    {
        Assert.Equal(TestFlags.Read | TestFlags.Write, SpireEnum<TestFlags>.FromFlags(3));
    }

    [Fact]
    public void FromFlags_Int_InvalidBit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<TestFlags>.FromFlags(8));
    }

    // ── FromFlagsOrFallback (int) ──

    [Fact]
    public void FromFlagsOrFallback_Int_ValidComposite_ReturnsEnum()
    {
        Assert.Equal(TestFlags.Read | TestFlags.Write, SpireEnum<TestFlags>.FromFlagsOrFallback(3));
    }

    [Fact]
    public void FromFlagsOrFallback_Int_InvalidBit_ReturnsFallback()
    {
        Assert.Equal(TestFlags.None, SpireEnum<TestFlags>.FromFlagsOrFallback(8));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SpireEnumTests"`
Expected: all 18 tests PASS

- [ ] **Step 4: Commit**

```
feat: add SpireEnum<TEnum> flags validation (TryFromFlags, FromFlags, FromFlagsOrFallback)
```

---

### Task 3: SpireEnum<TEnum> — All 8 Integer Type Overloads

**Files:**
- Modify: `src/Houtamelo.Spire/SpireEnum.cs`
- Modify: `tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs`

- [ ] **Step 1: Add remaining 7 overloads for each method**

For each of `long`, `short`, `sbyte`, `uint`, `ulong`, `ushort`, `byte`, add overloads that delegate to shared internals. Restructure TryFrom/TryFromFlags to use internal `long`/`ulong` helpers:

```csharp
    // ── Non-flags: TryFrom overloads ──

    public static bool TryFrom(long value, out TEnum result)
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryFrom(short value, out TEnum result) => TryFrom((long)value, out result);
    public static bool TryFrom(sbyte value, out TEnum result) => TryFrom((long)value, out result);
    public static bool TryFrom(uint value, out TEnum result) => TryFrom((long)value, out result);
    public static bool TryFrom(ushort value, out TEnum result) => TryFrom((long)value, out result);
    public static bool TryFrom(byte value, out TEnum result) => TryFrom((long)value, out result);

    public static bool TryFrom(ulong value, out TEnum result)
    {
        if (value <= long.MaxValue)
            return TryFrom((long)value, out result);

        // ulong values above long.MaxValue: only possible for ulong-backed enums
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    // ── Non-flags: From overloads ──

    public static TEnum From(long value)
    {
        if (!TryFrom(value, out var result))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Value {value} does not map to a valid member of '{typeof(TEnum).Name}'.");
        return result;
    }

    public static TEnum From(short value) => From((long)value);
    public static TEnum From(sbyte value) => From((long)value);
    public static TEnum From(uint value) => From((long)value);
    public static TEnum From(ushort value) => From((long)value);
    public static TEnum From(byte value) => From((long)value);

    public static TEnum From(ulong value)
    {
        if (!TryFrom(value, out var result))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Value {value} does not map to a valid member of '{typeof(TEnum).Name}'.");
        return result;
    }

    // ── Non-flags: FromOrFallback overloads ──

    public static TEnum FromOrFallback(long value) => TryFrom(value, out var r) ? r : Fallback();
    public static TEnum FromOrFallback(short value) => FromOrFallback((long)value);
    public static TEnum FromOrFallback(sbyte value) => FromOrFallback((long)value);
    public static TEnum FromOrFallback(uint value) => FromOrFallback((long)value);
    public static TEnum FromOrFallback(ushort value) => FromOrFallback((long)value);
    public static TEnum FromOrFallback(byte value) => FromOrFallback((long)value);
    public static TEnum FromOrFallback(ulong value) => TryFrom(value, out var r) ? r : Fallback();

    // ── Flags: TryFromFlags overloads ──

    public static bool TryFromFlags(long value, out TEnum result)
    {
        if (IsValidFlagsCombination(value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryFromFlags(short value, out TEnum result) => TryFromFlags((long)value, out result);
    public static bool TryFromFlags(sbyte value, out TEnum result) => TryFromFlags((long)value, out result);
    public static bool TryFromFlags(uint value, out TEnum result) => TryFromFlags((long)value, out result);
    public static bool TryFromFlags(ushort value, out TEnum result) => TryFromFlags((long)value, out result);
    public static bool TryFromFlags(byte value, out TEnum result) => TryFromFlags((long)value, out result);

    public static bool TryFromFlags(ulong value, out TEnum result)
    {
        if (IsValidFlagsCombinationUlong(value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    // ── Flags: FromFlags overloads ──

    public static TEnum FromFlags(long value)
    {
        if (!TryFromFlags(value, out var result))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Value {value} does not map to a valid flags combination of '{typeof(TEnum).Name}'.");
        return result;
    }

    public static TEnum FromFlags(short value) => FromFlags((long)value);
    public static TEnum FromFlags(sbyte value) => FromFlags((long)value);
    public static TEnum FromFlags(uint value) => FromFlags((long)value);
    public static TEnum FromFlags(ushort value) => FromFlags((long)value);
    public static TEnum FromFlags(byte value) => FromFlags((long)value);

    public static TEnum FromFlags(ulong value)
    {
        if (!TryFromFlags(value, out var result))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Value {value} does not map to a valid flags combination of '{typeof(TEnum).Name}'.");
        return result;
    }

    // ── Flags: FromFlagsOrFallback overloads ──

    public static TEnum FromFlagsOrFallback(long value) => TryFromFlags(value, out var r) ? r : Fallback();
    public static TEnum FromFlagsOrFallback(short value) => FromFlagsOrFallback((long)value);
    public static TEnum FromFlagsOrFallback(sbyte value) => FromFlagsOrFallback((long)value);
    public static TEnum FromFlagsOrFallback(uint value) => FromFlagsOrFallback((long)value);
    public static TEnum FromFlagsOrFallback(ushort value) => FromFlagsOrFallback((long)value);
    public static TEnum FromFlagsOrFallback(byte value) => FromFlagsOrFallback((long)value);
    public static TEnum FromFlagsOrFallback(ulong value) => TryFromFlags(value, out var r) ? r : Fallback();
```

Also update the `int` overloads to delegate to `long` instead of calling `Enum.IsDefined` directly (DRY):

```csharp
    public static bool TryFrom(int value, out TEnum result) => TryFrom((long)value, out result);
    public static TEnum From(int value) => From((long)value);
    public static TEnum FromOrFallback(int value) => FromOrFallback((long)value);
    public static bool TryFromFlags(int value, out TEnum result) => TryFromFlags((long)value, out result);
    public static TEnum FromFlags(int value) => FromFlags((long)value);
    public static TEnum FromFlagsOrFallback(int value) => FromFlagsOrFallback((long)value);
```

Also add a `ulong` path to `IsValidFlagsCombination`:

```csharp
    private static bool IsValidFlagsCombinationUlong(ulong bits)
    {
        ulong allNamedBits = 0;
        bool hasZeroMember = false;

        foreach (var member in AllValues)
        {
            ulong memberValue = Convert.ToUInt64(member);
            if (memberValue == 0)
                hasZeroMember = true;
            else
                allNamedBits |= memberValue;
        }

        if (bits == 0)
            return hasZeroMember;

        return (bits & ~allNamedBits) == 0;
    }

    private static bool IsValidFlagsCombination(long value)
    {
        if (value < 0)
            return Enum.IsDefined(typeof(TEnum), value);

        return IsValidFlagsCombinationUlong((ulong)value);
    }
```

- [ ] **Step 2: Add a few overload-specific tests**

Add to `SpireEnumTests.cs`:

```csharp
    // ── Overload tests (spot-check, not exhaustive per type) ──

    [Fact]
    public void TryFrom_Long_ValidValue_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestStatus>.TryFrom(1L, out var result));
        Assert.Equal(TestStatus.Active, result);
    }

    [Fact]
    public void TryFrom_Byte_ValidValue_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestStatus>.TryFrom((byte)2, out var result));
        Assert.Equal(TestStatus.Inactive, result);
    }

    [Fact]
    public void TryFrom_Short_InvalidValue_ReturnsFalse()
    {
        Assert.False(SpireEnum<TestStatus>.TryFrom((short)99, out _));
    }

    [Fact]
    public void From_UInt_ValidValue_ReturnsEnum()
    {
        Assert.Equal(TestStatus.Pending, SpireEnum<TestStatus>.From(3u));
    }

    [Fact]
    public void TryFromFlags_Long_ValidComposite_ReturnsTrue()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(3L, out var result));
        Assert.Equal(TestFlags.Read | TestFlags.Write, result);
    }

    [Fact]
    public void FromFlags_Byte_ValidValue_ReturnsEnum()
    {
        Assert.Equal(TestFlags.Read, SpireEnum<TestFlags>.FromFlags((byte)1));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SpireEnumTests"`
Expected: all 24 tests PASS

- [ ] **Step 4: Commit**

```
feat: add all 8 integer-type overloads for SpireEnum<TEnum> methods
```

---

### Task 4: SPIRE016 Analyzer — Diagnostic Properties + Constant Value Message

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/Descriptors.cs`
- Modify: `src/Houtamelo.Spire.Analyzers/Rules/SPIRE016InvalidEnforceInitializationEnumValueAnalyzer.cs`

- [ ] **Step 1: Add constant-value descriptor to Descriptors.cs**

Add after the existing `SPIRE016_InvalidEnforceInitializationEnumValue`:

```csharp
    public static readonly DiagnosticDescriptor SPIRE016_InvalidEnforceInitializationEnumConstantValue = new(
        id: "SPIRE016",
        title: "Cast to [EnforceInitialization] enum uses invalid constant value",
        messageFormat: "{0} to '{1}' — value {2} does not map to a valid member",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant integer value is cast to an [EnforceInitialization] enum but does not correspond "
                   + "to any named member (or valid flags combination).",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE016.md"
    );
```

- [ ] **Step 2: Update the analyzer to attach diagnostic properties and use the new descriptor for constants**

In `SPIRE016InvalidEnforceInitializationEnumValueAnalyzer.cs`, modify the `AnalyzeConversion` method. Replace the existing `ReportDiagnostic` call (and the constant-valid early return logic before it) with:

```csharp
        var constantValue = operation.Operand.ConstantValue;
        bool hasFlagsAttribute = flagsAttributeType is not null
            && HasAttribute(targetType, flagsAttributeType);

        if (constantValue.HasValue)
        {
            bool isValid = hasFlagsAttribute
                ? IsValidFlagsCombination(targetType, constantValue.Value)
                : IsNamedMemberValue(targetType, constantValue.Value);

            if (isValid)
                return;

            // Constant invalid value — use specific descriptor with value in message
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("IsFlags", hasFlagsAttribute ? "true" : "false")
                .Add("IsConstant", "true");

            string castLabel = sourceType.TypeKind == TypeKind.Enum ? "Enum cast" : "Integer cast";

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.SPIRE016_InvalidEnforceInitializationEnumConstantValue,
                    operation.Syntax.GetLocation(),
                    properties,
                    castLabel,
                    targetType.Name,
                    constantValue.Value));
            return;
        }

        // Non-constant value — use original descriptor
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("IsFlags", hasFlagsAttribute ? "true" : "false")
                .Add("IsConstant", "false");

            string castLabel = sourceType.TypeKind == TypeKind.Enum ? "Enum cast" : "Integer cast";

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.SPIRE016_InvalidEnforceInitializationEnumValue,
                    operation.Syntax.GetLocation(),
                    properties,
                    castLabel,
                    targetType.Name));
        }
```

Also add `SupportedDiagnostics` to include both descriptors:

```csharp
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            Descriptors.SPIRE016_InvalidEnforceInitializationEnumValue,
            Descriptors.SPIRE016_InvalidEnforceInitializationEnumConstantValue);
```

And add the missing `using`:

```csharp
using System.Collections.Immutable;  // already there
// Ensure this exists:
using System.Collections.Generic;    // needed if not already present — check
```

Note: `ImmutableDictionary` is in `System.Collections.Immutable` which is already imported.

- [ ] **Step 3: Run existing SPIRE016 tests to ensure no regressions**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE016"`
Expected: all 51 existing tests PASS (diagnostic properties don't affect line-based matching)

- [ ] **Step 4: Commit**

```
feat: add SPIRE016 diagnostic properties (IsFlags, IsConstant) and constant-value message
```

---

### Task 5: AnalyzerCodeFixTestBase — Non-Generator Code Fix Test Infrastructure

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/AnalyzerCodeFixTestBase.cs`

- [ ] **Step 1: Create the test base**

This is like `CodeFixTestBase` from the SourceGenerators test project, but without the generator step. It reads `before.cs`/`after.cs` pairs from `{Category}/cases/{caseName}/`, compiles `before.cs` + `_shared.cs`, runs analyzers, applies code fix, and compares with `after.cs`.

```csharp
// tests/Houtamelo.Spire.Analyzers.Tests/AnalyzerCodeFixTestBase.cs
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.Analyzers.Tests;

/// Base class for analyzer code fix tests (non-generator).
/// Discovers before.cs/after.cs pairs in {Category}/cases/{caseName}/ folders.
public abstract class AnalyzerCodeFixTestBase
{
    protected abstract string Category { get; }
    protected abstract ImmutableArray<DiagnosticAnalyzer> GetAnalyzers();
    protected abstract ImmutableArray<CodeFixProvider> GetCodeFixes();

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Rules.SPIRE016InvalidEnforceInitializationEnumValueAnalyzer).Assembly.Location);

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> CachedReferences =
        new(() => ResolveReferencesAsync());

    [Theory]
    [AnalyzerCodeFixCaseDiscovery]
    public async Task Verify(string caseName)
    {
        if (caseName == AnalyzerCodeFixCaseDiscoveryAttribute.NoCasesSentinel)
            return;

        var casesDir = Path.Combine(AppContext.BaseDirectory, Category, "cases");
        var caseDir = Path.Combine(casesDir, caseName);
        var sharedPath = Path.Combine(casesDir, "_shared.cs");

        var beforeSource = File.ReadAllText(Path.Combine(caseDir, "before.cs"));
        var afterSource = File.ReadAllText(Path.Combine(caseDir, "after.cs"));
        var sharedSource = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : null;

        var references = await CachedReferences.Value;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var beforeTree = CSharpSyntaxTree.ParseText(beforeSource, parseOptions, path: "case.cs");
        var trees = new List<SyntaxTree> { beforeTree };
        if (sharedSource != null)
            trees.Add(CSharpSyntaxTree.ParseText(sharedSource, parseOptions, path: "_shared.cs"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Build workspace from compilation
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestAssembly", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options)
            .WithProjectParseOptions(projectId, parseOptions);

        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocId = null;
        foreach (var tree in trees)
        {
            var docId = DocumentId.CreateNewId(projectId);
            var fileName = Path.GetFileName(tree.FilePath);
            solution = solution.AddDocument(docId, fileName, tree.GetText());
            if (tree.FilePath == "case.cs")
                userDocId = docId;
        }

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply workspace changes");

        if (userDocId is null)
            throw new InvalidOperationException("User document 'case.cs' not found");

        // Run analyzers
        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var wsCompilation = (await project.GetCompilationAsync())!;
        var withAnalyzers = wsCompilation.WithAnalyzers(GetAnalyzers());
        var allDiags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        var fixableIds = GetCodeFixes()
            .SelectMany(p => p.FixableDiagnosticIds)
            .ToHashSet();

        var fixableDiags = allDiags
            .Where(d => fixableIds.Contains(d.Id))
            .Where(d => d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();

        if (fixableDiags.Count == 0)
        {
            var diagSummary = string.Join(", ", allDiags.Select(d => $"{d.Id}@{d.Location}"));
            throw new XunitException(
                $"Case '{caseName}': no fixable diagnostics found. All diagnostics: [{diagSummary}]");
        }

        var diagnostic = fixableDiags[0];
        var provider = GetCodeFixes().First(p => p.FixableDiagnosticIds.Contains(diagnostic.Id));

        var document = workspace.CurrentSolution.GetDocument(userDocId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);

        if (actions.Count == 0)
            throw new XunitException(
                $"Case '{caseName}': code fix provider registered no actions for {diagnostic.Id}");

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();
        applyOp.Apply(workspace, CancellationToken.None);

        var modifiedDoc = workspace.CurrentSolution.GetDocument(userDocId)!;
        var modifiedText = (await modifiedDoc.GetTextAsync()).ToString();

        var actualTree = CSharpSyntaxTree.ParseText(modifiedText);
        var expectedTree = CSharpSyntaxTree.ParseText(afterSource);

        if (!actualTree.GetRoot().NormalizeWhitespace().IsEquivalentTo(
                expectedTree.GetRoot().NormalizeWhitespace()))
        {
            throw new XunitException(
                $"Code fix mismatch for case '{caseName}'.\n\n" +
                $"=== EXPECTED ===\n{afterSource}\n\n" +
                $"=== ACTUAL ===\n{modifiedText}");
        }
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference);
    }
}

/// Discovers code fix test case folders by scanning for directories containing before.cs.
[AttributeUsage(AttributeTargets.Method)]
public sealed class AnalyzerCodeFixCaseDiscoveryAttribute : DataAttribute
{
    public const string NoCasesSentinel = "__NO_CASES__";

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testClass = testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new InvalidOperationException("Could not determine test class");

        var category = ExtractCategory(testClass.Name);
        var casesDir = Path.Combine(AppContext.BaseDirectory, category, "cases");

        if (!Directory.Exists(casesDir))
        {
            yield return new object[] { NoCasesSentinel };
            yield break;
        }

        bool found = false;
        foreach (var dir in Directory.GetDirectories(casesDir).OrderBy(d => d))
        {
            if (File.Exists(Path.Combine(dir, "before.cs")))
            {
                found = true;
                yield return new object[] { Path.GetFileName(dir) };
            }
        }

        if (!found)
            yield return new object[] { NoCasesSentinel };
    }

    private static string ExtractCategory(string className)
    {
        if (className.EndsWith("Tests", StringComparison.Ordinal))
            return className.Substring(0, className.Length - 5);

        throw new InvalidOperationException(
            $"Test class name '{className}' does not follow the '{{Category}}Tests' convention.");
    }
}
```

- [ ] **Step 2: Commit**

```
feat: add AnalyzerCodeFixTestBase for non-generator code fix tests
```

---

### Task 6: SPIRE016 Code Fix — FromEnumCodeFix

**Files:**
- Create: `src/Houtamelo.Spire.CodeFixes/FromEnumCodeFix.cs`
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/SPIRE016CodeFixTests.cs`
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/cases/_shared.cs`
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/cases/` (test case folders)

- [ ] **Step 1: Write the test runner**

```csharp
// tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/SPIRE016CodeFixTests.cs
using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Tests;

public class SPIRE016CodeFixTests : AnalyzerCodeFixTestBase
{
    protected override string Category => "SPIRE016CodeFix";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers() =>
        ImmutableArray.Create<DiagnosticAnalyzer>(
            new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer());

    protected override ImmutableArray<CodeFixProvider> GetCodeFixes() =>
        ImmutableArray.Create<CodeFixProvider>(new FromEnumCodeFix());
}
```

- [ ] **Step 2: Add project reference to CodeFixes from the test project**

In `tests/Houtamelo.Spire.Analyzers.Tests/Houtamelo.Spire.Analyzers.Tests.csproj`, add:

```xml
    <ProjectReference Include="..\..\src\Houtamelo.Spire.CodeFixes\Houtamelo.Spire.CodeFixes.csproj" />
```

Also add to the `<ItemGroup>` with `<Compile Remove>` and `<None Include>`:

```xml
    <None Include="SPIRE016CodeFix/cases/**" CopyToOutputDirectory="PreserveNewest" />
```

Wait — the existing glob `**/cases/**` already covers this. No change needed for content files.

- [ ] **Step 3: Create _shared.cs for code fix tests**

```csharp
// tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/cases/_shared.cs
global using System;
global using Houtamelo.Spire;

[EnforceInitialization]
public enum StatusNoZero { Active = 1, Inactive = 2, Pending = 3 }

[EnforceInitialization]
[Flags]
public enum FlagsNoZero { Read = 1, Write = 2, Execute = 4 }
```

- [ ] **Step 4: Create test case — NonFlags_LocalVar**

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_LocalVar/before.cs
public class Test
{
    public void Method()
    {
        int v = 1;
        StatusNoZero s = (StatusNoZero)v;
    }
}
```

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_LocalVar/after.cs
public class Test
{
    public void Method()
    {
        int v = 1;
        StatusNoZero s = SpireEnum<StatusNoZero>.From(v);
    }
}
```

- [ ] **Step 5: Create test case — NonFlags_ReturnStatement**

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_ReturnStatement/before.cs
public class Test
{
    public StatusNoZero Method(int v)
    {
        return (StatusNoZero)v;
    }
}
```

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_ReturnStatement/after.cs
public class Test
{
    public StatusNoZero Method(int v)
    {
        return SpireEnum<StatusNoZero>.From(v);
    }
}
```

- [ ] **Step 6: Create test case — NonFlags_MethodArgument**

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_MethodArgument/before.cs
public class Test
{
    public void Method(int v)
    {
        Accept((StatusNoZero)v);
    }

    private void Accept(StatusNoZero s) { }
}
```

```csharp
// tests/.../SPIRE016CodeFix/cases/NonFlags_MethodArgument/after.cs
public class Test
{
    public void Method(int v)
    {
        Accept(SpireEnum<StatusNoZero>.From(v));
    }

    private void Accept(StatusNoZero s) { }
}
```

- [ ] **Step 7: Create test case — Flags_LocalVar**

```csharp
// tests/.../SPIRE016CodeFix/cases/Flags_LocalVar/before.cs
public class Test
{
    public void Method()
    {
        int v = 3;
        FlagsNoZero f = (FlagsNoZero)v;
    }
}
```

```csharp
// tests/.../SPIRE016CodeFix/cases/Flags_LocalVar/after.cs
public class Test
{
    public void Method()
    {
        int v = 3;
        FlagsNoZero f = SpireEnum<FlagsNoZero>.FromFlags(v);
    }
}
```

- [ ] **Step 8: Create test case — ConstantInvalid_NoFix (no code fix offered)**

This case verifies no code fix is offered for constant invalid values. The test should fail because no fixable diagnostics are found. We need a way to express "no fix expected" — the simplest approach is to have the `before.cs` and `after.cs` be identical, and handle the "no fixable diagnostics" case in the test base.

Actually, the simpler approach: skip this as a code fix test. The analyzer tests already cover constant invalid values. The code fix just doesn't apply to them (it checks `IsConstant` property). We don't need a negative test case in the code fix suite — the property check in the provider is the guard.

- [ ] **Step 9: Write FromEnumCodeFix.cs**

```csharp
// src/Houtamelo.Spire.CodeFixes/FromEnumCodeFix.cs
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class FromEnumCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE016");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        var isConstant = diagnostic.Properties.GetValueOrDefault("IsConstant");
        if (isConstant == "true")
            return;

        var isFlags = diagnostic.Properties.GetValueOrDefault("IsFlags") == "true";
        var methodName = isFlags ? "FromFlags" : "From";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Replace with SpireEnum<T>.{methodName}(value)",
                createChangedDocument: ct =>
                    ReplaceWithFromAsync(context.Document, diagnostic, methodName, ct),
                equivalenceKey: "ReplaceWithSpireEnumFrom"),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithFromAsync(
        Document document, Diagnostic diagnostic, string methodName, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Find the cast expression
        var castExpr = node.AncestorsAndSelf()
            .OfType<CastExpressionSyntax>()
            .FirstOrDefault();
        if (castExpr is null) return document;

        var targetType = castExpr.Type;
        var operand = castExpr.Expression;

        // Build: SpireEnum<TargetType>.MethodName(operand)
        var spireEnumType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("SpireEnum"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(targetType.WithoutTrivia())));

        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            spireEnumType,
            SyntaxFactory.IdentifierName(methodName));

        var invocation = SyntaxFactory.InvocationExpression(
            memberAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(operand.WithoutTrivia()))));

        var newRoot = root.ReplaceNode(castExpr, invocation.WithTriviaFrom(castExpr));
        return document.WithSyntaxRoot(newRoot);
    }
}
```

- [ ] **Step 10: Run code fix tests**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE016CodeFixTests"`
Expected: all 4 test cases PASS

- [ ] **Step 11: Commit**

```
feat: add SPIRE016 code fix — replace cast with SpireEnum<T>.From/FromFlags
```

---

### Task 7: Enum.IsDefined Refactoring Provider

**Files:**
- Create: `src/Houtamelo.Spire.CodeFixes/IsDefinedRefactoring.cs`

Note: `CodeRefactoringProvider` tests require a different infrastructure (cursor position / span selection). For now, we implement the refactoring and test it manually. A dedicated test base can be added later.

- [ ] **Step 1: Write IsDefinedRefactoring.cs**

```csharp
// src/Houtamelo.Spire.CodeFixes/IsDefinedRefactoring.cs
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp)]
[Shared]
public sealed class IsDefinedRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var node = root.FindNode(context.Span);

        // Find the invocation expression for Enum.IsDefined
        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        if (invocation is null) return;

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (model is null) return;

        var symbolInfo = model.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method) return;

        // Must be System.Enum.IsDefined
        if (method.ContainingType?.SpecialType != SpecialType.System_Enum) return;
        if (method.Name != "IsDefined") return;

        // Case 1: Enum.IsDefined<TEnum>(value) — generic overload
        if (method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            var enumType = method.TypeArguments[0];
            if (enumType.TypeKind != TypeKind.Enum) return;

            var valueArg = invocation.ArgumentList.Arguments[0].Expression;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Replace with SpireEnum<{enumType.Name}>.TryFrom(value, out var result)",
                    createChangedDocument: ct =>
                        ReplaceIsDefinedGenericAsync(context.Document, invocation, enumType, valueArg, ct),
                    equivalenceKey: "ReplaceIsDefinedWithTryFrom"));
            return;
        }

        // Case 2: Enum.IsDefined(typeof(TEnum), value) — non-generic overload
        if (!method.IsGenericMethod && method.Parameters.Length == 2)
        {
            var args = invocation.ArgumentList.Arguments;
            if (args.Count != 2) return;

            if (args[0].Expression is not TypeOfExpressionSyntax typeOfExpr) return;

            var typeInfo = model.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
            if (typeInfo.Type is not { TypeKind: TypeKind.Enum } enumType) return;

            var valueArg = args[1].Expression;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Replace with SpireEnum<{enumType.Name}>.TryFrom(value, out var result)",
                    createChangedDocument: ct =>
                        ReplaceIsDefinedNonGenericAsync(context.Document, invocation, typeOfExpr.Type, valueArg, ct),
                    equivalenceKey: "ReplaceIsDefinedWithTryFrom"));
        }
    }

    private static async Task<Document> ReplaceIsDefinedGenericAsync(
        Document document, InvocationExpressionSyntax invocation,
        ITypeSymbol enumType, ExpressionSyntax valueArg, CancellationToken ct)
    {
        var typeSyntax = SyntaxFactory.ParseTypeName(
            enumType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

        return await ReplaceWithTryFromAsync(document, invocation, typeSyntax, valueArg, ct);
    }

    private static async Task<Document> ReplaceIsDefinedNonGenericAsync(
        Document document, InvocationExpressionSyntax invocation,
        TypeSyntax enumTypeSyntax, ExpressionSyntax valueArg, CancellationToken ct)
    {
        return await ReplaceWithTryFromAsync(document, invocation, enumTypeSyntax, valueArg, ct);
    }

    private static async Task<Document> ReplaceWithTryFromAsync(
        Document document, InvocationExpressionSyntax invocation,
        TypeSyntax enumTypeSyntax, ExpressionSyntax valueArg, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        // Build: SpireEnum<TEnum>.TryFrom(value, out var result)
        var spireEnumType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("SpireEnum"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(enumTypeSyntax.WithoutTrivia())));

        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            spireEnumType,
            SyntaxFactory.IdentifierName("TryFrom"));

        var outArg = SyntaxFactory.Argument(
            SyntaxFactory.DeclarationExpression(
                SyntaxFactory.IdentifierName("var"),
                SyntaxFactory.SingleVariableDesignation(
                    SyntaxFactory.Identifier("result"))))
            .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword));

        var newInvocation = SyntaxFactory.InvocationExpression(
            memberAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(valueArg.WithoutTrivia()),
                    outArg
                })));

        var newRoot = root.ReplaceNode(invocation, newInvocation.WithTriviaFrom(invocation));
        return document.WithSyntaxRoot(newRoot);
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build src/Houtamelo.Spire.CodeFixes/`
Expected: build succeeds

- [ ] **Step 3: Commit**

```
feat: add Enum.IsDefined refactoring to SpireEnum<T>.TryFrom
```

---

### Task 8: Update SPIRE016 Docs

**Files:**
- Modify: `docs/rules/SPIRE016.md`

- [ ] **Step 1: Update docs to document code fix and new message format**

Add a "Code Fix" section and update the description to mention the constant-value message:

After the "When to Suppress" section, add:

```markdown
## Code Fix

For **non-constant** casts (`(Status)variable`), the code fix replaces the cast with a safe conversion:

```csharp
// Before
Status s = (Status)variable;

// After (non-[Flags])
Status s = SpireEnum<Status>.From(variable);

// After ([Flags])
Perms p = SpireEnum<Perms>.FromFlags(variable);
```

`SpireEnum<T>.From` throws `ArgumentOutOfRangeException` if the value is not a named member. For more control, use `SpireEnum<T>.TryFrom(value, out var result)` manually.

No code fix is offered for constant invalid casts (`(Status)42`) — the diagnostic message reports the exact invalid value.
```

- [ ] **Step 2: Commit**

```
docs: update SPIRE016 with code fix documentation
```

---

### Task 9: Final Verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, including existing SPIRE001-016 tests and new SpireEnum + code fix tests

- [ ] **Step 2: Build entire solution**

Run: `dotnet build`
Expected: clean build, no warnings (except suppressed ones)
