//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling GC.AllocateArray with size zero.
public class NoReport_GCAllocateArray_ZeroSize
{
    public void Method()
    {
        var arr = GC.AllocateArray<MustInitStruct>(0);
    }
}
