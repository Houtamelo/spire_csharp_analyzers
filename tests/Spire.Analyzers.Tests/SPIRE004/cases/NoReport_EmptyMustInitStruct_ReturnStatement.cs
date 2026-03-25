//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is returned for a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyEnforceInitializationStruct_ReturnStatement
{
    public EmptyEnforceInitializationStruct Method()
    {
        return new EmptyEnforceInitializationStruct();
    }
}
