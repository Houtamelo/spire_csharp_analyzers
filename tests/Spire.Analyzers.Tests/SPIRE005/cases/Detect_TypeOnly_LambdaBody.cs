//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is used inside a lambda body.
public class Detect_TypeOnly_LambdaBody
{
    public void Method()
    {
        Func<object> factory = () => Activator.CreateInstance(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
