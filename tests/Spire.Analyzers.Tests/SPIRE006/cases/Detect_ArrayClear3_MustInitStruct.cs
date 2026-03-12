//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr, 0, n) is called with a MustInitStruct[] argument.
public class Detect_ArrayClear3_MustInitStruct
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
