//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray in a lambda body.
public class Detect_GCAllocateUninitializedArray_LambdaBody
{
    public void Method()
    {
        Func<MustInitStruct[]> factory = () => GC.AllocateUninitializedArray<MustInitStruct>(5); //~ ERROR
    }
}
