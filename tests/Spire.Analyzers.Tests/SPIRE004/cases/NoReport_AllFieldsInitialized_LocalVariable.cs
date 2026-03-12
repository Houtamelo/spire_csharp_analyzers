//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used and all fields of T have initializers.
public class NoReport_AllFieldsInitialized_LocalVariable
{
    public void Method()
    {
        var x = new MustInitAllFieldsInitialized();
    }
}
