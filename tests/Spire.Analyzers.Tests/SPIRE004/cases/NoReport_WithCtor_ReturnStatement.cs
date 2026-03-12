//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is returned and T has a user-defined parameterless ctor.
public class NoReport_WithCtor_ReturnStatement
{
    public MustInitWithCtor Method()
    {
        return new MustInitWithCtor();
    }
}
