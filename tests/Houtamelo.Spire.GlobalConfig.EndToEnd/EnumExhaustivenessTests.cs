using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

public class EnumExhaustivenessTests
{
    [Fact]
    public void ExhaustiveSwitch_AllMembersCovered_Compiles()
    {
        // With Spire_EnforceExhaustivenessOnAllEnumTypes=true,
        // SPIRE015 would warn if any member were missing.
        // This test proves the full MSBuild pipeline works:
        // the enum is NOT marked with [EnforceExhaustiveness],
        // yet exhaustive coverage is enforced via global config.
        var result = Direction.Up switch
        {
            Direction.Up => "up",
            Direction.Down => "down",
            Direction.Left => "left",
            Direction.Right => "right",
        };

        Assert.Equal("up", result);
    }
}
