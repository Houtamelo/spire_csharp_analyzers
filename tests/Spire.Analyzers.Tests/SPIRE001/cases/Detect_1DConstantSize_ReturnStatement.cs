//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a return statement.
public class Detect_1DConstantSize_ReturnStatement
{
    public MustInitStruct[] Method()
    {
        return new MustInitStruct[5]; //~ ERROR
    }
}
