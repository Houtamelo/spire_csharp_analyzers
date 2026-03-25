//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<EnforceInitializationRecordStruct> local variable.
public class Detect_SpanClear_RecordStruct
{
    public void Method()
    {
        Span<EnforceInitializationRecordStruct> span = new EnforceInitializationRecordStruct[5];
        span.Clear(); //~ ERROR
    }
}
