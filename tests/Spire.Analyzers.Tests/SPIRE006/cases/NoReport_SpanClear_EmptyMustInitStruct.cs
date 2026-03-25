//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called on a Span<EmptyEnforceInitializationStruct> (fieldless).
public class NoReport_SpanClear_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        Span<EmptyEnforceInitializationStruct> span = new EmptyEnforceInitializationStruct[5];
        span.Clear();
    }
}
