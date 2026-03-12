//@ should_pass
// Ensure that SPIRE006 is NOT triggered when span.Clear() is called inside a generic method on Span<T> where T is an unresolved type parameter.
public class NoReport_SpanClear_GenericTypeParam
{
    public void Method<T>(Span<T> span) where T : struct
    {
        span.Clear();
    }
}
