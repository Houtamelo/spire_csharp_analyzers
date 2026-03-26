//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is used in a return statement.
public class Detect_GenericOverload_ReturnStatement
{
    public EnforceInitializationStruct Method()
    {
        return Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
    }
}
