//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array inside an async method.
public class Detect_1DConstantSize_AsyncMethod
{
    public async System.Threading.Tasks.Task<EnforceInitializationStruct[]> MethodAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        return new EnforceInitializationStruct[5]; //~ ERROR
    }
}
