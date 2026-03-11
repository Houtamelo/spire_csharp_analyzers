//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateUninitializedArray in a ternary expression.
public class Detect_GCAllocateUninitializedArray_TernaryExpression
{
    public MustInitStruct[] Method(bool flag)
    {
        return flag
            ? GC.AllocateUninitializedArray<MustInitStruct>(5) //~ ERROR
            : new MustInitStruct[] { new MustInitStruct(1) };
    }
}
