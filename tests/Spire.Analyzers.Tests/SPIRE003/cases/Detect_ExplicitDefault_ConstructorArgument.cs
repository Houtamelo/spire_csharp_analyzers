//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is passed as a constructor argument.
public class Holder
{
    public Holder(MustInitStruct s) { }
}

public class Detect_ExplicitDefault_ConstructorArgument
{
    public void Method()
    {
        var h = new Holder(default(MustInitStruct)); //~ ERROR
    }
}
