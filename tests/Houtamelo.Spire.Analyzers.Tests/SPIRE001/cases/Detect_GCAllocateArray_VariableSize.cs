//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray with a variable size.
public class Detect_GCAllocateArray_VariableSize
{
    public void Method(int n)
    {
        var arr = GC.AllocateArray<EnforceInitializationStruct>(n); //~ ERROR
    }
}
