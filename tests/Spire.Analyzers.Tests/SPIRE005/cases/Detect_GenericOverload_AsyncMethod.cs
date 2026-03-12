//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is used inside an async method.
public class Detect_GenericOverload_AsyncMethod
{
    public async Task<MustInitStruct> Method()
    {
        await Task.Yield();
        return Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
