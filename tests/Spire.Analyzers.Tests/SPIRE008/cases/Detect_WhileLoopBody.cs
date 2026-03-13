//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is inside a while loop body.
class TestClass
{
    void Method(bool condition)
    {
        while (condition)
        {
            var x = RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
        }
    }
}
