//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray in a return statement.
public class Detect_GCAllocateArray_ReturnStatement
{
    public MustInitStruct[] Method()
    {
        return GC.AllocateArray<MustInitStruct>(5); //~ ERROR
    }
}
