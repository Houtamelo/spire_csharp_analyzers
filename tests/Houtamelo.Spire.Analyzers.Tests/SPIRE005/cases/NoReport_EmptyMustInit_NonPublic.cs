//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(EmptyEnforceInitializationStruct), true) is used on a fieldless [EnforceInitialization] struct.
public class NoReport_EmptyEnforceInitialization_NonPublic
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), nonPublic: true);
    }
}
