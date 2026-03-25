//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a return statement.
public class Detect_1DConstantSize_ReturnStatement
{
    public EnforceInitializationStruct[] Method()
    {
        return new EnforceInitializationStruct[5]; //~ ERROR
    }
}
