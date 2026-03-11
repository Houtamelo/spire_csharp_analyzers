//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using ArrayPool.Rent with a [MustBeInit] struct with no fields.
public class NoReport_EmptyStruct_ArrayPoolRent
{
    public void Method()
    {
        var arr = System.Buffers.ArrayPool<EmptyMustInitStruct>.Shared.Rent(5);
    }
}
