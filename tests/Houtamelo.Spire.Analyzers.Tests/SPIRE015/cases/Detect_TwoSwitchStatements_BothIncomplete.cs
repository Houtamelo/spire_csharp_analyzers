//@ should_fail
// Ensure that SPIRE015 IS triggered once per switch when two separate incomplete switch statements on Color exist in the same method.
public class Detect_TwoSwitchStatements_BothIncomplete
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                break;
        }

        switch (color) //~ ERROR
        {
            case Color.Green:
                break;
        }
    }
}
