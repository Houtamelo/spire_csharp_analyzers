//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling Array.Resize on a plain struct.
public class NoReport_ArrayResize_PlainStruct
{
    public void Method()
    {
        PlainStruct[]? arr = null;
        Array.Resize(ref arr, 5);
    }
}
