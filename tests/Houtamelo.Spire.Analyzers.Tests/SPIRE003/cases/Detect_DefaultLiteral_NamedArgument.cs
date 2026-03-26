//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as a named argument to a method expecting EnforceInitializationStruct.
public class Detect_DefaultLiteral_NamedArgument
{
    public void Consume(EnforceInitializationStruct s) { }

    public void Method()
    {
        Consume(s: default); //~ ERROR
    }
}
