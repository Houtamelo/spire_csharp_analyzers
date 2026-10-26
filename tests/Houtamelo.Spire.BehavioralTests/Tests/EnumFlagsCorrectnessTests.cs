using System;
using Houtamelo.Spire;
using Spire.BehavioralTests.Types;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class EnumFlagsCorrectnessTests
{
    [Fact]
    public void AllUnderlyingWidths_ExerciseRangeNegativeAndRawBits()
    {
        Assert.True(SpireEnum<NativeSignedByteFlags>.TryFromFlags((sbyte)127, out var signedBytePositive));
        Assert.Equal((NativeSignedByteFlags)127, signedBytePositive);
        Assert.True(SpireEnum<NativeSignedByteFlags>.TryFromFlags((sbyte)-128, out var signedByteHighBit));
        Assert.Equal(NativeSignedByteFlags.HighBit, signedByteHighBit);
        Assert.True(SpireEnum<NativeSignedByteFlags>.TryFromFlags((sbyte)-1, out var signedByteAll));
        Assert.Equal(NativeSignedByteFlags.All, signedByteAll);
        AssertInvalid(
            128,
            (long value, out NativeSignedByteFlags result) =>
                SpireEnum<NativeSignedByteFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -2,
            (long value, out NativeSignedByteFlags result) =>
                SpireEnum<NativeSignedByteFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeUnsignedByteFlags>.TryFromFlags(byte.MaxValue, out var unsignedByte));
        Assert.Equal(NativeUnsignedByteFlags.All, unsignedByte);
        AssertInvalid(
            256,
            (long value, out NativeUnsignedByteFlags result) =>
                SpireEnum<NativeUnsignedByteFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -1,
            (long value, out NativeUnsignedByteFlags result) =>
                SpireEnum<NativeUnsignedByteFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeSignedShortFlags>.TryFromFlags(short.MaxValue, out var signedShortPositive));
        Assert.Equal((NativeSignedShortFlags)short.MaxValue, signedShortPositive);
        Assert.True(SpireEnum<NativeSignedShortFlags>.TryFromFlags(short.MinValue, out var signedShortHighBit));
        Assert.Equal(NativeSignedShortFlags.HighBit, signedShortHighBit);
        Assert.True(SpireEnum<NativeSignedShortFlags>.TryFromFlags((short)-1, out var signedShortAll));
        Assert.Equal(NativeSignedShortFlags.All, signedShortAll);
        AssertInvalid(
            (long)short.MaxValue + 1,
            (long value, out NativeSignedShortFlags result) =>
                SpireEnum<NativeSignedShortFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -2,
            (long value, out NativeSignedShortFlags result) =>
                SpireEnum<NativeSignedShortFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeUnsignedShortFlags>.TryFromFlags(ushort.MaxValue, out var unsignedShort));
        Assert.Equal(NativeUnsignedShortFlags.All, unsignedShort);
        AssertInvalid(
            (long)ushort.MaxValue + 1,
            (long value, out NativeUnsignedShortFlags result) =>
                SpireEnum<NativeUnsignedShortFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -1,
            (long value, out NativeUnsignedShortFlags result) =>
                SpireEnum<NativeUnsignedShortFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeSignedIntFlags>.TryFromFlags(int.MaxValue, out var signedIntPositive));
        Assert.Equal((NativeSignedIntFlags)int.MaxValue, signedIntPositive);
        Assert.True(SpireEnum<NativeSignedIntFlags>.TryFromFlags(int.MinValue, out var signedIntHighBit));
        Assert.Equal(NativeSignedIntFlags.HighBit, signedIntHighBit);
        Assert.True(SpireEnum<NativeSignedIntFlags>.TryFromFlags(-1, out var signedIntAll));
        Assert.Equal(NativeSignedIntFlags.All, signedIntAll);
        AssertInvalid(
            (long)int.MaxValue + 1,
            (long value, out NativeSignedIntFlags result) =>
                SpireEnum<NativeSignedIntFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -2,
            (long value, out NativeSignedIntFlags result) =>
                SpireEnum<NativeSignedIntFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeUnsignedIntFlags>.TryFromFlags(uint.MaxValue, out var unsignedInt));
        Assert.Equal(NativeUnsignedIntFlags.All, unsignedInt);
        AssertInvalid(
            (ulong)uint.MaxValue + 1,
            (ulong value, out NativeUnsignedIntFlags result) =>
                SpireEnum<NativeUnsignedIntFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            -1,
            (long value, out NativeUnsignedIntFlags result) =>
                SpireEnum<NativeUnsignedIntFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeSignedLongFlags>.TryFromFlags(long.MaxValue, out var signedLongPositive));
        Assert.Equal((NativeSignedLongFlags)long.MaxValue, signedLongPositive);
        Assert.True(SpireEnum<NativeSignedLongFlags>.TryFromFlags(long.MinValue, out var signedLongHighBit));
        Assert.Equal(NativeSignedLongFlags.HighBit, signedLongHighBit);
        Assert.True(SpireEnum<NativeSignedLongFlags>.TryFromFlags(-1, out var signedLongAll));
        Assert.Equal(NativeSignedLongFlags.All, signedLongAll);
        AssertInvalid(
            ulong.MaxValue,
            (ulong value, out NativeSignedLongFlags result) =>
                SpireEnum<NativeSignedLongFlags>.TryFromFlags(value, out result));
        AssertInvalid(
            long.MinValue + 1,
            (long value, out NativeSignedLongFlags result) =>
                SpireEnum<NativeSignedLongFlags>.TryFromFlags(value, out result));

        Assert.True(SpireEnum<NativeUnsignedLongFlags>.TryFromFlags(ulong.MaxValue, out var unsignedLong));
        Assert.Equal(NativeUnsignedLongFlags.All, unsignedLong);
        AssertInvalid(
            -1,
            (long value, out NativeUnsignedLongFlags result) =>
                SpireEnum<NativeUnsignedLongFlags>.TryFromFlags(value, out result));
    }

    [Fact]
    public void SignedFlags_CompositeAndDeclaredNegativeRoundTrip()
    {
        Assert.True(SpireEnum<NativeSignedIntFlags>.TryFromFlags(3, out var composite));
        Assert.Equal(NativeSignedIntFlags.Read | NativeSignedIntFlags.Write, composite);
        Assert.True(SpireEnum<NativeSignedIntFlags>.TryFromFlags(-1L, out var all));
        Assert.Equal(NativeSignedIntFlags.All, all);
        Assert.False(SpireEnum<NativeSignedIntFlags>.TryFromFlags(-2L, out var invalid));
        Assert.Equal(default, invalid);
    }

    [Fact]
    public void UnsignedLongFlags_HighBitAndMaximumRoundTrip()
    {
        Assert.True(
            SpireEnum<NativeUnsignedLongFlags>.TryFromFlags(
                0x8000000000000000UL,
                out var highBit));
        Assert.Equal(NativeUnsignedLongFlags.HighBit, highBit);
        Assert.Equal(
            NativeUnsignedLongFlags.All,
            SpireEnum<NativeUnsignedLongFlags>.FromFlags(ulong.MaxValue));
    }

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
