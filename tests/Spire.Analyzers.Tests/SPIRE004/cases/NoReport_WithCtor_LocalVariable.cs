//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a [MustBeInit] struct that has a user-defined parameterless ctor.
public class NoReport_WithCtor_LocalVariable
{
    public void Method()
    {
        var x = new MustInitWithCtor();
    }
}
