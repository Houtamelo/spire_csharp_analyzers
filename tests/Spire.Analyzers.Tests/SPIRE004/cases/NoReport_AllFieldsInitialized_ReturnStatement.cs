//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is returned and all fields of T have initializers.
public class NoReport_AllFieldsInitialized_ReturnStatement
{
    public EnforceInitializationAllFieldsInitialized Method()
    {
        return new EnforceInitializationAllFieldsInitialized();
    }
}
