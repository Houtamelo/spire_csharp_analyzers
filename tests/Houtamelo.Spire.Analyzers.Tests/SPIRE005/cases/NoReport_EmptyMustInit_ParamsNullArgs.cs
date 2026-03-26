//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(EmptyEnforceInitializationStruct), (object[])null) is used on a fieldless [EnforceInitialization] struct.
public class NoReport_EmptyEnforceInitialization_ParamsNullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), (object[])null);
    }
}
