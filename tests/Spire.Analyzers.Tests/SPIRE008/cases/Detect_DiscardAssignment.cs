//@ should_fail
// Ensure that SPIRE008 IS triggered when the result is discarded.
class TestClass
{
    void Method()
    {
        _ = RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
    }
}
