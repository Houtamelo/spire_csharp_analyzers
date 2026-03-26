//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as a constructor argument where the parameter is EnforceInitializationStruct.
public class Wrapper
{
    public Wrapper(EnforceInitializationStruct s) { }
}

public class Detect_DefaultLiteral_ConstructorArgument
{
    public void Method()
    {
        var w = new Wrapper(default); //~ ERROR
    }
}
