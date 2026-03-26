//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears in a static method.
public class Detect_SpanClear_InStaticMethod
{
    public static void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        Span<EnforceInitializationStruct> span = arr;
        span.Clear(); //~ ERROR
    }
}
