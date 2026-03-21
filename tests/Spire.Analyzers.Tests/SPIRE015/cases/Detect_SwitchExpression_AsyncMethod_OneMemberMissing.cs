//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression on Color appears inside an async Task<string> method.
public class Detect_SwitchExpression_AsyncMethod_OneMemberMissing
{
    public async Task<string> Method(Color value)
    {
        await Task.Yield();
        return value switch //~ ERROR
        {
            Color.Red => "red",
            Color.Green => "green",
        };
    }
}
