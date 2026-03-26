//@ should_fail
// Ensure that SPIRE006 IS triggered when arr.AsSpan().Clear() is called where arr is EnforceInitializationStruct[].
public class Detect_SpanClear_AsSpanChain
{
    public void Method()
    {
        EnforceInitializationStruct[] arr = new EnforceInitializationStruct[5];
        arr.AsSpan().Clear(); //~ ERROR
    }
}
