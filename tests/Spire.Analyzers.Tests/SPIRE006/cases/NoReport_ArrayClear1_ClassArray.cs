//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with a string[] argument.
public class NoReport_ArrayClear1_ClassArray
{
    public void Method()
    {
        var arr = new string[5];
        Array.Clear(arr);
    }
}
