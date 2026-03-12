//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is used in a lambda body for a [MustBeInit] struct.
public class NoReport_ParameterizedCtor_LambdaBody
{
    public void Method()
    {
        Func<MustInitNoCtor> factory = () => new MustInitNoCtor(42, "hello");
        var result = factory();
    }
}
