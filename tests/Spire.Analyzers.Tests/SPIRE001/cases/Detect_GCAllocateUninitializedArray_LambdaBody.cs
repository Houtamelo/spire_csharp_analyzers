//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray in a lambda body.
public class Detect_GCAllocateUninitializedArray_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationStruct[]> factory = () => GC.AllocateUninitializedArray<EnforceInitializationStruct>(5); //~ ERROR
    }
}
