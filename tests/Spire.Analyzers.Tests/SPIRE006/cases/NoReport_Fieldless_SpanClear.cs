//@ should_pass
// Ensure that SPIRE006 is NOT triggered for span.Clear() when span is Span<EmptyMustInitStruct>.
public class NoReport_Fieldless_SpanClear
{
    public void Method()
    {
        Span<EmptyMustInitStruct> span = new EmptyMustInitStruct[5];
        span.Clear();
    }
}
