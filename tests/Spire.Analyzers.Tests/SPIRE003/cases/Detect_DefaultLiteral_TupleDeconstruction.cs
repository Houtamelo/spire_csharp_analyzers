//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used in a tuple deconstruction targeting EnforceInitializationStruct.
public class Detect_DefaultLiteral_TupleDeconstruction
{
    public void Method()
    {
        (EnforceInitializationStruct a, EnforceInitializationStruct b) = (default, default); //~ ERROR
    }
}
