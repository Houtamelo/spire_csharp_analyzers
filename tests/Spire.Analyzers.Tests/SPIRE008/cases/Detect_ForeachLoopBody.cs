//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is inside a foreach loop body.
class TestClass
{
    void Method(int[] items)
    {
        foreach (var item in items)
        {
            var x = RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
        }
    }
}
