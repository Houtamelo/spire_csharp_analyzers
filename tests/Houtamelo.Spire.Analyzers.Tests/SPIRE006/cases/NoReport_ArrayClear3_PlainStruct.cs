//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr, 0, n) is called with a PlainStruct[] argument.
public class NoReport_ArrayClear3_PlainStruct
{
    public void Method()
    {
        var arr = new PlainStruct[5];
        Array.Clear(arr, 0, arr.Length);
    }
}
