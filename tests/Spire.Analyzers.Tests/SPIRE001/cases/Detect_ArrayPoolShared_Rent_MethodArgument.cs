//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent as a method argument.
public class Detect_ArrayPoolShared_Rent_MethodArgument
{
    public void Consume(MustInitStruct[] arr) { }

    public void Method()
    {
        Consume(ArrayPool<MustInitStruct>.Shared.Rent(5)); //~ ERROR
    }
}
