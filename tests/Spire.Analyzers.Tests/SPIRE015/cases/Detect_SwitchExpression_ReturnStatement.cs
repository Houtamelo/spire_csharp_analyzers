//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression appears as the value in a return statement.
public class Detect_SwitchExpression_ReturnStatement
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            Color.Red => "red",
            Color.Green => "green",
            _ => "other",
        };
    }
}
