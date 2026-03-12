//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<MustInitStruct> method parameter.
public class Detect_SpanClear_Parameter
{
    public void Method(Span<MustInitStruct> span)
    {
        span.Clear(); //~ ERROR
    }
}
