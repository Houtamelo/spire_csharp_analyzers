//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr, 0, 3) is called with a variable typed as Array.
public class NoReport_ArrayClear3_ArrayTypedVar
{
    public void Method()
    {
        Array arr = new EnforceInitializationStruct[5];
        Array.Clear(arr, 0, 3);
    }
}
