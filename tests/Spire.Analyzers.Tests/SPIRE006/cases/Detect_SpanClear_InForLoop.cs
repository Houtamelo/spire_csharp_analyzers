//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears inside a for loop body.
public class Detect_SpanClear_InForLoop
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        Span<MustInitStruct> span = arr;
        for (int i = 0; i < 1; i++)
        {
            span.Clear(); //~ ERROR
        }
    }
}
