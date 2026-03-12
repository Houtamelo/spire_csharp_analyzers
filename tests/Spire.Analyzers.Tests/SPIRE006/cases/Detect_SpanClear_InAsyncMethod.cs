//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears in an async method body.
public class Detect_SpanClear_InAsyncMethod
{
    public async Task Method()
    {
        var arr = new MustInitStruct[5];
        Span<MustInitStruct> span = arr;
        span.Clear(); //~ ERROR
        await Task.CompletedTask;
    }
}
