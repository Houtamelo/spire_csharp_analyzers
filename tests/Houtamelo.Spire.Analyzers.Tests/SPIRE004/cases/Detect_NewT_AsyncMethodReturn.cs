//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is returned from an async method.
public class Detect_NewT_AsyncMethodReturn
{
    public async Task<EnforceInitializationNoCtor> Method()
    {
        await Task.Yield();
        return new EnforceInitializationNoCtor(); //~ ERROR
    }
}
