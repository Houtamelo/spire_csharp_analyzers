//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyEnforceInitializationStruct_LocalVariable
{
    public void Method()
    {
        var x = new EmptyEnforceInitializationStruct();
    }
}
