//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<EnforceInitializationStruct> local variable.
public class Detect_SpanClear_EnforceInitializationStruct
{
    public void Method()
    {
        Span<EnforceInitializationStruct> span = new EnforceInitializationStruct[5];
        span.Clear(); //~ ERROR
    }
}
