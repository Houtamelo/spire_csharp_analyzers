//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.Resize with a constant size.
public class Detect_ArrayResize_ConstantSize
{
    public void Method()
    {
        EnforceInitializationStruct[]? arr = null;
        Array.Resize(ref arr, 5); //~ ERROR
    }
}
