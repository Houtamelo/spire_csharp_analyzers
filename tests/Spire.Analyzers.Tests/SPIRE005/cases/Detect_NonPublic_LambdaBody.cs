//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct), true) appears inside a lambda body.
public class Detect_NonPublic_LambdaBody
{
    public void Method()
    {
        Func<object> factory = () => Activator.CreateInstance(typeof(MustInitStruct), true); //~ ERROR
    }
}
