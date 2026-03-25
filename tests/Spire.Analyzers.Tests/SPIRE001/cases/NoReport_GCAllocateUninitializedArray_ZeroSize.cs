//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling GC.AllocateUninitializedArray with size zero.
public class NoReport_GCAllocateUninitializedArray_ZeroSize
{
    public void Method()
    {
        var arr = GC.AllocateUninitializedArray<EnforceInitializationStruct>(0);
    }
}
