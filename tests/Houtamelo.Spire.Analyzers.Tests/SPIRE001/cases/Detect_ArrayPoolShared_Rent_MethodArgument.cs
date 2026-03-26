//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Shared.Rent as a method argument.
public class Detect_ArrayPoolShared_Rent_MethodArgument
{
    public void Consume(EnforceInitializationStruct[] arr) { }

    public void Method()
    {
        Consume(ArrayPool<EnforceInitializationStruct>.Shared.Rent(5)); //~ ERROR
    }
}
