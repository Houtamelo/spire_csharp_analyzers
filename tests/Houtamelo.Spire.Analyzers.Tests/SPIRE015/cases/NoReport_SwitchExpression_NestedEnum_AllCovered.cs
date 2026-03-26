//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on a nested [EnforceExhaustiveness] enum with all members covered.
public class NoReport_SwitchExpression_NestedEnum_AllCovered
{
    [EnforceExhaustiveness]
    public enum Direction { North, South, East }

    public string Method(Direction value)
    {
        return value switch
        {
            Direction.North => "north",
            Direction.South => "south",
            Direction.East => "east",
        };
    }
}
