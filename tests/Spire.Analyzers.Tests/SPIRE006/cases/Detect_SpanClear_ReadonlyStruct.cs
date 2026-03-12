//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<MustInitReadonlyStruct> local variable.
public class Detect_SpanClear_ReadonlyStruct
{
    public void Method()
    {
        Span<MustInitReadonlyStruct> span = new MustInitReadonlyStruct[5];
        span.Clear(); //~ ERROR
    }
}
