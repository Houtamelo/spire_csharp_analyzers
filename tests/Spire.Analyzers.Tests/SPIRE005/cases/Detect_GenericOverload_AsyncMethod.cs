//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is used inside an async method.
public class Detect_GenericOverload_AsyncMethod
{
    public async Task<EnforceInitializationStruct> Method()
    {
        await Task.Yield();
        return Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
    }
}
