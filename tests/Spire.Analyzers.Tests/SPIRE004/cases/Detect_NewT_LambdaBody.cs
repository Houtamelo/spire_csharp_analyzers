//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears in the body of a lambda.
public class Detect_NewT_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationNoCtor> factory = () => new EnforceInitializationNoCtor(); //~ ERROR
    }
}
