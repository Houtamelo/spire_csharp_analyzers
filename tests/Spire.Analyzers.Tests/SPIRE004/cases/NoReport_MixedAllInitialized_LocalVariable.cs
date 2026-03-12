//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used and T has a mix of fields and auto-properties all with initializers.
public class NoReport_MixedAllInitialized_LocalVariable
{
    public void Method()
    {
        var x = new MustInitMixedAllInitialized();
    }
}
