//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is used in a lambda body for a [EnforceInitialization] struct.
public class NoReport_ParameterizedCtor_LambdaBody
{
    public void Method()
    {
        Func<EnforceInitializationNoCtor> factory = () => new EnforceInitializationNoCtor(42, "hello");
        var result = factory();
    }
}
