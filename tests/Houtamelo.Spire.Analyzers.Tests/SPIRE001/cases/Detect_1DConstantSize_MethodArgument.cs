//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array as a method argument.
public class Detect_1DConstantSize_MethodArgument
{
    public void Consume(EnforceInitializationStruct[] arr) { }

    public void Method()
    {
        Consume(new EnforceInitializationStruct[5]); //~ ERROR
    }
}
