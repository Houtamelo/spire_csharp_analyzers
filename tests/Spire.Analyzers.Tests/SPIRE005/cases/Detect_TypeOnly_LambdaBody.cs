//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is used inside a lambda body.
public class Detect_TypeOnly_LambdaBody
{
    public void Method()
    {
        Func<object> factory = () => Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
    }
}
