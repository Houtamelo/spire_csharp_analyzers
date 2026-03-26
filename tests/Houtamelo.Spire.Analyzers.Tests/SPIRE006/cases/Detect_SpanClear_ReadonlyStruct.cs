//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<EnforceInitializationReadonlyStruct> local variable.
public class Detect_SpanClear_ReadonlyStruct
{
    public void Method()
    {
        Span<EnforceInitializationReadonlyStruct> span = new EnforceInitializationReadonlyStruct[5];
        span.Clear(); //~ ERROR
    }
}
