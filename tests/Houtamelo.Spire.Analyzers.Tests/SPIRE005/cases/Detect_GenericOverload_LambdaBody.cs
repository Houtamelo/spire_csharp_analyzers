//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is used inside a lambda body.
public class Detect_GenericOverload_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationStruct> factory = () => Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
    }
}
