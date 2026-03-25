//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit appears in an async method.
public class Detect_SkipInit_InAsyncMethod
{
    public async Task Method()
    {
        Unsafe.SkipInit(out EnforceInitializationStruct s); //~ ERROR
        await Task.CompletedTask;
    }
}
