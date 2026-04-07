//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression on Color is the left-hand side of a null-coalescing operator.
public class Detect_SwitchExpression_NullCoalescing
{
    public string Method(Color color)
    {
        return (color switch //~ ERROR
        {
            Color.Red => "red",
        }) ?? "fallback";
    }
}
