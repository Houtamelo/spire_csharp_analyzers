//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears in a static method.
public class Detect_SpanClear_InStaticMethod
{
    public static void Method()
    {
        var arr = new MustInitStruct[5];
        Span<MustInitStruct> span = arr;
        span.Clear(); //~ ERROR
    }
}
