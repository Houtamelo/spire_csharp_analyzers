//@ should_pass
// Ensure that SPIRE006 is NOT triggered for arr.AsSpan().Clear() when arr is EmptyEnforceInitializationStruct[].
public class NoReport_Fieldless_AsSpanClear
{
    public void Method()
    {
        var arr = new EmptyEnforceInitializationStruct[5];
        arr.AsSpan().Clear();
    }
}
