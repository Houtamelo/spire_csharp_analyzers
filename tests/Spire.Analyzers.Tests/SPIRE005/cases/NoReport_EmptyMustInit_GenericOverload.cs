//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<EmptyEnforceInitializationStruct>() is used on a fieldless [EnforceInitialization] struct.
public class NoReport_EmptyEnforceInitialization_GenericOverload
{
    public void Method()
    {
        var x = Activator.CreateInstance<EmptyEnforceInitializationStruct>();
    }
}
