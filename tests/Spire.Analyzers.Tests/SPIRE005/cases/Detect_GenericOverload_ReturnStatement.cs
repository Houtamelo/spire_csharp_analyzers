//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is used in a return statement.
public class Detect_GenericOverload_ReturnStatement
{
    public MustInitStruct Method()
    {
        return Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
