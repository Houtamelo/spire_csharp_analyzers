//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent with a variable size.
public class Detect_ArrayPoolShared_Rent_VariableSize
{
    public void Method(int n)
    {
        var arr = ArrayPool<MustInitStruct>.Shared.Rent(n); //~ ERROR
    }
}
