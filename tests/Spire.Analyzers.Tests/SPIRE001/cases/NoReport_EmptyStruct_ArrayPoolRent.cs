//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using ArrayPool.Rent with a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_ArrayPoolRent
{
    public void Method()
    {
        var arr = System.Buffers.ArrayPool<EmptyEnforceInitializationStruct>.Shared.Rent(5);
    }
}
