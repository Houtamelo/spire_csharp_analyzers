//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray with a record struct.
public class Detect_RecordStruct_GCAllocateUninitializedArray
{
    public void Method()
    {
        var arr = GC.AllocateUninitializedArray<EnforceInitializationRecordStruct>(5); //~ ERROR
    }
}
