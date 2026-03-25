//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as argument to a method expecting EnforceInitializationStruct.
public class Detect_DefaultLiteral_MethodArgument
{
    public void Consume(EnforceInitializationStruct s) { }

    public void Method()
    {
        Consume(default); //~ ERROR
    }
}
