//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling GC.AllocateArray on a plain struct.
public class NoReport_GCAllocateArray_PlainStruct
{
    public void Method()
    {
        var arr = GC.AllocateArray<PlainStruct>(5);
    }
}
