//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used in a tuple deconstruction targeting MustInitStruct.
public class Detect_DefaultLiteral_TupleDeconstruction
{
    public void Method()
    {
        (MustInitStruct a, MustInitStruct b) = (default, default); //~ ERROR
    }
}
