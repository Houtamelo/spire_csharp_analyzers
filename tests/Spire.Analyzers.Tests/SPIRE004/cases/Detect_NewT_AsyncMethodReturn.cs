//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is returned from an async method.
public class Detect_NewT_AsyncMethodReturn
{
    public async Task<MustInitNoCtor> Method()
    {
        await Task.Yield();
        return new MustInitNoCtor(); //~ ERROR
    }
}
