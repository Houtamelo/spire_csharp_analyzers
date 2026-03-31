using System;
using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

/// All enums are enforced via Spire_EnforceExhaustivenessOnAllEnumTypes=true.
/// None of these enums have [EnforceExhaustiveness].
/// If any switch arm were missing, SPIRE015 would fire as an error (TreatWarningsAsErrors).
public class EnumExhaustivenessTests
{
    [Fact]
    public void Direction_ExhaustiveSwitch()
    {
        var result = Direction.Up switch
        {
            Direction.Up => "up",
            Direction.Down => "down",
            Direction.Left => "left",
            Direction.Right => "right",
        };
        Assert.Equal("up", result);
    }

    [Fact]
    public void Priority_ExhaustiveSwitch_NoZeroMember()
    {
        var result = Priority.Low switch
        {
            Priority.Low => 1,
            Priority.Medium => 2,
            Priority.High => 3,
        };
        Assert.Equal(1, result);
    }

    [Fact]
    public void Color_ExhaustiveSwitch_WithZeroMember()
    {
        var result = Color.Red switch
        {
            Color.Red => "r",
            Color.Green => "g",
            Color.Blue => "b",
        };
        Assert.Equal("r", result);
    }

    [Fact]
    public void Flags_ExhaustiveSwitch()
    {
        var result = Permissions.Read switch
        {
            Permissions.Read => "r",
            Permissions.Write => "w",
            Permissions.Execute => "x",
        };
        Assert.Equal("r", result);
    }

    [Fact]
    public void ExternalEnum_DayOfWeek_ExhaustiveSwitch()
    {
        var result = DayOfWeek.Monday switch
        {
            DayOfWeek.Sunday => 0,
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
        };
        Assert.Equal(1, result);
    }

    [Fact]
    public void SwitchStatement_ExhaustiveOnUnmarkedEnum()
    {
        var dir = GetDirection();
        string result = "";
        switch (dir)
        {
            case Direction.Up: result = "up"; break;
            case Direction.Down: result = "down"; break;
            case Direction.Left: result = "left"; break;
            case Direction.Right: result = "right"; break;
        }
        Assert.Equal("left", result);
    }

    private static Direction GetDirection() => Direction.Left;

    [Fact]
    public void Color_DefaultIsValid_ZeroMemberExists()
    {
        // Color has Red=0, so default(Color) == Color.Red — valid.
        // SPIRE003 should NOT fire even with global enforcement.
        var c = default(Color);
        Assert.Equal(Color.Red, c);
    }
}
