//@ should_fail
// Ensure that SPIRE006 IS triggered when arr.AsSpan().Clear() appears in an async method body.
public class Detect_SpanClear_InAsyncMethod
{
    public async Task Method()
    {
        var arr = new EnforceInitializationStruct[5];
        arr.AsSpan().Clear(); //~ ERROR
        await Task.CompletedTask;
    }
}
