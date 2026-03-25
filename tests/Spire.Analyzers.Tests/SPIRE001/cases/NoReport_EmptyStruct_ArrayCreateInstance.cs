//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using Array.CreateInstance with a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_ArrayCreateInstance
{
    public void Method()
    {
        var arr = System.Array.CreateInstance(typeof(EmptyEnforceInitializationStruct), 5);
    }
}
