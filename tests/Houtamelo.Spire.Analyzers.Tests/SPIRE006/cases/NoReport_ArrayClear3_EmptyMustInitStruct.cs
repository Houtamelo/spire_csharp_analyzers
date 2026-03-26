//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr, 0, n) is called with an EmptyEnforceInitializationStruct[] (fieldless [EnforceInitialization] struct).
public class NoReport_ArrayClear3_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        var arr = new EmptyEnforceInitializationStruct[5];
        Array.Clear(arr, 0, arr.Length);
    }
}
