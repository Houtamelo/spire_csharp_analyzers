//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit appears inside a lambda.
public class Detect_SkipInit_InLambda
{
    public void Method()
    {
        Action a = () =>
        {
            Unsafe.SkipInit(out EnforceInitializationStruct s); //~ ERROR
        };
        a();
    }
}
