//@ should_fail
// Ensure that SPIRE015 IS triggered when switching on a nested [EnforceExhaustiveness] enum with no arms.
public class Detect_SwitchStatement_NestedEnum_AllMembersMissing
{
    [EnforceExhaustiveness]
    public enum Direction { North, South, East }

    public void Method(Direction value)
    {
        switch (value) //~ ERROR
        {
        }
    }
}
