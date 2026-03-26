//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is passed as a constructor argument.
public class Holder
{
    public Holder(EnforceInitializationStruct s) { }
}

public class Detect_ExplicitDefault_ConstructorArgument
{
    public void Method()
    {
        var h = new Holder(default(EnforceInitializationStruct)); //~ ERROR
    }
}
