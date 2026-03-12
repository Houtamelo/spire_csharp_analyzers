//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called on a Span<PlainStruct>.
public class NoReport_SpanClear_PlainStruct
{
    public void Method()
    {
        Span<PlainStruct> span = new PlainStruct[5];
        span.Clear();
    }
}
