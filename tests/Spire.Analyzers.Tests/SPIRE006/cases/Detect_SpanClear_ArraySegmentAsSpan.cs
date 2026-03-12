//@ should_fail
// Ensure that SPIRE006 IS triggered when an explicit cast to Span<MustInitStruct> then .Clear() is called.
public class Detect_SpanClear_ArraySegmentAsSpan
{
    public void Method()
    {
        MustInitStruct[] arr = new MustInitStruct[5];
        var segment = new ArraySegment<MustInitStruct>(arr);
        ((Span<MustInitStruct>)segment).Clear(); //~ ERROR
    }
}
