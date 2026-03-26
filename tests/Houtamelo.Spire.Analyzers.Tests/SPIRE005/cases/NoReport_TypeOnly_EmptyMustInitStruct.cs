//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct)) is used on a fieldless [EnforceInitialization] struct.
public class NoReport_TypeOnly_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct));
    }
}
