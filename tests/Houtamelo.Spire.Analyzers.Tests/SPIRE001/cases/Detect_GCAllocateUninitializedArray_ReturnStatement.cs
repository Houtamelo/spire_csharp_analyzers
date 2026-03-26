//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray in a return statement.
public class Detect_GCAllocateUninitializedArray_ReturnStatement
{
    public EnforceInitializationStruct[] Method()
    {
        return GC.AllocateUninitializedArray<EnforceInitializationStruct>(5); //~ ERROR
    }
}
