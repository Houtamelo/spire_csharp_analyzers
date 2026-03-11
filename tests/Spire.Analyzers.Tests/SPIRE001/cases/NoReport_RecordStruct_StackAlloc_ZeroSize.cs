//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using stackalloc with size zero on a record struct.
public class NoReport_RecordStruct_StackAlloc_ZeroSize
{
    public void Method()
    {
        Span<MustInitRecordStruct> span = stackalloc MustInitRecordStruct[0];
    }
}
