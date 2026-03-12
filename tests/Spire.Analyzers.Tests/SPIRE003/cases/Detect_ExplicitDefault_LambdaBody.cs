//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is returned from a lambda body.
public class Detect_ExplicitDefault_LambdaBody
{
    public void Method()
    {
        Func<MustInitStruct> f = () => default(MustInitStruct); //~ ERROR
    }
}
