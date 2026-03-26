//@ should_fail
// Ensure that SPIRE016 IS triggered when a parameter is cast to StatusNoZero inside an async method.
public class Detect_CastToEnum_Variable_AsyncMethod
{
    public async Task<StatusNoZero> Method(int intParam)
    {
        await Task.Yield();
        return (StatusNoZero)intParam; //~ ERROR
    }
}
