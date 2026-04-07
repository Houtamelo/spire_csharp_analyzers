//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression initializes a local variable.
public class Detect_SwitchExpression_LocalVariableInit
{
    public void Method(Color color)
    {
        string label = color switch //~ ERROR
        {
            Color.Red => "red",
        };
    }
}
