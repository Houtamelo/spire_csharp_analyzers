//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray in a return statement.
public class Detect_GCAllocateArray_ReturnStatement
{
    public EnforceInitializationStruct[] Method()
    {
        return GC.AllocateArray<EnforceInitializationStruct>(5); //~ ERROR
    }
}
