//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used in a lambda body and T has a user-defined parameterless ctor.
public class NoReport_WithCtor_LambdaBody
{
    public void Method()
    {
        Func<MustInitWithCtor> factory = () => new MustInitWithCtor();
        var result = factory();
    }
}
