//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is directly returned from a method.
class TestClass
{
    object Method()
    {
        return RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
    }
}
