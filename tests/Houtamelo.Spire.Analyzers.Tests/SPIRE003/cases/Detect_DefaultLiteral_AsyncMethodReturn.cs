//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is used in an async Task<EnforceInitializationStruct> method.
public class Detect_DefaultLiteral_AsyncMethodReturn
{
    public async Task<EnforceInitializationStruct> Method()
    {
        await Task.Yield();
        return default; //~ ERROR
    }
}
