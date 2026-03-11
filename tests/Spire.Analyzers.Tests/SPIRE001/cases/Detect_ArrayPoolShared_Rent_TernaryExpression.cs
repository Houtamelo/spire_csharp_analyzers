//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent in a ternary expression.
public class Detect_ArrayPoolShared_Rent_TernaryExpression
{
    public MustInitStruct[] Method(bool flag)
    {
        return flag
            ? ArrayPool<MustInitStruct>.Shared.Rent(5) //~ ERROR
            : new MustInitStruct[] { new MustInitStruct(1) };
    }
}
