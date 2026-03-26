//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray with a record struct.
public class Detect_GCAllocateArray_RecordStruct
{
    public void Method()
    {
        var arr = GC.AllocateArray<EnforceInitializationRecordStruct>(5); //~ ERROR
    }
}
