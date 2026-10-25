using System;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests;

public enum TestStatus { Active = 1, Inactive = 2, Pending = 3 }

[Flags]
public enum TestFlags { None = 0, Read = 1, Write = 2, Execute = 4 }

[Flags]
public enum TestFlagsNoZero { Read = 1, Write = 2, Execute = 4 }

public enum ByteStatus : byte { First = 1, Last = byte.MaxValue }

public enum SByteStatus : sbyte { First = 1, Last = sbyte.MaxValue }

public enum Int16Status : short { First = 1, Last = short.MaxValue }

public enum UInt16Status : ushort { First = 1, Last = ushort.MaxValue }

public enum Int32Status : int { First = 1, Last = int.MaxValue }

public enum UInt32Status : uint { First = 1, Last = uint.MaxValue }

public enum Int64Status : long { First = 1, Last = long.MaxValue }

public enum ULongStatus : ulong
{
    First = 1,
    High = 0x8000000000000000UL,
    Last = ulong.MaxValue
}

public enum EmptyStatus { }

[Flags]
public enum EmptyFlags { }

public class SpireEnumTests
{
    [Fact]
    public void Fallback_ReturnsFirstMember()
    {
        Assert.Equal(TestStatus.Active, SpireEnum<TestStatus>.Fallback());
    }

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

    [Fact]
    public void TryFrom_AllIntegralOverloads_ValidValue_ReturnsTrue()
    {
        RunAll(
            ValidTryCase((sbyte)3, TestStatus.Pending,
                (sbyte value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase((byte)3, TestStatus.Pending,
                (byte value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase((short)3, TestStatus.Pending,
                (short value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase((ushort)3, TestStatus.Pending,
                (ushort value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase(3, TestStatus.Pending,
                (int value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase(3u, TestStatus.Pending,
                (uint value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase(3L, TestStatus.Pending,
                (long value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            ValidTryCase(3UL, TestStatus.Pending,
                (ulong value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)));
    }

    [Fact]
    public void From_AllIntegralOverloads_ValidValue_ReturnsEnum()
    {
        AssertEnumValues(
            TestStatus.Pending,
            () => SpireEnum<TestStatus>.From((sbyte)3),
            () => SpireEnum<TestStatus>.From((byte)3),
            () => SpireEnum<TestStatus>.From((short)3),
            () => SpireEnum<TestStatus>.From((ushort)3),
            () => SpireEnum<TestStatus>.From((int)3),
            () => SpireEnum<TestStatus>.From((uint)3),
            () => SpireEnum<TestStatus>.From((long)3),
            () => SpireEnum<TestStatus>.From((ulong)3));
    }

    [Fact]
    public void FromOrFallback_AllIntegralOverloads_ValidValue_ReturnsEnum()
    {
        AssertEnumValues(
            TestStatus.Pending,
            () => SpireEnum<TestStatus>.FromOrFallback((sbyte)3),
            () => SpireEnum<TestStatus>.FromOrFallback((byte)3),
            () => SpireEnum<TestStatus>.FromOrFallback((short)3),
            () => SpireEnum<TestStatus>.FromOrFallback((ushort)3),
            () => SpireEnum<TestStatus>.FromOrFallback((int)3),
            () => SpireEnum<TestStatus>.FromOrFallback((uint)3),
            () => SpireEnum<TestStatus>.FromOrFallback((long)3),
            () => SpireEnum<TestStatus>.FromOrFallback((ulong)3));
    }

    [Fact]
    public void TryFrom_AllIntegralOverloads_InvalidValue_ReturnsFalseAndDefault()
    {
        RunAll(
            InvalidTryCase((sbyte)99,
                (sbyte value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase((byte)99,
                (byte value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase((short)99,
                (short value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase((ushort)99,
                (ushort value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase(99,
                (int value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase(99u,
                (uint value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase(99L,
                (long value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)),
            InvalidTryCase(99UL,
                (ulong value, out TestStatus result) => SpireEnum<TestStatus>.TryFrom(value, out result)));
    }

    [Fact]
    public void FromAndFromOrFallback_AllIntegralOverloads_InvalidValue_ThrowOrFallback()
    {
        RunAll(
            InvalidFromCase((sbyte)99,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase((byte)99,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase((short)99,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase((ushort)99,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase(99,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase(99u,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase(99L,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active),
            InvalidFromCase(99UL,
                value => SpireEnum<TestStatus>.From(value),
                value => SpireEnum<TestStatus>.FromOrFallback(value), TestStatus.Active));
    }

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

    [Fact]
    public void TryFromFlags_AllIntegralOverloads_ValidComposite_ReturnsTrue()
    {
        RunAll(
            ValidTryCase((sbyte)3, TestFlags.Read | TestFlags.Write,
                (sbyte value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase((byte)3, TestFlags.Read | TestFlags.Write,
                (byte value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase((short)3, TestFlags.Read | TestFlags.Write,
                (short value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase((ushort)3, TestFlags.Read | TestFlags.Write,
                (ushort value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase(3, TestFlags.Read | TestFlags.Write,
                (int value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase(3u, TestFlags.Read | TestFlags.Write,
                (uint value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase(3L, TestFlags.Read | TestFlags.Write,
                (long value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            ValidTryCase(3UL, TestFlags.Read | TestFlags.Write,
                (ulong value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)));
    }

    [Fact]
    public void FromFlags_AllIntegralOverloads_ValidComposite_ReturnsEnum()
    {
        AssertEnumValues(
            TestFlags.Read | TestFlags.Write,
            () => SpireEnum<TestFlags>.FromFlags((sbyte)3),
            () => SpireEnum<TestFlags>.FromFlags((byte)3),
            () => SpireEnum<TestFlags>.FromFlags((short)3),
            () => SpireEnum<TestFlags>.FromFlags((ushort)3),
            () => SpireEnum<TestFlags>.FromFlags((int)3),
            () => SpireEnum<TestFlags>.FromFlags((uint)3),
            () => SpireEnum<TestFlags>.FromFlags((long)3),
            () => SpireEnum<TestFlags>.FromFlags((ulong)3));
    }

    [Fact]
    public void FromFlagsOrFallback_AllIntegralOverloads_ValidComposite_ReturnsEnum()
    {
        AssertEnumValues(
            TestFlags.Read | TestFlags.Write,
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((sbyte)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((byte)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((short)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((ushort)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((int)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((uint)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((long)3),
            () => SpireEnum<TestFlags>.FromFlagsOrFallback((ulong)3));
    }

    [Fact]
    public void TryFromFlags_AllIntegralOverloads_InvalidBit_ReturnsFalseAndDefault()
    {
        RunAll(
            InvalidTryCase((sbyte)8,
                (sbyte value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase((byte)8,
                (byte value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase((short)8,
                (short value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase((ushort)8,
                (ushort value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase(8,
                (int value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase(8u,
                (uint value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase(8L,
                (long value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)),
            InvalidTryCase(8UL,
                (ulong value, out TestFlags result) => SpireEnum<TestFlags>.TryFromFlags(value, out result)));
    }

    [Fact]
    public void FromFlagsAndFromFlagsOrFallback_AllIntegralOverloads_InvalidBit_ThrowOrFallback()
    {
        RunAll(
            InvalidFromCase((sbyte)8,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase((byte)8,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase((short)8,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase((ushort)8,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase(8,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase(8u,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase(8L,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None),
            InvalidFromCase(8UL,
                value => SpireEnum<TestFlags>.FromFlags(value),
                value => SpireEnum<TestFlags>.FromFlagsOrFallback(value), TestFlags.None));
    }

    [Fact]
    public void TryFromFlags_InvalidBit_SetsDefaultOutValue()
    {
        var result = TestFlags.Execute;

        Assert.False(SpireEnum<TestFlags>.TryFromFlags(8, out result));
        Assert.Equal(default(TestFlags), result);
    }

    [Fact]
    public void Flags_ZeroValue_RespectsZeroMember()
    {
        Assert.True(SpireEnum<TestFlags>.TryFromFlags(0, out var withZero));
        Assert.Equal(TestFlags.None, withZero);
        Assert.Equal(TestFlags.None, SpireEnum<TestFlags>.FromFlags(0));
        Assert.Equal(TestFlags.None, SpireEnum<TestFlags>.FromFlagsOrFallback(0));

        Assert.False(SpireEnum<TestFlagsNoZero>.TryFromFlags(0, out var withoutZero));
        Assert.Equal(default(TestFlagsNoZero), withoutZero);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<TestFlagsNoZero>.FromFlags(0));
        Assert.Equal(
            TestFlagsNoZero.Read,
            SpireEnum<TestFlagsNoZero>.FromFlagsOrFallback(0));
    }

    // ── Overload tests (spot-check) ──

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

    [Fact]
    public void ULongAboveLongMax_TargetTypeDeterminesAcceptance()
    {
        const ulong high = 0x8000000000000000UL;

        Assert.True(SpireEnum<ULongStatus>.TryFrom(high, out var accepted));
        Assert.Equal(ULongStatus.High, accepted);
        Assert.Equal(ULongStatus.High, SpireEnum<ULongStatus>.From(high));
        Assert.Equal(ULongStatus.High, SpireEnum<ULongStatus>.FromOrFallback(high));

        var rejected = TestStatus.Pending;
        Assert.False(SpireEnum<TestStatus>.TryFrom(high, out rejected));
        Assert.Equal(default(TestStatus), rejected);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<TestStatus>.From(high));
        Assert.Equal(TestStatus.Active, SpireEnum<TestStatus>.FromOrFallback(high));
    }

    [Fact]
    public void EmptyEnum_FallbackPaths_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => SpireEnum<EmptyStatus>.Fallback());
        Assert.Throws<InvalidOperationException>(() => SpireEnum<EmptyStatus>.FromOrFallback(1));
        Assert.Throws<InvalidOperationException>(() => SpireEnum<EmptyFlags>.Fallback());
        Assert.Throws<InvalidOperationException>(() => SpireEnum<EmptyFlags>.FromFlagsOrFallback(1));
    }

    [Fact]
    public void UnderlyingWidthBoundaries_RejectOverflowAndAcceptUlongMaximum()
    {
        AssertUnderlyingOverflow(
            128L,
            SByteStatus.First,
            (long value, out SByteStatus result) =>
                SpireEnum<SByteStatus>.TryFrom(value, out result),
            value => SpireEnum<SByteStatus>.From(value),
            value => SpireEnum<SByteStatus>.FromOrFallback(value));
        AssertUnderlyingOverflow(
            256L,
            ByteStatus.First,
            (long value, out ByteStatus result) =>
                SpireEnum<ByteStatus>.TryFrom(value, out result),
            value => SpireEnum<ByteStatus>.From(value),
            value => SpireEnum<ByteStatus>.FromOrFallback(value));
        AssertUnderlyingOverflow(
            32768L,
            Int16Status.First,
            (long value, out Int16Status result) =>
                SpireEnum<Int16Status>.TryFrom(value, out result),
            value => SpireEnum<Int16Status>.From(value),
            value => SpireEnum<Int16Status>.FromOrFallback(value));
        AssertUnderlyingOverflow(
            65536L,
            UInt16Status.First,
            (long value, out UInt16Status result) =>
                SpireEnum<UInt16Status>.TryFrom(value, out result),
            value => SpireEnum<UInt16Status>.From(value),
            value => SpireEnum<UInt16Status>.FromOrFallback(value));
        AssertUnderlyingOverflow(
            2147483648L,
            Int32Status.First,
            (long value, out Int32Status result) =>
                SpireEnum<Int32Status>.TryFrom(value, out result),
            value => SpireEnum<Int32Status>.From(value),
            value => SpireEnum<Int32Status>.FromOrFallback(value));
        AssertUnderlyingOverflow(
            4294967296L,
            UInt32Status.First,
            (long value, out UInt32Status result) =>
                SpireEnum<UInt32Status>.TryFrom(value, out result),
            value => SpireEnum<UInt32Status>.From(value),
            value => SpireEnum<UInt32Status>.FromOrFallback(value));

        AssertUnderlyingOverflow(
            0x8000000000000000UL,
            Int64Status.First,
            (ulong value, out Int64Status result) =>
                SpireEnum<Int64Status>.TryFrom(value, out result),
            value => SpireEnum<Int64Status>.From(value),
            value => SpireEnum<Int64Status>.FromOrFallback(value));
        AssertUnderlyingAccepted(
            ulong.MaxValue,
            ULongStatus.Last,
            (ulong value, out ULongStatus result) =>
                SpireEnum<ULongStatus>.TryFrom(value, out result),
            value => SpireEnum<ULongStatus>.From(value),
            value => SpireEnum<ULongStatus>.FromOrFallback(value));
    }

    private static void RunAll(params Action[] assertions)
    {
        foreach (var assertion in assertions)
            assertion();
    }

    private static Action ValidTryCase<TInput, TEnum>(
        TInput value,
        TEnum expected,
        TryConversion<TInput, TEnum> conversion)
        where TEnum : struct, Enum
    {
        return () =>
        {
            Assert.True(conversion(value, out var result));
            Assert.Equal(expected, result);
        };
    }

    private static void AssertEnumValues<TEnum>(
        TEnum expected,
        params Func<TEnum>[] conversions)
        where TEnum : struct, Enum
    {
        foreach (var conversion in conversions)
            Assert.Equal(expected, conversion());
    }

    private static Action InvalidTryCase<TInput, TEnum>(
        TInput value,
        TryConversion<TInput, TEnum> conversion)
        where TEnum : struct, Enum
    {
        return () =>
        {
            var result = default(TEnum);
            Assert.False(conversion(value, out result));
            Assert.Equal(default(TEnum), result);
        };
    }

    private static Action InvalidFromCase<TInput, TEnum>(
        TInput value,
        Func<TInput, TEnum> from,
        Func<TInput, TEnum> fromOrFallback,
        TEnum expectedFallback)
        where TEnum : struct, Enum
    {
        return () =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => from(value));
            Assert.Equal(expectedFallback, fromOrFallback(value));
        };
    }

    private static void AssertUnderlyingOverflow<TInput, TEnum>(
        TInput value,
        TEnum expectedFallback,
        TryConversion<TInput, TEnum> tryFrom,
        Func<TInput, TEnum> from,
        Func<TInput, TEnum> fromOrFallback)
        where TEnum : struct, Enum
    {
        var result = expectedFallback;
        Assert.False(tryFrom(value, out result));
        Assert.Equal(default(TEnum), result);
        Assert.Throws<ArgumentOutOfRangeException>(() => from(value));
        Assert.Equal(expectedFallback, fromOrFallback(value));
    }

    private static void AssertUnderlyingAccepted<TInput, TEnum>(
        TInput value,
        TEnum expected,
        TryConversion<TInput, TEnum> tryFrom,
        Func<TInput, TEnum> from,
        Func<TInput, TEnum> fromOrFallback)
        where TEnum : struct, Enum
    {
        Assert.True(tryFrom(value, out var result));
        Assert.Equal(expected, result);
        Assert.Equal(expected, from(value));
        Assert.Equal(expected, fromOrFallback(value));
    }

    private delegate bool TryConversion<TInput, TEnum>(TInput value, out TEnum result)
        where TEnum : struct, Enum;
}
