//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is passed as a positional method argument.
public class Detect_ExplicitDefault_MethodArgument
{
    public void Consume(EnforceInitializationStruct s) { }

    public void Method()
    {
        Consume(default(EnforceInitializationStruct)); //~ ERROR
    }
}
