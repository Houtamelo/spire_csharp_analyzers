//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is passed as a method argument and T has a user-defined parameterless ctor.
public class NoReport_WithCtor_MethodArgument
{
    public void Consume(MustInitWithCtor value) { }

    public void Method()
    {
        Consume(new MustInitWithCtor());
    }
}
