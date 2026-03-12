//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is used inside a lambda body.
public class Detect_GenericOverload_LambdaBody
{
    public void Method()
    {
        Func<MustInitStruct> factory = () => Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
