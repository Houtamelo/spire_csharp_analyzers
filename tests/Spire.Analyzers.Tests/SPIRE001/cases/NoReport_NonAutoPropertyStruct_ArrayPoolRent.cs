//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using ArrayPool.Rent with a [MustBeInit] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_ArrayPoolRent
{
    public void Method()
    {
        var arr = System.Buffers.ArrayPool<MustInitStructWithNonAutoProperty>.Shared.Rent(5);
    }
}
