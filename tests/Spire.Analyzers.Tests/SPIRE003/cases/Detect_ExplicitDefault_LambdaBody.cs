//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is returned from a lambda body.
public class Detect_ExplicitDefault_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationStruct> f = () => default(EnforceInitializationStruct); //~ ERROR
    }
}
