//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Span<EnforceInitializationStruct?>.Clear() is called — element type is Nullable<T>, not [EnforceInitialization] itself.
public class NoReport_SpanClear_NullableEnforceInitialization
{
    public void Method()
    {
        Span<EnforceInitializationStruct?> span = new EnforceInitializationStruct?[5];
        span.Clear();
    }
}
