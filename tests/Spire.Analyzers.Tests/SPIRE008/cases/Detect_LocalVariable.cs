//@ should_fail
// Ensure that SPIRE008 IS triggered when the result is assigned to a local variable.
class TestClass
{
    void Method()
    {
        var x = RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
    }
}
