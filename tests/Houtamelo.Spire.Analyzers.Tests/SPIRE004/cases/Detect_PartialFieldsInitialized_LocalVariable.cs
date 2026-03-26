//@ should_fail
// Ensure that SPIRE004 IS triggered when using `new EnforceInitializationPartialFieldsInitialized()` — struct with SOME fields having initializers.
public class Detect_PartialFieldsInitialized_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationPartialFieldsInitialized(); //~ ERROR
    }
}
