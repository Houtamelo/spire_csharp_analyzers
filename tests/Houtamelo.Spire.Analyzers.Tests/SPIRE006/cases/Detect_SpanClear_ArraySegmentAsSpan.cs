//@ should_fail
// Ensure that SPIRE006 IS triggered when an explicit cast to Span<EnforceInitializationStruct> then .Clear() is called.
public class Detect_SpanClear_ArraySegmentAsSpan
{
    public void Method()
    {
        EnforceInitializationStruct[] arr = new EnforceInitializationStruct[5];
        var segment = new ArraySegment<EnforceInitializationStruct>(arr);
        ((Span<EnforceInitializationStruct>)segment).Clear(); //~ ERROR
    }
}
