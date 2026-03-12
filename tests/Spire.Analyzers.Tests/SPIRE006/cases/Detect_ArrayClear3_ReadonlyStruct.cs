//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr, 0, n) is called with a MustInitReadonlyStruct[] argument.
public class Detect_ArrayClear3_ReadonlyStruct
{
    public void Method()
    {
        var arr = new MustInitReadonlyStruct[5];
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
