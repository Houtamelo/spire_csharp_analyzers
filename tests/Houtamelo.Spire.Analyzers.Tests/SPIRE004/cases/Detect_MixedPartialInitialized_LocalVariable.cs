//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationMixedPartialInitialized()` — struct with mix of fields/auto-props, not all initialized.
public class Detect_MixedPartialInitialized_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationMixedPartialInitialized(); //~ ERROR
    }
}
