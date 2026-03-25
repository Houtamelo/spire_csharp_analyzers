//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is inside a lambda body.
class TestClass
{
    void Method()
    {
        Func<object> factory = () => RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
