//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling Array.Resize with size zero.
public class NoReport_ArrayResize_ZeroSize
{
    public void Method()
    {
        EnforceInitializationStruct[]? arr = null;
        Array.Resize(ref arr, 0);
    }
}
