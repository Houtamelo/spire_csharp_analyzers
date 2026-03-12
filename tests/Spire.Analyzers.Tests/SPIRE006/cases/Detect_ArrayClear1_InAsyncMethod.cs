//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears in an async method body.
public class Detect_ArrayClear1_InAsyncMethod
{
    public async Task Method()
    {
        var arr = new MustInitStruct[5];
        Array.Clear(arr); //~ ERROR
        await Task.CompletedTask;
    }
}
