//@ should_fail
// Ensure that SPIRE008 IS triggered when GetUninitializedObject is called inside a static class method.
static class StaticContainer
{
    static void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
