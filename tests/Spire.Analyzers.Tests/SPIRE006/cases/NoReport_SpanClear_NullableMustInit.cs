//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Span<MustInitStruct?>.Clear() is called — element type is Nullable<T>, not [MustBeInit] itself.
public class NoReport_SpanClear_NullableMustInit
{
    public void Method()
    {
        Span<MustInitStruct?> span = new MustInitStruct?[5];
        span.Clear();
    }
}
