//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance targets EmptyEnforceInitializationStruct with (object[])null.
public class NoReport_Params_EmptyEnforceInitializationStruct_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), (object[])null);
    }
}
