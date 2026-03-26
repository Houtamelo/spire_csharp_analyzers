//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), true) is called on a fieldless [EnforceInitialization] struct.
public class NoReport_NonPublic_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), true);
    }
}
