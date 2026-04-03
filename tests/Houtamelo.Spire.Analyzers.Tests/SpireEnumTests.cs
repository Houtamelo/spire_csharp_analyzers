using System;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests;

public enum TestStatus { Active = 1, Inactive = 2, Pending = 3 }

[Flags]
public enum TestFlags { None = 0, Read = 1, Write = 2, Execute = 4 }

[Flags]
public enum TestFlagsNoZero { Read = 1, Write = 2, Execute = 4 }

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
}
