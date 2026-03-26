//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called on a Span<int>.
public class NoReport_SpanClear_BuiltinInt
{
    public void Method()
    {
        Span<int> span = new int[5];
        span.Clear();
    }
}
