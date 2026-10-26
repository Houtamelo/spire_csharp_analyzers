using System;
using Houtamelo.Spire;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.Correctness;

[Flags]
public enum SignedByteFlags : sbyte
{
    None = 0,
    Read = 1,
    Write = 2,
    Min = sbyte.MinValue,
    All = -1,
}

[Flags]
public enum UnsignedByteFlags : byte
{
    None = 0,
    All = byte.MaxValue,
}

[Flags]
public enum SignedShortFlags : short
{
    None = 0,
    All = -1,
}

[Flags]
public enum UnsignedShortFlags : ushort
{
    None = 0,
    All = ushort.MaxValue,
}

[Flags]
public enum SignedIntFlags : int
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = -1,
}

[Flags]
public enum UnsignedIntFlags : uint
{
    None = 0,
    All = uint.MaxValue,
}

[Flags]
public enum SignedLongFlags : long
{
    None = 0,
    HighBit = long.MinValue,
    All = -1,
}

[Flags]
public enum UnsignedLongFlags : ulong
{
    None = 0,
    HighBit = 0x8000000000000000UL,
    All = ulong.MaxValue,
}

[Flags]
public enum NoZeroFlags
{
    Read = 1,
    Write = 2,
}

public sealed class EnumFlagsCorrectnessTests
{
    [Fact]
    public void TryFromFlags_AllUnderlyingWidths_ValidForwardingOverloads()
    {
        Assert.True(SpireEnum<SignedByteFlags>.TryFromFlags((sbyte)3, out var signedByte));
        Assert.Equal(SignedByteFlags.Read | SignedByteFlags.Write, signedByte);

        Assert.True(SpireEnum<UnsignedByteFlags>.TryFromFlags((byte)255, out var unsignedByte));
        Assert.Equal(UnsignedByteFlags.All, unsignedByte);

        Assert.True(SpireEnum<SignedShortFlags>.TryFromFlags((short)32767, out var signedShort));
        Assert.Equal((SignedShortFlags)32767, signedShort);

        Assert.True(SpireEnum<UnsignedShortFlags>.TryFromFlags((ushort)65535, out var unsignedShort));
        Assert.Equal(UnsignedShortFlags.All, unsignedShort);

        Assert.True(SpireEnum<SignedIntFlags>.TryFromFlags(7, out var signedInt));
        Assert.Equal(SignedIntFlags.Read | SignedIntFlags.Write | SignedIntFlags.Execute, signedInt);

        Assert.True(SpireEnum<UnsignedIntFlags>.TryFromFlags(uint.MaxValue, out var unsignedInt));
        Assert.Equal(UnsignedIntFlags.All, unsignedInt);

        Assert.True(SpireEnum<SignedLongFlags>.TryFromFlags(long.MaxValue, out var signedLong));
        Assert.Equal((SignedLongFlags)long.MaxValue, signedLong);

        Assert.True(SpireEnum<UnsignedLongFlags>.TryFromFlags(ulong.MaxValue, out var unsignedLong));
        Assert.Equal(UnsignedLongFlags.All, unsignedLong);
    }

    [Fact]
    public void TryFromFlags_DeclaredNegativeMembers_AcceptExactValueAndRejectOtherNegativeValues()
    {
        Assert.True(SpireEnum<SignedByteFlags>.TryFromFlags((sbyte)-1, out var signedByte));
        Assert.Equal(SignedByteFlags.All, signedByte);
        Assert.True(SpireEnum<SignedByteFlags>.TryFromFlags(sbyte.MinValue, out var minimumSignedByte));
        Assert.Equal(SignedByteFlags.Min, minimumSignedByte);
        Assert.False(SpireEnum<SignedByteFlags>.TryFromFlags((short)-2, out var invalidSignedByte));
        Assert.Equal(default, invalidSignedByte);

        Assert.True(SpireEnum<SignedIntFlags>.TryFromFlags(-1L, out var signedInt));
        Assert.Equal(SignedIntFlags.All, signedInt);
        Assert.False(SpireEnum<SignedIntFlags>.TryFromFlags(-2L, out var invalidSignedInt));
        Assert.Equal(default, invalidSignedInt);

        Assert.True(SpireEnum<SignedLongFlags>.TryFromFlags(long.MinValue, out var signedLong));
        Assert.Equal(SignedLongFlags.HighBit, signedLong);
        Assert.False(SpireEnum<SignedLongFlags>.TryFromFlags(long.MinValue + 1, out var invalidSignedLong));
        Assert.Equal(default, invalidSignedLong);
    }

    [Fact]
    public void TryFromFlags_SignedNegativeMembersContributeUnderlyingWidthBits()
    {
        Assert.True(SpireEnum<SignedByteFlags>.TryFromFlags((byte)127, out var signedByte));
        Assert.Equal((SignedByteFlags)127, signedByte);

        Assert.True(SpireEnum<SignedShortFlags>.TryFromFlags(32767, out var signedShort));
        Assert.Equal((SignedShortFlags)32767, signedShort);

        Assert.True(SpireEnum<SignedIntFlags>.TryFromFlags(int.MaxValue, out var signedInt));
        Assert.Equal((SignedIntFlags)int.MaxValue, signedInt);
    }

    [Fact]
    public void TryFromFlags_OutOfRangeValuesReturnFalseAndDefaultWithoutThrowing()
    {
        AssertInvalid(
            256L,
            (long value, out UnsignedByteFlags result) =>
                SpireEnum<UnsignedByteFlags>.TryFromFlags((short)value, out result));
        AssertInvalid(
            65536L,
            (long value, out UnsignedShortFlags result) =>
                SpireEnum<UnsignedShortFlags>.TryFromFlags((int)value, out result));
        AssertInvalid(
            (long)uint.MaxValue + 1,
            (long value, out UnsignedIntFlags result) =>
                SpireEnum<UnsignedIntFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            ulong.MaxValue,
            (ulong value, out SignedLongFlags result) =>
                SpireEnum<SignedLongFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -1L,
            (long value, out UnsignedLongFlags result) =>
                SpireEnum<UnsignedLongFlags>.TryFromFlags(value, out result));
    }

    [Fact]
    public void TryFromFlags_UInt64HighBitAndMaximumAreAcceptedWhenNamed()
    {
        Assert.True(SpireEnum<UnsignedLongFlags>.TryFromFlags(0x8000000000000000UL, out var highBit));
        Assert.Equal(UnsignedLongFlags.HighBit, highBit);
        Assert.True(SpireEnum<UnsignedLongFlags>.TryFromFlags(ulong.MaxValue, out var maximum));
        Assert.Equal(UnsignedLongFlags.All, maximum);
    }

    [Fact]
    public void ZeroFlagsValueRequiresADeclaredZeroMember()
    {
        Assert.True(SpireEnum<SignedIntFlags>.TryFromFlags(0, out var withZero));
        Assert.Equal(SignedIntFlags.None, withZero);

        Assert.False(SpireEnum<NoZeroFlags>.TryFromFlags(0, out var withoutZero));
        Assert.Equal(default, withoutZero);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<NoZeroFlags>.FromFlags(0));
        Assert.Equal(NoZeroFlags.Read, SpireEnum<NoZeroFlags>.FromFlagsOrFallback(0));
    }

    [Fact]
    public void InvalidFromFlagsThrowsArgumentOutOfRangeInsteadOfConversionExceptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<SignedByteFlags>.FromFlags((short)-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<UnsignedShortFlags>.FromFlags(65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpireEnum<SignedLongFlags>.FromFlags(ulong.MaxValue));
    }

    [Fact]
    public void EmptyFlagsFallbackStillThrowsInvalidOperation()
    {
        Assert.False(SpireEnum<EmptyFlags>.TryFromFlags(0, out var zero));
        Assert.Equal(default, zero);
        Assert.False(SpireEnum<EmptyFlags>.TryFromFlags(1, out var nonZero));
        Assert.Equal(default, nonZero);
        Assert.Throws<InvalidOperationException>(() => SpireEnum<EmptyFlags>.FromFlagsOrFallback(1));
    }

    [Fact]
    public void FromFlagsOrFallback_UndeclaredNegativePreservesFirstNamedFallback()
    {
        Assert.Equal(SignedIntFlags.All, SpireEnum<SignedIntFlags>.FromFlags(-1L));
        Assert.Equal(SignedIntFlags.All, SpireEnum<SignedIntFlags>.FromFlagsOrFallback(-1L));
        Assert.Equal(
            SignedIntFlags.None,
            SpireEnum<SignedIntFlags>.FromFlagsOrFallback(-2L));
        Assert.Equal(
            NoZeroFlags.Read,
            SpireEnum<NoZeroFlags>.FromFlagsOrFallback(-1L));
    }

    [Flags]
    private enum EmptyFlags { }

    private static void AssertInvalid<TEnum>(long value, TryLong<TEnum> conversion)
        where TEnum : struct, Enum
    {
        var result = (TEnum)Enum.ToObject(typeof(TEnum), 1);
        Assert.False(conversion(value, out result));
        Assert.Equal(default, result);
    }

    private static void AssertInvalid<TEnum>(ulong value, TryULong<TEnum> conversion)
        where TEnum : struct, Enum
    {
        var result = (TEnum)Enum.ToObject(typeof(TEnum), 1);
        Assert.False(conversion(value, out result));
        Assert.Equal(default, result);
    }

    private delegate bool TryLong<TEnum>(long value, out TEnum result)
        where TEnum : struct, Enum;

    private delegate bool TryULong<TEnum>(ulong value, out TEnum result)
        where TEnum : struct, Enum;
}
