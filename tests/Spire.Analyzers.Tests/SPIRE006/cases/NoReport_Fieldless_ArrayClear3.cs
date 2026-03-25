//@ should_pass
// Ensure that SPIRE006 is NOT triggered for Array.Clear(arr, 0, n) when element type is EmptyEnforceInitializationStruct (fieldless, [EnforceInitialization]).
public class NoReport_Fieldless_ArrayClear3
{
    public void Method()
    {
        var arr = new EmptyEnforceInitializationStruct[5];
        Array.Clear(arr, 0, arr.Length);
    }
}
