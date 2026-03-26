//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray in a ternary expression.
public class Detect_GCAllocateUninitializedArray_TernaryExpression
{
    public EnforceInitializationStruct[] Method(bool flag)
    {
        return flag
            ? GC.AllocateUninitializedArray<EnforceInitializationStruct>(5) //~ ERROR
            : new EnforceInitializationStruct[] { new EnforceInitializationStruct(1) };
    }
}
