//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression on Color appears inside a foreach loop.
public class Detect_SwitchExpression_InForeachLoop
{
    public void Method(Color[] colors)
    {
        foreach (var color in colors)
        {
            string label = color switch //~ ERROR
            {
                Color.Red => "red",
            };
        }
    }
}
