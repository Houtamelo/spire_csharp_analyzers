//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using Array.CreateInstance with a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_ArrayCreateInstance
{
    public void Method()
    {
        var arr = System.Array.CreateInstance(typeof(EnforceInitializationStructWithNonAutoProperty), 5);
    }
}
