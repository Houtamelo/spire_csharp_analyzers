//@ should_fail
// Ensure that SPIRE003 IS triggered when default is passed as a constructor argument where the parameter is MustInitStruct.
public class Wrapper
{
    public Wrapper(MustInitStruct s) { }
}

public class Detect_DefaultLiteral_ConstructorArgument
{
    public void Method()
    {
        var w = new Wrapper(default); //~ ERROR
    }
}
