//@ should_fail
// Ensure that SPIRE001 IS triggered when calling GC.AllocateArray in a ternary expression.
public class Detect_GCAllocateArray_TernaryExpression
{
    public MustInitStruct[] Method(bool flag)
    {
        return flag
            ? GC.AllocateArray<MustInitStruct>(5) //~ ERROR
            : new MustInitStruct[] { new MustInitStruct(1) };
    }
}
