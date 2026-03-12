//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears inside a while loop body.
public class Detect_SpanClear_InWhileLoop
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        Span<MustInitStruct> span = arr;
        bool condition = false;
        while (condition)
        {
            span.Clear(); //~ ERROR
        }
    }
}
