//@ should_fail
// Ensure that SPIRE015 IS triggered when switching on an [EnforceExhaustiveness] enum declared inside a class, with one member missing.
public class Detect_SwitchExpression_NestedEnum_OneMemberMissing
{
    [EnforceExhaustiveness]
    public enum Direction { North, South, East }

    public string Method(Direction value)
    {
        return value switch //~ ERROR
        {
            Direction.North => "north",
            Direction.South => "south",
        };
    }
}
