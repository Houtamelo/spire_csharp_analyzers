//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray as a method argument.
public class Detect_GCAllocateArray_MethodArgument
{
    public void Consume(EnforceInitializationStruct[] arr) { }

    public void Method()
    {
        Consume(GC.AllocateArray<EnforceInitializationStruct>(5)); //~ ERROR
    }
}
