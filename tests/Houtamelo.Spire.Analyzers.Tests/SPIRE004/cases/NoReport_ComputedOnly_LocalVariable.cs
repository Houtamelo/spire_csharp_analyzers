//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a [EnforceInitialization] struct with only computed (non-auto) properties.
public class NoReport_ComputedOnly_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationComputedOnly();
    }
}
