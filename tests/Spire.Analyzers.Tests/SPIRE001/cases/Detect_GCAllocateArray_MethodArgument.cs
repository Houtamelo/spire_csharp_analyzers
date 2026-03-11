//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray as a method argument.
public class Detect_GCAllocateArray_MethodArgument
{
    public void Consume(MustInitStruct[] arr) { }

    public void Method()
    {
        Consume(GC.AllocateArray<MustInitStruct>(5)); //~ ERROR
    }
}
