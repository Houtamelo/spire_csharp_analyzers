//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling ArrayPool.Shared.Rent on a plain struct.
public class NoReport_ArrayPoolShared_Rent_PlainStruct
{
    public void Method()
    {
        var arr = ArrayPool<PlainStruct>.Shared.Rent(5);
    }
}
