//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling GC.AllocateUninitializedArray on a plain struct.
public class NoReport_GCAllocateUninitializedArray_PlainStruct
{
    public void Method()
    {
        var arr = GC.AllocateUninitializedArray<PlainStruct>(5);
    }
}
