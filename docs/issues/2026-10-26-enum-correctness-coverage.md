# Enum correctness coverage matrix

This matrix defines the regression contract for the approved enum batch. It
covers `SpireEnum<TEnum>` flags validation and the SPIRE016 enum-to-enum codefix.
Tests should remain real executable tests: `SpireEnumTests` exercises the public
API directly; SPIRE016 before/after pairs must compile, execute the converted
value, and preserve the surrounding expression. No expected behavior below
should be implemented by weakening existing assertions.

The policy is:

- Validate the input against the target enum's underlying range before creating
  an enum value. An invalid `Try*` call returns `false` and writes `default`.
- A zero flags value is valid only when the target enum declares a zero member.
- Positive flags are valid when all bits occur in declared members, including
  the raw underlying-width bits of signed negative members such as `All = -1`.
- Negative flags are valid only when that exact negative value is a declared
  member. Preserve the existing long-backed behavior; do not accept arbitrary
  negative bit combinations.
- `From*` throws `ArgumentOutOfRangeException` for invalid input and
  `From*OrFallback` preserves the existing `Fallback()` result (the first
  `Enum.GetValues` entry, not source declaration order). Empty enums still throw
  `InvalidOperationException` when a fallback is needed.
- The SPIRE016 codefix casts an enum operand to its actual source underlying
  type before calling `SpireEnum<TTarget>.From`/`FromFlags`; it must preserve
  wide values, evaluate the operand once, and preserve parentheses/trivia.

## Category A: flags bit validation and zero policy

### should_pass

| File/Fact | Description |
|---|---|
| `TryFromFlags_Int_NamedAndCompositeBits` | `int`-backed `[Flags]` enum accepts one named bit, a named composite, and all declared positive bits. |
| `TryFromFlags_Int_RejectsUnknownBitAndMixedValue` | Reject `8` and `Read | 8`; write the default out value. |
| `TryFromFlags_Int_ZeroNamedMember` | Accept zero and return the declared `None` member. |
| `TryFromFlags_Int_ZeroWithoutMember` | Reject zero when no zero member is declared; `FromFlags` throws and `FromFlagsOrFallback` returns the first named member. |
| `TryFromFlags_EmptyEnum` | Reject every nonzero/zero value; fallback paths throw `InvalidOperationException` because no member exists. |

## Category B: every enum underlying width

### should_pass

| File/Fact | Description |
|---|---|
| `TryFromFlags_SByte_AllOverloads` | `[Flags] sbyte` enum accepts `sbyte` values and the widened `short`, `int`, and `long` overloads when in range; rejects values outside `sbyte` range without throwing. |
| `TryFromFlags_Byte_AllOverloads` | `[Flags] byte` enum accepts `byte.MaxValue` and rejects `256` from wider overloads. |
| `TryFromFlags_Int16_AllOverloads` | `[Flags] short` enum accepts `short.MaxValue` where all bits are named and rejects `short.MaxValue + 1`. |
| `TryFromFlags_UInt16_AllOverloads` | `[Flags] ushort` enum accepts `ushort.MaxValue` and rejects `ushort.MaxValue + 1` from `uint`/`ulong`. |
| `TryFromFlags_Int32_Boundaries` | `[Flags] int` enum accepts a declared positive maximum and a declared `-1`; rejects a long value outside `int` range. |
| `TryFromFlags_UInt32_Boundaries` | `[Flags] uint` accepts `uint.MaxValue` when named and rejects `uint.MaxValue + 1UL` without wrapping. |
| `TryFromFlags_Int64_Boundaries` | `[Flags] long` accepts `long.MaxValue`/a declared negative member and rejects values outside the type through `ulong`. |
| `TryFromFlags_UInt64_HighBitAndMax` | `[Flags] ulong` accepts `0x8000000000000000UL` and `ulong.MaxValue` when named, while signed overloads with negative input return `false`. |

### should_fail

| File/Fact | Description |
|---|---|
| `TryFromFlags_NarrowSignedRange_DoesNotThrow` | Every signed narrow-width overload and `FromFlags` path returns the documented result instead of `ArgumentException`/`OverflowException` from `Enum.IsDefined` or bit conversion. |
| `TryFromFlags_UInt64Input_DoesNotWrapIntoSignedEnum` | `ulong` values above a signed target's maximum are rejected rather than cast/wrapped into a valid-looking negative value. |

## Category C: signed negative members and raw-width masks

### should_pass

| File/Fact | Description |
|---|---|
| `TryFromFlags_Int_DeclaredMinusOne` | An `int`-backed enum with `All = -1` accepts `-1` exactly and returns `All`. |
| `TryFromFlags_Long_DeclaredNegativeHighBit` | A `long`-backed enum with a declared negative high-bit/member accepts that exact value. |
| `TryFromFlags_SByte_DeclaredMinus128` | A narrow signed enum with a declared `-128` accepts the exact value without `Convert.ToUInt64` overflow. |
| `TryFromFlags_NegativeUndeclaredCombination` | A negative value that is not a declared member is rejected even when its raw bits are composed of named positive flags. |
| `TryFromFlags_SignedMaskUsesUnderlyingWidth` | A signed `byte`/`short`-width mask does not inherit sign-extension bits from a widened `long` input. |

This distinction is intentionally called out because it is the
main policy boundary: positive combinations use a raw-bit mask; negative values
require exact declaration.

## Category D: `FromFlags` and fallback behavior

### should_pass

| File/Fact | Description |
|---|---|
| `FromFlags_InvalidValue_ThrowsArgumentOutOfRange` | Invalid positive, zero-without-member, negative-undeclared, and out-of-range inputs throw `ArgumentOutOfRangeException`, never conversion exceptions. |
| `FromFlagsOrFallback_InvalidValue_ReturnsFirstNamed` | The invalid cases above return the first declared member for every underlying width. |
| `FromFlagsOrFallback_EmptyEnum_ThrowsInvalidOperation` | A fallback request on an empty flags enum throws the documented `InvalidOperationException`. |
| `FromFlags_ValidNegativeDeclaredMember` | A declared negative member is returned by `FromFlags`, and the fallback path also returns it when supplied exactly. |

## Category E: codefix source-width preservation

These cases belong under `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE016CodeFix/cases/`
and use the existing `SPIRE016CodeFixTests` runner. Each after-source must run a
value above `int.MaxValue` or below `int.MinValue` and assert the resulting target
enum value, so compiling the cast alone is insufficient.

### should_pass

| File Name | Description |
|---|---|
| `NonFlags_EnumToEnum_LongSource` | Convert a `long`-backed source enum value above `int.MaxValue` to a target enum; expected code uses `(long)source`. |
| `NonFlags_EnumToEnum_ULongSource` | Convert a `ulong`-backed source enum value above `uint.MaxValue`; expected code uses `(ulong)source` and preserves the value. |
| `NonFlags_EnumToEnum_SByteSource` | Convert a signed narrow source enum; expected code uses `(sbyte)source`, establishing actual-underlying-type selection rather than always `int`. |
| `NonFlags_EnumToEnum_AllEightSourceWidths` | One executable parameterized/combined case covers `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, and `ulong` source enums with target enums of compatible values. |
| `Flags_EnumToEnum_LongSource` | Flags conversion uses `SpireEnum<TTarget>.FromFlags((long)source)` and preserves a wide composite. |
| `Flags_EnumToEnum_ULongSource` | Flags conversion uses `(ulong)source` and preserves a high-bit composite. |
| `NonFlags_EnumToEnum_CompoundOperandEvaluatedOnce` | A cast around a method/property expression is fixed without duplicating or re-evaluating the operand; a counter proves one call. |
| `NonFlags_EnumToEnum_ParenthesizedAndNestedExpression` | The fixed invocation remains valid inside a ternary, interpolation, binary expression, object initializer, and method argument while preserving required parentheses. |
| `NonFlags_EnumToEnum_GenericAndNestedType` | Source/target enums nested in generic/nested types retain correct qualification and compile after the fix. |
| `Flags_EnumToEnum_MethodReturnAndLambda` | Wide flags conversion works in a return expression and lambda body, covering existing statement/expression contexts together. |

### should_fail

| File Name | Description |
|---|---|
| `NonFlags_EnumToEnum_WideValueWouldTruncate` | Regression fixture whose old `(int)` output changes the runtime result; the after source must make this impossible. |
| `Flags_EnumToEnum_WideHighBitWouldTruncate` | Regression fixture with a `ulong` high bit that old output silently loses. |

## Category F: codefix false-positive and action boundaries

### should_pass

| File Name | Description |
|---|---|
| `NonFlags_ConstantCast_NoCodeFix` | Constant enum casts remain governed by the analyzer's existing constant policy; the codefix must not register an action when `IsConstant=true`. |
| `NonFlags_AlreadyUnderlyingCast_NoNarrowing` | A valid explicit source-underlying conversion remains unchanged and does not acquire a second cast. |
| `NonFlags_SourceOperandSideEffect` | The action is offered only for the cast diagnostic and keeps unrelated side effects/trivia intact. |

## Coverage notes

The direct public API matrix belongs in `tests/Houtamelo.Spire.Analyzers.Tests/SpireEnumTests.cs`
because it tests the shipped `Houtamelo.Spire` runtime API. The codefix matrix
belongs in the existing SPIRE016 before/after runner, not generator snapshots:
the behavior being repaired is the generated replacement expression and its
runtime value. For every overload group, assert the `out` result is `default` on
failure and that `From`/`FromFlags` throw `ArgumentOutOfRangeException` rather
than leaking `ArgumentException` or `OverflowException`.

The existing test declarations already cover several non-flags widths and basic
flags values; retain them and add the missing signed-negative, raw-width,
out-of-range, and executable-wide-codefix cases instead of duplicating all
existing facts. The matrix deliberately groups all eight widths into bounded
parameterized facts where the behavior is identical, while keeping boundary
semantics in named cases for readable failures.
