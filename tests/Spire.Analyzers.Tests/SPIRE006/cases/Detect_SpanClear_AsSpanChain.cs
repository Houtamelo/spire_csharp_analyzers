//@ should_fail
// Ensure that SPIRE006 IS triggered when arr.AsSpan().Clear() is called where arr is MustInitStruct[].
public class Detect_SpanClear_AsSpanChain
{
    public void Method()
    {
        MustInitStruct[] arr = new MustInitStruct[5];
        arr.AsSpan().Clear(); //~ ERROR
    }
}
