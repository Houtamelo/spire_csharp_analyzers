//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr, 0, n) is called with a EnforceInitializationReadonlyStruct[] argument.
public class Detect_ArrayClear3_ReadonlyStruct
{
    public void Method()
    {
        var arr = new EnforceInitializationReadonlyStruct[5];
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
