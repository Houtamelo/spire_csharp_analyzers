//@ should_fail
// Ensure that SPIRE001 IS triggered when using stackalloc with a record struct.
public class Detect_RecordStruct_StackAlloc
{
    public void Method()
    {
        Span<EnforceInitializationRecordStruct> span = stackalloc EnforceInitializationRecordStruct[5]; //~ ERROR
    }
}
