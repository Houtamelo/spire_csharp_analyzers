//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr, 0, n) is called with an int[] argument.
public class NoReport_ArrayClear3_BuiltinInt
{
    public void Method()
    {
        var arr = new int[5];
        Array.Clear(arr, 0, arr.Length);
    }
}
