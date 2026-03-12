//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is used in an async Task<MustInitStruct> method.
public class Detect_DefaultLiteral_AsyncMethodReturn
{
    public async Task<MustInitStruct> Method()
    {
        await Task.Yield();
        return default; //~ ERROR
    }
}
