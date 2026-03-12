//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called inside a generic method where T : struct — T is unresolved.
public class NoReport_SpanClear_GenericConstrainedToStruct
{
    public static void M<T>(Span<T> span) where T : struct
    {
        span.Clear();
    }
}
