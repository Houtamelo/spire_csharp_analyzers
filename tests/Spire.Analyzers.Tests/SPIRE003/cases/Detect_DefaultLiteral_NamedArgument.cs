//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as a named argument to a method expecting MustInitStruct.
public class Detect_DefaultLiteral_NamedArgument
{
    public void Consume(MustInitStruct s) { }

    public void Method()
    {
        Consume(s: default); //~ ERROR
    }
}
