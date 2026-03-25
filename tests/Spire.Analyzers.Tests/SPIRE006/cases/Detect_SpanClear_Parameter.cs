//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<EnforceInitializationStruct> method parameter.
public class Detect_SpanClear_Parameter
{
    public void Method(Span<EnforceInitializationStruct> span)
    {
        span.Clear(); //~ ERROR
    }
}
