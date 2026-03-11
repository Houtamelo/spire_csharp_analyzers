//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling ArrayPool.Shared.Rent with size zero.
public class NoReport_ArrayPoolShared_Rent_ZeroSize
{
    public void Method()
    {
        var arr = ArrayPool<MustInitStruct>.Shared.Rent(0);
    }
}
