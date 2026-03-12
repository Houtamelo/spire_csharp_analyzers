//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<MustInitRecordStruct> local variable.
public class Detect_SpanClear_RecordStruct
{
    public void Method()
    {
        Span<MustInitRecordStruct> span = new MustInitRecordStruct[5];
        span.Clear(); //~ ERROR
    }
}
