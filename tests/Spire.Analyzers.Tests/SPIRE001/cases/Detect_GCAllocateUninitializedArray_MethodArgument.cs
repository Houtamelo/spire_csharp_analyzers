//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray as a method argument.
public class Detect_GCAllocateUninitializedArray_MethodArgument
{
    public void Consume(MustInitStruct[] arr) { }

    public void Method()
    {
        Consume(GC.AllocateUninitializedArray<MustInitStruct>(5)); //~ ERROR
    }
}
