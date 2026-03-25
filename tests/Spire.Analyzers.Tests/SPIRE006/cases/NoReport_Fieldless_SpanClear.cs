//@ should_pass
// Ensure that SPIRE006 is NOT triggered for span.Clear() when span is Span<EmptyEnforceInitializationStruct>.
public class NoReport_Fieldless_SpanClear
{
    public void Method()
    {
        Span<EmptyEnforceInitializationStruct> span = new EmptyEnforceInitializationStruct[5];
        span.Clear();
    }
}
