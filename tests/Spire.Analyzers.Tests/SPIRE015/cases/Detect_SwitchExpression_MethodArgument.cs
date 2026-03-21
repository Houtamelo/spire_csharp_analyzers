//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression is passed directly as a method argument.
public class Detect_SwitchExpression_MethodArgument
{
    public void Method(Color color)
    {
        Console.WriteLine(color switch //~ ERROR
        {
            Color.Red => "red",
            _ => "other",
        });
    }
}
