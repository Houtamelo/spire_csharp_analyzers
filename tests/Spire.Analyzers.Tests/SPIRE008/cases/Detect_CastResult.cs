//@ should_fail
// Ensure that SPIRE008 IS triggered when the result is immediately cast to the target struct type.
class TestClass
{
    void Method()
    {
        var x = (MustInitStruct)RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
    }
}
