//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<MustInitStruct> local variable.
public class Detect_SpanClear_MustInitStruct
{
    public void Method()
    {
        Span<MustInitStruct> span = new MustInitStruct[5];
        span.Clear(); //~ ERROR
    }
}
