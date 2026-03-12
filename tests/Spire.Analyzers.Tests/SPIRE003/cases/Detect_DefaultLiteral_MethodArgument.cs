//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as argument to a method expecting MustInitStruct.
public class Detect_DefaultLiteral_MethodArgument
{
    public void Consume(MustInitStruct s) { }

    public void Method()
    {
        Consume(default); //~ ERROR
    }
}
