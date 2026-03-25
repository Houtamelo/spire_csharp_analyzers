//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationPartialFieldsInitialized()` is returned from a method.
public class Detect_PartialFieldsInitialized_ReturnStatement
{
    public EnforceInitializationPartialFieldsInitialized Method()
    {
        return new EnforceInitializationPartialFieldsInitialized(); //~ ERROR
    }
}
