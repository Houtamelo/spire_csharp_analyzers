//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent.
public class Detect_ArrayPoolShared_Rent
{
    public void Method()
    {
        var arr = ArrayPool<EnforceInitializationStruct>.Shared.Rent(5); //~ ERROR
    }
}
