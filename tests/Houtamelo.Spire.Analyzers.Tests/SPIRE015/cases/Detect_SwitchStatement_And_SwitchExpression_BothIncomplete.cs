//@ should_fail
// Ensure that SPIRE015 IS triggered once per switch when an incomplete switch statement and an incomplete switch expression on Color exist in the same method.
public class Detect_SwitchStatement_And_SwitchExpression_BothIncomplete
{
    public string Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                break;
        }

        return color switch //~ ERROR
        {
            Color.Green => "green",
            _ => "other",
        };
    }
}
