//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with a variable typed as Array.
public class NoReport_ArrayClear1_ArrayTypedVar
{
    public void Method()
    {
        Array arr = new MustInitStruct[5];
        Array.Clear(arr);
    }
}
