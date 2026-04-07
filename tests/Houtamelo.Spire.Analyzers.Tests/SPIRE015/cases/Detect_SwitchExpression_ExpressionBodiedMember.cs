//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression is the body of an expression-bodied method.
public class Detect_SwitchExpression_ExpressionBodiedMember
{
    public string Describe(Color color) => color switch //~ ERROR
    {
        Color.Red => "red",
    };
}
