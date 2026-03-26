//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a lambda body.
public class Detect_1DConstantSize_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationStruct[]> factory = () => new EnforceInitializationStruct[5]; //~ ERROR
    }
}
