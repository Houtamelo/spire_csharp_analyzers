//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called on a Span<EmptyMustInitStruct> (fieldless).
public class NoReport_SpanClear_EmptyMustInitStruct
{
    public void Method()
    {
        Span<EmptyMustInitStruct> span = new EmptyMustInitStruct[5];
        span.Clear();
    }
}
