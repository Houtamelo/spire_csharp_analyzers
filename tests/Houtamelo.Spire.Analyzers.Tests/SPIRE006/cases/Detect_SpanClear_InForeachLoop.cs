//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears inside a foreach loop body.
public class Detect_SpanClear_InForeachLoop
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        var items = new[] { 1, 2, 3 };
        foreach (var item in items)
        {
            Span<EnforceInitializationStruct> span = arr;
            span.Clear(); //~ ERROR
        }
    }
}
