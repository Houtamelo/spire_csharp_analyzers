//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is passed as a positional method argument.
public class Detect_ExplicitDefault_MethodArgument
{
    public void Consume(MustInitStruct s) { }

    public void Method()
    {
        Consume(default(MustInitStruct)); //~ ERROR
    }
}
