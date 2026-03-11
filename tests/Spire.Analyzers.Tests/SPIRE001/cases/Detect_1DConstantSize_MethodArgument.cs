//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array as a method argument.
public class Detect_1DConstantSize_MethodArgument
{
    public void Consume(MustInitStruct[] arr) { }

    public void Method()
    {
        Consume(new MustInitStruct[5]); //~ ERROR
    }
}
