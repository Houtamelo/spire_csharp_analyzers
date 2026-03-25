//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray in a ternary expression.
public class Detect_GCAllocateArray_TernaryExpression
{
    public EnforceInitializationStruct[] Method(bool flag)
    {
        return flag
            ? GC.AllocateArray<EnforceInitializationStruct>(5) //~ ERROR
            : new EnforceInitializationStruct[] { new EnforceInitializationStruct(1) };
    }
}
