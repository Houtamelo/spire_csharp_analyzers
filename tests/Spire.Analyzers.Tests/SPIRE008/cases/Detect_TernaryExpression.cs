//@ should_fail
// Ensure that SPIRE008 IS triggered when the call appears in the true branch of a ternary expression.
class TestClass
{
    object Method(bool condition)
    {
        return condition
            ? RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct)) //~ ERROR
            : null;
    }
}
