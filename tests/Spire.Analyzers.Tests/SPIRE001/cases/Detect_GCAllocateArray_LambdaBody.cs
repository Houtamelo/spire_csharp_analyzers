//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray in a lambda body.
public class Detect_GCAllocateArray_LambdaBody
{
    public void Method()
    {
        Func<MustInitStruct[]> factory = () => GC.AllocateArray<MustInitStruct>(5); //~ ERROR
    }
}
