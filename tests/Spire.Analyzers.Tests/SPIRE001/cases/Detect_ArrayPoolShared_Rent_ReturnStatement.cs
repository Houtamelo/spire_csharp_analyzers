//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent in a return statement.
public class Detect_ArrayPoolShared_Rent_ReturnStatement
{
    public EnforceInitializationStruct[] Method()
    {
        return ArrayPool<EnforceInitializationStruct>.Shared.Rent(5); //~ ERROR
    }
}
