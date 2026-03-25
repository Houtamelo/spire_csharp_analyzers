//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationAutoPropsPartialInitialized()` — struct with SOME auto-props initialized.
public class Detect_AutoPropsPartialInitialized_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationAutoPropsPartialInitialized(); //~ ERROR
    }
}
