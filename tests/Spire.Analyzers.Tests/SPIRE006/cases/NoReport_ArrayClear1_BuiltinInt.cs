//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with an int[] argument.
public class NoReport_ArrayClear1_BuiltinInt
{
    public void Method()
    {
        var arr = new int[5];
        Array.Clear(arr);
    }
}
