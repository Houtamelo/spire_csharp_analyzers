//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression appears inside a lambda body.
public class Detect_SwitchExpression_InLambda
{
    public void Method()
    {
        Func<Color, string> describe = color => color switch //~ ERROR
        {
            Color.Red => "red",
            _ => "other",
        };
    }
}
