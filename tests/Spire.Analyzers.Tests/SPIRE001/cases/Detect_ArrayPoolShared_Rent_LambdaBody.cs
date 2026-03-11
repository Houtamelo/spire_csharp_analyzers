//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent in a lambda body.
public class Detect_ArrayPoolShared_Rent_LambdaBody
{
    public void Method()
    {
        Func<MustInitStruct[]> factory = () => ArrayPool<MustInitStruct>.Shared.Rent(5); //~ ERROR
    }
}
