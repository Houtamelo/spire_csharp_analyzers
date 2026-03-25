//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used and all auto-properties of T have initializers.
public class NoReport_AutoPropsAllInitialized_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationAutoPropsAllInitialized();
    }
}
