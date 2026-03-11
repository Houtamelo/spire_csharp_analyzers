//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray with a variable size.
public class Detect_GCAllocateUninitializedArray_VariableSize
{
    public void Method(int n)
    {
        var arr = GC.AllocateUninitializedArray<MustInitStruct>(n); //~ ERROR
    }
}
