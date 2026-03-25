//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` appears in a lambda body.
public class Detect_ObjectInit_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationNoCtor> f = () => new EnforceInitializationNoCtor { }; //~ ERROR
    }
}
