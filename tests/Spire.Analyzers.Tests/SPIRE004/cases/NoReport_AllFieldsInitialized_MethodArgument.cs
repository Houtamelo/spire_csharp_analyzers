//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is passed as a method argument and all fields of T have initializers.
public class NoReport_AllFieldsInitialized_MethodArgument
{
    public void Consume(MustInitAllFieldsInitialized value) { }

    public void Method()
    {
        Consume(new MustInitAllFieldsInitialized());
    }
}
