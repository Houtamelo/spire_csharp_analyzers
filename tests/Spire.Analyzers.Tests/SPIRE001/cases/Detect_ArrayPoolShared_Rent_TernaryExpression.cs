//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent in a ternary expression.
public class Detect_ArrayPoolShared_Rent_TernaryExpression
{
    public EnforceInitializationStruct[] Method(bool flag)
    {
        return flag
            ? ArrayPool<EnforceInitializationStruct>.Shared.Rent(5) //~ ERROR
            : new EnforceInitializationStruct[] { new EnforceInitializationStruct(1) };
    }
}
