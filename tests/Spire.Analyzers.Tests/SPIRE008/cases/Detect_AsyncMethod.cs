//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is inside an async method.
class TestClass
{
    async Task Method()
    {
        await Task.Yield();
        var x = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
