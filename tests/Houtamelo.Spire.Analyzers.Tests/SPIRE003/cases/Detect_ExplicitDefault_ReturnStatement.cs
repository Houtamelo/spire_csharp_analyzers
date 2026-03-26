//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used in a return statement.
public class Detect_ExplicitDefault_ReturnStatement
{
    public EnforceInitializationStruct Method()
    {
        return default(EnforceInitializationStruct); //~ ERROR
    }
}
