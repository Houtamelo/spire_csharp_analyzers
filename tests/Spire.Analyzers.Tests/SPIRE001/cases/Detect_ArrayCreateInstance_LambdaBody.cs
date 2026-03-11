//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance in a lambda body.
public class Detect_ArrayCreateInstance_LambdaBody
{
    public void Method()
    {
        Func<Array> factory = () => Array.CreateInstance(typeof(MustInitStruct), 5); //~ ERROR
    }
}
