//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.Resize with a variable size.
public class Detect_ArrayResize_VariableSize
{
    public void Method(int n)
    {
        EnforceInitializationStruct[]? arr = null;
        Array.Resize(ref arr, n); //~ ERROR
    }
}
