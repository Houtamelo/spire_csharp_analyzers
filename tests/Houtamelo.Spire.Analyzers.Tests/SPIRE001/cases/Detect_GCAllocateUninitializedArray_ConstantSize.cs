//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray with a constant size.
public class Detect_GCAllocateUninitializedArray_ConstantSize
{
    public void Method()
    {
        var arr = GC.AllocateUninitializedArray<EnforceInitializationStruct>(5); //~ ERROR
    }
}
