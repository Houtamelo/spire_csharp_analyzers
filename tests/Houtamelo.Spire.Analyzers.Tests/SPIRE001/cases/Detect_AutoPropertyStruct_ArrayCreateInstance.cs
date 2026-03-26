//@ should_fail
// Ensure that SPIRE001 IS triggered when using Array.CreateInstance with a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_ArrayCreateInstance
{
    public void Method()
    {
        var arr = System.Array.CreateInstance(typeof(EnforceInitializationStructWithAutoProperty), 5); //~ ERROR
    }
}
