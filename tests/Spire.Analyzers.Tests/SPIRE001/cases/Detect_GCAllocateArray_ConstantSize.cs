//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray with a constant size.
public class Detect_GCAllocateArray_ConstantSize
{
    public void Method()
    {
        var arr = GC.AllocateArray<MustInitStruct>(5); //~ ERROR
    }
}
